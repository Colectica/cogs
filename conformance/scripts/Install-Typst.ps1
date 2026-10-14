[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ToolDirectory,
    [string] $ManifestPath = (Join-Path $PSScriptRoot '../tools.json')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$tools = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$platform = if ($IsWindows) { 'windows' } elseif ($IsLinux) { 'linux' } else { throw 'CI Typst installer supports Windows and Linux x64.' }
if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') { throw 'CI Typst installer requires x64.' }
$asset = $tools.typstAssets.$platform
$root = [IO.Path]::GetFullPath($ToolDirectory)
[IO.Directory]::CreateDirectory($root) | Out-Null
$archive = Join-Path $root ([IO.Path]::GetFileName(([uri]$asset.url).AbsolutePath))
Invoke-WebRequest -Uri $asset.url -OutFile $archive
if ($asset.sha256 -notmatch '^[0-9a-f]{64}$' -or
    (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -cne $asset.sha256) {
    throw 'Typst release archive SHA-256 verification failed.'
}
$destination = Join-Path $root "typst-$($tools.typst)"
[IO.Directory]::CreateDirectory($destination) | Out-Null
if ($IsWindows) { Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force }
else {
    & tar -xJf $archive -C $destination
    if ($LASTEXITCODE -ne 0) { throw 'Typst archive extraction failed.' }
}
$name = if ($IsWindows) { 'typst.exe' } else { 'typst' }
$executables = @(Get-ChildItem -LiteralPath $destination -Filter $name -File -Recurse)
if ($executables.Count -ne 1) { throw 'Expected exactly one Typst executable.' }
$executable = $executables[0].FullName
$version = (& $executable --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $version -notmatch '^typst (\d+\.\d+\.\d+)(?:\s|$)' -or
    $Matches[1] -cne $tools.typst) { throw "Unexpected Typst qualification version: $version" }
Write-Host "$version ($executable)"
$global:LASTEXITCODE = 0
Write-Output $executable
