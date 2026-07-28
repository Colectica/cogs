using Cogs.Common;
using Cogs.Model;
using Cogs.Publishers;
using System;
using System.Linq;
using Xunit;

namespace Cogs.Tests;

public sealed class InstanceValidatorTests
{
    private const string Namespace = "https://example.org/instance";

    [Fact]
    public void JsonValidationCombinesClosedSchemaAndCogsLexicalChecks()
    {
        CogsModel model = BuildModel();

        Assert.Empty(CogsInstanceValidator.ValidateJson(model,
            "{\"items\":[{\"$type\":\"Thing\",\"ID\":\"one\",\"Amount\":0.100,\"Observed\":\"2020-01-01T00:00:00Z\"}]}"));

        Assert.Contains(CogsInstanceValidator.ValidateJson(model,
            "{\"items\":[{\"$type\":\"Thing\",\"ID\":\"one\",\"ID\":\"two\"}]}"),
            error => error.Code == "INS1003");
        Assert.Contains(CogsInstanceValidator.ValidateJson(model,
            "{\"items\":[{\"$type\":\"Thing\",\"ID\":\"one\",\"Amount\":1e2}]}"),
            error => error.Code == "INS1005");
        Assert.Contains(CogsInstanceValidator.ValidateJson(model,
            "{\"items\":[{\"$type\":\"Thing\",\"ID\":\"one\",\"Observed\":\"2019-12-31T23:59:59Z\"}]}"),
            error => error.Code == "INS1007");
        Assert.Contains(CogsInstanceValidator.ValidateJson(model,
            "{\"items\":[{\"$type\":\"Thing\",\"ID\":\"one\"},{\"$type\":\"Thing\",\"ID\":\"one\"}]}"),
            error => error.Code == "INS1004");
        Assert.Contains(CogsInstanceValidator.ValidateJson(model,
            "{\"items\":[{\"$type\":\"Thing\",\"ID\":\"one\",\"Observed\":\"2020-01-01T00:00:00\"}]}"),
            error => error.Code == "INS1007");
        Assert.Contains(CogsInstanceValidator.ValidateJson(model,
            "{\"items\":[{\"$type\":\"Thing\",\"ID\":\"one\",\"Elapsed\":\"P30D\"}]}"),
            error => error.Code == "INS1007");
    }

    [Fact]
    public void XmlValidationUsesTheGeneratedXsdAndProhibitsDtds()
    {
        CogsModel model = BuildModel();

        Assert.Empty(CogsInstanceValidator.ValidateXml(model,
            $"<ItemContainer xmlns='{Namespace}'><Thing><ID>one</ID><Amount>0.100</Amount><Observed>2020-01-01T00:00:00Z</Observed></Thing></ItemContainer>"));
        Assert.NotEmpty(CogsInstanceValidator.ValidateXml(model,
            $"<ItemContainer xmlns='{Namespace}'><Thing><ID>one</ID><Unknown>x</Unknown></Thing></ItemContainer>"));
        Assert.Contains(CogsInstanceValidator.ValidateXml(model,
            $"<!DOCTYPE ItemContainer [<!ENTITY x 'bad'>]><ItemContainer xmlns='{Namespace}'><Thing><ID>&x;</ID></Thing></ItemContainer>"),
            error => error.Code == "INS2003");
        Assert.Contains(CogsInstanceValidator.ValidateXml(model,
            $"<ItemContainer xmlns='{Namespace}'><Thing><ID>one</ID><Observed>2020-01-01T00:00:00</Observed></Thing></ItemContainer>"),
            error => error.Code == "INS2007");
        Assert.Contains(CogsInstanceValidator.ValidateXml(model,
            $"<ItemContainer xmlns='{Namespace}'><Thing><ID>one</ID><Elapsed>P30D</Elapsed></Thing></ItemContainer>"),
            error => error.Code == "INS2007");
    }

    [Fact]
    public void TemporalAndDurationBoundsUseTheXsdPartialOrder()
    {
        Assert.Equal(CogsPrimitiveOrder.Indeterminate,
            CogsPrimitiveLexical.Compare("dateTime", "2020-01-01T00:00:00", "2020-01-01T00:00:00Z"));
        Assert.Equal(CogsPrimitiveOrder.Indeterminate,
            CogsPrimitiveLexical.Compare("duration", "P30D", "P1M"));
        Assert.Equal(CogsPrimitiveOrder.Greater,
            CogsPrimitiveLexical.Compare("duration", "P2M", "P1M"));
    }

    [Fact]
    public void SyntaxAndDuplicateDiagnosticsCarrySourceLineAndColumn()
    {
        CogsModel model = BuildModel();
        const string duplicate = "{\n \"items\": [\n  {\"$type\":\"Thing\",\"ID\":\"one\",\n   \"ID\":\"two\"}\n ]\n}";

        CogsError duplicateError = Assert.Single(CogsInstanceValidator.ValidateJson(model, duplicate, "duplicate.json"));
        Assert.Equal("INS1003", duplicateError.Code);
        Assert.Equal("duplicate.json", duplicateError.SourcePath);
        Assert.Equal(4, duplicateError.Line);
        Assert.Equal(4, duplicateError.Column);

        CogsError jsonError = Assert.Single(CogsInstanceValidator.ValidateJson(model, "{\n  \"items\": [", "broken.json"));
        Assert.Equal("INS1001", jsonError.Code);
        Assert.Equal("broken.json", jsonError.SourcePath);
        Assert.NotNull(jsonError.Line);
        Assert.NotNull(jsonError.Column);

        CogsError xmlError = Assert.Single(CogsInstanceValidator.ValidateXml(model,
            $"<ItemContainer xmlns='{Namespace}'>\n  <Thing><ID>one</Thing>\n</ItemContainer>", "broken.xml"));
        Assert.Equal("INS2003", xmlError.Code);
        Assert.Equal("broken.xml", xmlError.SourcePath);
        Assert.Equal(2, xmlError.Line);
        Assert.NotNull(xmlError.Column);
    }

    [Fact]
    public void XmlValidationCompensatesForDotNetXsdIntegerAndMidnightLexicalLimitations()
    {
        CogsModel model = BuildModel();
        string xml = $"<ItemContainer xmlns='{Namespace}'><Thing><ID>one</ID>" +
            "<Huge>123456789012345678901234567890</Huge><TimeValue>24:00:00Z</TimeValue>" +
            "</Thing></ItemContainer>";

        Assert.Empty(CogsInstanceValidator.ValidateXml(model, xml));
        Assert.Contains(CogsInstanceValidator.ValidateXml(model, xml.Replace(
            "123456789012345678901234567890", "-1", StringComparison.Ordinal)),
            error => error.Code == "INS2002" || error.Code == "INS2006");
        Assert.Contains(CogsInstanceValidator.ValidateXml(model, xml.Replace(
            "24:00:00Z", "25:00:00Z", StringComparison.Ordinal)),
            error => error.Code == "INS2002" || error.Code == "INS2006");
    }

    private static CogsModel BuildModel()
    {
        var dto = new Cogs.Dto.CogsDtoModel();
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "CogsVersion", Value = "2.0" });
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "Title", Value = "Instance Test" });
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "ShortTitle", Value = "Instance" });
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "Slug", Value = "instance_test" });
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "Description", Value = string.Empty });
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "Version", Value = "2.0.0" });
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "Author", Value = string.Empty });
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "Copyright", Value = string.Empty });
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespaceUrl", Value = Namespace });
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespacePrefix", Value = "i" });
        dto.Identification.Add(Property("ID", "string", "1", "1"));

        var thing = new Cogs.Dto.ItemType { Name = "Thing" };
        thing.Properties.Add(Property("Amount", "decimal"));
        var observed = Property("Observed", "dateTime");
        observed.MinInclusive = "2020-01-01T00:00:00Z";
        thing.Properties.Add(observed);
        var elapsed = Property("Elapsed", "duration");
        elapsed.MinInclusive = "P1M";
        thing.Properties.Add(elapsed);
        thing.Properties.Add(Property("Huge", "positiveInteger"));
        thing.Properties.Add(Property("TimeValue", "time"));
        dto.ItemTypes.Add(thing);

        CogsBuildResult result = new CogsModelBuilder().BuildResult(dto);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(x => x.ToString())));
        return result.Model!;
    }

    private static Cogs.Dto.Property Property(string name, string type, string minimum = "0", string maximum = "1") => new()
    {
        Name = name,
        DataType = type,
        MinCardinality = minimum,
        MaxCardinality = maximum
    };
}
