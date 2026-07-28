using Cogs.Model;
using Cogs.Publishers;
using Cogs.Publishers.Csharp;
using Cogs.SimpleTypes;
using __CogsGeneratedNamespace;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using VDS.RDF;
using Xunit;

namespace Cogs.Tests;

public sealed class CSharpPublisherTests
{
    [Fact]
    public void PublishEmitsNet10StrictSystemTextJsonPackageAndLosslessMappings()
    {
        CogsModel model = BuildModel();
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            new CSharpPublisher(model, target)
            {
                WriteCsproj = true,
                IsNullableEnabled = true,
            }.Publish();

            XDocument project = XDocument.Load(Path.Combine(target, "Test.Generated.csproj"));
            Assert.Equal("net10.0", project.Descendants("TargetFramework").Single().Value);
            Assert.Equal("test_model", project.Descendants("PackageId").Single().Value);
            Assert.Equal("2.3.4-rc.1", project.Descendants("Version").Single().Value);
            Assert.DoesNotContain(project.Descendants("PackageReference"), x =>
                x.Attribute("Include")?.Value.Contains("Newtonsoft", StringComparison.OrdinalIgnoreCase) == true);

            string runtime = File.ReadAllText(Path.Combine(target, "DependantTypes.cs"));
            Assert.Contains("using System.Text.Json;", runtime);
            Assert.Contains("JsonConverter<ItemContainer>", runtime);
            Assert.Contains("Utf8JsonReader", runtime);
            Assert.Contains("Utf8JsonWriter", runtime);
            Assert.Contains("WriteRawValue", runtime);
            Assert.Contains("HashSet<CogsIdentityKey>", runtime);
            Assert.Contains("Duplicate JSON field", runtime);
            Assert.Contains("var definitions = new List<(JsonElement Json, IIdentifiable Item)>", runtime);
            Assert.Contains("return true;", runtime);
            Assert.Contains("LoadJson(Stream stream)", runtime);
            Assert.Contains("DumpJson(Stream stream", runtime);
            Assert.Contains("LoadXml(Stream stream)", runtime);
            Assert.Contains("DumpXml(Stream stream", runtime);
            Assert.Contains("LoadXmlAsync(Stream stream", runtime);
            Assert.Contains("DumpXmlAsync(Stream stream", runtime);
            Assert.Contains("DtdProcessing = DtdProcessing.Prohibit", runtime);
            Assert.Contains("EnsurePrimitiveContent(typeElement, langString: false)", runtime);
            Assert.Contains("AddNamespaceDeclarations(element)", runtime);
            Assert.Contains("element.SetAttributeValue(\"isReference\", \"true\")", runtime);
            Assert.Contains("allowReferenceMarker: true", runtime);
            Assert.Contains("attribute.Name == XName.Get(\"isReference\")", runtime);
            Assert.Contains("attribute.Value is not (\"true\" or \"1\")", runtime);
            Assert.Contains("ReadCanonicalJsonInteger", runtime);
            Assert.Contains("IsUriReference", runtime);
            Assert.Contains("\"gYear\" => ReadGYear(element)", runtime);
            Assert.Contains("EnsureFields(element, \"gYearMonth\", [\"Year\", \"Month\"], [\"Timezone\"])", runtime);
            Assert.Contains("writer.WriteNumber(\"Year\", value.Year)", runtime);
            Assert.Contains("case CogsDateType.GYearMonth: WriteGYearMonth", runtime);
            Assert.Contains("internal static ILiteralNode CreateRdfLiteral", runtime);
            Assert.Contains("CogsDateType.GYearMonth => NamespaceMapper.XMLSCHEMA + \"gYearMonth\"", runtime);
            Assert.Contains("public const string CogsVersion = \"2.0\"", runtime);
            Assert.Contains("https://example.org/test#instance/", runtime);
            Assert.Contains("Uri.EscapeDataString(identifiable.ReferenceId)", runtime);
            Assert.DoesNotContain("Newtonsoft", runtime, StringComparison.OrdinalIgnoreCase);

            string value = File.ReadAllText(Path.Combine(target, "ValueObject.cs"));
            Assert.Contains("public CogsDecimal? ExactDecimal", value);
            Assert.Contains("public BigInteger? ArbitraryInteger", value);
            Assert.Contains("public CogsDuration? FullDuration", value);
            Assert.Contains("public CogsDateTime? Moment", value);
            Assert.Contains("public bool? Enabled", value);
            Assert.Contains("public int? Count", value);
            Assert.Contains("Pattern = \"[A-Z]{2}\\\\.[0-9]{2}\"", value);
            Assert.Contains("MinInclusive = \"-12345678901234567890.123\"", value);

            string item = File.ReadAllText(Path.Combine(target, "BaseItem.cs"));
            Assert.Contains("https://example.org/test#BaseItem", item);
            Assert.Contains("https://example.org/test#displayName", item);
            Assert.Contains("https://example.org/test#id", item);
            Assert.Contains("https://example.org/test#agencyUri", item);
            Assert.Contains("CogsPrimitiveCodec.CreateRdfLiteral(graph, DisplayName, \"string\")", item);
            Assert.DoesNotContain("test:DisplayName", item);

            string container = File.ReadAllText(Path.Combine(target, "ItemContainer.Xml.cs"));
            Assert.Contains("https://example.org/test#", container);
        });
    }

    [Fact]
    public void DirectGeneratedXmlWritersDelegateToTheMarkerAwareCodec()
    {
        CogsModel model = BuildModel(dto =>
        {
            var derived = dto.ItemTypes.Single(x => x.Name == "DerivedItem");
            derived.Properties.Add(DtoProperty("Parent", "DerivedItem"));
            derived.Properties.Add(DtoProperty("Children", "DerivedItem", maximum: "n"));
        });

        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            new CSharpPublisher(model, target).Publish();

            string item = File.ReadAllText(Path.Combine(target, "DerivedItem.cs"));
            Assert.Contains("return CogsXmlCodec.WriteStandalone(this, \"DerivedItem\")", item);
            Assert.DoesNotContain("public bool IsReference", item, StringComparison.Ordinal);
            string runtime = File.ReadAllText(Path.Combine(target, "DependantTypes.cs"));
            Assert.Contains("element.SetAttributeValue(\"isReference\", \"true\")", runtime);
        });
    }

    [Fact]
    public void OptionalValueTypesRemainNullableWhenNullableAnnotationsAreDisabled()
    {
        CogsModel model = BuildModel();
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            new CSharpPublisher(model, target) { IsNullableEnabled = false }.Publish();

            string value = File.ReadAllText(Path.Combine(target, "ValueObject.cs"));
            Assert.Contains("public bool? Enabled", value);
            Assert.Contains("public int? Count", value);
        });
    }

    [Fact]
    public void PublishPreservesExplicitXmlNamespaceAndPrefixOverrides()
    {
        CogsModel model = BuildModel();
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            new CSharpPublisher(model, target)
            {
                TargetNamespace = "https://example.org/overridden",
                TargetNamespacePrefix = "xsi",
            }.Publish();

            string runtime = File.ReadAllText(Path.Combine(target, "DependantTypes.cs"));
            Assert.Contains("public static string XmlNamespace { get; } = \"https://example.org/overridden\"", runtime);
            Assert.Contains("public static string XmlNamespacePrefix { get; } = \"xsi\"", runtime);
            Assert.Contains("? \"cogs\"", runtime);

            string item = File.ReadAllText(Path.Combine(target, "BaseItem.cs"));
            Assert.Contains("https://example.org/overridden#BaseItem", item);
            Assert.Contains("https://example.org/overridden#displayName", item);
            string container = File.ReadAllText(Path.Combine(target, "ItemContainer.Xml.cs"));
            Assert.Contains("https://example.org/overridden#", container);
        });
    }

    [Fact]
    public void PublishRejectsInvalidTargetNamespaceOptionsBeforeCommittingOutput()
    {
        CogsModel model = BuildModel();
        WithTemporaryDirectory(parent =>
        {
            string badPrefixTarget = Path.Combine(parent, "bad-prefix");
            Assert.Throws<InvalidOperationException>(() => new CSharpPublisher(model, badPrefixTarget)
            {
                TargetNamespacePrefix = "bad prefix",
            }.Publish());
            Assert.False(Directory.Exists(badPrefixTarget));
        });

        CogsModel badCSharpNamespace = BuildModel(dto =>
            dto.Settings.Single(setting => setting.Key == "CSharpNamespace").Value = "invalid namespace");
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "bad-csharp-namespace");
            Assert.Throws<InvalidOperationException>(() => new CSharpPublisher(badCSharpNamespace, target).Publish());
            Assert.False(Directory.Exists(target));
        });
    }

    [Fact]
    public void PublishDoesNotMutateModelAndIsByteDeterministic()
    {
        CogsModel model = BuildModel();
        string[] before = model.ItemTypes.Concat(model.ReusableDataTypes)
            .SelectMany(x => x.Properties).Select(x => x.DataTypeName).ToArray();

        WithTemporaryDirectory(parent =>
        {
            string first = Path.Combine(parent, "first");
            string second = Path.Combine(parent, "second");
            new CSharpPublisher(model, first) { WriteCsproj = true, IsNullableEnabled = true }.Publish();
            new CSharpPublisher(model, second) { WriteCsproj = true, IsNullableEnabled = true }.Publish();

            Assert.Equal(before, model.ItemTypes.Concat(model.ReusableDataTypes)
                .SelectMany(x => x.Properties).Select(x => x.DataTypeName));
            string[] files = Directory.GetFiles(first).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray()!;
            Assert.Equal(files, Directory.GetFiles(second).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal));
            foreach (string file in files)
                Assert.Equal(File.ReadAllBytes(Path.Combine(first, file)), File.ReadAllBytes(Path.Combine(second, file)));
        });
    }

    [Fact]
    public void PublishPreservesWireNamesWhileNormalizingCSharpIdentifiersAndCompoundIds()
    {
        CogsModel model = BuildModel(hyphenatedNames: true);
        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            new CSharpPublisher(model, target) { IsNullableEnabled = true }.Publish();

            string item = File.ReadAllText(Path.Combine(target, "BaseItem.cs"));
            Assert.Contains("public abstract partial class BaseItem", item);
            Assert.Contains("[CogsType(\"Base-Item\"", item);
            Assert.Contains("[CogsProperty(\"Display-Name\"", item);
            Assert.Contains("public string? DisplayName", item);
            Assert.Contains("https://example.org/test#displayName", item);

            string identity = File.ReadAllText(Path.Combine(target, "IIdentifiable.Properties.cs"));
            Assert.Contains("string ID", identity);
            Assert.Contains("Uri AgencyURI", identity);
            string runtime = File.ReadAllText(Path.Combine(target, "DependantTypes.cs"));
            Assert.Contains("internal readonly struct CogsIdentityKey", runtime);
            Assert.Contains("Values.SequenceEqual(other.Values, StringComparer.Ordinal)", runtime);
            Assert.DoesNotContain("string.Join(\"|\"", runtime);
        });
    }

    [Fact]
    public void PublishRejectsGeneratedRuntimeAndMemberCollisions()
    {
        CogsModel runtimeCollision = BuildModel(dto => dto.ItemTypes[1].Name = "CogsIdentity");
        WithTemporaryDirectory(parent => Assert.Throws<InvalidOperationException>(() =>
            new CSharpPublisher(runtimeCollision, Path.Combine(parent, "output")).Publish()));

        CogsModel memberCollision = BuildModel(dto =>
            dto.ItemTypes[0].Properties.Add(DtoProperty("ReferenceId", "string")));
        WithTemporaryDirectory(parent => Assert.Throws<InvalidOperationException>(() =>
            new CSharpPublisher(memberCollision, Path.Combine(parent, "output")).Publish()));
    }

    [Fact]
    public void PublishRejectsRdfPropertyNameCollisionsWithoutReplacingExistingOutput()
    {
        CogsModel collision = BuildModel(dto =>
        {
            dto.ItemTypes[0].Properties.Add(DtoProperty("URLValue", "string"));
            dto.ReusableDataTypes[0].Properties.Add(DtoProperty("UrlValue", "string"));
        });

        WithTemporaryDirectory(parent =>
        {
            string target = Path.Combine(parent, "output");
            Directory.CreateDirectory(target);
            string sentinel = Path.Combine(target, "sentinel.txt");
            File.WriteAllText(sentinel, "preserve");

            CogsPublicationException exception = Assert.Throws<CogsPublicationException>(() =>
                new CSharpPublisher(collision, target) { Overwrite = true }.Publish());

            Assert.Contains("CSH1001", exception.Message);
            Assert.Equal("preserve", File.ReadAllText(sentinel));
            Assert.Single(Directory.GetFiles(target));
        });
    }

    [Theory]
    [InlineData("P2DT3H4M5.678S")]
    [InlineData("-P1Y2M3DT4H5M6.0000001S")]
    [InlineData("PT.5S")]
    [InlineData("PT1.S")]
    [InlineData("PT0S")]
    public void DurationRetainsFullXsdLexicalValue(string lexical)
    {
        var duration = new CogsDuration(lexical);
        Assert.Equal(lexical, duration.LexicalValue);
        Assert.Equal(lexical, duration.ToString());
    }

    [Fact]
    public void LosslessHelpersRejectMalformedValuesAndOnlyExposeExactNativeConversions()
    {
        Assert.Throws<FormatException>(() => new CogsDuration("P2DT3H4M5.6"));
        Assert.Throws<FormatException>(() => new CogsDecimal("1e3"));
        Assert.Throws<FormatException>(() => new CogsDateOnly("2023-02-29"));
        Assert.Throws<FormatException>(() => new GMonthDay(2, 30));
        Assert.Throws<FormatException>(() => new LangString("not_a_language", "value"));

        var huge = new CogsDecimal("123456789012345678901234567890.123456789");
        Assert.False(huge.TryGetDecimal(out _));
        var exact = new CogsDecimal("1.2300");
        Assert.True(exact.TryGetDecimal(out decimal native));
        Assert.Equal(1.23m, native);

        Assert.False(new CogsDateTime("2024-02-29T12:00:00").TryGetDateTimeOffset(out _));
        Assert.True(new CogsDateTime("2024-02-29T12:00:00Z").TryGetDateTimeOffset(out _));
        Assert.False(new CogsDateOnly("2024-02-29Z").TryGetDateOnly(out _));
        Assert.Equal(int.MaxValue, new GYear("2147483647").Year);
        Assert.Equal(int.MinValue, new GYear("-2147483648").Year);
        Assert.Equal("-2147483648", new GYear(int.MinValue).LexicalValue);
        Assert.Equal(int.MinValue, new GYearMonth(int.MinValue, 12, "Z").Year);
        Assert.Throws<FormatException>(() => new GYear("2147483648"));
        Assert.Throws<FormatException>(() => new GYear("-2147483649"));
        Assert.Throws<FormatException>(() => new GYear("0000"));
        Assert.Throws<FormatException>(() => new GYearMonth("2147483648-01"));
        Assert.Throws<FormatException>(() => new CogsDateOnly("2147483648-01-01"));
        Assert.Throws<FormatException>(() => new CogsDateTime("-2147483649-01-01T00:00:00Z"));
        Assert.Equal("2147483647-12-31", new CogsDateOnly("2147483647-12-31").LexicalValue);
        Assert.Equal("-2147483648-01-01T00:00:00Z",
            new CogsDateTime("-2147483648-01-01T00:00:00Z").LexicalValue);
    }

    [Fact]
    public void GeneratedRuntimeUsesStructuredGregorianJsonAndLexicalXml()
    {
        GYear year = Read<GYear>("""{"Year":-2147483648,"Timezone":"+05:30"}""", "gYear");
        Assert.Equal(int.MinValue, year.Year);
        Assert.Equal("+05:30", year.Timezone);
        Assert.Equal("-2147483648+05:30", CogsPrimitiveCodec.WriteXml(year, "gYear"));
        AssertJson("""{"Year":-2147483648,"Timezone":"+05:30"}""", Write(year, "gYear"));

        GYearMonth yearMonth = Read<GYearMonth>(
            """{"Year":2147483647,"Month":2,"Timezone":"Z"}""",
            "gYearMonth");
        Assert.Equal("2147483647-02Z", CogsPrimitiveCodec.WriteXml(yearMonth, "gYearMonth"));
        AssertJson("""{"Year":2147483647,"Month":2,"Timezone":"Z"}""",
            Write(yearMonth, "gYearMonth"));

        GMonthDay monthDay = Read<GMonthDay>(
            """{"Month":2,"Day":29,"Timezone":"-14:00"}""",
            "gMonthDay");
        Assert.Equal("--02-29-14:00", CogsPrimitiveCodec.WriteXml(monthDay, "gMonthDay"));
        AssertJson("""{"Month":2,"Day":29,"Timezone":"-14:00"}""",
            Write(monthDay, "gMonthDay"));

        GDay day = Read<GDay>("""{"Day":31}""", "gDay");
        Assert.Equal("---31", CogsPrimitiveCodec.WriteXml(day, "gDay"));
        AssertJson("""{"Day":31}""", Write(day, "gDay"));

        GMonth month = Read<GMonth>("""{"Month":12,"Timezone":"+00:00"}""", "gMonth");
        Assert.Equal("--12--+00:00", CogsPrimitiveCodec.WriteXml(month, "gMonth"));
        AssertJson("""{"Month":12,"Timezone":"+00:00"}""", Write(month, "gMonth"));

        CogsDate cogsDate = Read<CogsDate>(
            """{"GYearMonth":{"Year":2147483647,"Month":2,"Timezone":"Z"}}""",
            "cogsDate");
        Assert.Equal(CogsDateType.GYearMonth, cogsDate.UsedType);
        Assert.Equal("2147483647-02Z", CogsPrimitiveCodec.WriteXml(cogsDate, "cogsDate"));
        AssertJson(
            """{"GYearMonth":{"Year":2147483647,"Month":2,"Timezone":"Z"}}""",
            Write(cogsDate, "cogsDate"));

        Assert.Throws<JsonException>(() => Read<GYear>("""{"Year":1,"Unknown":2}""", "gYear"));
        Assert.Throws<JsonException>(() => Read<GYear>("""{"Year":1,"Year":2}""", "gYear"));
        Assert.Throws<JsonException>(() => Read<GYear>("""{"Year":0}""", "gYear"));
        Assert.Throws<JsonException>(() => Read<GYear>("""{"Year":2147483648}""", "gYear"));
        Assert.Throws<JsonException>(() => Read<GMonthDay>("""{"Month":2,"Day":30}""", "gMonthDay"));
        Assert.Throws<JsonException>(() => Read<GMonth>("""{"Month":1,"Timezone":"15:00"}""", "gMonth"));

        static T Read<T>(string json, string dataType)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return (T)CogsPrimitiveCodec.ReadJson(document.RootElement, dataType, typeof(T));
        }

        static string Write(object value, string dataType)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                CogsPrimitiveCodec.WriteJson(writer, value, dataType);
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        static void AssertJson(string expected, string actual)
        {
            using JsonDocument expectedDocument = JsonDocument.Parse(expected);
            using JsonDocument actualDocument = JsonDocument.Parse(actual);
            Assert.True(JsonElement.DeepEquals(expectedDocument.RootElement, actualDocument.RootElement),
                $"Expected JSON {expected}, actual JSON {actual}.");
        }
    }

    [Fact]
    public void GeneratedRuntimeCreatesInvariantTypedRdfLiterals()
    {
        var graph = new Graph();

        ILiteralNode year = CogsPrimitiveCodec.CreateRdfLiteral(
            graph,
            new GYear(-2147483648, "+05:30"),
            "gYear");
        Assert.Equal("-2147483648+05:30", year.Value);
        Assert.Equal(NamespaceMapper.XMLSCHEMA + "gYear", year.DataType?.AbsoluteUri);

        ILiteralNode date = CogsPrimitiveCodec.CreateRdfLiteral(
            graph,
            new CogsDate(new GYearMonth(2147483647, 2, "Z")),
            "cogsDate");
        Assert.Equal("2147483647-02Z", date.Value);
        Assert.Equal(NamespaceMapper.XMLSCHEMA + "gYearMonth", date.DataType?.AbsoluteUri);

        ILiteralNode integer = CogsPrimitiveCodec.CreateRdfLiteral(graph, 1234, "int");
        Assert.Equal("1234", integer.Value);
        Assert.Equal(NamespaceMapper.XMLSCHEMA + "int", integer.DataType?.AbsoluteUri);

        ILiteralNode text = CogsPrimitiveCodec.CreateRdfLiteral(graph, "value", "string");
        Assert.Equal(NamespaceMapper.XMLSCHEMA + "string", text.DataType?.AbsoluteUri);

        ILiteralNode language = CogsPrimitiveCodec.CreateRdfLiteral(
            graph,
            new LangString("en-US", "value"),
            "langString");
        Assert.Equal("en-us", language.Language);
        Assert.Equal("value", language.Value);
    }

    private static CogsModel BuildModel(
        Action<Cogs.Dto.CogsDtoModel> customize = null,
        bool hyphenatedNames = false)
    {
        var dto = new Cogs.Dto.CogsDtoModel();
        AddSetting(dto, "CogsVersion", "2.0");
        AddSetting(dto, "Title", "Test Model");
        AddSetting(dto, "ShortTitle", "Test");
        AddSetting(dto, "Slug", "test_model");
        AddSetting(dto, "Description", "C# publisher test model");
        AddSetting(dto, "Version", "2.3.4-rc.1");
        AddSetting(dto, "Author", "COGS tests");
        AddSetting(dto, "Copyright", "");
        AddSetting(dto, "NamespaceUrl", "https://example.org/test");
        AddSetting(dto, "NamespacePrefix", "test");
        AddSetting(dto, "CSharpNamespace", "Test.Generated");

        dto.Identification.Add(DtoProperty("ID", "string", "1", "1"));
        dto.IdentificationMixin.Add(DtoProperty("AgencyURI", "anyURI", "1", "1"));

        var baseItem = new Cogs.Dto.ItemType
        {
            Name = hyphenatedNames ? "Base-Item" : "BaseItem",
            Description = "Abstract base",
            IsAbstract = true,
        };
        baseItem.Properties.Add(DtoProperty(hyphenatedNames ? "Display-Name" : "DisplayName", "string"));
        dto.ItemTypes.Add(baseItem);
        dto.ItemTypes.Add(new Cogs.Dto.ItemType
        {
            Name = "DerivedItem",
            Description = "Concrete item",
            Extends = baseItem.Name,
        });

        var value = new Cogs.Dto.DataType { Name = "ValueObject", Description = "Lossless primitive values" };
        value.Properties.Add(DtoProperty("ExactDecimal", "decimal", minInclusive: "-12345678901234567890.123"));
        value.Properties.Add(DtoProperty("ArbitraryInteger", "positiveInteger"));
        value.Properties.Add(DtoProperty("FullDuration", "duration"));
        value.Properties.Add(DtoProperty("Moment", "dateTime"));
        value.Properties.Add(DtoProperty("Enabled", "boolean"));
        value.Properties.Add(DtoProperty("Count", "int"));
        value.Properties.Add(DtoProperty("Code", "string", pattern: @"[A-Z]{2}\.[0-9]{2}"));
        dto.ReusableDataTypes.Add(value);
        customize?.Invoke(dto);
        CogsBuildResult result = new CogsModelBuilder().BuildResult(dto);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return Assert.IsType<CogsModel>(result.Model);
    }

    private static Cogs.Dto.Property DtoProperty(
        string name,
        string dataType,
        string minimum = "0",
        string maximum = "1",
        string pattern = "",
        string minInclusive = "") => new()
        {
            Name = name,
            DataType = dataType,
            MinCardinality = minimum,
            MaxCardinality = maximum,
            Pattern = pattern,
            MinInclusive = minInclusive,
        };

    private static void AddSetting(Cogs.Dto.CogsDtoModel dto, string key, string value) =>
        dto.Settings.Add(new Cogs.Dto.Setting { Key = key, Value = value });

    private static void WithTemporaryDirectory(Action<string> action)
    {
        string path = Path.Combine(Path.GetTempPath(), "cogs-csharp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try { action(path); }
        finally { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
    }
}
