[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]] $Paths
)

$ErrorActionPreference = 'Stop'
$knownXmi = @{
    'http://www.omg.org/spec/XMI/20110701' = @{
        Version = '2.4.2'; Uml = 'http://www.omg.org/spec/UML/20110701'; Extension = $false
    }
    'http://www.omg.org/spec/XMI/20131001' = @{
        Version = '2.5.1'; Uml = 'http://www.omg.org/spec/UML/20131001'; Extension = $true
    }
}
$knownTypes = @(
    'uml:Association', 'uml:Class', 'uml:Comment', 'uml:Constraint', 'uml:Generalization',
    'uml:LiteralInteger', 'uml:LiteralUnlimitedNatural', 'uml:OpaqueExpression',
    'uml:Package', 'uml:PrimitiveType', 'uml:Property'
)
$referenceAttributes = @(
    'general', 'association', 'memberEnd', 'constrainedElement', 'annotatedElement',
    'subject', 'package', 'owner'
)

function Get-RequiredAttribute([Xml.Linq.XElement] $element, [Xml.Linq.XName] $name, [string] $path) {
    $attribute = $element.Attribute($name)
    if ($null -eq $attribute -or [string]::IsNullOrWhiteSpace($attribute.Value)) {
        throw "XMI '$path' element '$($element.Name)' is missing required attribute '$name'."
    }
    return $attribute.Value
}

foreach ($pathValue in $Paths) {
    $path = [IO.Path]::GetFullPath($pathValue)
    if (-not [IO.File]::Exists($path)) { throw "XMI artifact not found: $path" }

    $readerSettings = [Xml.XmlReaderSettings]::new()
    $readerSettings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $readerSettings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($path, $readerSettings)
    try { $document = [Xml.Linq.XDocument]::Load($reader, [Xml.Linq.LoadOptions]::SetLineInfo) }
    finally { $reader.Dispose() }

    $root = $document.Root
    if ($null -eq $root -or $root.Name.LocalName -cne 'XMI' -or -not $knownXmi.ContainsKey($root.Name.NamespaceName)) {
        throw "XMI '$path' has an unsupported root QName '$($root.Name)'."
    }
    $contract = $knownXmi[$root.Name.NamespaceName]
    $xmi = [Xml.Linq.XNamespace]::Get($root.Name.NamespaceName)
    $uml = [Xml.Linq.XNamespace]::Get($contract.Uml)
    $umlPrefixNamespace = $root.GetNamespaceOfPrefix('uml')
    if ($null -eq $umlPrefixNamespace -or $umlPrefixNamespace.NamespaceName -cne $contract.Uml) {
        throw "XMI '$path' does not bind the uml prefix to '$($contract.Uml)'."
    }
    if ((Get-RequiredAttribute $root ($xmi + 'version') $path) -cne $contract.Version) {
        throw "XMI '$path' namespace and xmi:version disagree."
    }

    $models = @($root.Elements($uml + 'Model'))
    if ($models.Count -ne 1) { throw "XMI '$path' must contain exactly one UML Model; found $($models.Count)." }
    $extensions = @($root.Elements($xmi + 'Extension'))
    if ($contract.Extension -and $extensions.Count -ne 1) {
        throw "EA XMI '$path' must contain exactly one xmi:Extension."
    }
    if (-not $contract.Extension -and $extensions.Count -ne 0) {
        throw "Normative XMI '$path' must not contain an xmi:Extension."
    }

    $all = @($root.DescendantsAndSelf())
    $ids = [Collections.Generic.Dictionary[string, Xml.Linq.XElement]]::new([StringComparer]::Ordinal)
    foreach ($element in $all) {
        $id = $element.Attribute($xmi + 'id')
        if ($null -eq $id) { continue }
        if ([string]::IsNullOrWhiteSpace($id.Value)) { throw "XMI '$path' contains an empty xmi:id." }
        if ($ids.ContainsKey($id.Value)) { throw "XMI '$path' repeats xmi:id '$($id.Value)'." }
        $ids.Add($id.Value, $element)
    }
    if ($ids.Count -eq 0) { throw "XMI '$path' contains no xmi:id values." }

    foreach ($element in $all) {
        $type = $element.Attribute($xmi + 'type')
        if ($null -ne $type -and $type.Value -cnotin $knownTypes) {
            throw "XMI '$path' uses unsupported xmi:type '$($type.Value)' on '$($element.Name)'."
        }
        foreach ($attribute in $element.Attributes()) {
            if ($attribute.Name.LocalName -notin $referenceAttributes) { continue }
            foreach ($reference in $attribute.Value.Split(' ', [StringSplitOptions]::RemoveEmptyEntries)) {
                if (-not $ids.ContainsKey($reference)) {
                    throw "XMI '$path' has dangling $($attribute.Name.LocalName) reference '$reference'."
                }
            }
        }

        $plainType = $element.Attribute('type')
        if ($null -ne $plainType -and $plainType.Value.StartsWith('cogs.', [StringComparison]::Ordinal) -and
            -not $ids.ContainsKey($plainType.Value)) {
            throw "XMI '$path' has dangling property type reference '$($plainType.Value)'."
        }
    }

    $classifiers = @($root.Descendants('packagedElement') | Where-Object {
        $_.Attribute($xmi + 'type').Value -in @('uml:Class', 'uml:PrimitiveType')
    })
    if ($classifiers.Count -eq 0) { throw "XMI '$path' contains no UML classifiers." }
    $classifierIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($classifier in $classifiers) {
        [void] $classifierIds.Add((Get-RequiredAttribute $classifier ($xmi + 'id') $path))
        [void] (Get-RequiredAttribute $classifier 'name' $path)
        if ($classifier.Attribute($xmi + 'type').Value -ceq 'uml:Class') {
            $abstract = Get-RequiredAttribute $classifier 'isAbstract' $path
            if ($abstract -cnotin @('true', 'false')) { throw "XMI '$path' has invalid isAbstract '$abstract'." }
        }
    }

    foreach ($generalization in $root.Descendants('generalization')) {
        $general = Get-RequiredAttribute $generalization 'general' $path
        if (-not $classifierIds.Contains($general) -or
            $ids[$general].Attribute($xmi + 'type').Value -cne 'uml:Class') {
            throw "XMI '$path' generalization target '$general' is not a UML Class."
        }
    }

    foreach ($property in $root.Descendants() | Where-Object {
        $_.Attribute($xmi + 'type').Value -ceq 'uml:Property'
    }) {
        $propertyId = Get-RequiredAttribute $property ($xmi + 'id') $path
        $typeId = Get-RequiredAttribute $property 'type' $path
        if (-not $classifierIds.Contains($typeId)) {
            throw "XMI '$path' property '$propertyId' targets non-classifier '$typeId'."
        }
        foreach ($booleanName in @('isOrdered', 'isUnique')) {
            $boolean = Get-RequiredAttribute $property $booleanName $path
            if ($boolean -cnotin @('true', 'false')) { throw "XMI '$path' property '$propertyId' has invalid $booleanName '$boolean'." }
        }

        $lower = @($property.Elements('lowerValue'))
        $upper = @($property.Elements('upperValue'))
        if ($lower.Count -ne 1 -or $upper.Count -ne 1) {
            throw "XMI '$path' property '$propertyId' must have one lowerValue and one upperValue."
        }
        $lowerValue = Get-RequiredAttribute $lower[0] 'value' $path
        $upperValue = Get-RequiredAttribute $upper[0] 'value' $path
        if ($lowerValue -cnotmatch '^(0|[1-9][0-9]*)$' -or $upperValue -cnotmatch '^(\*|0|[1-9][0-9]*)$') {
            throw "XMI '$path' property '$propertyId' has non-canonical multiplicity '$lowerValue..$upperValue'."
        }
        if ($upperValue -ne '*' -and [Numerics.BigInteger]::Parse($lowerValue) -gt [Numerics.BigInteger]::Parse($upperValue)) {
            throw "XMI '$path' property '$propertyId' has contradictory multiplicity '$lowerValue..$upperValue'."
        }
    }

    foreach ($association in $root.Descendants('packagedElement') | Where-Object {
        $_.Attribute($xmi + 'type').Value -ceq 'uml:Association'
    }) {
        $associationId = Get-RequiredAttribute $association ($xmi + 'id') $path
        $members = (Get-RequiredAttribute $association 'memberEnd' $path).Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
        if ($members.Count -ne 2 -or $members[0] -ceq $members[1]) {
            throw "XMI '$path' association '$associationId' must have two distinct member ends."
        }
        foreach ($member in $members) {
            if (-not $ids.ContainsKey($member) -or $ids[$member].Attribute($xmi + 'type').Value -cne 'uml:Property') {
                throw "XMI '$path' association '$associationId' member '$member' is not a UML Property."
            }
            if ($ids[$member].Attribute('association').Value -cne $associationId) {
                throw "XMI '$path' association member '$member' does not point back to '$associationId'."
            }
        }
    }

    foreach ($constraint in $root.Descendants('ownedRule')) {
        $constraintId = Get-RequiredAttribute $constraint ($xmi + 'id') $path
        $target = Get-RequiredAttribute $constraint 'constrainedElement' $path
        if ($ids[$target].Attribute($xmi + 'type').Value -cne 'uml:Property') {
            throw "XMI '$path' constraint '$constraintId' does not constrain a UML Property."
        }
        $specifications = @($constraint.Elements('specification'))
        if ($specifications.Count -ne 1 -or $specifications[0].Element('language').Value -cne 'COGS' -or
            [string]::IsNullOrWhiteSpace($specifications[0].Element('body').Value)) {
            throw "XMI '$path' constraint '$constraintId' has no machine-readable COGS specification."
        }
    }

    if ($contract.Extension) {
        $extension = $extensions[0]
        if ((Get-RequiredAttribute $extension 'extender' $path) -cne 'Enterprise Architect') {
            throw "EA XMI '$path' has an unexpected extension owner."
        }
        foreach ($diagramElement in $extension.Descendants('element')) {
            $subject = Get-RequiredAttribute $diagramElement 'subject' $path
            if (-not $classifierIds.Contains($subject)) {
                throw "EA XMI '$path' diagram element targets non-classifier '$subject'."
            }
        }
    }

    Write-Host "UML/XMI semantic validation passed: $path ($($classifiers.Count) classifiers, $($ids.Count) IDs)."
}
