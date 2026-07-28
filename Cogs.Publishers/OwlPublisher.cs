// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Writing;

namespace Cogs.Publishers;

/// <summary>
/// Publishes the authoritative OWL ontology and class-semantics representation
/// of a COGS model as W3C Turtle, with OWL2002 and OWL2003 as documented
/// authority exceptions. Instance ordering and unsupported lexical constraints
/// remain authoritative in JSON Schema and XSD.
/// </summary>
public sealed class OwlPublisher
{
    private const string Rdf = NamespaceMapper.RDF;
    private const string Rdfs = NamespaceMapper.RDFS;
    private const string Owl = NamespaceMapper.OWL;
    private const string Xsd = NamespaceMapper.XMLSCHEMA;
    private static readonly string[] DeclaredXsdDatatypes =
    [
        "date", "duration", "gDay", "gMonth", "gMonthDay", "gYear", "gYearMonth", "time"
    ];

    public string? CogsLocation { get; set; }
    public required string TargetDirectory { get; set; }
    public bool Overwrite { get; set; }
    public required string TargetNamespacePrefix { get; set; }
    public required string TargetNamespace { get; set; }
    public string? VersionInfo { get; set; }
    public string? Description { get; set; }
    public string? Title { get; set; }
    public PublicationResult? LastResult { get; private set; }

    private CogsModel _model = null!;
    private Graph _graph = null!;
    private string _termBase = null!;
    private readonly List<CogsError> _diagnostics = new();

    public void Publish(CogsModel model)
    {
        PublicationResult result = PublishResult(model);
        if (!result.Success)
        {
            CogsError error = result.Diagnostics.First(diagnostic => diagnostic.Level >= ErrorLevel.Error);
            throw new CogsPublicationException(error.Message, error.Exception ?? new InvalidOperationException(error.Message));
        }
    }

    public PublicationResult PublishResult(CogsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _diagnostics.Clear();
        _diagnostics.AddRange(ValidateSharedPropertyCompatibility(model));
        _diagnostics.AddRange(RdfPublisherValidation.ValidatePropertyTermCollisions(
            model,
            "OWL1002",
            "OWL"));
        if (_diagnostics.Any(diagnostic => diagnostic.Level >= ErrorLevel.Error))
        {
            LastResult = new PublicationResult(Array.Empty<string>(), _diagnostics);
            return LastResult;
        }

        PublicationResult transaction = DirectoryPublication.PublishResult(
            TargetDirectory,
            Overwrite,
            stagingDirectory => PublishCore(model, stagingDirectory),
            string.IsNullOrWhiteSpace(model.SourceDirectory) ? CogsLocation : model.SourceDirectory);

        LastResult = new PublicationResult(transaction.Artifacts, transaction.Diagnostics.Concat(_diagnostics));
        return LastResult;
    }

    private void PublishCore(CogsModel model, string targetDirectory)
    {
        _model = model;
        _termBase = CogsRdfNaming.GetTermBase(TargetNamespace);

        _graph = new Graph { BaseUri = UriFactory.Create(TargetNamespace) };
        _graph.NamespaceMap.AddNamespace("rdf", UriFactory.Create(Rdf));
        _graph.NamespaceMap.AddNamespace("rdfs", UriFactory.Create(Rdfs));
        _graph.NamespaceMap.AddNamespace("owl", UriFactory.Create(Owl));
        _graph.NamespaceMap.AddNamespace("xsd", UriFactory.Create(Xsd));
        _graph.NamespaceMap.AddNamespace("xml", UriFactory.Create("http://www.w3.org/XML/1998/namespace"));
        string targetPrefix = TargetNamespacePrefix;
        if (targetPrefix is "rdf" or "rdfs" or "owl" or "xsd" or "xml")
        {
            targetPrefix = "cogs";
            int suffix = 2;
            while (_graph.NamespaceMap.HasNamespace(targetPrefix)) targetPrefix = "cogs" + suffix++;
            _diagnostics.Add(new CogsError(
                ErrorLevel.Warning,
                "OWL2005",
                $"OWL prefix '{TargetNamespacePrefix}' conflicts with an RDF vocabulary prefix; '{targetPrefix}' was used for the model namespace.",
                sourcePath: model.SourceDirectory,
                modelPath: "/Settings/NamespacePrefix"));
        }
        _graph.NamespaceMap.AddNamespace(targetPrefix, UriFactory.Create(_termBase));

        IUriNode ontology = UriNode(TargetNamespace);
        Assert(ontology, Rdf + "type", UriNode(Owl + "Ontology"));
        AddOptionalLiteral(ontology, Owl + "versionInfo", VersionInfo);
        AddOptionalLiteral(ontology, Rdfs + "comment", Description);
        AddOptionalLiteral(ontology, Rdfs + "label", Title);

        DeclareNonBuiltInDatatypes();
        DeclareCogsDate();
        foreach (DataType type in model.AllDataTypes.OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            DeclareClass(type);
        }

        var propertyUses = model.AllDataTypes
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .SelectMany(owner => owner.Properties.Select(property => (Owner: owner, Property: property)))
            .ToList();

        foreach ((DataType Owner, Property Property) first in propertyUses
                     .GroupBy(use => use.Property.Name, StringComparer.Ordinal)
                     .Select(group => group.First()))
        {
            DeclareSharedProperty(first.Property);
        }

        foreach (DataType owner in model.AllDataTypes.OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            foreach (Property property in owner.Properties)
            {
                DeclarePropertyUse(owner, property);
            }

            if (owner is ItemType && string.IsNullOrEmpty(owner.ExtendsTypeName))
            {
                DeclareIdentityKey(owner);
            }
        }

        string output = Path.Combine(targetDirectory, model.Settings.Slug + ".ttl");
        // Blank-node labels and statement order are serialization details.
        // Full compression keeps class-local restrictions beside their owning
        // class while repeated generation is compared as an RDF graph.
        var writer = new CompressingTurtleWriter(WriterCompressionLevel.More, TurtleSyntax.W3C)
        {
            PrettyPrintMode = true,
            HighSpeedModePermitted = false
        };
        using var buffer = new System.IO.StringWriter(CultureInfo.InvariantCulture)
        {
            NewLine = "\n"
        };
        writer.Save(_graph, buffer);
        string turtle = buffer.ToString()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        File.WriteAllText(output, turtle, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void DeclareNonBuiltInDatatypes()
    {
        // These XSD datatypes are valid COGS/RDF datatypes but are not in the
        // mandatory OWL 2 datatype map. Explicit declarations keep the
        // ontology in the OWL 2 DL structural profile.
        foreach (string datatype in DeclaredXsdDatatypes)
        {
            Assert(UriNode(Xsd + datatype), Rdf + "type", UriNode(Rdfs + "Datatype"));
        }
    }

    private void DeclareCogsDate()
    {
        IUriNode cogsDate = Term("CogsDate");
        Assert(cogsDate, Rdf + "type", UriNode(Rdfs + "Datatype"));
        Assert(cogsDate, Rdfs + "label", _graph.CreateLiteralNode("COGS date union", "en"));
        Assert(cogsDate, Rdfs + "comment", _graph.CreateLiteralNode(
            "A datatype whose values use exactly one supported COGS date lexical arm."));

        IBlankNode union = BlankNode();
        Assert(union, Rdf + "type", UriNode(Rdfs + "Datatype"));
        Assert(union, Owl + "unionOf", CreateList(
        [
            UriNode(Xsd + "dateTime"),
            UriNode(Xsd + "date"),
            UriNode(Xsd + "gYearMonth"),
            UriNode(Xsd + "gYear"),
            UriNode(Xsd + "duration"),
        ]));
        Assert(cogsDate, Owl + "equivalentClass", union);
    }

    private void DeclareClass(DataType type)
    {
        IUriNode classNode = Term(type.Name);
        Assert(classNode, Rdf + "type", UriNode(Owl + "Class"));
        Assert(classNode, Rdfs + "label", _graph.CreateLiteralNode(type.Name, "en"));
        AddOptionalLiteral(classNode, Rdfs + "comment", type.Description);

        if (!string.IsNullOrEmpty(type.ExtendsTypeName))
        {
            Assert(classNode, Rdfs + "subClassOf", Term(type.ExtendsTypeName));
        }

        if (type.IsDeprecated)
        {
            Assert(classNode, Owl + "deprecated", TypedLiteral("true", Xsd + "boolean"));
        }

        if (type.IsAbstract)
        {
            Warn("OWL2003",
                $"OWL cannot enforce that abstract COGS type '{type.Name}' has no direct instances.",
                type,
                $"/{Kind(type)}/{type.Name}");
        }
    }

    private void DeclareSharedProperty(Property property)
    {
        IUriNode propertyNode = PropertyNode(property);
        bool objectProperty = IsObjectProperty(property);
        Assert(propertyNode, Rdf + "type", UriNode(Owl + (objectProperty ? "ObjectProperty" : "DatatypeProperty")));
        Assert(propertyNode, Rdfs + "label", _graph.CreateLiteralNode(property.Name, "en"));
        AddOptionalLiteral(propertyNode, Rdfs + "comment", property.Description);
        Assert(propertyNode, Rdfs + "range", BaseRange(property, objectProperty));
    }

    private void DeclarePropertyUse(DataType owner, Property property)
    {
        IUriNode propertyNode = PropertyNode(property);
        bool objectProperty = IsObjectProperty(property);

        INode range = objectProperty
            ? ObjectRange(property)
            : DatatypeRange(owner, property);
        AddRangeRestriction(owner, property, propertyNode, range);
        // Anonymous OWL data ranges are syntax-tree nodes in the RDF mapping;
        // they cannot be shared between the range axiom and a qualified
        // cardinality expression. Build a second structurally equivalent tree.
        INode cardinalityRange = !objectProperty && range is IBlankNode
            ? DatatypeRange(owner, property)
            : range;
        AddCardinalityRestriction(owner, property, propertyNode, cardinalityRange, objectProperty);

        if (property.Ordered)
        {
            Warn("OWL2001",
                $"OWL cannot preserve the ordering of '{owner.Name}.{property.Name}'.",
                owner,
                $"/{Kind(owner)}/{owner.Name}/Properties/{property.Name}");
        }

        if (property.DataType is not null
            && !property.DataType.IsXmlPrimitive
            && !CogsTypeSystem.AllowsSubtypes(property)
            && property.DataType.ChildTypes.Any())
        {
            Warn("OWL2002",
                $"OWL class ranges cannot enforce the property-local subtype exclusion on '{owner.Name}.{property.Name}'.",
                owner,
                $"/{Kind(owner)}/{owner.Name}/Properties/{property.Name}");
        }
    }

    private void AddRangeRestriction(
        DataType owner,
        Property property,
        IUriNode propertyNode,
        INode range)
    {
        IBlankNode restriction = BlankNode();
        Assert(Term(owner.Name), Rdfs + "subClassOf", restriction);
        Assert(restriction, Rdf + "type", UriNode(Owl + "Restriction"));
        Assert(restriction, Owl + "onProperty", propertyNode);
        Assert(restriction, Owl + "allValuesFrom", range);
        AddOptionalLiteral(restriction, Rdfs + "comment", property.Description);
    }

    private void AddCardinalityRestriction(
        DataType owner,
        Property property,
        IUriNode propertyNode,
        INode range,
        bool objectProperty)
    {
        BigInteger minimum = BigInteger.Parse(property.MinCardinality, CultureInfo.InvariantCulture);
        BigInteger? maximum = property.MaxCardinality == "n"
            ? null
            : BigInteger.Parse(property.MaxCardinality, CultureInfo.InvariantCulture);

        if (minimum == 0 && maximum is null)
        {
            return;
        }

        IBlankNode restriction = BlankNode();
        Assert(Term(owner.Name), Rdfs + "subClassOf", restriction);
        Assert(restriction, Rdf + "type", UriNode(Owl + "Restriction"));
        Assert(restriction, Owl + "onProperty", propertyNode);
        Assert(restriction, objectProperty ? Owl + "onClass" : Owl + "onDataRange", range);

        if (maximum == minimum)
        {
            Assert(restriction, Owl + "qualifiedCardinality", CardinalityLiteral(minimum));
            return;
        }

        if (minimum > 0)
        {
            Assert(restriction, Owl + "minQualifiedCardinality", CardinalityLiteral(minimum));
        }

        if (maximum.HasValue)
        {
            Assert(restriction, Owl + "maxQualifiedCardinality", CardinalityLiteral(maximum.Value));
        }
    }

    private void DeclareIdentityKey(DataType itemRoot)
    {
        var keyNodes = new List<INode>();
        IReadOnlyList<Property> effective = CogsTypeSystem.EffectiveProperties(itemRoot);
        foreach (Property identity in _model.Identification)
        {
            Property? effectiveIdentity = effective.FirstOrDefault(property => property.Name == identity.Name);
            if (effectiveIdentity is null)
            {
                continue;
            }

            keyNodes.Add(PropertyNode(effectiveIdentity));
        }

        if (keyNodes.Count > 0)
        {
            Assert(Term(itemRoot.Name), Owl + "hasKey", CreateList(keyNodes));
        }
    }

    private INode ObjectRange(Property property) => Term(property.DataTypeName);

    private INode BaseRange(Property property, bool objectProperty) => objectProperty
        ? ObjectRange(property)
        : UriNode(GetDatatypeUri(property.DataTypeName));

    private INode DatatypeRange(DataType owner, Property property)
    {
        IUriNode datatype = UriNode(GetDatatypeUri(property.DataTypeName));
        bool hasLexicalFacets = property.MinLength.HasValue || property.MaxLength.HasValue
            || !string.IsNullOrEmpty(property.Pattern)
            || !string.IsNullOrEmpty(property.MinInclusive)
            || !string.IsNullOrEmpty(property.MinExclusive)
            || !string.IsNullOrEmpty(property.MaxInclusive)
            || !string.IsNullOrEmpty(property.MaxExclusive);
        bool hasFacets = property.Enumeration.Count > 0 || hasLexicalFacets;

        if (!hasFacets)
        {
            return datatype;
        }

        if (property.DataTypeName == "langString")
        {
            Warn("OWL2004",
                $"OWL cannot portably express COGS lexical facets on rdf:langString property '{owner.Name}.{property.Name}'.",
                owner,
                $"/{Kind(owner)}/{owner.Name}/Properties/{property.Name}");
            return datatype;
        }

        INode? enumerationRange = null;
        if (property.Enumeration.Count > 0)
        {
            IBlankNode enumerated = BlankNode();
            Assert(enumerated, Rdf + "type", UriNode(Rdfs + "Datatype"));
            Assert(enumerated, Owl + "oneOf", CreateList(property.Enumeration
                .Select(value => (INode)TypedLiteral(value, datatype.Uri.AbsoluteUri))));
            enumerationRange = enumerated;
        }

        bool hasBounds = !string.IsNullOrEmpty(property.MinInclusive)
            || !string.IsNullOrEmpty(property.MinExclusive)
            || !string.IsNullOrEmpty(property.MaxInclusive)
            || !string.IsNullOrEmpty(property.MaxExclusive);
        bool omitUnsupportedBounds = hasBounds
            && DeclaredXsdDatatypes.Contains(property.DataTypeName, StringComparer.Ordinal);
        if (omitUnsupportedBounds)
        {
            Warn("OWL2006",
                $"OWL 2's datatype map cannot express bounds on COGS {property.DataTypeName} property '{owner.Name}.{property.Name}'; the bounds were omitted.",
                owner,
                $"/{Kind(owner)}/{owner.Name}/Properties/{property.Name}");
        }

        var facets = new List<INode>();
        // OWL 2's RDF mapping requires length facet values to use xsd:integer
        // and pattern facet values to be plain RDF literals.
        AddFacet(facets, "minLength", property.MinLength?.ToString(CultureInfo.InvariantCulture), Xsd + "integer");
        AddFacet(facets, "maxLength", property.MaxLength?.ToString(CultureInfo.InvariantCulture), Xsd + "integer");
        AddFacet(facets, "pattern", property.Pattern, null);
        if (!omitUnsupportedBounds)
        {
            AddFacet(facets, "minInclusive", property.MinInclusive, datatype.Uri.AbsoluteUri);
            AddFacet(facets, "minExclusive", property.MinExclusive, datatype.Uri.AbsoluteUri);
            AddFacet(facets, "maxInclusive", property.MaxInclusive, datatype.Uri.AbsoluteUri);
            AddFacet(facets, "maxExclusive", property.MaxExclusive, datatype.Uri.AbsoluteUri);
        }

        INode? restrictionRange = null;
        if (facets.Count > 0)
        {
            IBlankNode restriction = BlankNode();
            Assert(restriction, Rdf + "type", UriNode(Rdfs + "Datatype"));
            Assert(restriction, Owl + "onDatatype", datatype);
            Assert(restriction, Owl + "withRestrictions", CreateList(facets));
            restrictionRange = restriction;
        }

        if (enumerationRange is not null && restrictionRange is not null)
        {
            IBlankNode intersection = BlankNode();
            Assert(intersection, Rdf + "type", UriNode(Rdfs + "Datatype"));
            Assert(intersection, Owl + "intersectionOf", CreateList([enumerationRange, restrictionRange]));
            return intersection;
        }
        return enumerationRange ?? restrictionRange ?? datatype;
    }

    private void AddFacet(List<INode> facets, string facetName, string? lexical, string? datatypeUri)
    {
        if (string.IsNullOrEmpty(lexical)) return;
        IBlankNode facet = BlankNode();
        ILiteralNode literal = datatypeUri is null
            ? _graph.CreateLiteralNode(lexical)
            : TypedLiteral(lexical, datatypeUri);
        Assert(facet, Xsd + facetName, literal);
        facets.Add(facet);
    }

    private INode CreateList(IEnumerable<INode> members)
    {
        INode nil = UriNode(Rdf + "nil");
        INode tail = nil;
        foreach (INode member in members.Reverse())
        {
            IBlankNode cell = BlankNode();
            Assert(cell, Rdf + "first", member);
            Assert(cell, Rdf + "rest", tail);
            tail = cell;
        }
        return tail;
    }

    private static bool IsObjectProperty(Property property) =>
        property.DataType is { IsXmlPrimitive: false };

    private string GetDatatypeUri(string cogsType) => cogsType switch
    {
        "langString" => Rdf + "langString",
        "cogsDate" => _termBase + "CogsDate",
        "dateTime" => Xsd + "dateTime",
        _ => Xsd + cogsType
    };

    private IUriNode PropertyNode(Property property) =>
        UriNode(CogsRdfNaming.PropertyIri(TargetNamespace, property.Name));
    private IUriNode Term(string localName) => UriNode(_termBase + localName);
    private IUriNode UriNode(string uri) => _graph.CreateUriNode(UriFactory.Create(uri));
    private IBlankNode BlankNode() => _graph.CreateBlankNode();
    private ILiteralNode TypedLiteral(string lexical, string datatypeUri) =>
        _graph.CreateLiteralNode(lexical, UriFactory.Create(datatypeUri));
    private ILiteralNode CardinalityLiteral(BigInteger value) =>
        TypedLiteral(value.ToString(CultureInfo.InvariantCulture), Xsd + "nonNegativeInteger");

    private void Assert(INode subject, string predicateUri, INode @object) =>
        _graph.Assert(subject, UriNode(predicateUri), @object);

    private void AddOptionalLiteral(INode subject, string predicate, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Assert(subject, predicate, _graph.CreateLiteralNode(value));
        }
    }

    private void Warn(string code, string message, DataType type, string modelPath)
    {
        if (_diagnostics.Any(diagnostic => diagnostic.Code == code && diagnostic.ModelPath == modelPath)) return;
        _diagnostics.Add(new CogsError(
            ErrorLevel.Warning,
            code,
            message,
            sourcePath: type.Path,
            modelPath: modelPath));
    }

    private static string Kind(DataType type) => type is ItemType ? "ItemTypes" : "CompositeTypes";

    private static IEnumerable<CogsError> ValidateSharedPropertyCompatibility(CogsModel model)
    {
        var uses = new List<(string OwnerName, string? SourcePath, string ModelPath, Property Property)>();
        foreach (Property property in model.Identification)
        {
            uses.Add(("Identification", model.SourceDirectory, $"Identification.{property.Name}", property));
        }
        foreach (DataType owner in model.AllDataTypes.OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            foreach (Property property in owner.Properties)
            {
                uses.Add((owner.Name, owner.Path,
                    $"/{Kind(owner)}/{owner.Name}/Properties/{property.Name}", property));
            }
        }

        foreach (var group in uses.GroupBy(use => use.Property.Name, StringComparer.Ordinal))
        {
            var first = group.First();
            bool firstIsObject = IsObjectProperty(first.Property);
            foreach (var current in group.Skip(1))
            {
                bool currentIsObject = IsObjectProperty(current.Property);
                if (string.Equals(current.Property.DataTypeName, first.Property.DataTypeName, StringComparison.Ordinal)
                    && currentIsObject == firstIsObject)
                {
                    continue;
                }

                yield return new CogsError(
                    ErrorLevel.Error,
                    "OWL1001",
                    $"OWL shared property '{group.Key}' is incompatible between " +
                    $"'{first.OwnerName}.{first.Property.Name}' ({first.Property.DataTypeName}, " +
                    $"{(firstIsObject ? "object" : "datatype")}) and " +
                    $"'{current.OwnerName}.{current.Property.Name}' ({current.Property.DataTypeName}, " +
                    $"{(currentIsObject ? "object" : "datatype")}).",
                    sourcePath: current.SourcePath,
                    modelPath: current.ModelPath);
            }
        }
    }
}
