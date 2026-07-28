[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]] $Roots
)

$ErrorActionPreference = 'Stop'
$xmlExtensions = @('.xml', '.xsd', '.xmi', '.svg')
$textExtensions = @('.cs', '.csv', '.dot', '.graphqls', '.json', '.md', '.py', '.rst', '.ts', '.ttl', '.txt', '.xmi', '.xml', '.xsd', '.yaml', '.yml')
$historicalSourceSentinels = @('http://example.org/legacy', 'LegacyNote')
$dctapPaths = [Collections.Generic.List[string]]::new()
$xmiPaths = [Collections.Generic.List[string]]::new()

foreach ($rootValue in $Roots) {
    $root = [IO.Path]::GetFullPath($rootValue)
    if (-not [IO.Directory]::Exists($root)) { throw "Generated artifact root not found: $root" }

    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
        if ($file.FullName -match '[\\/]node_modules[\\/]') { continue }
        if ($textExtensions -contains $file.Extension.ToLowerInvariant()) {
            $artifactText = [IO.File]::ReadAllText($file.FullName)
            foreach ($sentinel in $historicalSourceSentinels) {
                if ($artifactText.Contains($sentinel, [StringComparison]::Ordinal)) {
                    throw "Historical CSV-only metadata '$sentinel' leaked into '$($file.FullName)'."
                }
            }
        }
        if ($file.Extension -eq '.json') {
            $document = [Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($file.FullName),
                [Text.Json.JsonDocumentOptions]@{ AllowTrailingCommas = $false; CommentHandling = [Text.Json.JsonCommentHandling]::Disallow })
            $document.Dispose()
        }

        if ($xmlExtensions -contains $file.Extension.ToLowerInvariant()) {
            $settings = [Xml.XmlReaderSettings]::new()
            # Graphviz emits the standard SVG 1.1 DOCTYPE. External resolution
            # remains disabled. Wire schemas, XMI, and XML stay on the stricter
            # no-DTD path. Generated OWL is Turtle and is not XML parsed here.
            $settings.DtdProcessing = if ($file.Extension -eq '.svg') {
                [Xml.DtdProcessing]::Parse
            } else {
                [Xml.DtdProcessing]::Prohibit
            }
            $settings.XmlResolver = $null
            try {
                $reader = [Xml.XmlReader]::Create($file.FullName, $settings)
                try { while ($reader.Read()) { } } finally { $reader.Dispose() }
            } catch {
                throw "Invalid XML-family artifact '$($file.FullName)': $($_.Exception.Message)"
            }
        }

        if ($file.Name -eq 'dctap.csv') {
            $dctapPaths.Add($file.FullName)
        }

        if ($file.Extension -ieq '.xmi') {
            $xmiPaths.Add($file.FullName)
        }

        if ($file.Extension -ieq '.png') {
            $bytes = [IO.File]::ReadAllBytes($file.FullName)
            $signature = [byte[]](0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a)
            if ($bytes.Length -lt $signature.Length -or
                [string]::Join('-', $bytes[0..($signature.Length - 1)]) -cne [string]::Join('-', $signature)) {
                throw "Invalid PNG signature in $($file.FullName)"
            }
        }

        if ($file.Extension -in @('.jpg', '.jpeg')) {
            $bytes = [IO.File]::ReadAllBytes($file.FullName)
            if ($bytes.Length -lt 3 -or $bytes[0] -ne 0xff -or $bytes[1] -ne 0xd8 -or $bytes[2] -ne 0xff) {
                throw "Invalid JPEG signature in $($file.FullName)"
            }
        }

        if ($file.Extension -ieq '.pdf') {
            $bytes = [IO.File]::ReadAllBytes($file.FullName)
            $signature = [Text.Encoding]::ASCII.GetBytes('%PDF-')
            if ($bytes.Length -lt $signature.Length -or
                [string]::Join('-', $bytes[0..($signature.Length - 1)]) -cne [string]::Join('-', $signature)) {
                throw "Invalid PDF signature in $($file.FullName)"
            }
        }
    }
}

$uniqueDctapPaths = @($dctapPaths | Sort-Object -Unique)
$uniqueXmiPaths = @($xmiPaths | Sort-Object -Unique)
if ($uniqueDctapPaths.Count -eq 0) { throw 'No generated DCTAP artifacts were found.' }
if ($uniqueXmiPaths.Count -eq 0) { throw 'No generated UML/XMI artifacts were found.' }
& "$PSScriptRoot/Test-DctapProfile.ps1" -Paths $uniqueDctapPaths
& "$PSScriptRoot/Test-UmlXmiModel.ps1" -Paths $uniqueXmiPaths
$dctapSelfTestSource = $uniqueDctapPaths | Where-Object {
    [IO.File]::ReadAllText($_) -match '(?m)^,,[^,\r\n]+,[^,\r\n]*,[^,\r\n]*,[^,\r\n]*,(?:IRI|bnode),,[^,\r\n]+,'
} | Select-Object -First 1
if ($null -eq $dctapSelfTestSource) {
    throw 'No generated DCTAP artifact contains a valueShape statement for the negative self-test.'
}
& "$PSScriptRoot/Test-ProjectionValidatorSelfTests.ps1" `
    -DctapPath $dctapSelfTestSource `
    -XmiPath $uniqueXmiPaths[0]

Write-Host "Generated JSON, XML/XSD, Turtle, SVG, DCTAP, and UML/XMI checks passed."
