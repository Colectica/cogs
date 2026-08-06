using Cogs.Model;
using Cogs.Publishers;
using Cogs.Publishers.FluentJson;
using Json.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using System.Xml.Schema;
using Xunit;

namespace Cogs.Tests
{
    public sealed class SchemaAlignmentTests
    {
        private const string Namespace = "https://example.org/schema-test";

        [Fact]
        public void JsonSchemaUsesAllOfInheritanceAndBoundaryClosure()
        {
            using var output = new TemporaryDirectory();
            PublishJson(BuildModel(), output.Path);
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(output.Path, "jsonSchema.json")));
            var root = document.RootElement;
            var definitions = root.GetProperty("$defs");
            var baseItem = definitions.GetProperty("BaseItem");
            var derived = definitions.GetProperty("DerivedItem");
            var derivedComposition = derived.GetProperty("allOf");
            var derivedProperties = derivedComposition[1].GetProperty("properties");

            Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("$schema").GetString());
            Assert.False(root.GetProperty("additionalProperties").GetBoolean());
            Assert.False(derived.TryGetProperty("additionalProperties", out _));
            Assert.False(derived.TryGetProperty("unevaluatedProperties", out _));
            Assert.False(baseItem.TryGetProperty("additionalProperties", out _));
            Assert.False(baseItem.TryGetProperty("unevaluatedProperties", out _));
            Assert.Equal("#/$defs/BaseItem", derivedComposition[0].GetProperty("$ref").GetString());
            Assert.False(derivedProperties.TryGetProperty("ID", out _));
            Assert.False(derivedProperties.TryGetProperty("BaseValue", out _));
            Assert.True(baseItem.GetProperty("properties").TryGetProperty("ID", out _));
            Assert.True(baseItem.GetProperty("properties").TryGetProperty("BaseValue", out _));
            Assert.Equal(
                new[] { "DerivedItem", "OtherItem" },
                baseItem.GetProperty("properties").GetProperty("$type").GetProperty("enum")
                    .EnumerateArray().Select(x => x.GetString()).ToArray());
            Assert.Equal(
                "#/$defs/DerivedItem",
                definitions.GetProperty("OtherItem").GetProperty("allOf")[0].GetProperty("$ref").GetString());

            var itemSchema = root.GetProperty("properties").GetProperty("items").GetProperty("items");
            Assert.False(itemSchema.GetProperty("unevaluatedProperties").GetBoolean());
            Assert.Equal(
                new[] { "#/$defs/DerivedItem", "#/$defs/OtherItem" },
                itemSchema.GetProperty("oneOf").EnumerateArray()
                    .Select(x => x.GetProperty("allOf")[0].GetProperty("$ref").GetString())
                    .ToArray());
            Assert.Equal(
                new[] { "DerivedItem", "OtherItem" },
                itemSchema.GetProperty("oneOf").EnumerateArray()
                    .Select(x => x.GetProperty("allOf")[1].GetProperty("properties")
                        .GetProperty("$type").GetProperty("enum")[0].GetString())
                    .ToArray());

            var exact = derivedProperties.GetProperty("ExactValue");
            Assert.Equal("#/$defs/ValueBase", exact.GetProperty("allOf")[0].GetProperty("$ref").GetString());
            Assert.False(exact.GetProperty("unevaluatedProperties").GetBoolean());
            var flexibleSchema = derivedProperties.GetProperty("FlexibleValue");
            var flexible = flexibleSchema.GetProperty("oneOf");
            Assert.False(flexibleSchema.GetProperty("unevaluatedProperties").GetBoolean());
            Assert.Equal(
                new[] { "#/$defs/ValueBase", "#/$defs/ValueChild" },
                flexible.EnumerateArray()
                    .Select(x => x.GetProperty("allOf")[0].GetProperty("$ref").GetString())
                    .ToArray());
            Assert.Equal(
                new[] { "ValueBase", "ValueChild" },
                flexible.EnumerateArray()
                    .Select(x => x.GetProperty("allOf")[1].GetProperty("properties")
                        .GetProperty("$type").GetProperty("enum")[0].GetString())
                    .ToArray());
            Assert.False(definitions.GetProperty("ValueBase").GetProperty("properties").TryGetProperty("$type", out _));
            Assert.Equal("#/$defs/ValueBase", definitions.GetProperty("ValueChild")
                .GetProperty("allOf")[0].GetProperty("$ref").GetString());

            var referenceTypes = definitions.GetProperty("Reference").GetProperty("properties").GetProperty("$type").GetProperty("enum");
            Assert.Equal(new[] { "DerivedItem", "OtherItem" }, referenceTypes.EnumerateArray().Select(x => x.GetString()).ToArray());
            var exactReference = derivedProperties.GetProperty("ExactRelated").GetProperty("allOf");
            Assert.Equal("#/$defs/Reference",
                exactReference[0].GetProperty("$ref").GetString());
            Assert.Equal(new[] { "DerivedItem" }, exactReference[1].GetProperty("properties")
                .GetProperty("$type").GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToArray());
            Assert.Equal("#/$defs/Reference",
                derivedProperties.GetProperty("FlexibleRelated").GetProperty("$ref").GetString());
            Assert.Equal("#/$defs/Reference",
                derivedProperties.GetProperty("Related").GetProperty("$ref").GetString());
            Assert.DoesNotContain(definitions.EnumerateObject(), definition =>
                definition.Name.EndsWith("__Tagged", StringComparison.Ordinal) ||
                definition.Name.EndsWith("__Reference", StringComparison.Ordinal) ||
                definition.Name.EndsWith("__AssignableReference", StringComparison.Ordinal));
        }

        [Fact]
        public void JsonSchemaPrimitiveAndFacetContractsAreLossless()
        {
            using var output = new TemporaryDirectory();
            PublishJson(BuildModel(), output.Path);
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(output.Path, "jsonSchema.json")));
            var definitions = document.RootElement.GetProperty("$defs");

            Assert.Equal("string", definitions.GetProperty("duration").GetProperty("type").GetString());
            Assert.Equal("duration", definitions.GetProperty("duration").GetProperty("format").GetString());
            Assert.False(definitions.GetProperty("duration").TryGetProperty("pattern", out _));
            Assert.Equal("date-time", definitions.GetProperty("dateTime").GetProperty("format").GetString());
            Assert.Equal("time", definitions.GetProperty("time").GetProperty("format").GetString());
            Assert.Equal("date", definitions.GetProperty("date").GetProperty("format").GetString());
            Assert.Equal("string", definitions.GetProperty("anyURI").GetProperty("type").GetString());
            Assert.Equal("uri", definitions.GetProperty("anyURI").GetProperty("format").GetString());
            Assert.False(definitions.GetProperty("anyURI").TryGetProperty("pattern", out _));
            Assert.Equal("object", definitions.GetProperty("gYearMonth").GetProperty("type").GetString());
            Assert.Equal("number", definitions.GetProperty("decimal").GetProperty("type").GetString());
            Assert.Equal("18446744073709551615", definitions.GetProperty("unsignedLong").GetProperty("maximum").GetRawText());
            Assert.Equal(5, definitions.GetProperty("cogsDate").GetProperty("oneOf").GetArrayLength());

            var temporal = definitions.GetProperty("DerivedItem").GetProperty("allOf")[1]
                .GetProperty("properties").GetProperty("ObservedAt");
            var extension = temporal.GetProperty("x-cogs-minInclusive");
            Assert.Equal("dateTime", extension.GetProperty("datatype").GetString());
            Assert.Equal("2020-01-01T00:00:00Z", extension.GetProperty("value").GetString());
            Assert.True(File.Exists(Path.Combine(output.Path, "cogs-meta-schema.json")));
        }

        [Fact]
        public void JsonSchemaCanBeBuiltInMemoryForInstanceValidation()
        {
            var schema = new FluentJsonSchemaPublisher().BuildSchema(BuildModel());
            using var document = JsonDocument.Parse("{\"items\":[{\"$type\":\"DerivedItem\",\"ID\":\"1\",\"BaseValue\":\"x\"}]}");

            var result = schema.Evaluate(document.RootElement);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void JsonSchemaHandlesAbstractCompositeWithoutConcreteDescendants()
        {
            var dto = new Cogs.Dto.CogsDtoModel();
            dto.Settings.Add(new Cogs.Dto.Setting { Key = "Title", Value = "Uninhabited Composite Test" });
            dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespaceUrl", Value = Namespace });
            dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespacePrefix", Value = "m" });
            dto.Identification.Add(DtoProperty("ID", "string", "1", "1"));
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "AbstractValue", IsAbstract = true });
            var item = new Cogs.Dto.ItemType { Name = "Thing" };
            var unavailable = DtoProperty("Unavailable", "AbstractValue");
            unavailable.Description = "No concrete subtype is available.";
            item.Properties.Add(unavailable);
            dto.ItemTypes.Add(item);

            CogsBuildResult built = new CogsModelBuilder().BuildResult(dto);
            Assert.True(built.Success, string.Join(Environment.NewLine, built.Diagnostics));

            JsonSchema schema = new FluentJsonSchemaPublisher().BuildSchema(built.Model);
            using var omitted = JsonDocument.Parse("{\"items\":[{\"$type\":\"Thing\",\"ID\":\"1\"}]}");
            using var supplied = JsonDocument.Parse("{\"items\":[{\"$type\":\"Thing\",\"ID\":\"1\",\"Unavailable\":{}}]}");

            Assert.True(schema.Evaluate(omitted.RootElement).IsValid);
            Assert.False(schema.Evaluate(supplied.RootElement).IsValid);
            using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(schema));
            JsonElement propertySchema = serialized.RootElement.GetProperty("$defs")
                .GetProperty("Thing").GetProperty("properties").GetProperty("Unavailable");
            Assert.Equal("No concrete subtype is available.", propertySchema.GetProperty("description").GetString());
            Assert.Equal(JsonValueKind.False, propertySchema.GetProperty("allOf")[0].ValueKind);
        }

        [Theory]
        [InlineData("{\"items\":[{\"$type\":\"DerivedItem\",\"ID\":\"1\",\"BaseValue\":\"x\"}]}", true)]
        [InlineData("{\"items\":[{\"$type\":\"DerivedItem\",\"ID\":\"1\",\"BaseValue\":\"x\",\"Unknown\":1}]}", false)]
        [InlineData("{\"items\":[{\"$type\":\"OtherItem\",\"ID\":\"1\",\"BaseValue\":\"x\"}]}", true)]
        [InlineData("{\"items\":[{\"$type\":\"OtherItem\",\"ID\":\"1\",\"BaseValue\":\"x\",\"Unknown\":1}]}", false)]
        [InlineData("{\"items\":[{\"$type\":\"BaseItem\",\"ID\":\"1\",\"BaseValue\":\"x\"}]}", false)]
        [InlineData("{\"items\":[{\"$type\":\"DerivedItem\",\"ID\":\"1\",\"BaseValue\":\"x\",\"ExactRelated\":{\"$type\":\"OtherItem\",\"ID\":\"2\"}}]}", false)]
        [InlineData("{\"items\":[{\"$type\":\"DerivedItem\",\"ID\":\"1\",\"BaseValue\":\"x\",\"ExactRelated\":{\"$type\":\"DerivedItem\",\"ID\":\"2\"}}]}", true)]
        [InlineData("{\"items\":[{\"$type\":\"DerivedItem\",\"ID\":\"1\",\"BaseValue\":\"x\",\"ExactRelated\":{\"$type\":\"DerivedItem\",\"ID\":\"2\",\"Unknown\":1}}]}", false)]
        [InlineData("{\"items\":[{\"$type\":\"DerivedItem\",\"ID\":\"1\",\"BaseValue\":\"x\",\"FlexibleRelated\":{\"$type\":\"OtherItem\",\"ID\":\"2\"}}]}", true)]
        [InlineData("{\"items\":[{\"$type\":\"DerivedItem\",\"ID\":\"1\",\"BaseValue\":\"x\",\"ExactValue\":{}}]}", true)]
        [InlineData("{\"items\":[{\"$type\":\"DerivedItem\",\"ID\":\"1\",\"BaseValue\":\"x\",\"ExactValue\":{\"Unknown\":1}}]}", false)]
        [InlineData("{\"items\":[{\"$type\":\"DerivedItem\",\"ID\":\"1\",\"BaseValue\":\"x\",\"FlexibleValue\":{\"$type\":\"ValueChild\"}}]}", true)]
        [InlineData("{\"items\":[{\"$type\":\"DerivedItem\",\"ID\":\"1\",\"BaseValue\":\"x\",\"FlexibleValue\":{\"$type\":\"ValueChild\",\"Unknown\":1}}]}", false)]
        [InlineData("{\"items\":[{\"$type\":\"DerivedItem\",\"ID\":\"1\",\"BaseValue\":\"x\",\"Calendar\":{}}]}", false)]
        [InlineData("{\"items\":[{\"$type\":\"DerivedItem\",\"ID\":\"1\",\"BaseValue\":\"x\",\"Calendar\":{\"Date\":\"2020-01-01\",\"GYear\":\"2020\"}}]}", false)]
        public void JsonSchemaValidatesClosedItemsAndExactOneCogsDate(string instance, bool expected)
        {
            using var output = new TemporaryDirectory();
            PublishJson(BuildModel(), output.Path);
            var schema = JsonSchema.FromFile(Path.Combine(output.Path, "jsonSchema.json"));
            using var document = JsonDocument.Parse(instance);

            var result = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });

            Assert.Equal(expected, result.IsValid);
        }

        [Fact]
        public void XsdUsesLocalOrderedElementsAndConcreteContainerChoices()
        {
            using var output = new TemporaryDirectory();
            PublishXsd(BuildModel(), output.Path);
            var xml = new XmlDocument();
            xml.Load(Path.Combine(output.Path, "schema.xsd"));
            var manager = new XmlNamespaceManager(xml.NameTable);
            manager.AddNamespace("xs", XmlSchema.Namespace);

            Assert.NotNull(xml.SelectSingleNode(
                "/xs:schema/xs:annotation/xs:documentation[contains(., 'nonzero signed 32-bit integer')]",
                manager));
            Assert.Null(xml.SelectSingleNode("/xs:schema/xs:element[@name='BaseValue']", manager));
            Assert.NotNull(xml.SelectSingleNode("/xs:schema/xs:complexType[@name='BaseItem']/xs:sequence/xs:element[@name='BaseValue']", manager));
            var extension = xml.SelectSingleNode("/xs:schema/xs:complexType[@name='DerivedItem']/xs:complexContent/xs:extension", manager) as XmlElement;
            Assert.NotNull(extension);
            Assert.EndsWith(":BaseItem", extension.GetAttribute("base"), StringComparison.Ordinal);
            Assert.Null(xml.SelectSingleNode("/xs:schema/xs:complexType[@name='ItemContainerType']//xs:element[@name='BaseItem']", manager));
            Assert.NotNull(xml.SelectSingleNode("/xs:schema/xs:complexType[@name='ItemContainerType']//xs:element[@name='DerivedItem']", manager));

            var exact = xml.SelectSingleNode("/xs:schema/xs:complexType[@name='DerivedItem']//xs:element[@name='ExactValue']", manager) as XmlElement;
            var flexible = xml.SelectSingleNode("/xs:schema/xs:complexType[@name='DerivedItem']//xs:element[@name='FlexibleValue']", manager) as XmlElement;
            Assert.Contains("extension", exact.GetAttribute("block"));
            Assert.Equal(string.Empty, flexible.GetAttribute("block"));
        }

        [Fact]
        public void XsdUsesOneOrderedIdentificationGroupForEveryReferenceType()
        {
            using var output = new TemporaryDirectory();
            PublishXsd(BuildMultipleIdentificationModel(), output.Path);
            var xml = new XmlDocument();
            xml.Load(Path.Combine(output.Path, "schema.xsd"));
            var manager = new XmlNamespaceManager(xml.NameTable);
            manager.AddNamespace("xs", XmlSchema.Namespace);

            var identification = xml.SelectNodes(
                "/xs:schema/xs:group[@name='IdentificationGroup']/xs:sequence/xs:element",
                manager)!.Cast<XmlElement>().ToArray();
            Assert.Equal(new[] { "ID", "AgencyURI" }, identification.Select(x => x.GetAttribute("name")).ToArray());
            Assert.All(identification, x =>
            {
                Assert.Equal("1", x.GetAttribute("minOccurs"));
                Assert.Equal("1", x.GetAttribute("maxOccurs"));
            });
            Assert.Contains(
                "Base identifier.",
                identification[0].SelectSingleNode("xs:annotation/xs:documentation", manager)!.InnerText,
                StringComparison.Ordinal);
            Assert.NotNull(identification[0].SelectSingleNode(
                "xs:simpleType/xs:restriction/xs:pattern[contains(@value, '[A-Z]+')]", manager));
            Assert.Contains(
                "Agency identifier URI.",
                identification[1].SelectSingleNode("xs:annotation/xs:documentation", manager)!.InnerText,
                StringComparison.Ordinal);

            var referenceTypes = xml.SelectNodes(
                "/xs:schema/xs:complexType[@name='ReferenceType'] | " +
                "/xs:schema//xs:element[@name='Parent']/xs:complexType",
                manager)!.Cast<XmlElement>().ToArray();
            Assert.Equal(2, referenceTypes.Length);
            Assert.All(referenceTypes, referenceType =>
            {
                var group = Assert.IsType<XmlElement>(
                    referenceType.SelectSingleNode("xs:sequence/xs:group", manager));
                Assert.EndsWith(":IdentificationGroup", group.GetAttribute("ref"), StringComparison.Ordinal);
                Assert.NotNull(referenceType.SelectSingleNode(
                    "xs:sequence/xs:element[@name='TypeOfObject']", manager));
                var marker = Assert.IsType<XmlElement>(
                    referenceType.SelectSingleNode("xs:attribute[@name='isReference']", manager));
                Assert.EndsWith(":boolean", marker.GetAttribute("type"), StringComparison.Ordinal);
                Assert.Equal("true", marker.GetAttribute("fixed"));
                Assert.Equal(string.Empty, marker.GetAttribute("use"));
                Assert.Empty(referenceType.SelectNodes(
                    "xs:sequence/xs:element[@name='ID' or @name='AgencyURI']", manager)!);
            });

            Assert.Null(xml.SelectSingleNode(
                "/xs:schema/xs:complexType[@name='Thing']/xs:attribute[@name='isReference']", manager));
            Assert.Null(xml.SelectSingleNode(
                "/xs:schema/xs:complexType[@name='Thing']/xs:sequence/xs:group[@ref]", manager));
        }

        [Fact]
        public void XsdValidatesAssignabilityOrderAndSubtypeBlocking()
        {
            using var output = new TemporaryDirectory();
            PublishXsd(BuildModel(), output.Path);
            var schemas = LoadSchemaSet(Path.Combine(output.Path, "schema.xsd"));

            Assert.Empty(ValidateXml(schemas, $"<ItemContainer xmlns='{Namespace}'><TopLevelReference><ID>1</ID><TypeOfObject>DerivedItem</TypeOfObject></TopLevelReference><DerivedItem><ID>1</ID><BaseValue>x</BaseValue></DerivedItem></ItemContainer>"));
            Assert.NotEmpty(ValidateXml(schemas, $"<ItemContainer xmlns='{Namespace}'><TopLevelReference><ID>1</ID><TypeOfObject>BaseItem</TypeOfObject></TopLevelReference></ItemContainer>"));
            Assert.NotEmpty(ValidateXml(schemas, $"<ItemContainer xmlns='{Namespace}'><BaseItem><ID>1</ID><BaseValue>x</BaseValue></BaseItem></ItemContainer>"));
            Assert.NotEmpty(ValidateXml(schemas, $"<ItemContainer xmlns='{Namespace}' xmlns:m='{Namespace}' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'><DerivedItem><ID>1</ID><BaseValue>x</BaseValue><ExactValue xsi:type='m:ValueChild'/></DerivedItem></ItemContainer>"));
            Assert.Empty(ValidateXml(schemas, $"<ItemContainer xmlns='{Namespace}' xmlns:m='{Namespace}' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'><DerivedItem><ID>1</ID><BaseValue>x</BaseValue><FlexibleValue xsi:type='m:ValueChild'/></DerivedItem></ItemContainer>"));
            Assert.NotEmpty(ValidateXml(schemas, $"<ItemContainer xmlns='{Namespace}'><DerivedItem><ID>1</ID><BaseValue>x</BaseValue><ExactRelated><ID>2</ID><TypeOfObject>OtherItem</TypeOfObject></ExactRelated></DerivedItem></ItemContainer>"));
            Assert.Empty(ValidateXml(schemas, $"<ItemContainer xmlns='{Namespace}'><DerivedItem><ID>1</ID><BaseValue>x</BaseValue><ExactRelated><ID>2</ID><TypeOfObject>DerivedItem</TypeOfObject></ExactRelated></DerivedItem></ItemContainer>"));
            Assert.Empty(ValidateXml(schemas, $"<ItemContainer xmlns='{Namespace}'><DerivedItem><ID>1</ID><BaseValue>x</BaseValue><FlexibleRelated><ID>2</ID><TypeOfObject>OtherItem</TypeOfObject></FlexibleRelated></DerivedItem></ItemContainer>"));
        }

        [Theory]
        [InlineData("", true)]
        [InlineData(" isReference='true'", true)]
        [InlineData(" isReference='1'", true)]
        [InlineData(" isReference='false'", false)]
        [InlineData(" isReference='0'", false)]
        [InlineData(" m:isReference='true'", false)]
        [InlineData(" unexpected='true'", false)]
        public void XsdReferenceMarkerIsOptionalUnqualifiedAndFixedTrue(string attribute, bool expected)
        {
            using var output = new TemporaryDirectory();
            PublishXsd(BuildModel(), output.Path);
            var schemas = LoadSchemaSet(Path.Combine(output.Path, "schema.xsd"));
            var xml =
                $"<ItemContainer xmlns='{Namespace}' xmlns:m='{Namespace}'>" +
                $"<TopLevelReference{attribute}><ID>1</ID><TypeOfObject>DerivedItem</TypeOfObject></TopLevelReference>" +
                "</ItemContainer>";

            Assert.Equal(expected, ValidateXml(schemas, xml).Count == 0);
        }

        [Fact]
        public void XsdRejectsReferenceMarkerOnDefinitionsAndIncorrectIdentificationOrder()
        {
            using var output = new TemporaryDirectory();
            PublishXsd(BuildMultipleIdentificationModel(), output.Path);
            var schemas = LoadSchemaSet(Path.Combine(output.Path, "schema.xsd"));

            Assert.NotEmpty(ValidateXml(
                schemas,
                $"<ItemContainer xmlns='{Namespace}'><Thing isReference='true'><ID>A</ID><AgencyURI>urn:agency</AgencyURI></Thing></ItemContainer>"));
            Assert.NotEmpty(ValidateXml(
                schemas,
                $"<ItemContainer xmlns='{Namespace}'><TopLevelReference isReference='true'><AgencyURI>urn:agency</AgencyURI><ID>A</ID><TypeOfObject>Thing</TypeOfObject></TopLevelReference></ItemContainer>"));
            Assert.Empty(ValidateXml(
                schemas,
                $"<ItemContainer xmlns='{Namespace}'><TopLevelReference isReference='true'><ID>A</ID><AgencyURI>urn:agency</AgencyURI><TypeOfObject>Thing</TypeOfObject></TopLevelReference></ItemContainer>"));
        }

        [Fact]
        public void XsdPublisherCanBeReusedWithoutStaleDeclarations()
        {
            using var first = new TemporaryDirectory();
            using var second = new TemporaryDirectory();
            var publisher = CreateXsdPublisher(BuildModel(), first.Path);
            publisher.Publish();

            var secondModel = BuildModel(includeOtherItem: false);
            publisher.CogsModel = secondModel;
            publisher.TargetDirectory = second.Path;
            publisher.Publish();

            var text = File.ReadAllText(Path.Combine(second.Path, "schema.xsd"));
            Assert.DoesNotContain("name=\"OtherItem\"", text, StringComparison.Ordinal);
        }

        [Fact]
        public void XsdCanBeBuiltAndCompiledInMemoryForInstanceValidation()
        {
            var publisher = CreateXsdPublisher(BuildModel(), "unused-schema-output");

            var schemas = publisher.BuildSchemaSet();

            Assert.DoesNotContain(publisher.Errors, x => x.Level == Cogs.Common.ErrorLevel.Error);
            Assert.Empty(ValidateXml(schemas, $"<ItemContainer xmlns='{Namespace}'><DerivedItem><ID>1</ID><BaseValue>x</BaseValue></DerivedItem></ItemContainer>"));
        }

        private static CogsModel BuildModel(bool includeOtherItem = true)
        {
            var dto = new Cogs.Dto.CogsDtoModel();
            dto.Settings.Add(new Cogs.Dto.Setting { Key = "Title", Value = "Schema Test" });
            dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespaceUrl", Value = Namespace });
            dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespacePrefix", Value = "m" });
            dto.Identification.Add(DtoProperty("ID", "string", "1", "1"));

            var baseItem = new Cogs.Dto.ItemType { Name = "BaseItem", IsAbstract = true };
            baseItem.Properties.Add(DtoProperty("BaseValue", "string", "1", "1"));
            dto.ItemTypes.Add(baseItem);
            var derived = new Cogs.Dto.ItemType { Name = "DerivedItem", Extends = "BaseItem" };
            derived.Properties.Add(DtoProperty("ExactValue", "ValueBase"));
            var flexible = DtoProperty("FlexibleValue", "ValueBase");
            flexible.AllowSubtypes = "true";
            derived.Properties.Add(flexible);
            derived.Properties.Add(DtoProperty("Related", "BaseItem"));
            derived.Properties.Add(DtoProperty("ExactRelated", "DerivedItem"));
            var flexibleRelated = DtoProperty("FlexibleRelated", "DerivedItem");
            flexibleRelated.AllowSubtypes = "true";
            derived.Properties.Add(flexibleRelated);
            derived.Properties.Add(DtoProperty("Calendar", "cogsDate"));
            var observed = DtoProperty("ObservedAt", "dateTime");
            observed.MinInclusive = "2020-01-01T00:00:00Z";
            derived.Properties.Add(observed);
            dto.ItemTypes.Add(derived);
            if (includeOtherItem)
            {
                dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "OtherItem", Extends = "DerivedItem" });
            }

            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "ValueBase" });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "ValueChild", Extends = "ValueBase" });
            var result = new CogsModelBuilder().BuildResult(dto);
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            return result.Model;
        }

        private static CogsModel BuildMultipleIdentificationModel()
        {
            var dto = new Cogs.Dto.CogsDtoModel();
            dto.Settings.Add(new Cogs.Dto.Setting { Key = "Title", Value = "Identification Group Test" });
            dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespaceUrl", Value = Namespace });
            dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespacePrefix", Value = "m" });
            var id = DtoProperty("ID", "string", "1", "1");
            id.Description = "Base identifier.";
            id.Pattern = "[A-Z]+";
            dto.Identification.Add(id);
            var agency = DtoProperty("AgencyURI", "anyURI", "1", "1");
            agency.Description = "Agency identifier URI.";
            dto.IdentificationMixin.Add(agency);
            var item = new Cogs.Dto.ItemType { Name = "Thing" };
            item.Properties.Add(DtoProperty("Parent", "Thing"));
            dto.ItemTypes.Add(item);

            var result = new CogsModelBuilder().BuildResult(dto);
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            return result.Model;
        }

        private static Cogs.Dto.Property DtoProperty(string name, string datatype, string minimum = "0", string maximum = "1") => new Cogs.Dto.Property
        {
            Name = name,
            DataType = datatype,
            MinCardinality = minimum,
            MaxCardinality = maximum
        };

        private static void PublishJson(CogsModel model, string target)
        {
            new FluentJsonSchemaPublisher
            {
                CogsLocation = target + "-model-source",
                TargetDirectory = target
            }.Publish(model);
        }

        private static void PublishXsd(CogsModel model, string target) => CreateXsdPublisher(model, target).Publish();

        private static XmlSchemaPublisher CreateXsdPublisher(CogsModel model, string target) => new XmlSchemaPublisher
        {
            CogsLocation = target + "-model-source",
            TargetDirectory = target,
            TargetNamespace = Namespace,
            TargetNamespacePrefix = "m",
            CogsModel = model
        };

        private static XmlSchemaSet LoadSchemaSet(string schemaPath)
        {
            var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
            set.Add(null, schemaPath);
            set.Compile();
            return set;
        }

        private static List<string> ValidateXml(XmlSchemaSet schemas, string xml)
        {
            var errors = new List<string>();
            var settings = new XmlReaderSettings
            {
                Schemas = schemas,
                ValidationType = ValidationType.Schema,
                DtdProcessing = DtdProcessing.Prohibit
            };
            settings.ValidationEventHandler += (_, e) => errors.Add(e.Message);
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read()) { }
            return errors;
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cogs-schema-" + Guid.NewGuid().ToString("N"));
            }
            public string Path { get; }
            public void Dispose()
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
        }
    }
}
