using Cogs.Common;
using Cogs.Dto;
using Cogs.Validation;
using System;
using System.Linq;
using Xunit;

namespace Cogs.Tests
{
    public sealed class CogsRdfNamingTests
    {
        [Theory]
        [InlineData("ID", "id")]
        [InlineData("URI", "uri")]
        [InlineData("XMLPrefix", "xmlPrefix")]
        [InlineData("URLValue", "urlValue")]
        [InlineData("DDIMaintenanceAgencyID", "ddiMaintenanceAgencyId")]
        [InlineData("Display-Name", "displayName")]
        [InlineData("Display_Name", "displayName")]
        [InlineData("Version2ID", "version2Id")]
        [InlineData("Class", "class")]
        [InlineData("ÅngströmURL", "ångströmUrl")]
        public void PropertyTermsUseWordAwareCamelCase(string source, string expected)
        {
            Assert.Equal(expected, CogsRdfNaming.ToPropertyLocalName(source));
            Assert.True(CogsRdfNaming.TryToPropertyLocalName(source, out string actual));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void PropertyTermsNormalizeUnicodeToNfc()
        {
            Assert.Equal("éclairName", CogsRdfNaming.ToPropertyLocalName("ÉclairName"));
            Assert.Equal("éclairName", CogsRdfNaming.ToPropertyLocalName("E\u0301clairName"));
            Assert.Equal(
                CogsRdfNaming.ToPropertyLocalName("ÉclairName"),
                CogsRdfNaming.ToPropertyLocalName("E\u0301clairName"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("---")]
        public void PropertyTermsRejectValuesWithoutLettersOrDigits(string source)
        {
            Assert.False(CogsRdfNaming.TryToPropertyLocalName(source, out string result));
            Assert.Equal(string.Empty, result);
            Assert.Throws<ArgumentException>(() => CogsRdfNaming.ToPropertyLocalName(source));
        }

        [Theory]
        [InlineData("https://example.org/model", "https://example.org/model#")]
        [InlineData("https://example.org/model#", "https://example.org/model#")]
        [InlineData("https://example.org/model/", "https://example.org/model/")]
        public void TermBaseUsesOneStableDelimiter(string source, string expected)
        {
            Assert.Equal(expected, CogsRdfNaming.GetTermBase(source));
        }

        [Fact]
        public void FullTermsKeepClassesPascalCaseAndCamelizeProperties()
        {
            const string rdfNamespace = "https://example.org/model";

            Assert.Equal("https://example.org/model#XMLType",
                CogsRdfNaming.ClassIri(rdfNamespace, "XMLType"));
            Assert.Equal("https://example.org/model#xmlValue",
                CogsRdfNaming.PropertyIri(rdfNamespace, "XMLValue"));
        }

        [Fact]
        public void DistinctNamesThatMapToOneRdfTermAreRejectedAcrossTypes()
        {
            CogsDtoModel dto = ValidDto();
            var alpha = new ItemType { Name = "AlphaType" };
            alpha.Properties.Add(Property("URLValue", "string"));
            var zeta = new ItemType { Name = "ZetaType" };
            Property conflicting = Property("UrlValue", "string");
            conflicting.SourcePath = "ItemTypes/ZetaType/ZetaType.csv";
            conflicting.SourceLine = 4;
            zeta.Properties.Add(conflicting);
            dto.ItemTypes.Add(alpha);
            dto.ItemTypes.Add(zeta);

            CogsError diagnostic = Assert.Single(
                DtoValidation.Validate(dto), error => error.Code == "COGS-VAL-PROP-008");

            Assert.Equal(ErrorLevel.Error, diagnostic.Level);
            Assert.Equal("ItemTypes/ZetaType/ZetaType.csv", diagnostic.SourcePath);
            Assert.Equal(4, diagnostic.Line);
            Assert.Equal("ZetaType.UrlValue", diagnostic.ModelPath);
            Assert.Contains("URLValue", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("urlValue", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RdfTermCollisionsIncludeIdentificationAndMixinProperties()
        {
            CogsDtoModel dto = ValidDto();
            dto.IdentificationMixin.Add(Property("URIValue", "string", "1", "1"));
            Property conflicting = Property("UriValue", "string");
            conflicting.SourcePath = "ItemTypes/Thing/Thing.csv";
            conflicting.SourceLine = 5;
            dto.ItemTypes[0].Properties.Add(conflicting);

            CogsError diagnostic = Assert.Single(
                DtoValidation.Validate(dto), error => error.Code == "COGS-VAL-PROP-008");

            Assert.Equal("Thing.UriValue", diagnostic.ModelPath);
            Assert.Contains("IdentificationMixin.URIValue", diagnostic.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(
                DtoValidation.Validate(dto), error => error.Code == "COGS-VAL-PROP-007");
        }

        [Fact]
        public void ExactReusedNamesWithOneDatatypeRemainValidRdfTerms()
        {
            CogsDtoModel dto = ValidDto();
            var alpha = new ItemType { Name = "AlphaType" };
            alpha.Properties.Add(Property("SharedValue", "string"));
            var zeta = new ItemType { Name = "ZetaType" };
            zeta.Properties.Add(Property("SharedValue", "string"));
            dto.ItemTypes.Add(alpha);
            dto.ItemTypes.Add(zeta);

            var diagnostics = DtoValidation.Validate(dto);

            Assert.DoesNotContain(diagnostics, error =>
                error.Code is "COGS-VAL-PROP-007" or "COGS-VAL-PROP-008");
        }

        private static CogsDtoModel ValidDto()
        {
            var dto = new CogsDtoModel();
            foreach ((string key, string value) in new[]
            {
                ("CogsVersion", "2.0"), ("Title", "Test Model"), ("ShortTitle", "Test"),
                ("Slug", "test_model"), ("Description", ""), ("Version", "1.0.0"),
                ("Author", ""), ("Copyright", ""), ("NamespaceUrl", "https://example.org/model"),
                ("NamespacePrefix", "model")
            })
            {
                dto.Settings.Add(new Setting { Key = key, Value = value });
            }

            dto.Identification.Add(Property("ID", "string", "1", "1"));
            dto.ItemTypes.Add(new ItemType { Name = "Thing" });
            return dto;
        }

        private static Property Property(
            string name,
            string dataType,
            string minimum = "0",
            string maximum = "1") => new Property
            {
                Name = name,
                DataType = dataType,
                MinCardinality = minimum,
                MaxCardinality = maximum
            };
    }
}
