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
$root = New-PackageWorkDirectory $WorkDirectory 'packaging regression with spaces'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$console = Join-Path $repository 'Cogs.Console/Cogs.Console.csproj'
$common = Join-Path $repository 'Cogs.Common/Cogs.Common.csproj'
$sharedArtifacts = Join-Path $root 'contaminated shared artifacts'

function Assert-PackageFailure([scriptblock] $Action, [string] $Text) {
    try { & $Action | Out-Null } catch {
        if ($_.Exception.Message.Contains($Text)) { Write-Host "Rejected expected defect: $Text"; return }
        throw
    }
    throw "Expected rejection containing '$Text'."
}

function New-TamperedPackage([string] $Source, [string] $Name, [scriptblock] $Edit) {
    $path = Join-Path $root "$Name.nupkg"
    [IO.File]::Copy($Source, $path)
    $zip = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Update)
    try { & $Edit $zip } finally { $zip.Dispose() }
    return $path
}

function Set-PackageEntry($Zip, [string] $Name, [byte[]] $Content) {
    $Zip.GetEntry($Name).Delete()
    $entry = $Zip.CreateEntry($Name)
    $stream = $entry.Open()
    try { $stream.Write($Content, 0, $Content.Length) } finally { $stream.Dispose() }
}

try {
    $loadedBefore = @([AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetName().Name } |
        Where-Object { $_ -ceq 'cogs' -or $_.StartsWith('Cogs.', [StringComparison]::Ordinal) } | Sort-Object)
    # Reproduce the old shared-output sequence without touching the repository's bin/obj.
    # The release CLI keeps its versioned reference while a later runtime probe rebuilds Common.
    $releaseProperties = @("-p:Version=$PackageVersion", '-p:GeneratePackageOnBuild=false')
    Invoke-PackageProcess 'dotnet' (@('restore', $console, "-p:ArtifactsPath=$sharedArtifacts",
        "-p:Configuration=$Configuration", '--verbosity', 'minimal') + $releaseProperties) `
        (Join-Path $root 'initial-restore.log') | Out-Null
    Invoke-PackageProcess 'dotnet' (@('build', $console, '--configuration', $Configuration,
        '--no-restore', '--artifacts-path', $sharedArtifacts, '--verbosity', 'minimal') + $releaseProperties) `
        (Join-Path $root 'initial-build.log') | Out-Null
    Invoke-PackageProcess 'dotnet' @('restore', $common, "-p:ArtifactsPath=$sharedArtifacts",
        "-p:Configuration=$Configuration", '-p:GeneratePackageOnBuild=false', '--verbosity', 'minimal') `
        (Join-Path $root 'contamination-restore.log') | Out-Null
    Invoke-PackageProcess 'dotnet' @('build', $common, '--configuration', $Configuration,
        '--no-restore', '--artifacts-path', $sharedArtifacts, '-p:GeneratePackageOnBuild=false', '--verbosity', 'minimal') `
        (Join-Path $root 'contamination-build.log') | Out-Null
    $commonDll = @(Get-ChildItem -LiteralPath (Join-Path $sharedArtifacts 'bin') -Filter Cogs.Common.dll -Recurse |
        Where-Object { $_.FullName -match '[\\/]Cogs.Common[\\/]' })
    $cliDll = @(Get-ChildItem -LiteralPath (Join-Path $sharedArtifacts 'bin') -Filter cogs.dll -Recurse)
    if ($commonDll.Count -ne 1 -or $cliDll.Count -ne 1) { throw 'Could not locate contamination fixtures.' }
    $poisoned = Read-ToolAssemblyMetadata ([IO.File]::ReadAllBytes($commonDll[0].FullName))
    $release = Read-ToolAssemblyMetadata ([IO.File]::ReadAllBytes($cliDll[0].FullName))
    if ($poisoned.Version -ne [version]'1.0.0.0' -or
        $release.References['Cogs.Common'] -ne (Get-ToolAssemblyVersion $PackageVersion) -or
        $poisoned.Version -eq $release.References['Cogs.Common']) {
        throw 'Regression fixture did not reproduce a mismatched shared Cogs.Common build.'
    }
    Write-Host "Reproduced shared-output contamination: CLI requires $($release.References['Cogs.Common']), Common is $($poisoned.Version)."
    $first = & (Join-Path $PSScriptRoot 'New-ToolPackage.ps1') -PackageVersion $PackageVersion `
        -Configuration $Configuration -WorkDirectory (Join-Path $root 'isolated work') `
        -PackageOutputDirectory (Join-Path $root 'first qualified package')
    $metadata = Assert-ToolPackageMetadata $first.PackagePath $PackageVersion
    $loadedAfter = @([AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetName().Name } |
        Where-Object { $_ -ceq 'cogs' -or $_.StartsWith('Cogs.', [StringComparison]::Ordinal) } | Sort-Object)
    if (($loadedBefore -join "`n") -cne ($loadedAfter -join "`n")) {
        throw 'Package inspection loaded a COGS assembly into the verification process.'
    }
    $payload = $metadata.Payload

    $missing = New-TamperedPackage $first.PackagePath 'missing assembly' {
        param($zip); $zip.GetEntry($payload + 'Cogs.Common.dll').Delete()
    }
    Assert-PackageFailure { Assert-ToolPackageMetadata $missing $PackageVersion } 'exactly one'
    $mismatch = New-TamperedPackage $first.PackagePath 'wrong assembly version' {
        param($zip); Set-PackageEntry $zip ($payload + 'Cogs.Common.dll') ([IO.File]::ReadAllBytes($commonDll[0].FullName))
    }
    Assert-PackageFailure { Assert-ToolPackageMetadata $mismatch $PackageVersion } 'Assembly version mismatch'

    # Own version can be correct while a compiled internal assembly reference is wrong.
    $referenceFixture = Join-Path $root 'assembly reference fixture'
    [IO.Directory]::CreateDirectory($referenceFixture) | Out-Null
    $fixtureProject = Join-Path $referenceFixture 'ReferenceFixture.csproj'
    $hintPath = [Security.SecurityElement]::Escape($commonDll[0].FullName)
    [IO.File]::WriteAllText($fixtureProject, @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyName>Cogs.Dto</AssemblyName><Version>$PackageVersion</Version></PropertyGroup>
  <ItemGroup><Reference Include="Cogs.Common"><HintPath>$hintPath</HintPath></Reference></ItemGroup>
</Project>
"@)
    [IO.File]::WriteAllText((Join-Path $referenceFixture 'Probe.cs'),
        'public class Probe { public Cogs.Common.CogsError Error { get; set; } }')
    Invoke-PackageProcess 'dotnet' @('build', $fixtureProject, '--configuration', $Configuration,
        '--verbosity', 'minimal') (Join-Path $root 'reference-fixture.log') | Out-Null
    $fixtureDll = Join-Path $referenceFixture "bin/$Configuration/net10.0/Cogs.Dto.dll"
    $referenceMismatch = New-TamperedPackage $first.PackagePath 'wrong assembly reference' {
        param($zip); Set-PackageEntry $zip ($payload + 'Cogs.Dto.dll') ([IO.File]::ReadAllBytes($fixtureDll))
    }
    Assert-PackageFailure { Assert-ToolPackageMetadata $referenceMismatch $PackageVersion } 'Assembly reference mismatch'

    $depsMismatch = New-TamperedPackage $first.PackagePath 'wrong dependency version' {
        param($zip)
        $name = $payload + 'cogs.deps.json'
        $deps = Read-PackageText $zip $name | ConvertFrom-Json -AsHashtable
        $deps.targets[$deps.runtimeTarget.name]["cogs/$PackageVersion"].dependencies['Cogs.Common'] = '1.0.0'
        Set-PackageEntry $zip $name ([Text.Encoding]::UTF8.GetBytes(($deps | ConvertTo-Json -Depth 30)))
    }
    Assert-PackageFailure { Assert-ToolPackageMetadata $depsMismatch $PackageVersion } 'Dependency reference mismatch'
    $pathMismatch = New-TamperedPackage $first.PackagePath 'wrong runtime path' {
        param($zip)
        $name = $payload + 'cogs.deps.json'
        $deps = Read-PackageText $zip $name | ConvertFrom-Json -AsHashtable
        $deps.targets[$deps.runtimeTarget.name]["Cogs.Common/$PackageVersion"].runtime = @{ 'missing/Cogs.Common.dll' = @{} }
        Set-PackageEntry $zip $name ([Text.Encoding]::UTF8.GetBytes(($deps | ConvertTo-Json -Depth 30)))
    }
    Assert-PackageFailure { Assert-ToolPackageMetadata $pathMismatch $PackageVersion } 'Dependency runtime path mismatch'
    $libraryMismatch = New-TamperedPackage $first.PackagePath 'wrong dependency library' {
        param($zip)
        $name = $payload + 'cogs.deps.json'
        $deps = Read-PackageText $zip $name | ConvertFrom-Json -AsHashtable
        $entry = $deps.libraries["Cogs.Common/$PackageVersion"]
        $deps.libraries.Remove("Cogs.Common/$PackageVersion")
        $deps.libraries['Cogs.Common/1.0.0'] = $entry
        Set-PackageEntry $zip $name ([Text.Encoding]::UTF8.GetBytes(($deps | ConvertTo-Json -Depth 30)))
    }
    Assert-PackageFailure { Assert-ToolPackageMetadata $libraryMismatch $PackageVersion } 'Dependency library mismatch'
    $entryMismatch = New-TamperedPackage $first.PackagePath 'wrong tool entry point' {
        param($zip)
        $name = $payload + 'DotnetToolSettings.xml'
        $text = (Read-PackageText $zip $name).Replace('EntryPoint="cogs.dll"', 'EntryPoint="missing.dll"')
        Set-PackageEntry $zip $name ([Text.Encoding]::UTF8.GetBytes($text))
    }
    Assert-PackageFailure { Assert-ToolPackageMetadata $entryMismatch $PackageVersion } 'Invalid cogs tool entry point'
    Assert-PackageFailure { Assert-ToolPackageMetadata $first.PackagePath '0.0.0' } 'Package identity/version mismatch'

    # A second invocation gets another entirely fresh build, feed, cache, and installed-tool directory.
    $second = & (Join-Path $PSScriptRoot 'New-ToolPackage.ps1') -PackageVersion $PackageVersion `
        -Configuration $Configuration -WorkDirectory (Join-Path $root 'isolated work') `
        -PackageOutputDirectory $PackageOutputDirectory
    if ($first.WorkDirectory -eq $second.WorkDirectory) { throw 'Packaging reused a per-run workspace.' }
    Write-Host 'Isolated packaging, package metadata rejection cases, and repeated installed-tool general help passed.'
    $second
} catch {
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $root 'failure.log') -Encoding utf8
    throw
}
