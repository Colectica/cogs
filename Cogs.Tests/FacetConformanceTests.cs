using Cogs.Common;
using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cogs.Tests;

public sealed class FacetConformanceTests
{
    private const string Namespace = "https://example.org/facet-conformance";

    public static IEnumerable<object[]> FacetCases()
    {
        yield return Case("\"Label\":\"xAb\"", "<Label>xAb</Label>", true);
        yield return Case("\"Label\":\"A\"", "<Label>A</Label>", false);
        yield return Case("\"Label\":\"abc\"", "<Label>abc</Label>", false);
        yield return Case("\"Label\":\"xAbcd\"", "<Label>xAbcd</Label>", false);

        yield return Case("\"Status\":\"red\"", "<Status>red</Status>", true);
        yield return Case("\"Status\":\"blue\"", "<Status>blue</Status>", false);

        yield return Case("\"Amount\":1.2001", "<Amount>1.2001</Amount>", true);
        yield return Case("\"Amount\":1.20", "<Amount>1.20</Amount>", false);
        yield return Case("\"Amount\":2.50", "<Amount>2.50</Amount>", false);

        yield return Case("\"Count\":-2", "<Count>-2</Count>", true);
        yield return Case("\"Count\":2", "<Count>2</Count>", true);
        yield return Case("\"Count\":3", "<Count>3</Count>", false);

        yield return Case("\"Observed\":\"2020-01-01T00:00:00Z\"",
            "<Observed>2020-01-01T00:00:00Z</Observed>", true);
        yield return Case("\"Observed\":\"2021-01-01T00:00:00Z\"",
            "<Observed>2021-01-01T00:00:00Z</Observed>", false);
        yield return Case("\"Observed\":\"2020-01-01T00:00:00\"",
            "<Observed>2020-01-01T00:00:00</Observed>", false);

        yield return Case("\"Elapsed\":\"P2M\"", "<Elapsed>P2M</Elapsed>", true);
        yield return Case("\"Elapsed\":\"P1M\"", "<Elapsed>P1M</Elapsed>", false);
        yield return Case("\"Elapsed\":\"P30D\"", "<Elapsed>P30D</Elapsed>", false);

        yield return Case("\"Caption\":{\"@language\":\"en\",\"@value\":\"Ab\"}",
            "<Caption xml:lang='en'>Ab</Caption>", true);
        yield return Case("\"Caption\":{\"@language\":\"en\",\"@value\":\"a\"}",
            "<Caption xml:lang='en'>a</Caption>", false);
        yield return Case("\"Caption\":{\"@language\":\"en\",\"@value\":\"abc\"}",
            "<Caption xml:lang='en'>abc</Caption>", false);
    }

    [Theory]
    [MemberData(nameof(FacetCases))]
    public void JsonAndXmlAgreeAtEverySupportedFacetBoundary(
        string jsonProperty,
        string xmlProperty,
        bool expectedValid)
    {
        CogsModel model = BuildModel();
        string json = $"{{\"items\":[{{\"$type\":\"FacetItem\",\"ID\":\"one\",{jsonProperty}}}]}}";
        string xml = $"<ItemContainer xmlns='{Namespace}'><FacetItem><ID>one</ID>{xmlProperty}</FacetItem></ItemContainer>";

        IReadOnlyList<CogsError> jsonErrors = CogsInstanceValidator.ValidateJson(model, json);
        IReadOnlyList<CogsError> xmlErrors = CogsInstanceValidator.ValidateXml(model, xml);

        Assert.Equal(expectedValid, jsonErrors.Count == 0);
        Assert.Equal(expectedValid, xmlErrors.Count == 0);
    }

    private static object[] Case(string json, string xml, bool valid) => [json, xml, valid];

    private static CogsModel BuildModel()
    {
        var dto = new CogsDtoModel();
        dto.Settings.AddRange(
        [
            Setting("CogsVersion", "2.0"),
            Setting("Title", "Facet Conformance"),
            Setting("ShortTitle", "Facets"),
            Setting("Slug", "facet_conformance"),
            Setting("Description", string.Empty),
            Setting("Version", "2.0.0"),
            Setting("Author", string.Empty),
            Setting("Copyright", string.Empty),
            Setting("NamespaceUrl", Namespace),
            Setting("NamespacePrefix", "f")
        ]);
        dto.Identification.Add(Property("ID", "string", "1", "1"));

        var item = new Cogs.Dto.ItemType { Name = "FacetItem" };
        item.Properties.Add(new Cogs.Dto.Property
        {
            Name = "Label",
            DataType = "string",
            MinCardinality = "0",
            MaxCardinality = "1",
            MinLength = 2,
            MaxLength = 4,
            Pattern = "[A-Z][a-z]+"
        });
        item.Properties.Add(new Cogs.Dto.Property
        {
            Name = "Status",
            DataType = "string",
            MinCardinality = "0",
            MaxCardinality = "1",
            Enumeration = "red green"
        });
        item.Properties.Add(new Cogs.Dto.Property
        {
            Name = "Amount",
            DataType = "decimal",
            MinCardinality = "0",
            MaxCardinality = "1",
            MinExclusive = "1.20",
            MaxExclusive = "2.50"
        });
        item.Properties.Add(new Cogs.Dto.Property
        {
            Name = "Count",
            DataType = "int",
            MinCardinality = "0",
            MaxCardinality = "1",
            MinInclusive = "-2",
            MaxInclusive = "2"
        });
        item.Properties.Add(new Cogs.Dto.Property
        {
            Name = "Observed",
            DataType = "dateTime",
            MinCardinality = "0",
            MaxCardinality = "1",
            MinInclusive = "2020-01-01T00:00:00Z",
            MaxExclusive = "2021-01-01T00:00:00Z"
        });
        item.Properties.Add(new Cogs.Dto.Property
        {
            Name = "Elapsed",
            DataType = "duration",
            MinCardinality = "0",
            MaxCardinality = "1",
            MinExclusive = "P1M",
            MaxInclusive = "P2M"
        });
        item.Properties.Add(new Cogs.Dto.Property
        {
            Name = "Caption",
            DataType = "langString",
            MinCardinality = "0",
            MaxCardinality = "1",
            MinLength = 2,
            MaxLength = 4,
            Pattern = "[A-Z][a-z]+"
        });
        dto.ItemTypes.Add(item);

        CogsBuildResult result = new CogsModelBuilder().BuildResult(dto);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(x => x.ToString())));
        return result.Model!;
    }

    private static Setting Setting(string key, string value) => new() { Key = key, Value = value };

    private static Cogs.Dto.Property Property(string name, string datatype, string minimum, string maximum) => new()
    {
        Name = name,
        DataType = datatype,
        MinCardinality = minimum,
        MaxCardinality = maximum
    };
}
