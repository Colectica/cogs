[CmdletBinding()]
param([Parameter(Mandatory)][string] $Package)

$ErrorActionPreference = 'Stop'
$packagePath = (Resolve-Path -LiteralPath $Package).Path
$tools = Get-Content (Join-Path $PSScriptRoot '../tools.json') -Raw | ConvertFrom-Json
$npm = if ([string]::IsNullOrWhiteSpace($env:COGS_NPM)) { 'npm' } else { $env:COGS_NPM }
$manifest = Join-Path $packagePath 'package.json'
$before = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash
Push-Location -LiteralPath $packagePath
try {
    & $npm install --no-save --ignore-scripts --no-package-lock "typescript@$($tools.typescript)" "@xmldom/xmldom@$($tools.xmldom)"
    if ($LASTEXITCODE -ne 0) { throw "npm install failed for $packagePath (exit $LASTEXITCODE)." }
}
finally {
    Pop-Location
}
if ((Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash -cne $before) {
    throw "npm install rewrote $manifest."
}
foreach ($dependency in @(@{ Name = 'typescript'; Version = $tools.typescript }, @{ Name = '@xmldom/xmldom'; Version = $tools.xmldom })) {
    $resolved = Get-Content (Join-Path $packagePath "node_modules/$($dependency.Name)/package.json") -Raw | ConvertFrom-Json
    if ($resolved.version -cne $dependency.Version) {
        throw "$($dependency.Name): expected $($dependency.Version), found $($resolved.version)."
    }
    Write-Output "$($dependency.Name) $($resolved.version)"
}
