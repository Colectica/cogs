[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $CogsDll,

    [string] $Snapshot = (Join-Path $PSScriptRoot '..\downstream\snapshots.json')
)

$ErrorActionPreference = 'Stop'
$CogsDll = [IO.Path]::GetFullPath($CogsDll)
$Snapshot = [IO.Path]::GetFullPath($Snapshot)
$definition = Get-Content -LiteralPath $Snapshot -Raw | ConvertFrom-Json
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$tempRoot = Join-Path $tempBase ("cogs-downstream-" + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($tempRoot) | Out-Null

function Get-DiagnosticCounts([string] $Text) {
    $counts = [ordered]@{}
    [regex]::Matches($Text, '(?<![A-Z0-9-])(?:COGS|CLI|MIG|PUB|PROJ|INS)-[A-Z0-9-]*[0-9]{3,4}(?![A-Z0-9-])') |
        ForEach-Object Value | Group-Object | Sort-Object Name | ForEach-Object { $counts[$_.Name] = $_.Count }
    $counts
}

function Assert-Counts($Expected, $Actual, [string] $Label, [string] $Output) {
    $expectedPairs = @($Expected.PSObject.Properties | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" })
    $actualPairs = @($Actual.GetEnumerator() | Sort-Object Key | ForEach-Object { "$($_.Key)=$($_.Value)" })
    if ([string]::Join('|', $expectedPairs) -cne [string]::Join('|', $actualPairs)) {
        throw "$Label diagnostic drift.`nExpected: $($expectedPairs -join ', ')`nActual: $($actualPairs -join ', ')`n$Output"
    }
}

try {
    foreach ($model in $definition.models) {
        $repository = Join-Path $tempRoot $model.id
        & git clone --quiet --filter=blob:none --no-checkout $model.url $repository
        if ($LASTEXITCODE -ne 0) { throw "git clone failed for $($model.id)." }
        & git -C $repository checkout --quiet --detach $model.commit
        if ($LASTEXITCODE -ne 0) { throw "git checkout failed for $($model.id)@$($model.commit)." }
        $actualCommit = (& git -C $repository rev-parse HEAD).Trim()
        if ($actualCommit -cne [string]$model.commit) { throw "$($model.id) resolved to unexpected commit $actualCommit." }

        foreach ($command in $definition.publishCommands) {
            $arguments = @([string]$command.name) + @($command.arguments | ForEach-Object { [string]$_ })
            $target = $null
            if ($command.name -eq 'validate') {
                $arguments += $repository
            } else {
                $target = Join-Path $tempRoot ("output-$($model.id)-$($command.name)")
                $arguments += @('--overwrite', $repository, $target)
            }
            $output = & dotnet $CogsDll @arguments 2>&1 | Out-String
            $exitCode = $LASTEXITCODE
            if ($exitCode -ne [int]$model.expectedExitCode) {
                throw "$($model.id) $($command.name) expected exit $($model.expectedExitCode), received $exitCode.`n$output"
            }
            Assert-Counts $model.diagnosticCounts (Get-DiagnosticCounts $output) "$($model.id) $($command.name)" $output
            if ($target -and [IO.Directory]::Exists($target)) {
                throw "$($model.id) $($command.name) published output despite migration errors: $target"
            }
            Write-Host "PASS $($model.id)@$actualCommit $($command.name)"
        }
    }
}
finally {
    $resolved = [IO.Path]::GetFullPath($tempRoot)
    if (-not $resolved.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -or $resolved -eq $tempBase) {
        throw "Refusing to remove unsafe temporary path: $resolved"
    }
    if ([IO.Directory]::Exists($resolved)) {
        # Git pack files can retain the Windows read-only attribute. Do not
        # follow reparse points; normalize only files inside the verified temp
        # tree before deleting it.
        Get-ChildItem -LiteralPath $resolved -Recurse -Force -File | ForEach-Object {
            if ($_.IsReadOnly) { $_.IsReadOnly = $false }
        }
        [IO.Directory]::Delete($resolved, $true)
    }
}

Write-Host "Pinned downstream migration diagnostics are stable across every publisher pipeline."

# Every downstream model intentionally returns its expected migration-error
# exit code. GitHub Actions dot-sources PowerShell step scripts, so clear the
# final native process status after all assertions and cleanup have passed.
$global:LASTEXITCODE = 0
