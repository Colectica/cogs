using Cogs.Model;
using Cogs.Publishers;
using Cogs.Publishers.Csharp;
using Cogs.Publishers.LinkMl;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using VDS.RDF;
using VDS.RDF.Parsing;
using Xunit;
using YamlDotNet.Serialization;

namespace Cogs.Tests;

public sealed class SemanticPublisherTests
{
    [Fact]
    public void PublishersPreserveArbitrarySizeFiniteCardinalities()
    {
        const string minimum = "999999999999999999999999998";
        const string maximum = "999999999999999999999999999";
        CogsModel model = BuildModel();
        Property property = model.ReusableDataTypes.Single(type => type.Name == "Recipe")
            .Properties.Single(candidate => candidate.Name == "Parts");
        property.MinCardinality = minimum;
        property.MaxCardinality = maximum;
        using var temporary = new TemporaryDirectory();

        string csharpTarget = Path.Combine(temporary.Path, "csharp");
        new CSharpPublisher(model, csharpTarget)
        {
            Overwrite = false,
            IsNullableEnabled = true,
            WriteCsproj = false
        }.Publish();
        Assert.NotEmpty(Directory.EnumerateFiles(csharpTarget, "*.cs"));

        string owlTarget = Path.Combine(temporary.Path, "owl-huge");
        var owl = new OwlPublisher
        {
            TargetDirectory = owlTarget,
            TargetNamespace = model.Settings.NamespaceUrl,
            TargetNamespacePrefix = model.Settings.NamespacePrefix,
            VersionInfo = model.Settings.Version,
            Title = model.Settings.Title,
            Description = model.Settings.Description
        };
        Assert.True(owl.PublishResult(model).Success);
        string owlPath = Path.Combine(owlTarget, "semantic.ttl");
        string owlText = File.ReadAllText(owlPath);
        Assert.Contains($"owl:minQualifiedCardinality \"{minimum}\"^^xsd:nonNegativeInteger", owlText,
            StringComparison.Ordinal);
        Assert.Contains($"owl:maxQualifiedCardinality \"{maximum}\"^^xsd:nonNegativeInteger", owlText,
            StringComparison.Ordinal);
        var graph = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true)
            .Load(graph, owlPath);
        string[] cardinalities = graph.Triples
            .Select(triple => triple.Object)
            .OfType<ILiteralNode>()
            .Where(literal => literal.DataType?.AbsoluteUri == NamespaceMapper.XMLSCHEMA + "nonNegativeInteger")
            .Select(literal => literal.Value)
            .ToArray();
        Assert.Contains(minimum, cardinalities);
        Assert.Contains(maximum, cardinalities);

        string linkMlTarget = Path.Combine(temporary.Path, "linkml-huge");
        var linkMl = new LinkMlPublisher
        {
            TargetDirectory = linkMlTarget,
            Name = "Huge Cardinality",
            NamespaceUri = model.Settings.NamespaceUrl,
            NamespaceUriPrefix = model.Settings.NamespacePrefix
        };
        Assert.True(linkMl.PublishResult(model).Success);
        LinkMLModel projection = new DeserializerBuilder()
            .WithTypeConverter(new BigIntegerYamlTypeConverter())
            .Build()
            .Deserialize<LinkMLModel>(File.ReadAllText(Path.Combine(linkMlTarget, "linkml.yml")));
        LinkMLSlot parts = projection.classes["Recipe"].slot_usage["parts"];
        Assert.Equal(BigInteger.Parse(minimum, CultureInfo.InvariantCulture), parts.minimum_cardinality);
        Assert.Equal(BigInteger.Parse(maximum, CultureInfo.InvariantCulture), parts.maximum_cardinality);

        string graphQlTarget = Path.Combine(temporary.Path, "graphql-huge");
        new GraphQLPublisher { TargetDirectory = graphQlTarget }.Publish(model);
        string schema = File.ReadAllText(Path.Combine(graphQlTarget, "GraphQL.graphqls"));
        Assert.Contains($"@cogsCardinality(min: \"{minimum}\", max: \"{maximum}\", ordered: true)", schema);
    }

    [Fact]
    public void OwlUsesSharedPropertiesAndClassLocalRestrictionsAndKeys()
    {
        CogsModel model = BuildModel();
        DataType recipeType = model.ReusableDataTypes.Single(type => type.Name == "Recipe");
        Property shadeProperty = recipeType.Properties.Single(property => property.Name == "Shade");
        shadeProperty.MinLength = 1;
        DataType flavorType = model.ReusableDataTypes.Single(type => type.Name == "Flavor");
        recipeType.Properties.Add(new Property
        {
            Name = "SharedFlavor", DataTypeName = "Flavor", DataType = flavorType,
            MinCardinality = "0", MaxCardinality = "1", AllowSubtypes = true,
            Description = "Recipe-specific flavor"
        });
        recipeType.Properties.Add(new Property
        {
            Name = "BoundedDate",
            DataTypeName = "date",
            DataType = new DataType { Name = "date", IsPrimitive = true, IsXmlPrimitive = true },
            MinCardinality = "0",
            MaxCardinality = "1",
            MinInclusive = "2020-01-01"
        });
        model.ReusableDataTypes.Add(new DataType
        {
            Name = "Serving",
            Description = "Serving",
            Path = "CompositeTypes/Serving",
            Properties =
            [
                new Property
                {
                    Name = "Shade", DataTypeName = "string",
                    DataType = new DataType { Name = "string", IsPrimitive = true, IsXmlPrimitive = true },
                    MinCardinality = "1", MaxCardinality = "1", MinLength = 2,
                    Description = "Serving-specific shade"
                },
                new Property
                {
                    Name = "SharedFlavor", DataTypeName = "Flavor", DataType = flavorType,
                    MinCardinality = "1", MaxCardinality = "n", AllowSubtypes = false,
                    Description = "Serving-specific flavor"
                }
            ]
        });
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "owl");
        var publisher = new OwlPublisher
        {
            TargetDirectory = target,
            Overwrite = false,
            TargetNamespace = model.Settings.NamespaceUrl,
            TargetNamespacePrefix = model.Settings.NamespacePrefix,
            VersionInfo = model.Settings.Version,
            Title = "A & B <model> — semantic",
            Description = "Escaped & parseable\nAcross two lines"
        };

        PublicationResult result = publisher.PublishResult(model);

        Assert.True(result.Success);
        Assert.Equal(["semantic.ttl"], result.Artifacts);
        string turtlePath = Path.Combine(target, "semantic.ttl");
        Assert.True(File.Exists(turtlePath));
        Assert.False(File.Exists(Path.Combine(target, "semantic.owl")));
        byte[] turtleBytes = File.ReadAllBytes(turtlePath);
        Assert.False(turtleBytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        string turtle = File.ReadAllText(turtlePath);
        Assert.Contains("@prefix", turtle, StringComparison.Ordinal);
        Assert.DoesNotContain("<?xml", turtle, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", turtle, StringComparison.Ordinal);
        Assert.True(Regex.IsMatch(turtle,
            @"rdfs:subClassOf\s+\[\s*a\s+owl:Restriction\s*;",
            RegexOptions.CultureInvariant),
            "Expected class-local OWL restrictions to use inline Turtle blank-node syntax.");
        Assert.False(Regex.IsMatch(turtle,
            @"(?m)^_:[^\s]+\s+a\s+owl:Restriction\b",
            RegexOptions.CultureInvariant),
            "OWL restrictions should not be serialized as top-level labeled blank nodes.");
        Assert.True(Regex.IsMatch(turtle,
            @"owl:(?:hasKey|oneOf|withRestrictions)\s+\(",
            RegexOptions.CultureInvariant),
            "Expected RDF lists to use compact Turtle collection syntax.");
        Assert.Contains("semantic", turtle, StringComparison.Ordinal);
        Assert.Contains("Across two lines", turtle, StringComparison.Ordinal);
        var graph = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true).Load(graph, turtlePath);
        IUriNode recipe = Uri(graph, "https://example.test/cogs#Recipe");
        IUriNode serving = Uri(graph, "https://example.test/cogs#Serving");
        IUriNode product = Uri(graph, "https://example.test/cogs#Product");
        IUriNode parts = Uri(graph, "https://example.test/cogs#parts");
        IUriNode id = Uri(graph, "https://example.test/cogs#id");
        IUriNode subclass = Uri(graph, NamespaceMapper.RDFS + "subClassOf");
        IUriNode onProperty = Uri(graph, NamespaceMapper.OWL + "onProperty");
        IUriNode allValuesFrom = Uri(graph, NamespaceMapper.OWL + "allValuesFrom");
        IUriNode hasKey = Uri(graph, NamespaceMapper.OWL + "hasKey");
        IUriNode range = Uri(graph, NamespaceMapper.RDFS + "range");
        IUriNode domain = Uri(graph, NamespaceMapper.RDFS + "domain");
        IUriNode langString = Uri(graph, NamespaceMapper.RDF + "langString");
        IUriNode onDataRange = Uri(graph, NamespaceMapper.OWL + "onDataRange");
        IUriNode intersectionOf = Uri(graph, NamespaceMapper.OWL + "intersectionOf");
        IUriNode rdfType = Uri(graph, NamespaceMapper.RDF + "type");
        IUriNode rdfFirst = Uri(graph, NamespaceMapper.RDF + "first");
        IUriNode rdfRest = Uri(graph, NamespaceMapper.RDF + "rest");
        IUriNode rdfNil = Uri(graph, NamespaceMapper.RDF + "nil");
        IUriNode rdfsDatatype = Uri(graph, NamespaceMapper.RDFS + "Datatype");
        IUriNode datatypeProperty = Uri(graph, NamespaceMapper.OWL + "DatatypeProperty");
        IUriNode objectProperty = Uri(graph, NamespaceMapper.OWL + "ObjectProperty");
        IUriNode owlClass = Uri(graph, NamespaceMapper.OWL + "Class");
        IUriNode equivalentClass = Uri(graph, NamespaceMapper.OWL + "equivalentClass");
        IUriNode unionOf = Uri(graph, NamespaceMapper.OWL + "unionOf");
        IUriNode ontology = Uri(graph, model.Settings.NamespaceUrl);
        IUriNode label = Uri(graph, NamespaceMapper.RDFS + "label");
        IUriNode comment = Uri(graph, NamespaceMapper.RDFS + "comment");

        INode UsageRestriction(IUriNode owner, IUriNode property) => graph
            .GetTriplesWithSubjectPredicate(owner, subclass)
            .Select(triple => triple.Object)
            .Single(node => graph.GetTriplesWithSubjectPredicate(node, onProperty)
                    .Any(triple => triple.Object.Equals(property))
                && graph.GetTriplesWithSubjectPredicate(node, allValuesFrom).Any());

        Assert.Contains(graph.GetTriplesWithSubjectPredicate(ontology, label), triple =>
            triple.Object is ILiteralNode literal && literal.Value == "A & B <model> — semantic");
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(ontology, comment), triple =>
            triple.Object is ILiteralNode literal && literal.Value == "Escaped & parseable\nAcross two lines");

        Assert.Contains(graph.GetTriplesWithSubjectPredicate(recipe, subclass), triple =>
            triple.Object.NodeType == NodeType.Blank
            && graph.GetTriplesWithSubjectPredicate(triple.Object, onProperty)
                .Any(restriction => restriction.Object.Equals(parts)));
        INode keyList = Assert.Single(graph.GetTriplesWithSubjectPredicate(
            Uri(graph, "https://example.test/cogs#Base"), hasKey)).Object;
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(keyList, rdfFirst), triple => triple.Object.Equals(id));
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(
                Uri(graph, "https://example.test/cogs#localizedName"), range),
            triple => triple.Object.Equals(langString));
        Assert.Contains(graph.Triples, triple => triple.Subject.Equals(id));
        Assert.DoesNotContain(graph.Triples, triple => triple.Predicate.Equals(domain));
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(Uri(graph, NamespaceMapper.XMLSCHEMA + "date"), rdfType),
            triple => triple.Object.Equals(rdfsDatatype));
        IUriNode shade = Uri(graph, "https://example.test/cogs#shade");
        Assert.Single(graph.GetTriplesWithSubjectPredicate(shade, rdfType),
            triple => triple.Object.Equals(datatypeProperty));
        Assert.Equal(Uri(graph, NamespaceMapper.XMLSCHEMA + "string"),
            Assert.Single(graph.GetTriplesWithSubjectPredicate(shade, range)).Object);
        Assert.Equal("Shade", Assert.IsAssignableFrom<ILiteralNode>(
            Assert.Single(graph.GetTriplesWithSubjectPredicate(shade, comment)).Object).Value);
        Assert.DoesNotContain(graph.GetTriplesWithSubjectPredicate(shade, comment), triple =>
            triple.Object is ILiteralNode literal && literal.Value == "Serving-specific shade");
        IUriNode urlValue = Uri(graph, "https://example.test/cogs#urlValue");
        Assert.Single(graph.GetTriplesWithSubjectPredicate(urlValue, rdfType),
            triple => triple.Object.Equals(datatypeProperty));
        IUriNode cogsDate = Uri(graph, "https://example.test/cogs#CogsDate");
        Assert.Single(graph.GetTriplesWithSubjectPredicate(cogsDate, rdfType),
            triple => triple.Object.Equals(rdfsDatatype));
        Assert.DoesNotContain(graph.GetTriplesWithSubjectPredicate(cogsDate, rdfType),
            triple => triple.Object.Equals(owlClass));
        IUriNode when = Uri(graph, "https://example.test/cogs#when");
        Assert.Single(graph.GetTriplesWithSubjectPredicate(when, rdfType),
            triple => triple.Object.Equals(datatypeProperty));
        Assert.Equal(cogsDate, Assert.Single(graph.GetTriplesWithSubjectPredicate(when, range)).Object);

        INode cogsDateUnion = Assert.Single(
            graph.GetTriplesWithSubjectPredicate(cogsDate, equivalentClass)).Object;
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(cogsDateUnion, rdfType),
            triple => triple.Object.Equals(rdfsDatatype));
        INode unionMembers = Assert.Single(
            graph.GetTriplesWithSubjectPredicate(cogsDateUnion, unionOf)).Object;
        var memberUris = new List<string>();
        while (!unionMembers.Equals(rdfNil))
        {
            memberUris.Add(Assert.IsAssignableFrom<IUriNode>(Assert.Single(
                graph.GetTriplesWithSubjectPredicate(unionMembers, rdfFirst)).Object).Uri.AbsoluteUri);
            unionMembers = Assert.Single(
                graph.GetTriplesWithSubjectPredicate(unionMembers, rdfRest)).Object;
        }
        Assert.Equal(
        [
            NamespaceMapper.XMLSCHEMA + "dateTime",
            NamespaceMapper.XMLSCHEMA + "date",
            NamespaceMapper.XMLSCHEMA + "gYearMonth",
            NamespaceMapper.XMLSCHEMA + "gYear",
            NamespaceMapper.XMLSCHEMA + "duration",
        ], memberUris);

        INode recipeShadeUsage = UsageRestriction(recipe, shade);
        INode servingShadeUsage = UsageRestriction(serving, shade);
        Assert.Equal("Shade", Assert.IsAssignableFrom<ILiteralNode>(
            Assert.Single(graph.GetTriplesWithSubjectPredicate(recipeShadeUsage, comment)).Object).Value);
        Assert.Equal("Serving-specific shade",
            Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(
                graph.GetTriplesWithSubjectPredicate(servingShadeUsage, comment)).Object).Value);
        INode recipeUsageRange = Assert.Single(
            graph.GetTriplesWithSubjectPredicate(recipeShadeUsage, allValuesFrom)).Object;
        INode servingUsageRange = Assert.Single(
            graph.GetTriplesWithSubjectPredicate(servingShadeUsage, allValuesFrom)).Object;
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(recipeUsageRange, intersectionOf),
            triple => triple.Object.NodeType == NodeType.Blank);
        Assert.NotEqual(recipeUsageRange, servingUsageRange);
        INode cardinalityRange = graph.GetTriplesWithSubjectPredicate(recipe, subclass)
            .Select(triple => triple.Object)
            .Where(node => graph.GetTriplesWithSubjectPredicate(node, onProperty)
                .Any(triple => triple.Object.Equals(shade)))
            .SelectMany(node => graph.GetTriplesWithSubjectPredicate(node, onDataRange))
            .Select(triple => triple.Object)
            .Single();
        Assert.NotEqual(recipeUsageRange, cardinalityRange);
        IUriNode sharedFlavor = Uri(graph, "https://example.test/cogs#sharedFlavor");
        Assert.Single(graph.GetTriplesWithSubjectPredicate(sharedFlavor, rdfType),
            triple => triple.Object.Equals(objectProperty));
        Assert.Equal(Uri(graph, "https://example.test/cogs#Flavor"),
            Assert.Single(graph.GetTriplesWithSubjectPredicate(sharedFlavor, range)).Object);
        Assert.Equal("Recipe-specific flavor", Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(
            graph.GetTriplesWithSubjectPredicate(sharedFlavor, comment)).Object).Value);
        INode recipeFlavorUsage = UsageRestriction(recipe, sharedFlavor);
        INode servingFlavorUsage = UsageRestriction(serving, sharedFlavor);
        Assert.Equal("Recipe-specific flavor", Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(
            graph.GetTriplesWithSubjectPredicate(recipeFlavorUsage, comment)).Object).Value);
        Assert.Equal("Serving-specific flavor", Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(
            graph.GetTriplesWithSubjectPredicate(servingFlavorUsage, comment)).Object).Value);
        Assert.DoesNotContain(graph.GetTriplesWithSubjectPredicate(product, subclass), triple =>
            graph.GetTriplesWithSubjectPredicate(triple.Object, onProperty)
                .Any(restriction => restriction.Object.Equals(id)));
        Assert.Contains(graph.GetTriplesWithSubjectPredicate(
                Uri(graph, "https://example.test/cogs#boundedDate"), range),
            triple => triple.Object.Equals(Uri(graph, NamespaceMapper.XMLSCHEMA + "date")));
        Assert.DoesNotContain(graph.Triples, triple =>
            triple.Subject.Equals(Uri(graph, "https://example.test/cogs#Shade"))
            || triple.Subject.Equals(Uri(graph, "https://example.test/cogs#Parts"))
            || triple.Subject.Equals(Uri(graph, "https://example.test/cogs#Id"))
            || triple.Subject.Equals(Uri(graph, "https://example.test/cogs#URLValue"))
            || triple.Subject.Equals(Uri(graph, "https://example.test/cogs#Recipe.Shade"))
            || triple.Subject.Equals(Uri(graph, "https://example.test/cogs#Serving.Shade"))
            || triple.Subject.Equals(Uri(graph, "https://example.test/cogs#Base.Id")));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OWL2001"
            && diagnostic.SourcePath == "CompositeTypes/Recipe");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OWL2002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OWL2006"
            && diagnostic.ModelPath == "/CompositeTypes/Recipe/Properties/BoundedDate");

        string secondTarget = Path.Combine(temporary.Path, "owl-second");
        publisher.TargetDirectory = secondTarget;
        PublicationResult secondResult = publisher.PublishResult(model);
        Assert.True(secondResult.Success);
        var secondGraph = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true)
            .Load(secondGraph, Path.Combine(secondTarget, "semantic.ttl"));
        Assert.True(graph.Equals(secondGraph),
            "Repeated OWL generation must produce the same RDF graph regardless of blank-node labels or Turtle ordering.");
    }

    [Fact]
    public void OwlGraphEqualityIgnoresBlankNodeLabelsAndSerializationOrder()
    {
        const string leftTurtle = """
            @prefix ex: <https://example.test/> .
            _:first ex:name "same" ; ex:child _:second .
            _:second ex:value "value" .
            """;
        const string equivalentTurtle = """
            @prefix other: <https://example.test/> .
            _:beta other:value "value" .
            _:alpha other:child _:beta ; other:name "same" .
            """;
        const string changedTurtle = """
            @prefix ex: <https://example.test/> .
            _:first ex:name "changed" ; ex:child _:second .
            _:second ex:value "value" .
            """;
        var parser = new TurtleParser(TurtleSyntax.W3C, validateIris: true);
        var left = new Graph();
        var equivalent = new Graph();
        var changed = new Graph();
        parser.Load(left, new StringReader(leftTurtle));
        parser.Load(equivalent, new StringReader(equivalentTurtle));
        parser.Load(changed, new StringReader(changedTurtle));

        Assert.True(left.Equals(equivalent));
        Assert.False(left.Equals(changed));
    }

    [Fact]
    public void OwlSharedPropertyKeepsBlankFirstDescriptionAndLocalLaterDescription()
    {
        CogsModel model = BuildModel();
        DataType recipe = model.ReusableDataTypes.Single(type => type.Name == "Recipe");
        recipe.Properties.Single(property => property.Name == "Shade").Description = string.Empty;
        model.ReusableDataTypes.Add(new DataType
        {
            Name = "Serving",
            Path = "CompositeTypes/Serving",
            Properties =
            [
                new Property
                {
                    Name = "Shade", DataTypeName = "string",
                    DataType = new DataType { Name = "string", IsPrimitive = true, IsXmlPrimitive = true },
                    MinCardinality = "0", MaxCardinality = "1",
                    Description = "Later local description"
                }
            ]
        });
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "owl");
        var publisher = CreateOwlPublisher(model, target);

        PublicationResult result = publisher.PublishResult(model);

        Assert.True(result.Success);
        var graph = new Graph();
        new TurtleParser(TurtleSyntax.W3C, validateIris: true)
            .Load(graph, Path.Combine(target, "semantic.ttl"));
        IUriNode shade = Uri(graph, "https://example.test/cogs#shade");
        IUriNode comment = Uri(graph, NamespaceMapper.RDFS + "comment");
        IUriNode subclass = Uri(graph, NamespaceMapper.RDFS + "subClassOf");
        IUriNode onProperty = Uri(graph, NamespaceMapper.OWL + "onProperty");
        IUriNode allValuesFrom = Uri(graph, NamespaceMapper.OWL + "allValuesFrom");
        Assert.Empty(graph.GetTriplesWithSubjectPredicate(shade, comment));

        INode recipeUsage = UsageRestriction(
            graph, Uri(graph, "https://example.test/cogs#Recipe"), shade,
            subclass, onProperty, allValuesFrom);
        INode servingUsage = UsageRestriction(
            graph, Uri(graph, "https://example.test/cogs#Serving"), shade,
            subclass, onProperty, allValuesFrom);
        Assert.Empty(graph.GetTriplesWithSubjectPredicate(recipeUsage, comment));
        Assert.Equal("Later local description", Assert.IsAssignableFrom<ILiteralNode>(Assert.Single(
            graph.GetTriplesWithSubjectPredicate(servingUsage, comment)).Object).Value);
    }

    [Fact]
    public void OwlPreflightRejectsSharedPropertyDatatypeConflictsWithoutChangingTarget()
    {
        CogsModel model = BuildModel();
        model.ReusableDataTypes.Add(new DataType
        {
            Name = "ZConflict",
            Path = "CompositeTypes/ZConflict",
            Properties =
            [
                new Property
                {
                    Name = "Shade", DataTypeName = "int",
                    DataType = new DataType { Name = "int", IsPrimitive = true, IsXmlPrimitive = true },
                    MinCardinality = "0", MaxCardinality = "1"
                }
            ]
        });
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "owl");
        Directory.CreateDirectory(target);
        string sentinelPath = Path.Combine(target, "sentinel.txt");
        File.WriteAllText(sentinelPath, "unchanged");
        var publisher = CreateOwlPublisher(model, target);
        publisher.Overwrite = true;

        PublicationResult result = publisher.PublishResult(model);

        Assert.False(result.Success);
        Assert.Empty(result.Artifacts);
        Cogs.Common.CogsError diagnostic = Assert.Single(result.Diagnostics,
            diagnostic => diagnostic.Code == "OWL1001");
        Assert.Equal("CompositeTypes/ZConflict", diagnostic.SourcePath);
        Assert.Equal("/CompositeTypes/ZConflict/Properties/Shade", diagnostic.ModelPath);
        Assert.Equal("unchanged", File.ReadAllText(sentinelPath));
        Assert.False(File.Exists(Path.Combine(target, "semantic.ttl")));
    }

    [Fact]
    public void OwlPreflightRejectsSharedPropertyKindConflicts()
    {
        CogsModel model = BuildModel();
        model.ReusableDataTypes.Add(new DataType
        {
            Name = "ZConflict",
            Path = "CompositeTypes/ZConflict",
            Properties =
            [
                new Property
                {
                    Name = "Shade", DataTypeName = "string",
                    DataType = new DataType { Name = "string", IsPrimitive = false, IsXmlPrimitive = false },
                    MinCardinality = "0", MaxCardinality = "1"
                }
            ]
        });
        using var temporary = new TemporaryDirectory();
        var publisher = CreateOwlPublisher(model, Path.Combine(temporary.Path, "owl"));

        PublicationResult result = publisher.PublishResult(model);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OWL1001"
            && diagnostic.Message.Contains("object", StringComparison.Ordinal)
            && diagnostic.Message.Contains("datatype", StringComparison.Ordinal));
    }

    [Fact]
    public void OwlPreflightChecksIdentityMetadataAgainstOwningProperties()
    {
        CogsModel model = BuildModel();
        model.Identification[0] = new Property
        {
            Name = "Id", DataTypeName = "int",
            DataType = new DataType { Name = "int", IsPrimitive = true, IsXmlPrimitive = true },
            MinCardinality = "1", MaxCardinality = "1"
        };
        using var temporary = new TemporaryDirectory();
        var publisher = CreateOwlPublisher(model, Path.Combine(temporary.Path, "owl"));

        PublicationResult result = publisher.PublishResult(model);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "OWL1001"
            && diagnostic.Message.Contains("Identification.Id", StringComparison.Ordinal)
            && diagnostic.Message.Contains("Base.Id", StringComparison.Ordinal));
    }

    [Fact]
    public void LinkMlResolvesBuiltinsAndPreservesModelMetadata()
    {
        CogsModel model = BuildModel();
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "linkml");
        var publisher = new LinkMlPublisher
        {
            TargetDirectory = target,
            Name = "Requested Name",
            NamespaceUri = model.Settings.NamespaceUrl,
            NamespaceUriPrefix = model.Settings.NamespacePrefix
        };

        PublicationResult result = publisher.PublishResult(model);

        Assert.True(result.Success);
        string yaml = File.ReadAllText(Path.Combine(target, "linkml.yml"));
        LinkMLModel projection = new DeserializerBuilder()
            .WithTypeConverter(new BigIntegerYamlTypeConverter())
            .Build()
            .Deserialize<LinkMLModel>(yaml);
        Assert.Equal("Requested_Name", projection.name);
        Assert.Equal("Base", projection.classes["Product"].is_a);
        Assert.True(projection.classes["Base"].IsAbstract);
        Assert.Equal(["id"], projection.classes["Base"].unique_keys["identification"].unique_key_slots);
        Assert.Equal("cogs_lang_string", projection.classes["Recipe"].slot_usage["localizedName"].range);
        Assert.Equal("Flavor", projection.classes["Recipe"].slot_usage["parts"].range);
        Assert.True(projection.classes["Recipe"].slot_usage["parts"].multivalued);
        Assert.True(projection.classes["Recipe"].slot_usage["parts"].list_elements_ordered);
        Assert.Equal(2, projection.classes["Recipe"].slot_usage["parts"].minimum_cardinality);
        Assert.Equal(["red", "green"], projection.classes["Recipe"].slot_usage["shade"].equals_string_in);
        Assert.Equal("sem:urlValue", projection.slots["urlValue"].slot_uri);
        Assert.Equal("https://example.test/cogs#", projection.prefixes["sem"]);
        Assert.Contains("urlValue", projection.classes["Recipe"].slots);
        Assert.Contains("cogs_g_year_month", projection.types.Keys);
        Assert.Contains("cogs_unsigned_long", projection.types.Keys);
        Assert.Equal("rdf:langString", projection.types["cogs_lang_string"].uri);
        Assert.Equal("string", projection.types["cogs_duration"].TypeOf);
        Assert.Equal("string", projection.types["cogs_date_time"].TypeOf);
        Assert.Equal("xsd:dateTime", projection.types["cogs_date_time"].uri);
        Assert.Contains("signed 32-bit", projection.types["cogs_date_time"].description!);
        Assert.Equal("string", projection.types["cogs_date_only"].TypeOf);
        Assert.Equal("xsd:date", projection.types["cogs_date_only"].uri);
        Assert.Equal("xsd:integer", projection.types["cogs_int"].uri);
        Assert.Equal(int.MinValue.ToString(), projection.types["cogs_int"].minimum_value?.ToString());
        Assert.Equal(int.MaxValue.ToString(), projection.types["cogs_int"].maximum_value?.ToString());
        Assert.Equal("string", projection.types["cogs_date"].TypeOf);
        Assert.Equal("https://cogsdata.org/types/cogsDate", projection.types["cogs_date"].uri);
        Assert.Contains("cogs_date_only", projection.types["cogs_date"].union_of);
        Assert.DoesNotContain("date", projection.types["cogs_date"].union_of);
        Assert.DoesNotContain("type_uri:", yaml, StringComparison.Ordinal);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "LNK2001"
            && diagnostic.ModelPath == "/CompositeTypes/Recipe/Properties/ExactFlavor");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "LNK2003");
    }

    [Fact]
    public void DcTapIsNonMutatingAndUsesSingleConformantCells()
    {
        CogsModel model = BuildModel();
        DataType flavorChild = model.ReusableDataTypes.Single(type => type.Name == "SpicyFlavor");
        Assert.False(flavorChild.IsAbstract);
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "dctap");
        var publisher = new DcTapPublisher
        {
            TargetDirectory = target,
            CogsModel = model
        };

        PublicationResult result = publisher.PublishResult();

        Assert.True(result.Success);
        Assert.False(flavorChild.IsAbstract);
        using var reader = new StreamReader(Path.Combine(target, "dctap.csv"));
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        List<DcTapEntry> entries = csv.GetRecords<DcTapEntry>().ToList();
        DcTapEntry parts = entries.Single(entry => string.IsNullOrEmpty(entry.ShapeId) && entry.PropertyId == "sem:parts");
        DcTapEntry language = entries.Single(entry => string.IsNullOrEmpty(entry.ShapeId) && entry.PropertyId == "sem:localizedName");
        DcTapEntry date = entries.Single(entry => string.IsNullOrEmpty(entry.ShapeId) && entry.PropertyId == "sem:when");
        DcTapEntry shade = entries.Single(entry => string.IsNullOrEmpty(entry.ShapeId) && entry.PropertyId == "sem:shade");
        Assert.Contains(entries, entry => string.IsNullOrEmpty(entry.ShapeId) && entry.PropertyId == "sem:urlValue");

        Assert.Equal("bnode", parts.ValueNodeType);
        Assert.Equal("sem:Flavor", parts.ValueShape);
        Assert.Equal("rdf:langString", language.ValueDataType);
        Assert.True(string.IsNullOrEmpty(date.ValueDataType));
        Assert.Equal("picklist", shade.ValueConstraintType);
        Assert.Equal("red,green", shade.ValueConstraint);
        Assert.Contains(entries, entry => entry.ShapeId == "sem:Flavor");
        Assert.All(entries.Where(entry => !string.IsNullOrEmpty(entry.ShapeId) && string.IsNullOrEmpty(entry.PropertyId)),
            entry => Assert.True(string.IsNullOrEmpty(entry.Note)));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DCT2001");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DCT2002");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DCT2003");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DCT2009"
            && diagnostic.ModelPath == "/CompositeTypes/Recipe");
    }

    [Fact]
    public void RdfPublishersRejectCamelCaseTermCollisionsBeforeChangingTargets()
    {
        CogsModel model = BuildModel();
        model.ReusableDataTypes.Add(new DataType
        {
            Name = "Collision",
            Path = "CompositeTypes/Collision",
            Properties =
            [
                new Property
                {
                    Name = "UrlValue", DataTypeName = "string",
                    DataType = new DataType { Name = "string", IsPrimitive = true, IsXmlPrimitive = true },
                    MinCardinality = "0", MaxCardinality = "1"
                }
            ]
        });
        using var temporary = new TemporaryDirectory();

        string owlTarget = ExistingTarget(temporary.Path, "owl-collision");
        OwlPublisher owl = CreateOwlPublisher(model, owlTarget);
        owl.Overwrite = true;
        PublicationResult owlResult = owl.PublishResult(model);
        AssertCollision(owlResult, "OWL1002", owlTarget);

        string dctapTarget = ExistingTarget(temporary.Path, "dctap-collision");
        PublicationResult dctapResult = new DcTapPublisher
        {
            CogsModel = model,
            TargetDirectory = dctapTarget,
            Overwrite = true
        }.PublishResult();
        AssertCollision(dctapResult, "DCT1001", dctapTarget);

        string linkMlTarget = ExistingTarget(temporary.Path, "linkml-collision");
        PublicationResult linkMlResult = new LinkMlPublisher
        {
            TargetDirectory = linkMlTarget,
            Overwrite = true,
            Name = "Collision",
            NamespaceUri = model.Settings.NamespaceUrl,
            NamespaceUriPrefix = model.Settings.NamespacePrefix
        }.PublishResult(model);
        AssertCollision(linkMlResult, "LNK1001", linkMlTarget);

        static string ExistingTarget(string parent, string name)
        {
            string target = Path.Combine(parent, name);
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "sentinel.txt"), "unchanged");
            return target;
        }

        static void AssertCollision(PublicationResult result, string code, string target)
        {
            Assert.False(result.Success);
            Assert.Empty(result.Artifacts);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == code
                && diagnostic.Message.Contains("URLValue", StringComparison.Ordinal)
                && diagnostic.Message.Contains("UrlValue", StringComparison.Ordinal)
                && diagnostic.Message.Contains("urlValue", StringComparison.Ordinal));
            Assert.Equal("unchanged", File.ReadAllText(Path.Combine(target, "sentinel.txt")));
            Assert.Single(Directory.EnumerateFiles(target));
        }
    }

    private static CogsModel BuildModel()
    {
        var model = new CogsModel
        {
            SourceDirectory = "fixture",
            Settings = new Cogs.Model.Settings
            {
                CogsVersion = "2.0",
                Title = "Semantic projection",
                ShortTitle = "Semantic",
                Slug = "semantic",
                Description = "Semantic publisher fixture",
                Version = "2.0.0",
                Author = string.Empty,
                Copyright = string.Empty,
                NamespaceUrl = "https://example.test/cogs",
                NamespacePrefix = "sem",
                CSharpNamespace = "Semantic"
            }
        };

        DataType StringType() => new() { Name = "string", IsXmlPrimitive = true, IsPrimitive = true };
        DataType LangStringType() => new() { Name = "langString", IsXmlPrimitive = true, IsPrimitive = true };
        DataType CogsDateType() => new() { Name = "cogsDate", IsXmlPrimitive = true, IsPrimitive = true };

        var identifier = new Property
        {
            Name = "Id",
            DataTypeName = "string",
            DataType = StringType(),
            MinCardinality = "1",
            MaxCardinality = "1",
            Description = "Identifier"
        };
        var itemBase = new ItemType
        {
            Name = "Base",
            IsAbstract = true,
            Description = "Abstract item base",
            Path = "ItemTypes/Base",
            Properties = [identifier]
        };
        var product = new ItemType
        {
            Name = "Product",
            ExtendsTypeName = "Base",
            Description = "Concrete product",
            Path = "ItemTypes/Product"
        };
        product.ParentTypes.Add(itemBase);
        itemBase.ChildTypes.Add(product);
        model.ItemTypes.Add(itemBase);
        model.ItemTypes.Add(product);
        model.Identification.Add(identifier);

        var flavor = new DataType
        {
            Name = "Flavor",
            IsAbstract = true,
            Description = "Flavor base",
            Path = "CompositeTypes/Flavor"
        };
        var spicy = new DataType
        {
            Name = "SpicyFlavor",
            ExtendsTypeName = "Flavor",
            Description = "Spicy flavor",
            Path = "CompositeTypes/SpicyFlavor"
        };
        spicy.ParentTypes.Add(flavor);
        flavor.ChildTypes.Add(spicy);
        var extraSpicy = new DataType
        {
            Name = "ExtraSpicyFlavor",
            ExtendsTypeName = "SpicyFlavor",
            Description = "Extra-spicy flavor",
            Path = "CompositeTypes/ExtraSpicyFlavor"
        };
        extraSpicy.ParentTypes.Add(spicy);
        spicy.ChildTypes.Add(extraSpicy);

        var recipe = new DataType
        {
            Name = "Recipe",
            Description = "Recipe",
            Path = "CompositeTypes/Recipe",
            Properties =
            [
                new Property
                {
                    Name = "LocalizedName", DataTypeName = "langString", DataType = LangStringType(),
                    MinCardinality = "1", MaxCardinality = "1", Description = "Localized name"
                },
                new Property
                {
                    Name = "Parts", DataTypeName = "Flavor", DataType = flavor,
                    MinCardinality = "2", MaxCardinality = "n", Ordered = true, AllowSubtypes = false,
                    Description = "Ordered flavor parts"
                },
                new Property
                {
                    Name = "ExactFlavor", DataTypeName = "SpicyFlavor", DataType = spicy,
                    MinCardinality = "0", MaxCardinality = "1", AllowSubtypes = false,
                    Description = "An exact concrete flavor"
                },
                new Property
                {
                    Name = "When", DataTypeName = "cogsDate", DataType = CogsDateType(),
                    MinCardinality = "0", MaxCardinality = "1", Description = "Flexible date"
                },
                new Property
                {
                    Name = "Shade", DataTypeName = "string", DataType = StringType(),
                    MinCardinality = "0", MaxCardinality = "1", Description = "Shade",
                    Enumeration = ["red", "green"], Pattern = "[a-z]+"
                },
                new Property
                {
                    Name = "URLValue", DataTypeName = "string", DataType = StringType(),
                    MinCardinality = "0", MaxCardinality = "1", Description = "A URL value"
                }
            ]
        };
        model.ReusableDataTypes.Add(flavor);
        model.ReusableDataTypes.Add(spicy);
        model.ReusableDataTypes.Add(extraSpicy);
        model.ReusableDataTypes.Add(recipe);
        return model;
    }

    private static IUriNode Uri(IGraph graph, string value) =>
        graph.CreateUriNode(UriFactory.Create(value));

    private static INode UsageRestriction(
        IGraph graph,
        IUriNode owner,
        IUriNode property,
        IUriNode subclass,
        IUriNode onProperty,
        IUriNode allValuesFrom) => graph
        .GetTriplesWithSubjectPredicate(owner, subclass)
        .Select(triple => triple.Object)
        .Single(node => graph.GetTriplesWithSubjectPredicate(node, onProperty)
                .Any(triple => triple.Object.Equals(property))
            && graph.GetTriplesWithSubjectPredicate(node, allValuesFrom).Any());

    private static OwlPublisher CreateOwlPublisher(CogsModel model, string target) => new()
    {
        TargetDirectory = target,
        TargetNamespace = model.Settings.NamespaceUrl,
        TargetNamespacePrefix = model.Settings.NamespacePrefix,
        VersionInfo = model.Settings.Version,
        Title = model.Settings.Title,
        Description = model.Settings.Description
    };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cogs-semantic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
