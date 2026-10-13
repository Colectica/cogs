Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Install-Graphviz.ps1')
$tools = Get-Content (Join-Path $PSScriptRoot '../tools.json') -Raw | ConvertFrom-Json

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-Failure([scriptblock] $Action, [string] $Text) {
    try { & $Action } catch {
        if ($_.Exception.Message.Contains($Text)) { return }
        throw
    }
    throw "Expected failure containing '$Text'."
}
function Test-InstalledDot { $global:LASTEXITCODE = 0; "dot - graphviz version $script:installedVersion (build)" }
function Test-FallbackDot { $global:LASTEXITCODE = 0; "dot - graphviz version $script:fallbackVersion (build)" }
function Test-FailedDot { $global:LASTEXITCODE = 9; 'dot - graphviz version 16.1.0' }
function Test-UnknownDot { $global:LASTEXITCODE = 0; 'not a Graphviz banner' }

$script:fallbackCalls = 0
$script:installedVersion = '17.0.0'
$script:fallbackVersion = '16.1.0'
$fallback = { param($asset); $script:fallbackCalls++; Assert-True ($asset.url -match 'graphviz-releases') 'Expected official fallback.'; 'Test-FallbackDot' }
foreach ($version in @('16.1', '16.1.0', '16.2.0', '17.0.0')) {
    $script:installedVersion = $version
    Assert-True ((Resolve-Graphviz 'Test-InstalledDot' $tools 'unknown-platform' '' $fallback) -eq 'Test-InstalledDot') 'A sufficiently new package-manager executable must be retained.'
}
Assert-True ($script:fallbackCalls -eq 0) 'Newer package-manager installs must not download a fallback.'
$script:installedVersion = '2.42.2'
foreach ($platform in @('windows', 'ubuntu')) {
    Assert-True ((Resolve-Graphviz 'Test-InstalledDot' $tools $platform '24.04' $fallback) -eq 'Test-FallbackDot') 'An old package-manager version must use the fallback.'
}
Assert-True ($script:fallbackCalls -eq 2) 'Expected one fallback per old installation.'
$script:fallbackVersion = '18.0.0'
Assert-True ((Resolve-Graphviz 'Test-InstalledDot' $tools 'ubuntu' '24.04' $fallback) -eq 'Test-FallbackDot') 'Fallback versions above the minimum must pass.'
$script:fallbackVersion = '15.0.0'
Assert-Failure { Resolve-Graphviz 'Test-InstalledDot' $tools 'ubuntu' '24.04' $fallback } 'expected 16.1.0 or later'
Assert-Failure { Resolve-Graphviz 'Test-FailedDot' $tools 'ubuntu' '24.04' $fallback } 'Cannot determine'
Assert-Failure { Resolve-Graphviz 'Test-UnknownDot' $tools 'ubuntu' '24.04' $fallback } 'Cannot determine'
Assert-Failure { Select-GraphvizFallback $tools 'ubuntu' '99.04' } 'refusing to install a package from another release'
Assert-Failure { Select-GraphvizFallback $tools 'debian' '12' } 'No Graphviz fallback'
foreach ($release in @('22.04', '24.04', '26.04')) {
    $asset = Select-GraphvizFallback $tools 'ubuntu' $release
    Assert-True ($asset.url.Contains("ubuntu_${release}_")) 'Fallback must match the exact Ubuntu release.'
    Assert-True ($asset.sha256 -match '^[a-f0-9]{64}$') 'Fallback must have a SHA-256 digest.'
}
Assert-True ($tools.graphvizFallbacks.windows.sha256 -match '^[a-f0-9]{64}$') 'Windows fallback must have a SHA-256 digest.'

$temporary = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('cogs-graphviz-test-' + [guid]::NewGuid().ToString('N'))))
[IO.Directory]::CreateDirectory($temporary) | Out-Null
try {
    $file = Join-Path $temporary 'download.bin'
    [IO.File]::WriteAllText($file, 'verified content')
    Assert-GraphvizChecksum $file (Get-FileHash $file -Algorithm SHA256).Hash
    Assert-Failure { Assert-GraphvizChecksum $file ('0' * 64) } 'SHA-256 verification failed'
    Assert-Failure { Assert-GraphvizChecksum $file 'invalid' } 'SHA-256 verification failed'

    # Exercise Ubuntu installation without downloading packages or invoking sudo.
    # The system plugin cache requires elevation, unlike the later smoke renders.
    & {
        param([string] $TestDirectory, [string] $ArchiveFixture)
        . (Join-Path $PSScriptRoot 'Install-Graphviz.ps1')
        $systemDot = (Join-Path $TestDirectory 'system Graphviz/bin/dot').Replace('\', '/')
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($systemDot)) | Out-Null
        [IO.File]::WriteAllText($systemDot, 'This fixture must never be executed.')
        $sudoCalls = [Collections.Generic.List[object]]::new()
        $configExit = 0
        function Invoke-WebRequest {
            param([string] $Uri, [string] $OutFile)
            [IO.File]::Copy($ArchiveFixture, $OutFile)
        }
        function dpkg-deb { $global:LASTEXITCODE = 0; 'graphviz-fixture' }
        function dpkg { $global:LASTEXITCODE = 0; $systemDot }
        function sudo {
            $sudoCalls.Add(@($args))
            if ($args[0] -eq 'apt') { $global:LASTEXITCODE = 0; return }
            Assert-True ($args.Count -eq 3 -and $args[0] -eq '--' -and $args[1] -ceq $systemDot -and $args[2] -eq '-c') `
                'Plugin configuration must invoke the selected absolute dot path through sudo.'
            $global:LASTEXITCODE = $configExit
        }
        $asset = [pscustomobject]@{
            url = 'https://example.invalid/graphviz.deb'
            sha256 = (Get-FileHash -LiteralPath $ArchiveFixture -Algorithm SHA256).Hash
        }
        $selected = Install-GraphvizFallback $asset 'ubuntu' (Join-Path $TestDirectory 'successful install')
        Assert-True ($selected -ceq $systemDot) 'Installation must return the selected dot path.'
        Assert-True ($sudoCalls.Count -eq 2) 'Expected elevated package installation and plugin configuration.'
        Assert-True ($sudoCalls[1][0] -eq '--' -and $sudoCalls[1][1] -ceq $systemDot -and $sudoCalls[1][2] -eq '-c') `
            'Plugin configuration must be elevated, including paths with spaces.'
        $configExit = 7
        Assert-Failure { Install-GraphvizFallback $asset 'ubuntu' (Join-Path $TestDirectory 'failed configuration') } `
            'Graphviz plugin configuration failed'
    } $temporary $file
} finally {
    if (-not $temporary.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $temporary -Recurse -Force
}
Write-Host 'Graphviz installation selection and checksum tests passed.'
