using Cogs.Common;
using Cogs.Model;
using Cogs.Publishers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Cogs.Tests;

public sealed class GraphQlUmlPublisherTests
{
    [Fact]
    public void GraphQlPublishesBuildableContractWithoutMutatingModel()
    {
        CogsModel model = BuildModel();
        DataType decimalType = model.ReusableDataTypes.Single(type => type.Name == "Metric")
            .Properties.Single(property => property.Name == "Value").DataType;
        using var output = new TemporaryDirectory();
        var publisher = new GraphQLPublisher { TargetDirectory = output.Path, Overwrite = true };

        publisher.Publish(model);

        string schema = File.ReadAllText(Path.Combine(output.Path, "GraphQL.graphqls"));
        Assert.Equal("decimal", decimalType.Name);
        Assert.Contains("scalar CogsDecimal", schema);
        Assert.Contains("directive @cogsCardinality", schema);
        Assert.Contains("interface Entity", schema);
        Assert.Contains("interface AssetAssignable implements Entity", schema);
        Assert.Contains("type Asset implements Entity & AssetAssignable", schema);
        Assert.Contains("type Book implements Entity & AssetAssignable", schema);
        Assert.Contains("tags: [String!]! @cogsCardinality(min: \"0\", max: \"n\", ordered: true)", schema);
        Assert.Contains("value: CogsDecimal!", schema);
        Assert.Contains("@cogsFacet(minInclusive: \"0.01\", maxExclusive: \"100.00\")", schema);
        Assert.Contains("metric: Metric", schema);
        Assert.Contains("entity(id: CogsAnyUri!): Entity", schema);
        Assert.Contains("asset(id: CogsAnyUri!): AssetAssignable", schema);
        Assert.Contains("allEntity: [Entity!]!", schema);
        Assert.Contains("allBook: [Book!]!", schema);
        Assert.Single(publisher.Errors, error => error.Code == "PROJ2501");
        AssertGraphQlStructure(schema);
    }

    [Fact]
    public void GraphQlRejectsUnrepresentableNamesTransactionally()
    {
        CogsModel model = BuildModel();
        model.ItemTypes[0].Name = "Éntity";
        using var output = new TemporaryDirectory();
        string sentinel = Path.Combine(output.Path, "sentinel.txt");
        File.WriteAllText(sentinel, "preserve");
        var publisher = new GraphQLPublisher { TargetDirectory = output.Path, Overwrite = true };

        CogsPublicationException exception = Assert.Throws<CogsPublicationException>(() => publisher.Publish(model));

        Assert.Contains("PROJ2503", exception.Message);
        Assert.Equal("preserve", File.ReadAllText(sentinel));
        Assert.False(File.Exists(Path.Combine(output.Path, "GraphQL.graphqls")));
    }

    [Fact]
    public void GraphQlImplementationsRepeatSyntheticFieldsFromEmptyBaseInterfaces()
    {
        CogsModel model = BuildModel();
        var baseValue = new DataType { Name = "BaseValue", IsAbstract = true };
        var numberValue = new DataType { Name = "NumberValue", ExtendsTypeName = "BaseValue" };
        numberValue.ParentTypes.Add(baseValue);
        baseValue.ChildTypes.Add(numberValue);
        numberValue.Properties.Add(Property("Number", Primitive("int"), "1", "1"));
        model.ReusableDataTypes.Add(baseValue);
        model.ReusableDataTypes.Add(numberValue);
        using var output = new TemporaryDirectory();
        var publisher = new GraphQLPublisher { TargetDirectory = output.Path, Overwrite = true };

        publisher.Publish(model);

        string schema = File.ReadAllText(Path.Combine(output.Path, "GraphQL.graphqls"));
        Assert.Matches(@"(?s)interface BaseValue\s*\{\s*_cogsType:", schema);
        Assert.Matches(@"(?s)type NumberValue implements BaseValue\s*\{\s*_cogsType:.*?number: Int!", schema);
        Assert.Single(publisher.Errors, error => error.Code == "PROJ2502" && error.ModelPath == "BaseValue");
    }

    [Fact]
    public void UmlNormativePublishesXmi242WithResolvableIdsAndConstraints()
    {
        CogsModel model = BuildModel();
        using var output = new TemporaryDirectory();
        var publisher = new UmlSchemaPublisher
        {
            TargetDirectory = output.Path,
            Overwrite = true,
            Normative = true
        };

        publisher.Publish(model);

        Assert.Equal("cogsDate", model.ReusableDataTypes.Single(type => type.Name == "Metric")
            .Properties.Single(property => property.Name == "DateValue").DataTypeName);
        XDocument document = XDocument.Load(Path.Combine(output.Path, "example.xmi"));
        XNamespace xmi = "http://www.omg.org/spec/XMI/20110701";
        Assert.Equal("2.4.2", document.Root!.Attribute(xmi + "version")?.Value);
        Assert.Null(document.Root.Element(xmi + "Extension"));
        Assert.Contains(document.Descendants("packagedElement"), element =>
            element.Attribute(xmi + "type")?.Value == "uml:PrimitiveType" && element.Attribute("name")?.Value == "cogsDate");
        Assert.Contains(document.Descendants("packagedElement"), element =>
            element.Attribute("name")?.Value == "Entity" && element.Attribute("isAbstract")?.Value == "true");
        Assert.Contains(document.Descendants("generalization"), element => element.Attribute("general")?.Value == "cogs.type.Entity");
        Assert.Contains(document.Descendants("ownedAttribute"), element =>
            element.Attribute("name")?.Value == "Tags" &&
            element.Attribute("isOrdered")?.Value == "true" &&
            element.Attribute("isUnique")?.Value == "false" &&
            element.Element("upperValue")?.Attribute("value")?.Value == "*");
        Assert.Contains(document.Descendants("body"), body => body.Value.Contains("COGS:identification=true;position=0", StringComparison.Ordinal));
        Assert.Contains(document.Descendants("ownedRule"), rule => rule.Attribute("name")?.Value == "COGS facets");
        Assert.Contains(document.Descendants("packagedElement"), element => element.Attribute(xmi + "type")?.Value == "uml:Association");
        Assert.Single(publisher.Errors, error => error.Code == "PROJ2601");
        AssertAllXmiReferencesResolve(document, xmi);
    }

    [Fact]
    public void UmlEaPublishesXmi251WithDeterministicDiagramExtension()
    {
        CogsModel model = BuildModel();
        using var output = new TemporaryDirectory();
        var publisher = new UmlSchemaPublisher
        {
            TargetDirectory = output.Path,
            Overwrite = true,
            Normative = false
        };

        publisher.Publish(model);

        XDocument document = XDocument.Load(Path.Combine(output.Path, "example.xmi"));
        XNamespace xmi = "http://www.omg.org/spec/XMI/20131001";
        Assert.Equal("2.5.1", document.Root!.Attribute(xmi + "version")?.Value);
        XElement extension = Assert.Single(document.Root.Elements(xmi + "Extension"));
        Assert.Equal("Enterprise Architect", extension.Attribute("extender")?.Value);
        Assert.Equal(model.AllDataTypes.Count(), extension.Descendants("element").Count());
        Assert.All(extension.Descendants("element"), element => Assert.StartsWith("cogs.type.", element.Attribute("subject")?.Value));
        AssertAllXmiReferencesResolve(document, xmi);
    }

    private static void AssertGraphQlStructure(string schema)
    {
        Assert.DoesNotContain("type duration", schema, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"(?m)^type Query \{\r?$", RegexOptions.CultureInvariant), schema);
        int depth = 0;
        bool inString = false;
        bool escaped = false;
        foreach (char character in schema)
        {
            if (inString)
            {
                if (escaped) escaped = false;
                else if (character == '\\') escaped = true;
                else if (character == '"') inString = false;
                continue;
            }
            if (character == '"') inString = true;
            else if (character == '{') depth++;
            else if (character == '}') depth--;
            Assert.True(depth >= 0, "GraphQL schema closed a block before opening it.");
        }
        Assert.False(inString);
        Assert.Equal(0, depth);
    }

    private static void AssertAllXmiReferencesResolve(XDocument document, XNamespace xmi)
    {
        string[] referenceAttributes = { "general", "association", "memberEnd", "constrainedElement", "annotatedElement", "subject", "package", "owner", "type" };
        string[] ids = document.Root!.DescendantsAndSelf().Attributes(xmi + "id").Select(attribute => attribute.Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        var idSet = ids.ToHashSet(StringComparer.Ordinal);
        foreach (XAttribute attribute in document.Root.DescendantsAndSelf().Attributes()
                     .Where(attribute => referenceAttributes.Contains(attribute.Name.LocalName, StringComparer.Ordinal) &&
                                         attribute.Name != xmi + "type" &&
                                         (attribute.Name.LocalName != "type" || attribute.Value.StartsWith("cogs.", StringComparison.Ordinal))))
        {
            foreach (string reference in attribute.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (reference.StartsWith("uml:", StringComparison.Ordinal)) continue;
                Assert.Contains(reference, idSet);
            }
        }
    }

    private static CogsModel BuildModel()
    {
        var model = new CogsModel
        {
            Settings = new Settings
            {
                CogsVersion = "2.0",
                Title = "Example",
                Slug = "example",
                Version = "2.0.0",
                NamespaceUrl = "https://example.test/model",
                NamespacePrefix = "example"
            }
        };
        var stringType = Primitive("string");
        var anyUriType = Primitive("anyURI");
        var decimalType = Primitive("decimal");
        var cogsDateType = Primitive("cogsDate");

        var id = Property("Id", anyUriType, "1", "1");
        model.Identification.Add(id);
        var entity = new ItemType { Name = "Entity", Description = "An abstract identified entity.", IsAbstract = true };
        entity.Properties.Add(id);

        var metric = new DataType { Name = "Metric", Description = "A measurement.", IsPrimitive = true };
        metric.Properties.Add(new Property
        {
            Name = "Value", DataTypeName = "decimal", DataType = decimalType,
            MinCardinality = "1", MaxCardinality = "1", MinInclusive = "0.01", MaxExclusive = "100.00"
        });
        metric.Properties.Add(Property("DateValue", cogsDateType, "0", "1"));
        var specialMetric = new DataType { Name = "SpecialMetric", ExtendsTypeName = "Metric" };
        specialMetric.ParentTypes.Add(metric);
        metric.ChildTypes.Add(specialMetric);
        specialMetric.Properties.Add(Property("Unit", stringType, "0", "1"));

        var asset = new ItemType { Name = "Asset", ExtendsTypeName = "Entity" };
        asset.ParentTypes.Add(entity);
        entity.ChildTypes.Add(asset);
        asset.Properties.Add(new Property
        {
            Name = "Tags", DataTypeName = "string", DataType = stringType,
            MinCardinality = "0", MaxCardinality = "n", Ordered = true,
            Enumeration = new List<string> { "red", "green" }
        });
        asset.Properties.Add(new Property
        {
            Name = "Metric", DataTypeName = "Metric", DataType = metric,
            MinCardinality = "0", MaxCardinality = "1", AllowSubtypes = false
        });

        var book = new ItemType { Name = "Book", ExtendsTypeName = "Asset" };
        book.ParentTypes.Add(entity);
        book.ParentTypes.Add(asset);
        asset.ChildTypes.Add(book);
        book.Properties.Add(Property("Title", stringType, "1", "1"));

        model.ItemTypes.Add(entity);
        model.ItemTypes.Add(asset);
        model.ItemTypes.Add(book);
        model.ReusableDataTypes.Add(metric);
        model.ReusableDataTypes.Add(specialMetric);
        return model;
    }

    private static DataType Primitive(string name) => new() { Name = name, IsPrimitive = true, IsXmlPrimitive = true };

    private static Property Property(string name, DataType dataType, string minimum, string maximum) => new()
    {
        Name = name,
        DataTypeName = dataType.Name,
        DataType = dataType,
        MinCardinality = minimum,
        MaxCardinality = maximum
    };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cogs-projection-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }
}
