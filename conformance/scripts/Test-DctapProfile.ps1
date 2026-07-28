[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]] $Paths
)

$ErrorActionPreference = 'Stop'
$expectedHeaders = @(
    'shapeID', 'shapeLabel', 'propertyID', 'propertyLabel', 'mandatory', 'repeatable',
    'valueNodeType', 'valueDataType', 'valueShape', 'valueConstraint',
    'valueConstraintType', 'note'
)
$booleanValues = @('true', 'false', '1', '0')
$nodeTypes = @('iri', 'literal', 'bnode')
$standardConstraintTypes = @(
    'picklist', 'IRIstem', 'pattern', 'languageTag', 'minLength', 'maxLength',
    'minInclusive', 'maxInclusive'
)

function Get-Value([object] $record, [string] $name) {
    $value = $record.$name
    if ($null -eq $value) { return '' }
    return [string] $value
}

function Assert-Empty([object] $record, [string[]] $names, [string] $location) {
    foreach ($name in $names) {
        if (-not [string]::IsNullOrEmpty((Get-Value $record $name))) {
            throw "$location has '$name' without a statement-template propertyID."
        }
    }
}

foreach ($pathValue in $Paths) {
    $path = [IO.Path]::GetFullPath($pathValue)
    if (-not [IO.File]::Exists($path)) { throw "DCTAP artifact not found: $path" }

    $firstLine = [IO.File]::ReadLines($path) | Select-Object -First 1
    if ($null -eq $firstLine -or
        [string]::Join('|', $firstLine.Split(',')) -cne [string]::Join('|', $expectedHeaders)) {
        throw "Unexpected DCTAP header in '$path': $firstLine"
    }

    $records = @(Import-Csv -LiteralPath $path)
    if ($records.Count -eq 0) { throw "DCTAP artifact contains no shapes: $path" }
    $actualHeaders = @($records[0].PSObject.Properties.Name)
    if ([string]::Join('|', $actualHeaders) -cne [string]::Join('|', $expectedHeaders)) {
        throw "DCTAP parser observed unexpected columns in '$path'."
    }

    $shapeIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $valueShapeReferences = [Collections.Generic.List[object]]::new()
    $currentShape = $null
    $statementCount = 0

    for ($index = 0; $index -lt $records.Count; $index++) {
        $record = $records[$index]
        $location = "DCTAP '$path' logical row $($index + 2)"
        $shapeId = Get-Value $record 'shapeID'
        $propertyId = Get-Value $record 'propertyID'

        $hasAnyValue = $false
        foreach ($header in $expectedHeaders) {
            if (-not [string]::IsNullOrEmpty((Get-Value $record $header))) {
                $hasAnyValue = $true
                break
            }
        }
        if (-not $hasAnyValue) {
            $currentShape = $null
            continue
        }

        if (-not [string]::IsNullOrEmpty($shapeId)) {
            if (-not $shapeIds.Add($shapeId)) {
                throw "$location repeats shapeID '$shapeId'; COGS emits each shape declaration once."
            }
            $currentShape = $shapeId
        } elseif ([string]::IsNullOrEmpty($currentShape) -and -not [string]::IsNullOrEmpty($propertyId)) {
            throw "$location contains propertyID '$propertyId' outside a shape."
        }

        if ([string]::IsNullOrEmpty($propertyId)) {
            if ([string]::IsNullOrEmpty($shapeId)) {
                throw "$location is neither a shape declaration nor a statement template."
            }
            Assert-Empty $record @(
                'propertyLabel', 'mandatory', 'repeatable', 'valueNodeType', 'valueDataType',
                'valueShape', 'valueConstraint', 'valueConstraintType', 'note'
            ) $location
            continue
        }

        $statementCount++
        foreach ($booleanColumn in @('mandatory', 'repeatable')) {
            $value = (Get-Value $record $booleanColumn).ToLowerInvariant()
            if (-not [string]::IsNullOrEmpty($value) -and $value -notin $booleanValues) {
                throw "$location has invalid $booleanColumn value '$value'."
            }
        }

        $nodeType = Get-Value $record 'valueNodeType'
        $nodeTypeLower = $nodeType.ToLowerInvariant()
        if (-not [string]::IsNullOrEmpty($nodeType) -and $nodeTypeLower -notin $nodeTypes) {
            throw "$location has invalid valueNodeType '$nodeType'."
        }

        $valueDataType = Get-Value $record 'valueDataType'
        $valueShape = Get-Value $record 'valueShape'
        if (-not [string]::IsNullOrEmpty($valueDataType) -and $nodeTypeLower -ne 'literal') {
            throw "$location has valueDataType '$valueDataType' for non-literal node type '$nodeType'."
        }
        if (-not [string]::IsNullOrEmpty($valueShape)) {
            if ($nodeTypeLower -eq 'literal') {
                throw "$location has valueShape '$valueShape' for a literal node."
            }
            if (-not [string]::IsNullOrEmpty($valueDataType)) {
                throw "$location combines valueShape and valueDataType."
            }
            $valueShapeReferences.Add([pscustomobject]@{ Location = $location; Shape = $valueShape })
        }

        $constraint = Get-Value $record 'valueConstraint'
        $constraintType = Get-Value $record 'valueConstraintType'
        if ([string]::IsNullOrEmpty($constraint) -ne [string]::IsNullOrEmpty($constraintType)) {
            throw "$location must provide valueConstraint and valueConstraintType together."
        }
        if (-not [string]::IsNullOrEmpty($constraintType)) {
            if ($constraintType -cnotin $standardConstraintTypes) {
                throw "$location uses unsupported COGS DCTAP constraint type '$constraintType'."
            }
            if ($constraintType -in @('minLength', 'maxLength') -and $constraint -cnotmatch '^(0|[1-9][0-9]*)$') {
                throw "$location has non-canonical $constraintType constraint '$constraint'."
            }
            if ($constraintType -ceq 'picklist') {
                $choices = @($constraint.Split(','))
                if ($choices.Count -eq 0 -or $choices | Where-Object { [string]::IsNullOrEmpty($_) }) {
                    throw "$location has an empty picklist member."
                }
            }
        }
    }

    if ($shapeIds.Count -eq 0) { throw "DCTAP artifact contains no shape declarations: $path" }
    if ($statementCount -eq 0) { throw "DCTAP artifact contains no statement templates: $path" }
    foreach ($reference in $valueShapeReferences) {
        if (-not $shapeIds.Contains($reference.Shape)) {
            throw "$($reference.Location) references missing valueShape '$($reference.Shape)'."
        }
    }

    Write-Host "DCTAP semantic validation passed: $path ($($shapeIds.Count) shapes, $statementCount statements)."
}
