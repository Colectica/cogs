using Cogs.Common;
using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using Cogs.Publishers.FluentJson;
using Json.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Cogs.Tests;

public sealed class GregorianJsonContractTests
{
    [Theory]
    [InlineData("dateTime", "-2147483648-01-01T24:00:00", true)]
    [InlineData("dateTime", "2147483647-12-31T23:59:59.5Z", true)]
    [InlineData("dateTime", "-2147483649-01-01T00:00:00Z", false)]
    [InlineData("date", "0000-01-01", false)]
    [InlineData("date", "2147483648-01-01", false)]
    [InlineData("gYearMonth", "-2147483648-02+14:00", true)]
    [InlineData("gYearMonth", "2147483648-02", false)]
    [InlineData("gYear", "0000", false)]
    [InlineData("gYear", "2024+14:01", false)]
    [InlineData("gMonthDay", "--02-29Z", true)]
    [InlineData("gMonthDay", "--02-30Z", false)]
    [InlineData("gDay", "---31-13:59", true)]
    [InlineData("gMonth", "--12--+14:00", true)]
    public void SharedLexicalRulesEnforceInt32YearsAndXsdGregorianValues(
        string dataType,
        string lexical,
        bool expected)
    {
        Assert.Equal(expected, CogsPrimitiveLexical.IsValid(dataType, lexical));
    }

    [Fact]
    public void GregorianCodecPreservesComponentsAndTimezoneLexeme()
    {
        Assert.True(CogsGregorianLexical.TryParse(
            "gYearMonth",
            "-0001-02+00:00",
            out CogsGregorianValue value));
        Assert.Equal(-1, value.Year);
        Assert.Equal(2, value.Month);
        Assert.Equal("+00:00", value.Timezone);
        Assert.True(CogsGregorianLexical.TryFormat("gYearMonth", value, out string lexical));
        Assert.Equal("-0001-02+00:00", lexical);
    }

    [Fact]
    public void SchemaUsesStandardFormatsAndClosedGregorianComponentObjects()
    {
        JsonSchema schema = new FluentJsonSchemaPublisher().BuildSchema(BuildModel());
        using JsonDocument serialized = JsonDocument.Parse(JsonSerializer.Serialize(schema));
        JsonElement definitions = serialized.RootElement.GetProperty("$defs");

        AssertStringFormat(definitions, "duration", "duration");
        AssertStringFormat(definitions, "dateTime", "date-time");
        AssertStringFormat(definitions, "time", "time");
        AssertStringFormat(definitions, "date", "date");
        AssertStringFormat(definitions, "anyURI", "uri");

        JsonElement yearMonth = definitions.GetProperty("gYearMonth");
        Assert.Equal("object", yearMonth.GetProperty("type").GetString());
        Assert.False(yearMonth.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "Year", "Month" },
            yearMonth.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.True(yearMonth.GetProperty("properties").TryGetProperty("Timezone", out _));

        JsonElement enumeration = definitions.GetProperty("Thing").GetProperty("properties")
            .GetProperty("YearMonthValue").GetProperty("enum");
        Assert.Equal(2020, enumeration[0].GetProperty("Year").GetInt32());
        Assert.Equal(1, enumeration[0].GetProperty("Month").GetInt32());
        Assert.Equal("Z", enumeration[0].GetProperty("Timezone").GetString());
    }

    [Theory]
    [InlineData("\"YearValue\":{\"Year\":2020}", true)]
    [InlineData("\"YearValue\":{\"Year\":0}", false)]
    [InlineData("\"YearValue\":{\"Year\":2147483648}", false)]
    [InlineData("\"YearValue\":{\"Year\":2020,\"Unknown\":1}", false)]
    [InlineData("\"MonthDayValue\":{\"Month\":2,\"Day\":29}", true)]
    [InlineData("\"MonthDayValue\":{\"Month\":2,\"Day\":30}", false)]
    [InlineData("\"MonthDayValue\":{\"Month\":4,\"Day\":31}", false)]
    [InlineData("\"DayValue\":{\"Day\":31,\"Timezone\":\"+14:00\"}", true)]
    [InlineData("\"DayValue\":{\"Day\":31,\"Timezone\":\"+14:01\"}", false)]
    [InlineData("\"MonthValue\":{\"Month\":12}", true)]
    [InlineData("\"MonthValue\":{\"Month\":13}", false)]
    [InlineData("\"Calendar\":{\"GYear\":{\"Year\":-1,\"Timezone\":\"+00:00\"}}", true)]
    [InlineData("\"Calendar\":{\"GYear\":\"-0001+00:00\"}", false)]
    public void SchemaValidatesGregorianComponents(string property, bool expected)
    {
        JsonSchema schema = new FluentJsonSchemaPublisher().BuildSchema(BuildModel());
        using JsonDocument instance = JsonDocument.Parse(
            $"{{\"items\":[{{\"$type\":\"Thing\",\"ID\":\"one\",{property}}}]}}");

        EvaluationResults result = schema.Evaluate(instance.RootElement, new EvaluationOptions
        {
            RequireFormatValidation = false
        });

        Assert.Equal(expected, result.IsValid);
    }

    [Fact]
    public void StandardFormatsRemainAnnotationsForTheWiderCogsXsdDomain()
    {
        JsonSchema schema = new FluentJsonSchemaPublisher().BuildSchema(BuildModel());
        using JsonDocument instance = JsonDocument.Parse(
            """
            {
              "items": [{
                "$type": "Thing",
                "ID": "one",
                "DateTimeValue": "-0001-01-01T24:00:00"
              }]
            }
            """);

        Assert.True(schema.Evaluate(instance.RootElement, new EvaluationOptions
        {
            RequireFormatValidation = false
        }).IsValid);
        Assert.False(schema.Evaluate(instance.RootElement, new EvaluationOptions
        {
            RequireFormatValidation = true
        }).IsValid);
    }

    [Fact]
    public void UriFormatRemainsAnAnnotationForRelativeCogsUriReferences()
    {
        CogsModel model = BuildModel();
        JsonSchema schema = new FluentJsonSchemaPublisher().BuildSchema(model);
        const string json =
            """{"items":[{"$type":"Thing","ID":"one","UriValue":"../relative?q=1#part"}]}""";
        using JsonDocument instance = JsonDocument.Parse(json);

        Assert.True(schema.Evaluate(instance.RootElement, new EvaluationOptions
        {
            RequireFormatValidation = false
        }).IsValid);
        Assert.False(schema.Evaluate(instance.RootElement, new EvaluationOptions
        {
            RequireFormatValidation = true
        }).IsValid);
        Assert.Empty(CogsInstanceValidator.ValidateJson(model, json));
    }

    [Fact]
    public void AuthoritativeValidatorRetainsFullXsdLexicalsAndGregorianBounds()
    {
        CogsModel model = BuildModel();
        string valid = """
            {
              "items": [{
                "$type": "Thing",
                "ID": "one",
                "DateTimeValue": "-2147483648-01-01T24:00:00",
                "DateValue": "-2147483648-01-01-06:00",
                "TimeValue": "24:00:00Z",
                "DurationValue": "-P1Y2M3DT0.5S",
                "YearMonthValue": {"Year": 2020, "Month": 1, "Timezone": "Z"},
                "YearValue": {"Year": 2020, "Timezone": "Z"},
                "MonthDayValue": {"Month": 2, "Day": 29},
                "DayValue": {"Day": 31},
                "MonthValue": {"Month": 12},
                "Calendar": {"GYearMonth": {"Year": -1, "Month": 2, "Timezone": "+00:00"}}
              }]
            }
            """;

        Assert.Empty(CogsInstanceValidator.ValidateJson(model, valid));
        Assert.Contains(CogsInstanceValidator.ValidateJson(model, valid.Replace(
            "\"Year\": 2020, \"Timezone\": \"Z\"}",
            "\"Year\": 2019, \"Timezone\": \"Z\"}",
            StringComparison.Ordinal)),
            error => error.Code == "INS1007");
        Assert.Contains(CogsInstanceValidator.ValidateJson(model, valid.Replace(
            "\"-P1Y2M3DT0.5S\"", "\"P1W\"", StringComparison.Ordinal)),
            error => error.Code == "INS1006");
        Assert.Contains(CogsInstanceValidator.ValidateJson(model, valid.Replace(
            "{\"GYearMonth\": {\"Year\": -1, \"Month\": 2, \"Timezone\": \"+00:00\"}}",
            "{\"DateTime\": \"not-a-date\"}", StringComparison.Ordinal)),
            error => error.Code == "INS1006");
    }

    [Fact]
    public void XmlValidationAppliesTheSameInt32YearContractBeforeProcessorWorkarounds()
    {
        CogsModel model = BuildModel();
        const string valid = """
            <ItemContainer xmlns="https://example.org/gregorian">
              <Thing>
                <ID>one</ID>
                <DateTimeValue>-2147483648-01-01T00:00:00Z</DateTimeValue>
                <DateValue>-2147483648-01-01Z</DateValue>
                <YearMonthValue>2020-01Z</YearMonthValue>
                <YearValue>2020Z</YearValue>
                <Calendar>-2147483648</Calendar>
              </Thing>
            </ItemContainer>
            """;

        Assert.Empty(CogsInstanceValidator.ValidateXml(model, valid));
        Assert.Contains(CogsInstanceValidator.ValidateXml(model, valid.Replace(
            "-2147483648-01-01T00:00:00Z",
            "-2147483649-01-01T00:00:00Z",
            StringComparison.Ordinal)),
            error => error.Code is "INS2002" or "INS2006");
        Assert.Contains(CogsInstanceValidator.ValidateXml(model, valid.Replace(
            "<Calendar>-2147483648</Calendar>",
            "<Calendar>not-a-date</Calendar>",
            StringComparison.Ordinal)),
            error => error.Code is "INS2002" or "INS2006");
    }

    private static void AssertStringFormat(JsonElement definitions, string name, string format)
    {
        JsonElement definition = definitions.GetProperty(name);
        Assert.Equal("string", definition.GetProperty("type").GetString());
        Assert.Equal(format, definition.GetProperty("format").GetString());
        Assert.False(definition.TryGetProperty("pattern", out _));
        Assert.Equal(name, definition.GetProperty("x-cogs-datatype").GetString());
    }

    private static CogsModel BuildModel()
    {
        var dto = new CogsDtoModel();
        dto.Settings.AddRange(
        [
            Setting("CogsVersion", "2.0"),
            Setting("Title", "Gregorian JSON"),
            Setting("ShortTitle", "Gregorian"),
            Setting("Slug", "gregorian_json"),
            Setting("Description", string.Empty),
            Setting("Version", "2.0.0"),
            Setting("Author", string.Empty),
            Setting("Copyright", string.Empty),
            Setting("NamespaceUrl", "https://example.org/gregorian"),
            Setting("NamespacePrefix", "g")
        ]);
        dto.Identification.Add(Property("ID", "string", "1", "1"));

        var item = new Cogs.Dto.ItemType { Name = "Thing" };
        item.Properties.Add(Property("DateTimeValue", "dateTime"));
        item.Properties.Add(Property("DateValue", "date"));
        item.Properties.Add(Property("TimeValue", "time"));
        item.Properties.Add(Property("DurationValue", "duration"));
        item.Properties.Add(Property("UriValue", "anyURI"));
        Cogs.Dto.Property yearMonth = Property("YearMonthValue", "gYearMonth");
        yearMonth.Enumeration = "2020-01Z 2021-02+05:30";
        item.Properties.Add(yearMonth);
        Cogs.Dto.Property year = Property("YearValue", "gYear");
        year.MinInclusive = "2020Z";
        item.Properties.Add(year);
        item.Properties.Add(Property("MonthDayValue", "gMonthDay"));
        item.Properties.Add(Property("DayValue", "gDay"));
        item.Properties.Add(Property("MonthValue", "gMonth"));
        item.Properties.Add(Property("Calendar", "cogsDate"));
        dto.ItemTypes.Add(item);

        CogsBuildResult result = new CogsModelBuilder().BuildResult(dto);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return result.Model;
    }

    private static Setting Setting(string key, string value) => new() { Key = key, Value = value };

    private static Cogs.Dto.Property Property(
        string name,
        string dataType,
        string minimum = "0",
        string maximum = "1") => new()
        {
            Name = name,
            DataType = dataType,
            MinCardinality = minimum,
            MaxCardinality = maximum
        };
}
