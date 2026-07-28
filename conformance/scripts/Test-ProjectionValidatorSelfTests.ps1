[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $DctapPath,

    [Parameter(Mandatory)]
    [string] $XmiPath
)

$ErrorActionPreference = 'Stop'
$systemTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$temporary = [IO.Path]::GetFullPath((Join-Path $systemTemp ("cogs-projection-validator-" + [guid]::NewGuid().ToString('N'))))
if (-not $temporary.StartsWith($systemTemp, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe projection-validator temporary path: $temporary"
}
[IO.Directory]::CreateDirectory($temporary) | Out-Null

try {
    $invalidDctap = Join-Path $temporary 'dangling-value-shape.csv'
    $dctapText = [IO.File]::ReadAllText([IO.Path]::GetFullPath($DctapPath))
    $dctapPattern = '(?m)^(,,[^,\r\n]+,[^,\r\n]*,[^,\r\n]*,[^,\r\n]*,(?:IRI|bnode),,)([^,\r\n]+)(,)'
    $mutatedDctap = [Text.RegularExpressions.Regex]::Replace(
        $dctapText,
        $dctapPattern,
        '${1}missing:Shape${3}',
        [Text.RegularExpressions.RegexOptions]::None,
        [TimeSpan]::FromSeconds(2))
    if ($mutatedDctap -ceq $dctapText) {
        throw 'DCTAP self-test source has no valueShape statement to mutate.'
    }
    [IO.File]::WriteAllText($invalidDctap, $mutatedDctap, [Text.UTF8Encoding]::new($false))
    $dctapOutput = & pwsh -NoProfile -File "$PSScriptRoot/Test-DctapProfile.ps1" -Paths $invalidDctap 2>&1
    if ($LASTEXITCODE -eq 0) { throw 'DCTAP validator accepted a dangling valueShape mutation.' }
    if (($dctapOutput | Out-String) -notmatch 'missing valueShape') {
        throw "DCTAP validator rejected the mutation for the wrong reason:`n$($dctapOutput | Out-String)"
    }

    $invalidXmi = Join-Path $temporary 'dangling-type.xmi'
    $xmiText = [IO.File]::ReadAllText([IO.Path]::GetFullPath($XmiPath))
    $xmiPattern = [Text.RegularExpressions.Regex]::new(
        '(?<!xmi:)type="cogs\.[^"]+"',
        [Text.RegularExpressions.RegexOptions]::CultureInvariant,
        [TimeSpan]::FromSeconds(2))
    $mutatedXmi = $xmiPattern.Replace($xmiText, 'type="cogs.missing"', 1)
    if ($mutatedXmi -ceq $xmiText) {
        throw 'XMI self-test source has no classifier reference to mutate.'
    }
    [IO.File]::WriteAllText($invalidXmi, $mutatedXmi, [Text.UTF8Encoding]::new($false))
    $xmiOutput = & pwsh -NoProfile -File "$PSScriptRoot/Test-UmlXmiModel.ps1" -Paths $invalidXmi 2>&1
    if ($LASTEXITCODE -eq 0) { throw 'UML/XMI validator accepted a dangling classifier mutation.' }
    if (($xmiOutput | Out-String) -notmatch 'dangling property type reference') {
        throw "UML/XMI validator rejected the mutation for the wrong reason:`n$($xmiOutput | Out-String)"
    }

    Write-Host 'Projection validator negative self-tests passed.'
}
finally {
    if ([IO.Directory]::Exists($temporary)) {
        $resolved = [IO.Path]::GetFullPath($temporary)
        if (-not $resolved.StartsWith($systemTemp, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove unsafe projection-validator path: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
