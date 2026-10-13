param(
    [Parameter(Mandatory)] [string] $PackageVersion,
    [string] $Configuration = 'Release',
    [Parameter(Mandatory)] [string] $WorkDirectory,
    [Parameter(Mandatory)] [string] $PackageOutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Verify-ToolPackage.ps1') -WorkDirectory $WorkDirectory
Get-ToolAssemblyVersion $PackageVersion | Out-Null
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Cogs.Console/Cogs.Console.csproj'))
$work = New-PackageWorkDirectory $WorkDirectory 'pack'
$artifacts = Join-Path $work 'artifacts'
$packages = Join-Path $work 'packages'
$properties = @("-p:Version=$PackageVersion", '-p:GeneratePackageOnBuild=false')
Write-Host "Isolated package build: $work"
try {
    # Restore must see the same global properties as build and pack, including all project references.
    Invoke-PackageProcess 'dotnet' (@('restore', $project, '--verbosity', 'minimal',
        "-p:ArtifactsPath=$artifacts", "-p:Configuration=$Configuration") + $properties) `
        (Join-Path $work 'restore.log') | Out-Null
    Invoke-PackageProcess 'dotnet' (@('build', $project, '--configuration', $Configuration,
        '--no-restore', '--artifacts-path', $artifacts, '--verbosity', 'minimal') + $properties) `
        (Join-Path $work 'build.log') | Out-Null
    Invoke-PackageProcess 'dotnet' (@('pack', $project, '--configuration', $Configuration,
        '--no-build', '--no-restore', '--artifacts-path', $artifacts, '--output', $packages,
        '--verbosity', 'minimal', '-p:IncludeSymbols=true', '-p:SymbolPackageFormat=snupkg') + $properties) `
        (Join-Path $work 'pack.log') | Out-Null
    $package = Join-Path $packages "cogs.$PackageVersion.nupkg"
    $symbols = Join-Path $packages "cogs.$PackageVersion.snupkg"
    if (-not (Test-Path -LiteralPath $symbols -PathType Leaf)) { throw "Missing symbol package '$symbols'." }
    Test-InstalledToolPackage $package $PackageVersion $work | Out-Null
    # Only qualified outputs leave the per-run workspace. Never select a stale package by wildcard.
    $output = [IO.Path]::GetFullPath($PackageOutputDirectory)
    [IO.Directory]::CreateDirectory($output) | Out-Null
    $result = Join-Path $output "cogs.$PackageVersion.nupkg"
    $symbolResult = Join-Path $output "cogs.$PackageVersion.snupkg"
    [IO.File]::Copy($package, $result, $true)
    [IO.File]::Copy($symbols, $symbolResult, $true)
    [pscustomobject]@{ PackagePath = $result; SymbolPackagePath = $symbolResult; WorkDirectory = $work }
} catch {
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $work 'failure.log') -Encoding utf8
    throw
}
