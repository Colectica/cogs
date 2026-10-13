param(
    [string] $ToolDirectory,
    [string] $ManifestPath = (Join-Path $PSScriptRoot '../tools.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-GraphvizVersion([string] $Executable) {
    $banner = (& $Executable -V 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $banner -notmatch '\bgraphviz\s+version\s+(\d+)\.(\d+)(?:\.(\d+))?(?=\s|\(|$)') {
        throw "Cannot determine Graphviz version from '$Executable': $banner"
    }
    $patch = if ($Matches[3]) { $Matches[3] } else { '0' }
    return [version]::Parse("$($Matches[1]).$($Matches[2]).$patch")
}

function Select-GraphvizFallback($Tools, [string] $Platform, [string] $UbuntuRelease) {
    if ($Platform -eq 'windows') { return $Tools.graphvizFallbacks.windows }
    if ($Platform -ne 'ubuntu') { throw "No Graphviz fallback is configured for platform '$Platform'." }
    $entry = $Tools.graphvizFallbacks.ubuntu.PSObject.Properties[$UbuntuRelease]
    if (-not $entry) {
        throw "No matching Graphviz fallback is configured for Ubuntu '$UbuntuRelease'; refusing to install a package from another release."
    }
    return $entry.Value
}

function Assert-GraphvizChecksum([string] $Path, [string] $Expected) {
    if ($Expected -notmatch '^[0-9a-fA-F]{64}$' -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Expected) {
        throw "Graphviz fallback SHA-256 verification failed for '$Path'."
    }
}

# Separate selection from installation so both package-manager and fallback paths
# can be exercised without installing tools into the test machine.
function Resolve-Graphviz([string] $Executable, $Tools, [string] $Platform,
    [string] $UbuntuRelease, [scriptblock] $InstallFallback) {
    $minimum = [version]::Parse($Tools.graphvizMinimum)
    $version = Get-GraphvizVersion $Executable
    if ($version -ge $minimum) { return $Executable }
    Write-Host "Graphviz $version is below $minimum; selecting the official release fallback."
    $asset = Select-GraphvizFallback $Tools $Platform $UbuntuRelease
    $replacement = & $InstallFallback $asset
    $version = Get-GraphvizVersion $replacement
    if ($version -lt $minimum) { throw "Graphviz fallback supplied $version; expected $minimum or later." }
    return $replacement
}

function Install-GraphvizFallback($Asset, [string] $Platform, [string] $Directory) {
    $directoryPath = [IO.Path]::GetFullPath($Directory)
    [IO.Directory]::CreateDirectory($directoryPath) | Out-Null
    $archive = Join-Path $directoryPath ([IO.Path]::GetFileName(([uri]$Asset.url).AbsolutePath))
    Invoke-WebRequest -Uri $Asset.url -OutFile $archive
    Assert-GraphvizChecksum $archive $Asset.sha256
    if ($Platform -eq 'windows') {
        $destination = Join-Path $directoryPath 'fallback'
        Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force
        $executables = @(Get-ChildItem -LiteralPath $destination -Filter dot.exe -File -Recurse)
        if ($executables.Count -ne 1) { throw 'The Graphviz archive must contain exactly one dot.exe.' }
        return $executables[0].FullName
    }
    # The official CMake package has no dependency declarations. Its JPEG
    # renderer uses GDK, unlike the older distro package's GD renderer.
    # This package brings the correct GTK runtime for each Ubuntu release.
    & sudo apt install --yes $archive libgtk-3-bin | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Graphviz fallback installation failed with exit $LASTEXITCODE." }
    $package = (& dpkg-deb --field $archive Package | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $package) { throw 'Cannot determine the installed Graphviz package name.' }
    $paths = @(& dpkg -L $package)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot locate dot in the installed Graphviz package.' }
    $executables = @($paths | Where-Object { $_ -match '/bin/dot$' -and (Test-Path -LiteralPath $_ -PathType Leaf) })
    if ($executables.Count -ne 1) { throw 'The Graphviz package must contain exactly one dot executable.' }
    # The package owns the system plugin cache, so configuration needs root.
    & sudo '--' $executables[0] -c | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Graphviz plugin configuration failed.' }
    return $executables[0]
}

function Test-GraphvizFormats([string] $Executable, [string] $Directory) {
    [IO.Directory]::CreateDirectory($Directory) | Out-Null
    $inputFile = Join-Path $Directory 'smoke.dot'
    [IO.File]::WriteAllText($inputFile, 'digraph { node [shape=box]; a [label=<<TABLE><TR><TD>Card</TD></TR></TABLE>>]; a -> b [label="0..n"]; }', [Text.UTF8Encoding]::new($false))
    foreach ($format in @('svg', 'png', 'jpeg', 'pdf')) {
        $output = Join-Path $Directory "smoke.$format"
        & $Executable "-T$format" -o $output $inputFile | Out-Host
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output) -or (Get-Item -LiteralPath $output).Length -eq 0) {
            throw "Graphviz $format smoke rendering failed."
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    if ([string]::IsNullOrWhiteSpace($ToolDirectory)) { throw 'ToolDirectory is required.' }
    $tools = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    $release = ''
    if ($IsWindows) {
        & choco install graphviz --yes --no-progress | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Graphviz Chocolatey installation failed with exit $LASTEXITCODE." }
        $platform = 'windows'
        $candidate = Get-Command dot -ErrorAction SilentlyContinue
        $executable = if ($candidate) { $candidate.Source } else { Join-Path $env:ProgramFiles 'Graphviz/bin/dot.exe' }
    } else {
        & sudo apt update | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "apt update failed with exit $LASTEXITCODE." }
        & sudo apt install --yes graphviz | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Graphviz apt installation failed with exit $LASTEXITCODE." }
        $executable = (Get-Command dot -ErrorAction Stop).Source
        $os = @{}
        foreach ($line in Get-Content -LiteralPath /etc/os-release) {
            if ($line -match '^(ID|VERSION_ID)=(.*)$') { $os[$Matches[1]] = $Matches[2].Trim('"') }
        }
        $platform = $os['ID']
        $release = $os['VERSION_ID']
    }
    $dot = Resolve-Graphviz $executable $tools $platform $release {
        param($asset)
        Install-GraphvizFallback $asset $platform $ToolDirectory
    }
    Write-Host "Selected Graphviz $(Get-GraphvizVersion $dot): $dot"
    Test-GraphvizFormats $dot (Join-Path $ToolDirectory 'smoke')
    $dot
}
