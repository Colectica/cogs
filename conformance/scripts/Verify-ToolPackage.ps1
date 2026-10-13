param(
    [string] $PackagePath,
    [string] $ExpectedVersion,
    [string] $WorkDirectory = [IO.Path]::GetTempPath()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ToolAssemblyVersion([string] $PackageVersion) {
    if ($PackageVersion -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
        throw "Expected the numeric major.minor.commit-count package version, got '$PackageVersion'."
    }
    return [version]::Parse("$PackageVersion.0")
}

function New-PackageWorkDirectory([string] $Parent, [string] $Prefix) {
    $path = [IO.Path]::GetFullPath((Join-Path $Parent "$Prefix-$([guid]::NewGuid().ToString('N'))"))
    [IO.Directory]::CreateDirectory($path) | Out-Null
    return $path
}

function Invoke-PackageProcess([string] $Executable, [string[]] $Arguments,
    [string] $LogPath, [hashtable] $Environment = @{}) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    foreach ($key in $Environment.Keys) { $start.Environment[$key] = $Environment[$key] }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw "Could not start '$Executable'." }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText($LogPath,
            "$Executable $($Arguments | ConvertTo-Json -Compress)`nExit: $($process.ExitCode)`n$output`n$errorOutput",
            [Text.UTF8Encoding]::new($false))
        if ($output) { Write-Host $output.TrimEnd() }
        if ($errorOutput) { Write-Host $errorOutput.TrimEnd() }
        if ($process.ExitCode -ne 0) {
            throw "'$Executable' exited with code $($process.ExitCode). See '$LogPath'."
        }
        return $output
    } finally { $process.Dispose() }
}

function Read-PackageEntry($Zip, [string] $Name) {
    $entries = @($Zip.Entries | Where-Object { $_.FullName -ceq $Name })
    if ($entries.Count -ne 1) { throw "Package must contain exactly one '$Name'." }
    $stream = $entries[0].Open()
    $buffer = [IO.MemoryStream]::new()
    try { $stream.CopyTo($buffer); return ,$buffer.ToArray() }
    finally { $stream.Dispose(); $buffer.Dispose() }
}

function Read-PackageText($Zip, [string] $Name) {
    $stream = [IO.MemoryStream]::new((Read-PackageEntry $Zip $Name), $false)
    $reader = [IO.StreamReader]::new($stream, [Text.UTF8Encoding]::new($false, $true), $true)
    try { return $reader.ReadToEnd() } finally { $reader.Dispose(); $stream.Dispose() }
}

function Read-ToolAssemblyMetadata([byte[]] $Bytes) {
    # Inspect PE metadata only: never load tool assemblies into this process.
    $stream = [IO.MemoryStream]::new($Bytes, $false)
    $pe = [Reflection.PortableExecutable.PEReader]::new($stream)
    try {
        $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $assembly = $metadata.GetAssemblyDefinition()
        $references = @{}
        foreach ($handle in $metadata.AssemblyReferences) {
            $reference = $metadata.GetAssemblyReference($handle)
            $references[$metadata.GetString($reference.Name)] = $reference.Version
        }
        return [pscustomobject]@{
            Name = $metadata.GetString($assembly.Name)
            Version = $assembly.Version
            References = $references
        }
    } finally { $pe.Dispose(); $stream.Dispose() }
}

function Assert-ToolPackageMetadata([string] $Path, [string] $Version) {
    $assemblyVersion = Get-ToolAssemblyVersion $Version
    $names = @('cogs', 'Cogs.Common', 'Cogs.Dto', 'Cogs.Model', 'Cogs.Publishers', 'Cogs.Validation')
    $zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Path))
    try {
        [xml] $nuspec = Read-PackageText $zip 'cogs.nuspec'
        $package = $nuspec.package.metadata
        if ($package.id -cne 'cogs' -or $package.version -cne $Version) {
            throw "Package identity/version mismatch: expected cogs/$Version, got $($package.id)/$($package.version)."
        }
        if ($package.packageTypes.packageType.name -cne 'DotnetTool') { throw 'Package is not a .NET tool.' }
        $settings = @($zip.Entries | Where-Object { $_.FullName -cmatch '^tools/[^/]+/any/DotnetToolSettings\.xml$' })
        if ($settings.Count -ne 1) { throw 'Expected exactly one tool payload.' }
        $payload = $settings[0].FullName.Substring(0, $settings[0].FullName.LastIndexOf('/') + 1)
        [xml] $tool = Read-PackageText $zip $settings[0].FullName
        $commands = @($tool.DotNetCliTool.Commands.Command)
        if ($commands.Count -ne 1 -or $commands[0].Name -cne 'cogs' -or
            $commands[0].EntryPoint -cne 'cogs.dll' -or $commands[0].Runner -cne 'dotnet') {
            throw 'Invalid cogs tool entry point.'
        }
        $assemblies = @{}
        foreach ($name in $names) {
            $assembly = Read-ToolAssemblyMetadata (Read-PackageEntry $zip "$payload$name.dll")
            if ($assembly.Name -cne $name -or $assembly.Version -ne $assemblyVersion) {
                throw "Assembly version mismatch for '$name': expected $assemblyVersion, got $($assembly.Name) $($assembly.Version)."
            }
            foreach ($referenceName in $assembly.References.Keys) {
                if ($referenceName -ceq 'cogs' -or $referenceName.StartsWith('Cogs.', [StringComparison]::Ordinal)) {
                    if ($names -cnotcontains $referenceName -or $assembly.References[$referenceName] -ne $assemblyVersion) {
                        throw "Assembly reference mismatch in '$name': $referenceName $($assembly.References[$referenceName]); expected $assemblyVersion."
                    }
                }
            }
            $assemblies[$name] = $assembly
        }
        $deps = Read-PackageText $zip ($payload + 'cogs.deps.json') | ConvertFrom-Json -AsHashtable
        if (-not $deps.targets.ContainsKey($deps.runtimeTarget.name)) { throw 'Missing dependency runtime target.' }
        foreach ($name in $names) {
            $key = "$name/$Version"
            $entries = @($deps.libraries.Keys | Where-Object { $_.StartsWith("$name/", [StringComparison]::Ordinal) })
            if ($entries.Count -ne 1 -or $entries[0] -cne $key -or $deps.libraries[$key].type -cne 'project') {
                throw "Dependency library mismatch for '$name': expected '$key'."
            }
        }
        foreach ($target in $deps.targets.Values) {
            foreach ($name in $names) {
                $key = "$name/$Version"
                $entries = @($target.Keys | Where-Object { $_.StartsWith("$name/", [StringComparison]::Ordinal) })
                if ($entries.Count -ne 1 -or $entries[0] -cne $key) { throw "Dependency target mismatch for '$name'." }
                $runtime = $target[$key].runtime
                if ($runtime.Count -ne 1 -or @($runtime.Keys)[0] -cne "$name.dll") {
                    throw "Dependency runtime path mismatch for '$name'."
                }
                $asset = $runtime["$name.dll"]
                if ($asset.ContainsKey('assemblyVersion') -and $asset.assemblyVersion -cne "$assemblyVersion") {
                    throw "Dependency runtime assembly version mismatch for '$name'."
                }
            }
            # Check COGS dependency edges in every entry, without imposing our version on third-party libraries.
            foreach ($entry in $target.Values) {
                if (-not $entry.ContainsKey('dependencies')) { continue }
                foreach ($name in $names) {
                    if ($entry.dependencies.ContainsKey($name) -and $entry.dependencies[$name] -cne $Version) {
                        throw "Dependency reference mismatch for '$name': expected '$Version'."
                    }
                }
            }
            foreach ($name in $names) {
                # Assembly references can be supplied through transitive project references.
                $reachable = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                $pending = [Collections.Generic.Queue[string]]::new()
                $pending.Enqueue($name)
                while ($pending.Count) {
                    $current = $target["$($pending.Dequeue())/$Version"]
                    if (-not $current.ContainsKey('dependencies')) { continue }
                    foreach ($dependency in $current.dependencies.Keys) {
                        if ($names -ccontains $dependency -and $reachable.Add($dependency)) { $pending.Enqueue($dependency) }
                    }
                }
                foreach ($referenceName in $assemblies[$name].References.Keys) {
                    if ($names -ccontains $referenceName -and -not $reachable.Contains($referenceName)) {
                        throw "Dependency reference mismatch in '$name' for '$referenceName'."
                    }
                }
            }
        }
        Read-PackageEntry $zip ($payload + 'cogs.runtimeconfig.json') | Out-Null
        return [pscustomobject]@{ PackageVersion = $Version; AssemblyVersion = "$assemblyVersion"; Payload = $payload }
    } finally { $zip.Dispose() }
}

function Test-InstalledToolPackage([string] $Path, [string] $Version, [string] $WorkRoot) {
    $work = New-PackageWorkDirectory $WorkRoot 'verify'
    Write-Host "Package verification logs: $work"
    try {
        $metadata = Assert-ToolPackageMetadata $Path $Version
        $metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'metadata.json') -Encoding utf8
        $feed = Join-Path $work 'local feed'
        $toolPath = Join-Path $work 'installed tool'
        [IO.Directory]::CreateDirectory($feed) | Out-Null
        [IO.File]::Copy([IO.Path]::GetFullPath($Path), (Join-Path $feed "cogs.$Version.nupkg"))
        $config = Join-Path $work 'NuGet.Config'
        $escapedFeed = [Security.SecurityElement]::Escape($feed)
        [IO.File]::WriteAllText($config, @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear/><add key="local" value="$escapedFeed"/></packageSources>
  <fallbackPackageFolders><clear/></fallbackPackageFolders>
</configuration>
"@, [Text.UTF8Encoding]::new($false))
        $environment = @{
            NUGET_PACKAGES = (Join-Path $work 'package cache')
            NUGET_HTTP_CACHE_PATH = (Join-Path $work 'http cache')
            DOTNET_CLI_HOME = (Join-Path $work 'cli home')
            DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
            DOTNET_NOLOGO = 'true'
        }
        Invoke-PackageProcess 'dotnet' @('tool', 'install', 'cogs', '--version', $Version,
            '--tool-path', $toolPath, '--configfile', $config, '--no-cache', '--verbosity', 'minimal') `
            (Join-Path $work 'install.log') $environment | Out-Null
        $executable = Join-Path $toolPath $(if ($IsWindows) { 'cogs.exe' } else { 'cogs' })
        $help = Invoke-PackageProcess $executable @('--help') (Join-Path $work 'help.log') $environment
        if ($help -notmatch '(?m)^Usage: Cogs ' -or $help -notmatch '(?m)^Commands:' -or
            $help -notmatch "(?m)^Version $([regex]::Escape($metadata.AssemblyVersion))\s*$") {
            throw "Installed tool did not produce normal general help. See '$work/help.log'."
        }
        Write-Host "Installed cogs $Version passed general --help; no model or publisher commands were run."
        return $metadata
    } catch {
        $_ | Out-String | Set-Content -LiteralPath (Join-Path $work 'failure.log') -Encoding utf8
        throw
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    if (-not $PackagePath -or -not $ExpectedVersion) { throw 'PackagePath and ExpectedVersion are required.' }
    Test-InstalledToolPackage $PackagePath $ExpectedVersion $WorkDirectory
}
