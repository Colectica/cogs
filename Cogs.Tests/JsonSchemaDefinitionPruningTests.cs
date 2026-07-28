using Cogs.Model;
using Cogs.Publishers.FluentJson;
using Json.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Cogs.Tests
{
    public sealed class JsonSchemaDefinitionPruningTests
    {
        private const string Namespace = "https://example.org/schema-pruning";

        [Fact]
        public void EmitsAllConcreteItemRootsButOnlyReachableModelDefinedValueTypes()
        {
            var dto = CreateDto();
            dto.ItemTypes.Add(new Cogs.Dto.ItemType
            {
                Name = "Root",
                Properties = { Property("Payload", "UsedPrimitive") }
            });
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "Standalone" });
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "AbstractItem", IsAbstract = true });
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "ConcreteChild", Extends = "AbstractItem" });

            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType
            {
                Name = "UsedPrimitive",
                IsPrimitive = true,
                Properties = { Property("Lexical", "string", "1", "1") }
            });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType
            {
                Name = "UnusedPrimitive",
                IsPrimitive = true,
                Properties = { Property("Amount", "decimal") }
            });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType
            {
                Name = "UnusedBase",
                Properties = { Property("Count", "int") }
            });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType
            {
                Name = "UnusedChild",
                Extends = "UnusedBase"
            });

            using JsonDocument schema = SerializeSchema(BuildModel(dto));
            JsonElement definitions = schema.RootElement.GetProperty("$defs");

            AssertDefinitionsPresent(definitions, "Root", "Standalone", "ConcreteChild", "UsedPrimitive", "Reference");
            AssertDefinitionsAbsent(
                definitions,
                "AbstractItem",
                "UsedPrimitive__Tagged",
                "UnusedPrimitive",
                "UnusedPrimitive__Tagged",
                "UnusedBase",
                "UnusedBase__Tagged",
                "UnusedChild",
                "UnusedChild__Tagged");

            // Builtins remain a stable catalog even when a particular model does
            // not use them. Pruning applies only to model-defined declarations.
            AssertDefinitionsPresent(definitions, "string", "decimal", "int", "cogsDate");
            Assert.Equal(
                "#/$defs/string",
                definitions.GetProperty("UsedPrimitive").GetProperty("properties")
                    .GetProperty("Lexical").GetProperty("$ref").GetString());

            string[] itemRoots = schema.RootElement.GetProperty("properties").GetProperty("items")
                .GetProperty("items").GetProperty("oneOf")
                .EnumerateArray()
                .Select(x => x.GetProperty("$ref").GetString()!)
                .ToArray();
            Assert.Equal(
                new[] { "#/$defs/ConcreteChild", "#/$defs/Root", "#/$defs/Standalone" },
                itemRoots);

            Assert.Equal(
                new[] { "ConcreteChild", "Root", "Standalone" },
                ReferenceTypes(definitions, "Reference"));
            Assert.DoesNotContain(
                definitions.EnumerateObject(),
                definition => definition.Name.EndsWith("__Reference", StringComparison.Ordinal));
        }

        [Fact]
        public void EmitsOnlyReachableCompositeShapesIncludingRecursiveDependencies()
        {
            var dto = CreateDto();
            var root = new Cogs.Dto.ItemType { Name = "Root" };
            root.Properties.Add(Property("Exact", "ExactValue"));
            root.Properties.Add(Property("Polymorphic", "PolyBase", allowSubtypes: true));
            root.Properties.Add(Property("AbstractChoice", "AbstractBase"));
            root.Properties.Add(Property("RecursiveChoice", "RecursiveValue", allowSubtypes: true));
            dto.ItemTypes.Add(root);

            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "ExactValue" });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "PolyBase" });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "PolyChild", Extends = "PolyBase" });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "AbstractBase", IsAbstract = true });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "AbstractLeaf", Extends = "AbstractBase" });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType
            {
                Name = "RecursiveValue",
                Properties = { Property("Next", "RecursiveValue") }
            });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "NeverReached" });

            CogsModel model = BuildModel(dto);
            using JsonDocument schema = SerializeSchema(model);
            JsonElement definitions = schema.RootElement.GetProperty("$defs");

            AssertDefinitionsPresent(
                definitions,
                "ExactValue",
                "PolyBase__Tagged",
                "PolyChild__Tagged",
                "AbstractLeaf__Tagged",
                "RecursiveValue",
                "RecursiveValue__Tagged");
            AssertDefinitionsAbsent(
                definitions,
                "ExactValue__Tagged",
                "PolyBase",
                "PolyChild",
                "AbstractBase",
                "AbstractBase__Tagged",
                "AbstractLeaf",
                "NeverReached",
                "NeverReached__Tagged");

            JsonElement rootSchema = definitions.GetProperty("Root");
            Assert.Equal(
                "#/$defs/ExactValue",
                rootSchema.GetProperty("properties").GetProperty("Exact").GetProperty("$ref").GetString());
            Assert.Equal(
                new[] { "#/$defs/PolyBase__Tagged", "#/$defs/PolyChild__Tagged" },
                References(rootSchema.GetProperty("properties").GetProperty("Polymorphic").GetProperty("oneOf")));
            Assert.Equal(
                new[] { "#/$defs/AbstractLeaf__Tagged" },
                References(rootSchema.GetProperty("properties").GetProperty("AbstractChoice").GetProperty("oneOf")));
            Assert.Equal(
                "#/$defs/RecursiveValue",
                definitions.GetProperty("RecursiveValue__Tagged").GetProperty("properties")
                    .GetProperty("Next").GetProperty("$ref").GetString());
            Assert.Equal(
                "#/$defs/RecursiveValue",
                definitions.GetProperty("RecursiveValue").GetProperty("properties")
                    .GetProperty("Next").GetProperty("$ref").GetString());

            JsonSchema builtSchema = new FluentJsonSchemaPublisher().BuildSchema(model);
            using JsonDocument valid = JsonDocument.Parse(
                """{"items":[{"$type":"Root","ID":"one","RecursiveChoice":{"$type":"RecursiveValue","Next":{"Next":{}}}}]}""");
            Assert.True(builtSchema.Evaluate(valid.RootElement).IsValid);
        }

        [Fact]
        public void EmitsOnlySemanticallyDistinctItemReferenceVariants()
        {
            var dto = CreateDto();
            var owner = new Cogs.Dto.ItemType { Name = "Owner" };
            owner.Properties.Add(Property("ExactBase", "Base"));
            owner.Properties.Add(Property("FlexibleBase", "Base", allowSubtypes: true));
            owner.Properties.Add(Property("ExactLeaf", "Leaf"));
            owner.Properties.Add(Property("FlexibleLeaf", "Leaf", allowSubtypes: true));
            owner.Properties.Add(Property("AbstractTarget", "AbstractBase"));
            owner.Properties.Add(Property("AnyItem", "AllBase"));
            dto.ItemTypes.Add(owner);
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "AllBase", IsAbstract = true });
            owner.Extends = "AllBase";
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "Base", Extends = "AllBase" });
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "Child", Extends = "Base" });
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "Leaf", Extends = "AllBase" });
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "AbstractBase", IsAbstract = true, Extends = "AllBase" });
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "AbstractChild", Extends = "AbstractBase" });

            CogsModel model = BuildModel(dto);
            using JsonDocument schema = SerializeSchema(model);
            JsonElement definitions = schema.RootElement.GetProperty("$defs");
            JsonElement ownerSchema = definitions.GetProperty("Owner");

            Assert.Equal(
                "#/$defs/Base__Reference",
                ownerSchema.GetProperty("properties").GetProperty("ExactBase").GetProperty("$ref").GetString());
            Assert.Equal(
                "#/$defs/Base__AssignableReference",
                ownerSchema.GetProperty("properties").GetProperty("FlexibleBase").GetProperty("$ref").GetString());
            Assert.Equal(
                "#/$defs/Leaf__Reference",
                ownerSchema.GetProperty("properties").GetProperty("ExactLeaf").GetProperty("$ref").GetString());
            Assert.Equal(
                "#/$defs/Leaf__Reference",
                ownerSchema.GetProperty("properties").GetProperty("FlexibleLeaf").GetProperty("$ref").GetString());
            string abstractTargetReference = ownerSchema.GetProperty("properties").GetProperty("AbstractTarget")
                .GetProperty("$ref").GetString()!;
            Assert.Equal("#/$defs/AbstractChild__Reference", abstractTargetReference);
            Assert.Equal(
                "#/$defs/Reference",
                ownerSchema.GetProperty("properties").GetProperty("AnyItem").GetProperty("$ref").GetString());

            Assert.Equal(new[] { "Base" }, ReferenceTypes(definitions, "Base__Reference"));
            Assert.Equal(new[] { "Base", "Child" }, ReferenceTypes(definitions, "Base__AssignableReference"));
            Assert.Equal(new[] { "Leaf" }, ReferenceTypes(definitions, "Leaf__Reference"));
            Assert.Equal(new[] { "AbstractChild" }, ReferenceTypes(definitions, "AbstractChild__Reference"));
            AssertDefinitionsAbsent(
                definitions,
                "Leaf__AssignableReference",
                "AbstractBase",
                "AbstractBase__Reference",
                "AbstractBase__AssignableReference",
                "AllBase",
                "AllBase__Reference",
                "AllBase__AssignableReference",
                "Owner__Reference",
                "Child__Reference",
                "Child__AssignableReference");

            Assert.Equal(
                new[] { "AbstractChild", "Base", "Child", "Leaf", "Owner" },
                ReferenceTypes(definitions, "Reference"));

            JsonSchema builtSchema = new FluentJsonSchemaPublisher().BuildSchema(model);
            AssertValid(builtSchema,
                """{"items":[{"$type":"Owner","ID":"owner","FlexibleBase":{"$type":"Child","ID":"child"},"FlexibleLeaf":{"$type":"Leaf","ID":"leaf"},"AbstractTarget":{"$type":"AbstractChild","ID":"abstract"}}]}""",
                true);
            AssertValid(builtSchema,
                """{"items":[{"$type":"Owner","ID":"owner","ExactBase":{"$type":"Child","ID":"child"}}]}""",
                false);
        }

        private static Cogs.Dto.CogsDtoModel CreateDto()
        {
            var dto = new Cogs.Dto.CogsDtoModel();
            dto.Settings.Add(new Cogs.Dto.Setting { Key = "Title", Value = "Schema Pruning" });
            dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespaceUrl", Value = Namespace });
            dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespacePrefix", Value = "m" });
            dto.Identification.Add(Property("ID", "string", "1", "1"));
            return dto;
        }

        private static CogsModel BuildModel(Cogs.Dto.CogsDtoModel dto)
        {
            CogsBuildResult result = new CogsModelBuilder().BuildResult(dto);
            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            return result.Model;
        }

        private static Cogs.Dto.Property Property(
            string name,
            string datatype,
            string minimum = "0",
            string maximum = "1",
            bool allowSubtypes = false) =>
            new Cogs.Dto.Property
            {
                Name = name,
                DataType = datatype,
                MinCardinality = minimum,
                MaxCardinality = maximum,
                AllowSubtypes = allowSubtypes ? "true" : string.Empty
            };

        private static JsonDocument SerializeSchema(CogsModel model) =>
            JsonDocument.Parse(JsonSerializer.Serialize(new FluentJsonSchemaPublisher().BuildSchema(model)));

        private static void AssertDefinitionsPresent(JsonElement definitions, params string[] names)
        {
            foreach (string name in names)
            {
                Assert.True(definitions.TryGetProperty(name, out _), $"Expected $defs/{name} to be emitted.");
            }
        }

        private static void AssertDefinitionsAbsent(JsonElement definitions, params string[] names)
        {
            foreach (string name in names)
            {
                Assert.False(definitions.TryGetProperty(name, out _), $"Expected unused $defs/{name} to be omitted.");
            }
        }

        private static string[] References(JsonElement alternatives) =>
            alternatives.EnumerateArray()
                .Select(x => x.GetProperty("$ref").GetString()!)
                .ToArray();

        private static string[] ReferenceTypes(JsonElement definitions, string definitionName) =>
            definitions.GetProperty(definitionName).GetProperty("properties").GetProperty("$type")
                .GetProperty("enum").EnumerateArray()
                .Select(x => x.GetString()!)
                .ToArray();

        private static void AssertValid(JsonSchema schema, string json, bool expected)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            Assert.Equal(expected, schema.Evaluate(document.RootElement).IsValid);
        }
    }
}
