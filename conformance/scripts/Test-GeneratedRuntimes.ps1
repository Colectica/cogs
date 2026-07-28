[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $CogsDll,

    [string] $Model = (Join-Path $PSScriptRoot '..\model'),

    [string] $Instances = (Join-Path $PSScriptRoot '..\instances'),

    [string] $GeneratedRoot = (Join-Path $PSScriptRoot '..\..\generated\conformance')
)

$ErrorActionPreference = 'Stop'
$CogsDll = [IO.Path]::GetFullPath($CogsDll)
$Model = [IO.Path]::GetFullPath($Model)
$Instances = [IO.Path]::GetFullPath($Instances)
$GeneratedRoot = [IO.Path]::GetFullPath($GeneratedRoot)
$ProbeRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\runtime'))

if (-not [IO.File]::Exists($CogsDll)) { throw "COGS CLI assembly not found: $CogsDll" }
if (-not [IO.Directory]::Exists($Model)) { throw "Conformance model not found: $Model" }
if (-not [IO.Directory]::Exists($Instances)) { throw "Conformance instances not found: $Instances" }

$PythonPackage = Join-Path $GeneratedRoot 'python'
$TypeScriptPackage = Join-Path $GeneratedRoot 'typescript'
$CSharpSources = Join-Path $GeneratedRoot 'src'
if (-not [IO.File]::Exists((Join-Path $PythonPackage 'cogs_conformance\model.py'))) {
    throw "Generated Python package not found below $PythonPackage."
}
if (-not [IO.File]::Exists((Join-Path $TypeScriptPackage 'dist\index.js'))) {
    throw "Built generated TypeScript package not found below $TypeScriptPackage."
}
if (@(Get-ChildItem -LiteralPath $CSharpSources -Filter '*.csproj' -File).Count -ne 1) {
    throw "Expected exactly one generated C# project below $CSharpSources."
}

function Test-Process([string] $FileName, [string[]] $Arguments) {
    try {
        & $FileName @Arguments *> $null
        return $LASTEXITCODE -eq 0
    }
    catch {
        return $false
    }
}

function Resolve-Python {
    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($env:COGS_PYTHON)) {
        $candidates += [pscustomobject]@{ File = $env:COGS_PYTHON; Prefix = @() }
    }
    $candidates += [pscustomobject]@{ File = 'python3'; Prefix = @() }
    $candidates += [pscustomobject]@{ File = 'python'; Prefix = @() }
    if ($IsWindows) { $candidates += [pscustomobject]@{ File = 'py'; Prefix = @('-3') } }
    foreach ($candidate in $candidates) {
        $versionCheck = @($candidate.Prefix) + @(
            '-c', 'import sys; raise SystemExit(0 if sys.version_info >= (3, 11) else 1)'
        )
        if (Test-Process $candidate.File $versionCheck) { return $candidate }
    }
    throw 'Python 3.11 or newer was not found. Set COGS_PYTHON to the interpreter executable.'
}

function Resolve-Node {
    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($env:COGS_NODE)) { $candidates += $env:COGS_NODE }
    $candidates += 'node'
    foreach ($candidate in $candidates) {
        try {
            $version = & $candidate --version 2>$null
            if ($LASTEXITCODE -eq 0 -and $version -match '^v(?<major>[0-9]+)\.' -and [int]$Matches.major -ge 22) {
                return $candidate
            }
        }
        catch { }
    }
    throw 'Node 22 or newer was not found. Set COGS_NODE to the executable.'
}

function Invoke-Checked([string] $Description, [string] $FileName, [object[]] $Arguments) {
    & $FileName @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Description failed with exit $LASTEXITCODE." }
}

function Assert-Instance([string] $Path, [string] $Format) {
    $output = & dotnet $CogsDll validate-instance $Model $Path --format $Format 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Generated $Format instance is invalid: $Path`n$output" }
    Write-Host "PASS validate-instance $([IO.Path]::GetFileName($Path))"
}

$python = Resolve-Python
$node = Resolve-Node
$csharpProject = Join-Path $ProbeRoot 'csharp\ConformanceRuntimeProbe.csproj'
$csharpDll = Join-Path $ProbeRoot 'csharp\bin\Release\net10.0\ConformanceRuntimeProbe.dll'
$pythonProbe = Join-Path $ProbeRoot 'python_probe.py'
$typeScriptProbe = Join-Path $ProbeRoot 'typescript_probe.mjs'

Invoke-Checked 'Generated C# runtime probe build' 'dotnet' @(
    'build', $csharpProject, '--configuration', 'Release', '--verbosity', 'minimal'
)
if (-not [IO.File]::Exists($csharpDll)) { throw "C# runtime probe output not found: $csharpDll" }

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$tempRoot = Join-Path $tempBase ("cogs-generated-runtime-" + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($tempRoot) | Out-Null

function Output-Paths([string] $Name) {
    [pscustomobject]@{
        Json = Join-Path $tempRoot "$Name.json"
        Xml = Join-Path $tempRoot "$Name.xml"
    }
}

function Invoke-CSharpProbe([string] $Format, [string] $InputPath, [object] $Paths) {
    Invoke-Checked "C# generated runtime ($Format)" 'dotnet' @(
        $csharpDll, $Format, $InputPath, $Paths.Json, $Paths.Xml
    )
    Assert-Instance $Paths.Json 'json'
    Assert-Instance $Paths.Xml 'xml'
}

function Invoke-PythonProbe([string] $Format, [string] $InputPath, [object] $Paths) {
    Invoke-Checked "Python generated runtime ($Format)" $python.File @(
        @($python.Prefix) + @($pythonProbe, $PythonPackage, $Format, $InputPath, $Paths.Json, $Paths.Xml)
    )
    Assert-Instance $Paths.Json 'json'
    Assert-Instance $Paths.Xml 'xml'
}

function Invoke-TypeScriptProbe([string] $Format, [string] $InputPath, [object] $Paths) {
    Invoke-Checked "TypeScript generated runtime ($Format)" $node @(
        $typeScriptProbe, $TypeScriptPackage, $Format, $InputPath, $Paths.Json, $Paths.Xml
    )
    Assert-Instance $Paths.Json 'json'
    Assert-Instance $Paths.Xml 'xml'
}

try {
    $sourceJson = Join-Path $Instances 'full.json'
    $sourceXml = Join-Path $Instances 'full.xml'
    Assert-Instance $sourceJson 'json'
    Assert-Instance $sourceXml 'xml'

    # Forward order alternates wire formats: C# JSON -> Python XML -> TypeScript JSON.
    $forwardCSharp = Output-Paths 'forward-csharp'
    Invoke-CSharpProbe 'json' $sourceJson $forwardCSharp
    $forwardPython = Output-Paths 'forward-python'
    Invoke-PythonProbe 'xml' $forwardCSharp.Xml $forwardPython
    $forwardTypeScript = Output-Paths 'forward-typescript'
    Invoke-TypeScriptProbe 'json' $forwardPython.Json $forwardTypeScript

    # Reverse language order starts from XML: TypeScript XML -> Python JSON -> C# XML.
    $reverseTypeScript = Output-Paths 'reverse-typescript'
    Invoke-TypeScriptProbe 'xml' $sourceXml $reverseTypeScript
    $reversePython = Output-Paths 'reverse-python'
    Invoke-PythonProbe 'json' $reverseTypeScript.Json $reversePython
    $reverseCSharp = Output-Paths 'reverse-csharp'
    Invoke-CSharpProbe 'xml' $reversePython.Xml $reverseCSharp

    Write-Host 'COGS 2 generated-runtime forward and reverse chains passed.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($tempRoot)
    if (-not $resolved.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -or $resolved -eq $tempBase) {
        throw "Refusing to remove unsafe temporary path: $resolved"
    }
    if ([IO.Directory]::Exists($resolved)) { [IO.Directory]::Delete($resolved, $true) }
}
