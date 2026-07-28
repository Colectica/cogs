[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $CogsDll,

    [string] $Model = (Join-Path $PSScriptRoot '..\model'),

    [string] $Manifest = (Join-Path $PSScriptRoot '..\invalid\manifest.json')
)

$ErrorActionPreference = 'Stop'
$CogsDll = [IO.Path]::GetFullPath($CogsDll)
$Model = [IO.Path]::GetFullPath($Model)
$Manifest = [IO.Path]::GetFullPath($Manifest)

if (-not [IO.File]::Exists($CogsDll)) { throw "COGS CLI assembly not found: $CogsDll" }
if (-not [IO.Directory]::Exists($Model)) { throw "Conformance model not found: $Model" }
if (-not [IO.File]::Exists($Manifest)) { throw "Negative-case manifest not found: $Manifest" }

function Invoke-CogsValidation([string] $Path) {
    $output = & dotnet $CogsDll validate $Path 2>&1 | Out-String
    [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output }
}

function Assert-UniqueText([string] $Text, [string] $Needle, [string] $CaseId) {
    $first = $Text.IndexOf($Needle, [StringComparison]::Ordinal)
    if ($first -lt 0) { throw "[$CaseId] mutation source text was not found: $Needle" }
    if ($Text.IndexOf($Needle, $first + $Needle.Length, [StringComparison]::Ordinal) -ge 0) {
        throw "[$CaseId] mutation source text is not unique: $Needle"
    }
}

function ConvertTo-NormalizedNewlines([string] $Text) {
    $Text.Replace("`r`n", "`n").Replace("`r", "`n")
}

function Read-NormalizedText([string] $Path) {
    ConvertTo-NormalizedNewlines ([IO.File]::ReadAllText($Path))
}

function Get-FirstTextDifference([string] $Expected, [string] $Actual) {
    $expectedLines = $Expected.Split([char]0x0a)
    $actualLines = $Actual.Split([char]0x0a)
    $sharedCount = [Math]::Min($expectedLines.Length, $actualLines.Length)
    $lineIndex = 0
    while ($lineIndex -lt $sharedCount -and
           $expectedLines[$lineIndex] -ceq $actualLines[$lineIndex]) {
        $lineIndex++
    }

    if ($lineIndex -eq $sharedCount -and
        $expectedLines.Length -eq $actualLines.Length) {
        return 'No text difference was found.'
    }

    $expectedLine = if ($lineIndex -lt $expectedLines.Length) {
        ConvertTo-Json -InputObject $expectedLines[$lineIndex] -Compress
    } else {
        '<end of file>'
    }
    $actualLine = if ($lineIndex -lt $actualLines.Length) {
        ConvertTo-Json -InputObject $actualLines[$lineIndex] -Compress
    } else {
        '<end of file>'
    }

    "First difference at line $($lineIndex + 1).`nChecked in: $expectedLine`nGenerated: $actualLine"
}

$valid = Invoke-CogsValidation $Model
if ($valid.ExitCode -ne 0) {
    throw "The checked-in conformance model is invalid (exit $($valid.ExitCode)):`n$($valid.Output)"
}

$usageOutput = & dotnet $CogsDll validate-instance 2>&1 | Out-String
if ($LASTEXITCODE -ne 2) {
    throw "A CLI usage error must return exit 2, received ${LASTEXITCODE}:`n$usageOutput"
}
Write-Host 'PASS CLI usage errors: exit 2'

$conflictingDotOutput = & dotnet $CogsDll publish-dot --all --single 2>&1 | Out-String
if ($LASTEXITCODE -ne 2 -or $conflictingDotOutput -notmatch '--all and --single cannot be used together') {
    throw "Conflicting publish-dot options must return exit 2, received ${LASTEXITCODE}:`n$conflictingDotOutput"
}
Write-Host 'PASS conflicting publish-dot options: exit 2'

$instanceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\instances'))
foreach ($format in @('json', 'xml')) {
    $instance = Join-Path $instanceRoot "full.$format"
    $output = & dotnet $CogsDll validate-instance $Model $instance --format $format 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "The checked-in $format conformance instance is invalid:`n$output"
    }
    Write-Host "PASS full.${format}: schema and COGS instance validation"
}

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$tempRoot = Join-Path $tempBase ("cogs-conformance-" + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($tempRoot) | Out-Null

try {
    $legacyNewTarget = Join-Path $tempRoot 'legacy-new-target'
    $legacyNewOutput = & dotnet $CogsDll cogs-new $legacyNewTarget ignored-second-argument 2>&1 | Out-String
    if ($LASTEXITCODE -ne 2) {
        throw "The retired two-argument cogs-new form must return exit 2, received ${LASTEXITCODE}:`n$legacyNewOutput"
    }
    if ([IO.Directory]::Exists($legacyNewTarget)) {
        throw 'The retired two-argument cogs-new form unexpectedly created output.'
    }
    Write-Host 'PASS retired two-argument cogs-new form: exit 2 and no output'

    $generatedReference = Join-Path $tempRoot 'generated-reference.rst'
    $referenceOutput = & dotnet $CogsDll generate-command-reference $generatedReference 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "Command-reference generation failed with exit ${LASTEXITCODE}:`n$referenceOutput"
    }
    $checkedInReference = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\docs\source\technical-guide\command-line\generated-reference.rst'))
    if (-not [IO.File]::Exists($checkedInReference)) {
        throw "The generated command reference is not checked in: $checkedInReference"
    }
    $generatedReferenceText = Read-NormalizedText $generatedReference
    $checkedInReferenceText = Read-NormalizedText $checkedInReference
    if (-not [string]::Equals(
        $generatedReferenceText,
        $checkedInReferenceText,
        [StringComparison]::Ordinal)) {
        $difference = Get-FirstTextDifference $checkedInReferenceText $generatedReferenceText
        throw "The checked-in command reference is stale.`n$difference`nRun: dotnet $CogsDll generate-command-reference $checkedInReference"
    }
    Write-Host 'PASS command reference: normalized CLI descriptor snapshot is current'

    $referenceFailureOutput = & dotnet $CogsDll generate-command-reference $tempRoot 2>&1 | Out-String
    if ($LASTEXITCODE -ne 100 -or $referenceFailureOutput -notmatch '(?<![A-Z0-9-])CLI2201(?![A-Z0-9-])') {
        throw "A command-reference write failure must return CLI2201/exit 100:`n$referenceFailureOutput"
    }
    Write-Host 'PASS command-reference write failure: CLI2201 / exit 100'

    $legacyGraphQlTarget = Join-Path $tempRoot 'legacy-graphql'
    $legacyGraphQlOutput = & dotnet $CogsDll publish-GraphQL $Model $legacyGraphQlTarget 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -or $legacyGraphQlOutput -notmatch '(?<![A-Z0-9-])CLI2002(?![A-Z0-9-])') {
        throw "The hidden publish-GraphQL alias must warn with CLI2002 and return exit 0:`n$legacyGraphQlOutput"
    }
    if (-not [IO.File]::Exists((Join-Path $legacyGraphQlTarget 'GraphQL.graphqls'))) {
        throw 'The hidden publish-GraphQL alias did not publish GraphQL.graphqls.'
    }
    Write-Host 'PASS deprecated publish-GraphQL alias: CLI2002 / exit 0'

    $removedTarget = Join-Path $tempRoot 'removed-additional-properties'
    $removedOutput = & dotnet $CogsDll publish-json $Model $removedTarget --allowAdditionalProperties 2>&1 | Out-String
    if ($LASTEXITCODE -ne 100 -or $removedOutput -notmatch '(?<![A-Z0-9-])CLI2001(?![A-Z0-9-])') {
        throw "The removed --allowAdditionalProperties option must return CLI2001/exit 100:`n$removedOutput"
    }
    if ([IO.Directory]::Exists($removedTarget)) {
        throw 'The removed --allowAdditionalProperties option unexpectedly published output.'
    }
    Write-Host 'PASS removed JSON open-content option: CLI2001 / exit 100'

    $definition = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
    foreach ($case in $definition.cases) {
        $caseRoot = Join-Path $tempRoot $case.id
        Copy-Item -LiteralPath $Model -Destination $caseRoot -Recurse

        $mutation = $case.mutation
        $path = Join-Path $caseRoot ([string]$mutation.path)
        switch ([string]$mutation.kind) {
            'removeLine' {
                $lines = [IO.File]::ReadAllLines($path)
                $matches = @($lines | Where-Object { $_ -ceq [string]$mutation.value })
                if ($matches.Count -ne 1) { throw "[$($case.id)] removeLine expected one exact line, found $($matches.Count)." }
                [IO.File]::WriteAllLines($path, @($lines | Where-Object { $_ -cne [string]$mutation.value }))
            }
            'replaceText' {
                $text = [IO.File]::ReadAllText($path)
                Assert-UniqueText $text ([string]$mutation.old) ([string]$case.id)
                [IO.File]::WriteAllText($path, $text.Replace([string]$mutation.old, [string]$mutation.new, [StringComparison]::Ordinal))
            }
            'writeFile' {
                $parent = [IO.Path]::GetDirectoryName($path)
                [IO.Directory]::CreateDirectory($parent) | Out-Null
                [IO.File]::WriteAllText($path, [string]$mutation.value)
            }
            'renamePath' {
                $destination = Join-Path ([IO.Path]::GetDirectoryName($path)) ([string]$mutation.value)
                $intermediate = "$path.cogs-case-rename"
                [IO.Directory]::Move($path, $intermediate)
                [IO.Directory]::Move($intermediate, $destination)
            }
            default { throw "[$($case.id)] unsupported mutation kind '$($mutation.kind)'." }
        }

        $actual = Invoke-CogsValidation $caseRoot
        if ($actual.ExitCode -ne 100) {
            throw "[$($case.id)] expected exit 100, received $($actual.ExitCode):`n$($actual.Output)"
        }
        $codePattern = '(?<![A-Z0-9-])' + [regex]::Escape([string]$case.expectedCode) + '(?![A-Z0-9-])'
        if ($actual.Output -notmatch $codePattern) {
            throw "[$($case.id)] expected diagnostic $($case.expectedCode):`n$($actual.Output)"
        }
        Write-Host "PASS $($case.id): $($case.expectedCode)"
    }
}
finally {
    $resolved = [IO.Path]::GetFullPath($tempRoot)
    if (-not $resolved.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -or $resolved -eq $tempBase) {
        throw "Refusing to remove unsafe temporary path: $resolved"
    }
    if ([IO.Directory]::Exists($resolved)) { [IO.Directory]::Delete($resolved, $true) }
}

Write-Host "COGS 2 conformance model and negative fixtures passed."

# The negative-fixture loop intentionally invokes COGS commands that return 100.
# GitHub Actions dot-sources PowerShell step scripts, so the final native
# process exit code otherwise leaks out even though every assertion passed.
$global:LASTEXITCODE = 0
