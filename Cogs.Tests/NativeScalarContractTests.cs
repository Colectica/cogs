#nullable enable
using Cogs.Common;
using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Xunit;
using Property = Cogs.Dto.Property;

namespace Cogs.Tests;

public sealed class NativeScalarContractTests
{
    [Theory]
    [InlineData("language", "en\n", false)]
    [InlineData("gMonthDay", "--02-29\n", false)]
    [InlineData("long", "9007199254740991", true)]
    [InlineData("long", "-9007199254740991", true)]
    [InlineData("long", "9007199254740992", false)]
    [InlineData("unsignedLong", "9007199254740991", true)]
    [InlineData("unsignedLong", "-1", false)]
    [InlineData("positiveInteger", "1.00e2", true)]
    [InlineData("positiveInteger", "0", false)]
    [InlineData("negativeInteger", "-1", true)]
    [InlineData("negativeInteger", "0", false)]
    [InlineData("nonPositiveInteger", "0e999999999999999", true)]
    [InlineData("nonNegativeInteger", "999999999999999999999", false)]
    [InlineData("int", "2147483647.0", true)]
    [InlineData("int", "2147483648", false)]
    [InlineData("int", "1.00000000000000000001", false)]
    [InlineData("decimal", "1e2", true)]
    [InlineData("decimal", "0.10000000000000000000000000000", true)]
    [InlineData("decimal", "0.10000000000000001", false)]
    [InlineData("decimal", "1e-28", true)]
    [InlineData("decimal", "1e-29", false)]
    [InlineData("decimal", "79228162514264337593543950335", false)]
    [InlineData("decimal", "79228162514264330000000000000", true)]
    [InlineData("decimal", "79228162514264340000000000000", false)]
    [InlineData("decimal", "9007199254740992", true)]
    [InlineData("decimal", "9007199254740993", false)]
    [InlineData("float", "1.00000006", true)]
    [InlineData("float", "1e-50", true)]
    [InlineData("float", "3.4028234663852886e38", true)]
    [InlineData("float", "3.4028235677973367e38", false)]
    [InlineData("double", "1e400", false)]
    [InlineData("dateTime", "2020-02-29T24:00:00.0000Z", true)]
    [InlineData("dateTime", "2020-02-29T00:00:00.123000+14:00", true)]
    [InlineData("dateTime", "2020-02-29T00:00:00.1231Z", false)]
    [InlineData("dateTime", "2020-02-29T00:00:00", false)]
    [InlineData("dateTime", "0001-01-01T00:00:00+00:01", false)]
    [InlineData("dateTime", "9999-12-31T23:59:59.999Z", true)]
    [InlineData("dateTime", "9999-12-31T24:00:00Z", false)]
    [InlineData("dateTime", "9999-12-31T24:00:00+14:00", true)]
    [InlineData("dateTime", "0001-01-01T24:00:00+14:00", true)]
    [InlineData("date", "0001-01-01", true)]
    [InlineData("date", "9999-12-31", true)]
    [InlineData("date", "2020-02-30", false)]
    [InlineData("date", "2020-01-01Z", false)]
    [InlineData("date", "10000-01-01", false)]
    [InlineData("time", "24:00:00.0000000", true)]
    [InlineData("time", "12:34:56.123456000", true)]
    [InlineData("time", "12:34:56.1234561", false)]
    [InlineData("time", "12:34:56Z", false)]
    [InlineData("duration", "-PT922337203685.477S", true)]
    [InlineData("duration", "PT922337203685.478S", false)]
    [InlineData("duration", "P1DT24H", true)]
    [InlineData("duration", "PT.5000S", true)]
    [InlineData("duration", "PT1.S", true)]
    [InlineData("duration", "P1Y", false)]
    [InlineData("duration", "P0M", false)]
    [InlineData("duration", "PT0.0001S", false)]
    [InlineData("duration", "P", false)]
    [InlineData("duration", "P1DT", false)]
    public void ApprovedNativeDomainsRejectRoundingAndOutOfRangeValues(string type, string lexical, bool expected)
    {
        Assert.Equal(expected, CogsPrimitiveLexical.IsValid(type, lexical));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("a:b", true)]
    [InlineData("../relative?x=1#part", true)]
    [InlineData("http://[::1]/", true)]
    [InlineData("http://[2001:db8::192.0.2.1]:80/", true)]
    [InlineData("http://[1:2:3:4:5:6:7:8]/", true)]
    [InlineData("http://[v1.a:b]/", true)]
    [InlineData("//user:pass@host:/a", true)]
    [InlineData("http://[not-ip]/", false)]
    [InlineData("http://[1:2:3:4:5:6:7:8:9]/", false)]
    [InlineData("http://[1::2::3]/", false)]
    [InlineData("http://[v.a]/", false)]
    [InlineData("path#x#y", false)]
    [InlineData("%2G", false)]
    [InlineData("a b", false)]
    [InlineData("é", false)]
    public void UriReferencesUseRfc3986Grammar(string value, bool expected)
    {
        Assert.Equal(expected, CogsUriReference.IsValid(value));
    }

    [Theory]
    [InlineData("int", "1.00e2", "+00100")]
    [InlineData("decimal", "1.25e0", "+001.2500")]
    [InlineData("decimal", "0.5", ".5")]
    [InlineData("decimal", "1", "1.")]
    [InlineData("float", "1.00000006", "+01.00000006")]
    [InlineData("dateTime", "\"2019-12-31T19:00:00-05:00\"", "2019-12-31T19:00:00-05:00")]
    [InlineData("time", "\"24:00:00\"", "24:00:00")]
    [InlineData("duration", "\"PT.5S\"", "PT.5S")]
    [InlineData("anyURI", "\"a:b\"", "a:b")]
    [InlineData("anyURI", "\"http://[v1.a:b]/\"", "http://[v1.a:b]/")]
    public void BothAuthoritiesAcceptEquivalentWireLexicals(string type, string jsonValue, string xmlValue)
    {
        CogsModel model = Model(type);
        Assert.Empty(CogsInstanceValidator.ValidateJson(model, Json(jsonValue)));
        Assert.Empty(CogsInstanceValidator.ValidateXml(model, Xml(xmlValue)));
    }

    [Theory]
    [InlineData("anyURI", "\"http://[bad]/\"", "http://[bad]/")]
    [InlineData("double", "1e400", "1e400")]
    [InlineData("float", "1e100", "1e100")]
    [InlineData("positiveInteger", "-1", "-1")]
    [InlineData("long", "9007199254740992", "9007199254740992")]
    [InlineData("decimal", "0.10000000000000001", "0.10000000000000001")]
    [InlineData("duration", "\"P1M\"", "P1M")]
    [InlineData("dateTime", "\"2020-01-01T00:00:00\"", "2020-01-01T00:00:00")]
    public void BothAuthoritiesEnforceEveryPrimitiveDomain(string type, string jsonValue, string xmlValue)
    {
        CogsModel model = Model(type);
        Assert.NotEmpty(CogsInstanceValidator.ValidateJson(model, Json(jsonValue)));
        Assert.NotEmpty(CogsInstanceValidator.ValidateXml(model, Xml(xmlValue)));
    }

    [Theory]
    [InlineData("\"\\u0001\"")]
    [InlineData("\"\\uD800\"")]
    [InlineData("\"\\uDC00\"")]
    [InlineData("\"\\uFFFE\"")]
    public void InvalidTextIsADiagnostic(string jsonValue)
    {
        Assert.NotEmpty(CogsInstanceValidator.ValidateJson(Model("string"), Json(jsonValue)));
    }

    [Fact]
    public void ReferenceIdsAreValidated()
    {
        CogsModel model = Model("string", uriIdentity: true);
        Assert.NotEmpty(CogsInstanceValidator.ValidateJson(model,
            """{"items":[],"topLevelReferences":[{"$type":"Thing","ID":"http://[bad]/"}]}"""));
        Assert.NotEmpty(CogsInstanceValidator.ValidateXml(model,
            """<ItemContainer xmlns="urn:scalar"><TopLevelReference><ID>http://[bad]/</ID><TypeOfObject>Thing</TypeOfObject></TopLevelReference></ItemContainer>"""));
    }

    [Fact]
    public void UnicodeLengthsCountScalars()
    {
        CogsModel model = Model("string", property =>
        {
            property.MinLength = 1;
            property.MaxLength = 1;
        });
        Assert.Equal(1, CogsScalarValues.TextLength("😀"));
        Assert.Empty(CogsInstanceValidator.ValidateJson(model, Json("\"😀\"")));
        Assert.Empty(CogsInstanceValidator.ValidateXml(model, Xml("😀")));
        Assert.NotEmpty(CogsInstanceValidator.ValidateJson(model, Json("\"😀😀\"")));
        Assert.NotEmpty(CogsInstanceValidator.ValidateXml(model, Xml("😀😀")));
    }

    [Theory]
    [InlineData("dateTime", "2020-01-01T00:00:00Z", "\"2019-12-31T19:00:00-05:00\"", "2019-12-31T19:00:00-05:00")]
    [InlineData("duration", "P1D", "\"PT24H\"", "PT24H")]
    [InlineData("float", "1.00000006", "1.00000007", "1.00000007")]
    public void EnumerationUsesValuesInBothFormats(string type, string enumeration, string json, string xml)
    {
        CogsModel model = Model(type, property => property.Enumeration = enumeration);
        Assert.Empty(CogsInstanceValidator.ValidateJson(model, Json(json)));
        Assert.Empty(CogsInstanceValidator.ValidateXml(model, Xml(xml)));
    }

    [Theory]
    [InlineData("x", "\nx\n", true)]
    [InlineData("\\$", "$", true)]
    [InlineData("\\+?", "+", true)]
    [InlineData("a.b", "a\nb", false)]
    [InlineData("a.b", "a\u2028b", false)]
    [InlineData(".{2}", "😀", false)]
    [InlineData("[^a]{2}", "😀", false)]
    [InlineData("😀{2}", "😀😀", true)]
    public void PortablePatternsAgreeWithXsd(string pattern, string value, bool expected)
    {
        Assert.True(CogsConventions.IsPortablePattern(pattern, out string error), error);
        CogsModel model = Model("string", property => property.Pattern = pattern);
        Assert.Equal(expected, !CogsInstanceValidator.ValidateJson(model, Json(JsonSerializer.Serialize(value))).Any());
        Assert.Equal(expected, !CogsInstanceValidator.ValidateXml(model, Xml(value)).Any());
    }

    [Theory]
    [InlineData("[a-z-[aeiou]]")]
    [InlineData("a*?")]
    [InlineData("a{1,2}?")]
    public void NonportablePatternsAreRejected(string pattern)
    {
        Assert.False(CogsConventions.IsPortablePattern(pattern, out _));
    }

    [Fact]
    public void JsonFloatLexemesSurviveNativeNumberParsing()
    {
        Assert.Equal(1f, CogsScalarValues.JsonFloat("1.000000059604644775390625"));
        Assert.Throws<FormatException>(() => CogsScalarValues.JsonFloat("1.0000000596046447753906250000000001"));
        CogsModel model = Model("float");
        Assert.NotEmpty(CogsInstanceValidator.ValidateJson(model, Json("1.0000000596046447753906250000000001")));
        Assert.Empty(CogsInstanceValidator.ValidateXml(model, Xml("1.0000000596046447753906250000000001")));
    }

    [Fact]
    public void NativeWritersRejectExcessTemporalPrecision()
    {
        Assert.Throws<FormatException>(() => CogsScalarValues.DateTimeText(DateTimeOffset.UnixEpoch.AddTicks(1)));
        Assert.Throws<FormatException>(() => CogsScalarValues.TimeText(new TimeOnly(1)));
        Assert.Throws<FormatException>(() => CogsScalarValues.DurationText(TimeSpan.FromTicks(1)));
        Assert.Equal("2020-01-01T00:00:00Z", CogsScalarValues.DateTimeText(
            CogsScalarValues.DateTime("2019-12-31T19:00:00-05:00")));
        Assert.Equal("00:00:00", CogsScalarValues.TimeText(CogsScalarValues.Time("24:00:00")));
    }

    private static string Json(string value)
    {
        return """{"items":[{"$type":"Thing","ID":"one","Value":""" + value + "}]}";
    }

    private static string Xml(string value)
    {
        return "<ItemContainer xmlns=\"urn:scalar\"><Thing><ID>one</ID><Value>" + value + "</Value></Thing></ItemContainer>";
    }

    private static CogsModel Model(string type, Action<Property>? configure = null, bool uriIdentity = false)
    {
        CogsDtoModel dto = new CogsDtoModel();
        foreach ((string key, string value) in new[]
        {
            ("CogsVersion", "2.0"), ("Title", "Scalars"), ("ShortTitle", "Scalars"), ("Slug", "scalars"),
            ("Description", ""), ("Version", "2.0.0"), ("Author", ""), ("Copyright", ""),
            ("NamespaceUrl", "urn:scalar"), ("NamespacePrefix", "s")
        })
        {
            dto.Settings.Add(new Setting { Key = key, Value = value });
        }
        dto.Identification.Add(new Property { Name = "ID", DataType = uriIdentity ? "anyURI" : "string", MinCardinality = "1", MaxCardinality = "1" });
        Property property = new Property { Name = "Value", DataType = type, MinCardinality = "0", MaxCardinality = "1" };
        configure?.Invoke(property);
        Cogs.Dto.ItemType item = new Cogs.Dto.ItemType { Name = "Thing" };
        item.Properties.Add(property);
        dto.ItemTypes.Add(item);
        CogsBuildResult result = new CogsModelBuilder().BuildResult(dto);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return result.Model!;
    }
}
