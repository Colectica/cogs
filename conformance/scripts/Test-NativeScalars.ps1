[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $CogsDll,
    [string] $GeneratedRoot = (Join-Path $PSScriptRoot '../../generated/conformance'),
    [string] $OutputRoot = (Join-Path $GeneratedRoot 'native-scalar-results')
)
$ErrorActionPreference = 'Stop'
$CogsDll = [IO.Path]::GetFullPath($CogsDll)
$GeneratedRoot = [IO.Path]::GetFullPath($GeneratedRoot)
$OutputRoot = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) ([guid]::NewGuid().ToString("N"))
Write-Host "Native scalar evidence: $OutputRoot"
$runtime = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../runtime'))
$model = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../model'))
$manifest = Join-Path $runtime 'native-scalars.json'
$python = $null
$candidates = @()
if ($env:COGS_PYTHON) { $candidates += @{ File = $env:COGS_PYTHON; Prefix = @() } }
$candidates += @{ File = 'python3'; Prefix = @() }
$candidates += @{ File = 'python'; Prefix = @() }
if ($IsWindows) { $candidates += @{ File = 'py'; Prefix = @('-3') } }
foreach ($candidate in $candidates) {
    try {
        $arguments = @($candidate.Prefix) + @('-c', 'import sys; raise SystemExit(sys.version_info < (3, 11))')
        & $candidate.File @arguments *> $null
        if ($LASTEXITCODE -eq 0) { $python = $candidate; break }
    } catch { }
}
if ($null -eq $python) { throw 'Python 3.11+ was not found; set COGS_PYTHON.' }
$node = if ($env:COGS_NODE) { $env:COGS_NODE } else { 'node' }

function Invoke-Checked([string] $executable, [object[]] $arguments) {
    & $executable @arguments
    if ($LASTEXITCODE -ne 0) { throw "$executable failed with exit $LASTEXITCODE" }
}
function Assert-Instance([string] $path, [string] $format, [bool] $valid) {
    $diagnostics = & dotnet $CogsDll validate-instance $model $path --format $format 2>&1 | Out-String
    $expected = if ($valid) { 0 } else { 100 }
    if ($LASTEXITCODE -ne $expected) {
        throw "$path expected exit $expected, actual $LASTEXITCODE`n$diagnostics"
    }
}

[IO.Directory]::CreateDirectory($OutputRoot) | Out-Null
$inputRoot = Join-Path $OutputRoot 'inputs'
[IO.Directory]::CreateDirectory($inputRoot) | Out-Null
$cases = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
foreach ($case in $cases) {
    foreach ($format in @('json', 'xml')) {
        $path = Join-Path $inputRoot "$($case.name).$format"
        [IO.File]::WriteAllText($path, $case.$format, [Text.UTF8Encoding]::new($false))
        $valid = if ($null -ne $case.($format + "Valid")) { $case.($format + "Valid") } else { $case.valid }
        Assert-Instance $path $format $valid
    }
}
Write-Host "PASS $($cases.Count) independently specified input vectors in both formats"

$project = Join-Path $runtime 'csharp/ConformanceRuntimeProbe.csproj'
Invoke-Checked 'dotnet' @('build', $project, '-c', 'Release', '--verbosity', 'minimal', "-p:GeneratedSourceDirectory=$(Join-Path $GeneratedRoot 'src')")
$dll = Join-Path $runtime 'csharp/bin/Release/net10.0/ConformanceRuntimeProbe.dll'
Invoke-Checked 'dotnet' @($dll, 'scalars', $manifest, (Join-Path $OutputRoot 'csharp'))
foreach ($flavor in @('python', 'python-pydantic')) {
    Invoke-Checked $python.File (@($python.Prefix) + @((Join-Path $runtime 'native_scalar_probe.py'), (Join-Path $GeneratedRoot $flavor), $manifest, (Join-Path $OutputRoot $flavor)))
}
Invoke-Checked $node @((Join-Path $runtime 'native_scalar_probe.mjs'), (Join-Path $GeneratedRoot 'typescript'), $manifest, (Join-Path $OutputRoot 'typescript'))

foreach ($language in @('csharp', 'python', 'python-pydantic', 'typescript')) {
    $files = @(Get-ChildItem -LiteralPath (Join-Path $OutputRoot $language) -File | Where-Object Extension -In '.json', '.xml')
    foreach ($file in $files) {
        Assert-Instance $file.FullName $file.Extension.TrimStart('.') $true
    }
    Write-Host "PASS ${language}: $($files.Count) emitted JSON/XML boundaries"
}
Write-Host 'Native scalar and CR identity conformance passed.'
