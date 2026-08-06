using Cogs.Common;
using Cogs.Model;
using Json.Schema;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cogs.Publishers.FluentJson
{
    /// <summary>Publishes the closed COGS 2.0 JSON instance contract.</summary>
    public class FluentJsonSchemaPublisher
    {
        private const string Draft202012 = "https://json-schema.org/draft/2020-12/schema";
        private const string CogsVocabulary = "https://cogsdata.org/schema/vocabulary/2.0";
        private static readonly IReadOnlyDictionary<string, string> PrimitiveDescriptions =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["boolean"] = "A Boolean value: true or false.",
                ["string"] = "A Unicode text string.",
                ["decimal"] = "An exact XML Schema decimal represented as a JSON number without exponent notation.",
                ["float"] = "A finite IEEE 754 binary32 value represented as a JSON number.",
                ["double"] = "A finite IEEE 754 binary64 value represented as a JSON number.",
                ["duration"] = "An XML Schema duration lexical value, including optional sign and year or month components.",
                ["dateTime"] = "An XML Schema dateTime lexical value with a nonzero signed 32-bit calendar year.",
                ["time"] = "An XML Schema time lexical value.",
                ["date"] = "An XML Schema date lexical value with a nonzero signed 32-bit calendar year.",
                ["gYearMonth"] = "An XML Schema gYearMonth represented by Year, Month, and optional Timezone components; Year is a nonzero signed 32-bit integer.",
                ["gYear"] = "An XML Schema gYear represented by Year and optional Timezone components; Year is a nonzero signed 32-bit integer.",
                ["gMonthDay"] = "An XML Schema gMonthDay represented by Month, Day, and optional Timezone components.",
                ["gDay"] = "An XML Schema gDay represented by Day and optional Timezone components.",
                ["gMonth"] = "An XML Schema gMonth represented by Month and optional Timezone components.",
                ["anyURI"] = "An RFC 3986 relative or absolute URI reference.",
                ["language"] = "A language tag using COGS BCP 47 syntax.",
                ["nonPositiveInteger"] = "An arbitrary-precision integer less than or equal to zero.",
                ["negativeInteger"] = "An arbitrary-precision integer less than zero.",
                ["long"] = "A signed 64-bit integer.",
                ["int"] = "A signed 32-bit integer.",
                ["nonNegativeInteger"] = "An arbitrary-precision integer greater than or equal to zero.",
                ["unsignedLong"] = "An integer from zero through 18446744073709551615.",
                ["positiveInteger"] = "An arbitrary-precision integer greater than zero.",
                ["cogsDate"] = "A date value containing exactly one DateTime, Date, GYearMonth, GYear, or Duration arm.",
                ["langString"] = "A Unicode text string paired with a required BCP 47 language tag."
            };

        public string CogsLocation { get; set; } = string.Empty;
        public string TargetDirectory { get; set; } = string.Empty;
        public bool Overwrite { get; set; }

        private CogsModel CogsModel { get; set; } = null!;
        private SchemaEmissionPlan EmissionPlan { get; set; } = SchemaEmissionPlan.Empty;

        public void Publish(CogsModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var originalTarget = TargetDirectory;
            DirectoryPublication.Publish(originalTarget, Overwrite, stagingDirectory =>
            {
                TargetDirectory = stagingDirectory;
                try { PublishCore(model); }
                finally { TargetDirectory = originalTarget; }
            }, string.IsNullOrWhiteSpace(model.SourceDirectory) ? CogsLocation : model.SourceDirectory);
        }

        private void PublishCore(CogsModel model)
        {
            if (string.IsNullOrWhiteSpace(TargetDirectory)) throw new InvalidOperationException("Target directory must be specified.");

            var schema = BuildSchema(model);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(Path.Combine(TargetDirectory, "jsonSchema.json"), JsonSerializer.Serialize(schema, options), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(TargetDirectory, "cogs-meta-schema.json"), BuildMetaSchema().ToJsonString(options), new UTF8Encoding(false));
        }

        /// <summary>
        /// Builds the complete in-memory COGS JSON Schema without writing files.
        /// This is the canonical entry point for instance validation.
        /// </summary>
        public JsonSchema BuildSchema(CogsModel model)
        {
            CogsModel = model ?? throw new ArgumentNullException(nameof(model));
            EmissionPlan = SchemaEmissionPlan.Empty;
            EmissionPlan = BuildEmissionPlan(model);

            var definitions = SimpleTypeDefinitions();
            foreach (var composite in model.ReusableDataTypes)
            {
                if (EmissionPlan.ModelTypeNames.Contains(composite.Name))
                {
                    definitions[composite.Name] = GetJsonSchema(composite);
                }
            }
            foreach (var item in model.ItemTypes)
            {
                if (EmissionPlan.ModelTypeNames.Contains(item.Name))
                {
                    definitions[item.Name] = GetJsonSchema(item);
                }
            }
            definitions["Reference"] = BuildReferenceSchema(model.ItemTypes.Where(x => !x.IsAbstract));

            var itemAlternatives = model.ItemTypes
                .Where(x => !x.IsAbstract)
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .Select(BuildExactItemAlternative)
                .ToArray();
            JsonSchemaBuilder itemSchema = itemAlternatives.Length == 0
                ? JsonSchemaBuilder.False
                : new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .OneOf(itemAlternatives)
                    .UnevaluatedProperties(JsonSchemaBuilder.False);

            var containerProperties = new Dictionary<string, JsonSchemaBuilder>
            {
                ["topLevelReferences"] = new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Ref(DefinitionRef("Reference")))
                    .MinItems(0)
                    .Description("References to the top-level items in this container."),
                ["items"] = new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(itemSchema)
                    .MinItems(0)
                    .Description("Complete item definitions serialized in this container.")
            };

            var root = new JsonSchemaBuilder()
                .Schema(Draft202012)
                .Comment(model.Settings?.Title ?? string.Empty)
                .Type(SchemaValueType.Object)
                .Properties(containerProperties)
                .Required("items")
                .AdditionalProperties(false)
                .Defs(definitions);
            root.Add("x-cogs-version", JsonValue.Create("2.0"));
            root.Add("x-cogs-vocabulary", JsonValue.Create(CogsVocabulary));
            return root.Build();
        }

        public JsonSchemaBuilder GetJsonSchema(DataType datatype)
        {
            var properties = datatype.Properties;
            var jsonProperties = new Dictionary<string, JsonSchemaBuilder>();
            DataType? parent = GetImmediateParent(datatype);
            if (datatype is ItemType item && parent is null)
            {
                string[] concreteTypes = CogsTypeSystem.ConcreteAssignableTypes(CogsModel, item)
                    .OfType<ItemType>()
                    .Select(x => x.Name)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray();
                jsonProperties["$type"] = new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(concreteTypes);
            }
            foreach (var property in properties)
            {
                jsonProperties[property.Name] = GetBuilderForProperty(property);
            }

            var localSchema = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(jsonProperties);

            var required = properties.Where(IsRequired).Select(x => x.Name).ToList();
            if (datatype is ItemType && parent is null) required.Insert(0, "$type");
            if (required.Count > 0) localSchema.Required(required);

            if (parent is null)
            {
                return localSchema.Description(datatype.Description ?? string.Empty);
            }

            return new JsonSchemaBuilder()
                .Description(datatype.Description ?? string.Empty)
                .AllOf(
                    new JsonSchemaBuilder().Ref(DefinitionRef(parent.Name)),
                    localSchema);
        }

        public JsonSchemaBuilder GetBuilderForProperty(Property property)
        {
            var valueSchema = GetBuilderForType(property);
            ApplyFacets(valueSchema, property);

            JsonSchemaBuilder result;
            if (property.MaxCardinality != "1")
            {
                result = new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(valueSchema);
                AddRawInteger(result, "minItems", string.IsNullOrWhiteSpace(property.MinCardinality) ? "0" : property.MinCardinality);
                if (property.MaxCardinality != "n" && !string.IsNullOrWhiteSpace(property.MaxCardinality))
                {
                    AddRawInteger(result, "maxItems", property.MaxCardinality);
                }
            }
            else
            {
                result = valueSchema;
            }
            result.Description(property.Description ?? string.Empty);
            return result;
        }

        public JsonSchemaBuilder GetBuilderForType(Property property)
        {
            if (property.DataType is ItemType item)
            {
                return BuildItemReferenceSchema(item, CogsTypeSystem.AllowsSubtypes(property));
            }
            if (property.DataType != null && !property.DataType.IsXmlPrimitive)
            {
                if (!CogsTypeSystem.AllowsSubtypes(property))
                {
                    return BuildClosedStructuralReference(property.DataType);
                }

                var alternatives = CogsTypeSystem.ConcreteAssignableTypes(CogsModel, property.DataType)
                    .Where(x => x is not ItemType)
                    .Select(BuildTaggedCompositeAlternative)
                    .ToArray();
                return alternatives.Length == 0
                    ? UninhabitedSchema()
                    : new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .OneOf(alternatives)
                        .UnevaluatedProperties(JsonSchemaBuilder.False);
            }

            return new JsonSchemaBuilder().Ref(DefinitionRef(property.DataTypeName));
        }

        private SchemaEmissionPlan BuildEmissionPlan(CogsModel model)
        {
            var modelTypes = new HashSet<string>(StringComparer.Ordinal);
            var traversedComposites = new HashSet<string>(StringComparer.Ordinal);

            void AddTypeAndAncestors(DataType type)
            {
                foreach (var parent in type.ParentTypes)
                {
                    modelTypes.Add(parent.Name);
                }
                modelTypes.Add(type.Name);
            }

            void TraverseProperties(DataType owner)
            {
                if (!traversedComposites.Add(owner.Name))
                {
                    return;
                }

                foreach (var property in CogsTypeSystem.EffectiveProperties(owner))
                {
                    TraverseProperty(property);
                }
            }

            void TraverseProperty(Property property)
            {
                if (property.DataType is ItemType)
                {
                    return;
                }

                if (property.DataType == null || property.DataType.IsXmlPrimitive)
                {
                    return;
                }

                DataType composite = property.DataType;
                if (!CogsTypeSystem.AllowsSubtypes(property))
                {
                    AddTypeAndAncestors(composite);
                    TraverseProperties(composite);
                    return;
                }

                foreach (var concrete in CogsTypeSystem.ConcreteAssignableTypes(model, composite)
                    .Where(x => x is not ItemType))
                {
                    AddTypeAndAncestors(concrete);
                    TraverseProperties(concrete);
                }
            }

            // Every concrete item remains a legal full object in ItemContainer.items.
            // Its inheritance chain and effective properties are therefore roots
            // of the model-defined value-type reachability graph.
            foreach (var item in model.ItemTypes.Where(x => !x.IsAbstract))
            {
                AddTypeAndAncestors(item);
                foreach (var property in CogsTypeSystem.EffectiveProperties(item))
                {
                    TraverseProperty(property);
                }
            }

            return new SchemaEmissionPlan(modelTypes);
        }

        private DataType? GetImmediateParent(DataType datatype)
        {
            if (string.IsNullOrWhiteSpace(datatype.ExtendsTypeName))
            {
                return null;
            }

            DataType? parent = datatype.ParentTypes.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, datatype.ExtendsTypeName, StringComparison.Ordinal));
            parent ??= CogsModel.AllDataTypes.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, datatype.ExtendsTypeName, StringComparison.Ordinal));
            return parent ?? throw new InvalidOperationException(
                $"Parent type '{datatype.ExtendsTypeName}' for '{datatype.Name}' is not present in the connected model.");
        }

        private JsonSchemaBuilder BuildExactItemAlternative(ItemType item) =>
            new JsonSchemaBuilder().AllOf(
                new JsonSchemaBuilder().Ref(DefinitionRef(item.Name)),
                BuildDiscriminatorRestriction(new[] { item.Name }));

        private JsonSchemaBuilder BuildClosedStructuralReference(DataType datatype) =>
            new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .AllOf(new JsonSchemaBuilder().Ref(DefinitionRef(datatype.Name)))
                .UnevaluatedProperties(JsonSchemaBuilder.False);

        private JsonSchemaBuilder BuildTaggedCompositeAlternative(DataType datatype) =>
            new JsonSchemaBuilder().AllOf(
                new JsonSchemaBuilder().Ref(DefinitionRef(datatype.Name)),
                BuildDiscriminatorRestriction(new[] { datatype.Name }));

        private JsonSchemaBuilder BuildItemReferenceSchema(ItemType declared, bool allowsSubtypes)
        {
            string[] permittedTypes = (allowsSubtypes
                    ? CogsTypeSystem.ConcreteAssignableTypes(CogsModel, declared)
                    : declared.IsAbstract ? Array.Empty<DataType>() : new DataType[] { declared })
                .OfType<ItemType>()
                .Select(x => x.Name)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            if (permittedTypes.Length == 0)
            {
                return UninhabitedSchema();
            }

            string[] allConcreteTypes = CogsModel.ItemTypes
                .Where(x => !x.IsAbstract)
                .Select(x => x.Name)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            if (permittedTypes.SequenceEqual(allConcreteTypes, StringComparer.Ordinal))
            {
                return new JsonSchemaBuilder().Ref(DefinitionRef("Reference"));
            }

            return new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .AllOf(
                    new JsonSchemaBuilder().Ref(DefinitionRef("Reference")),
                    BuildDiscriminatorRestriction(permittedTypes));
        }

        private static JsonSchemaBuilder BuildDiscriminatorRestriction(IEnumerable<string> typeNames) =>
            new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(("$type", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(typeNames.ToArray())))
                .Required("$type");

        private Dictionary<string, JsonSchemaBuilder> SimpleTypeDefinitions()
        {
            var definitions = new Dictionary<string, JsonSchemaBuilder>(StringComparer.Ordinal)
            {
                ["boolean"] = new JsonSchemaBuilder().Type(SchemaValueType.Boolean),
                ["string"] = new JsonSchemaBuilder().Type(SchemaValueType.String),
                ["decimal"] = NumericType("decimal"),
                ["float"] = NumericType("float"),
                ["double"] = NumericType("double"),
                ["duration"] = LexicalFormatType("duration", "duration"),
                ["dateTime"] = LexicalFormatType("dateTime", "date-time"),
                ["time"] = LexicalFormatType("time", "time"),
                ["date"] = LexicalFormatType("date", "date"),
                ["gYearMonth"] = GregorianType("gYearMonth"),
                ["gYear"] = GregorianType("gYear"),
                ["gMonthDay"] = GregorianType("gMonthDay"),
                ["gDay"] = GregorianType("gDay"),
                ["gMonth"] = GregorianType("gMonth"),
                ["anyURI"] = LexicalFormatType("anyURI", "uri"),
                ["language"] = LexicalType("language", CogsPrimitiveLexical.Bcp47Pattern),
                ["nonPositiveInteger"] = IntegerType(maximum: "0"),
                ["negativeInteger"] = IntegerType(maximum: "-1"),
                ["long"] = IntegerType("-9223372036854775808", "9223372036854775807"),
                ["int"] = IntegerType("-2147483648", "2147483647"),
                ["nonNegativeInteger"] = IntegerType("0"),
                ["unsignedLong"] = IntegerType("0", "18446744073709551615"),
                ["positiveInteger"] = IntegerType("1"),
                ["langString"] = new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("@language", new JsonSchemaBuilder().Ref(DefinitionRef("language"))),
                        ("@value", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .Required("@language", "@value")
                    .AdditionalProperties(false)
            };

            var cogsDateProperties = new Dictionary<string, JsonSchemaBuilder>
            {
                ["DateTime"] = new JsonSchemaBuilder().Ref(DefinitionRef("dateTime")),
                ["Date"] = new JsonSchemaBuilder().Ref(DefinitionRef("date")),
                ["GYearMonth"] = new JsonSchemaBuilder().Ref(DefinitionRef("gYearMonth")),
                ["GYear"] = new JsonSchemaBuilder().Ref(DefinitionRef("gYear")),
                ["Duration"] = new JsonSchemaBuilder().Ref(DefinitionRef("duration"))
            };
            definitions["cogsDate"] = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(cogsDateProperties)
                .OneOf(cogsDateProperties.Keys.Select(name => new JsonSchemaBuilder().Required(name)))
                .AdditionalProperties(false);

            foreach (string primitiveName in CogsTypes.SimpleTypeNames)
            {
                if (!definitions.TryGetValue(primitiveName, out JsonSchemaBuilder? definition))
                {
                    throw new InvalidOperationException(
                        $"The built-in COGS primitive '{primitiveName}' has no JSON Schema definition.");
                }
                if (!PrimitiveDescriptions.TryGetValue(primitiveName, out string? description) ||
                    string.IsNullOrWhiteSpace(description))
                {
                    throw new InvalidOperationException(
                        $"The built-in COGS primitive '{primitiveName}' has no JSON Schema description.");
                }
                definition.Description(description);
            }

            return definitions;
        }

        private JsonSchemaBuilder BuildReferenceSchema(IEnumerable<DataType> concreteTypes)
        {
            var typeNames = concreteTypes
                .Select(x => x.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            if (typeNames.Length == 0)
            {
                return JsonSchemaBuilder.False;
            }

            var properties = new Dictionary<string, JsonSchemaBuilder>
            {
                ["$type"] = new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(typeNames)
            };
            foreach (var identification in CogsModel.Identification)
            {
                properties[identification.Name] = GetBuilderForProperty(identification);
            }
            var required = new[] { "$type" }.Concat(CogsModel.Identification.Select(x => x.Name)).ToArray();
            return new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Description("A reference to an item containing its concrete $type and every configured identification property.")
                .Properties(properties)
                .Required(required)
                .AdditionalProperties(false);
        }

        private static void ApplyFacets(JsonSchemaBuilder builder, Property property)
        {
            if (property.DataTypeName == "langString" &&
                (property.MinLength.HasValue || property.MaxLength.HasValue || !string.IsNullOrWhiteSpace(property.Pattern) || property.Enumeration.Count > 0))
            {
                var content = new JsonSchemaBuilder().Type(SchemaValueType.String);
                ApplyStringFacets(content, property);
                if (property.Enumeration.Count > 0)
                {
                    content.Add("enum", BuildEnumeration(property));
                }
                builder.AllOf(new JsonSchemaBuilder().Properties(("@value", content)));
            }
            else
            {
                ApplyStringFacets(builder, property);
                if (property.Enumeration.Count > 0)
                {
                    builder.Add("enum", BuildEnumeration(property));
                }
            }

            if (IsNumeric(property.DataTypeName))
            {
                AddRawNumber(builder, "minimum", property.MinInclusive);
                AddRawNumber(builder, "exclusiveMinimum", property.MinExclusive);
                AddRawNumber(builder, "maximum", property.MaxInclusive);
                AddRawNumber(builder, "exclusiveMaximum", property.MaxExclusive);
            }
            else if (IsTemporal(property.DataTypeName))
            {
                AddCogsBound(builder, "x-cogs-minInclusive", property.DataTypeName, property.MinInclusive);
                AddCogsBound(builder, "x-cogs-minExclusive", property.DataTypeName, property.MinExclusive);
                AddCogsBound(builder, "x-cogs-maxInclusive", property.DataTypeName, property.MaxInclusive);
                AddCogsBound(builder, "x-cogs-maxExclusive", property.DataTypeName, property.MaxExclusive);
            }
        }

        private static void ApplyStringFacets(JsonSchemaBuilder builder, Property property)
        {
            if (!string.IsNullOrWhiteSpace(property.Pattern)) builder.Pattern(property.Pattern);
            if (property.MinLength.HasValue) builder.MinLength((uint)property.MinLength.Value);
            if (property.MaxLength.HasValue) builder.MaxLength((uint)property.MaxLength.Value);
        }

        private static JsonArray BuildEnumeration(Property property)
        {
            var array = new JsonArray();
            foreach (var lexical in property.Enumeration)
            {
                if (CogsGregorianLexical.TryParse(property.DataTypeName, lexical, out CogsGregorianValue gregorian))
                {
                    array.Add(BuildGregorianJsonValue(gregorian));
                }
                else if (IsNumeric(property.DataTypeName) || IsIntegerType(property.DataTypeName) || property.DataTypeName == "boolean")
                {
                    array.Add(JsonNode.Parse(lexical));
                }
                else
                {
                    array.Add(lexical);
                }
            }
            return array;
        }

        private static JsonObject BuildGregorianJsonValue(CogsGregorianValue value)
        {
            var result = new JsonObject();
            if (value.Year.HasValue) result["Year"] = value.Year.Value;
            if (value.Month.HasValue) result["Month"] = value.Month.Value;
            if (value.Day.HasValue) result["Day"] = value.Day.Value;
            if (value.Timezone is not null) result["Timezone"] = value.Timezone;
            return result;
        }

        private static JsonSchemaBuilder IntegerType(string? minimum = null, string? maximum = null)
        {
            var builder = new JsonSchemaBuilder().Type(SchemaValueType.Integer);
            AddRawNumber(builder, "minimum", minimum);
            AddRawNumber(builder, "maximum", maximum);
            return builder;
        }

        private static JsonSchemaBuilder NumericType(string datatype)
        {
            var builder = new JsonSchemaBuilder().Type(SchemaValueType.Number);
            builder.Add("x-cogs-datatype", JsonValue.Create(datatype));
            return builder;
        }

        private static JsonSchemaBuilder LexicalType(string datatype, string pattern)
        {
            var builder = new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern(pattern);
            builder.Add("x-cogs-datatype", JsonValue.Create(datatype));
            return builder;
        }

        private static JsonSchemaBuilder LexicalFormatType(string datatype, string format)
        {
            var builder = new JsonSchemaBuilder().Type(SchemaValueType.String).Format(format);
            builder.Add("x-cogs-datatype", JsonValue.Create(datatype));
            return builder;
        }

        private static JsonSchemaBuilder GregorianType(string datatype)
        {
            const string timezonePattern = @"^(?:Z|[+-](?:(?:0[0-9]|1[0-3]):[0-5][0-9]|14:00))$";
            var properties = new Dictionary<string, JsonSchemaBuilder>(StringComparer.Ordinal);
            var required = new List<string>();

            if (datatype is "gYearMonth" or "gYear")
            {
                properties["Year"] = new JsonSchemaBuilder().OneOf(
                    IntegerType(int.MinValue.ToString(CultureInfo.InvariantCulture), "-1"),
                    IntegerType("1", int.MaxValue.ToString(CultureInfo.InvariantCulture)));
                required.Add("Year");
            }
            if (datatype is "gYearMonth" or "gMonthDay" or "gMonth")
            {
                properties["Month"] = IntegerType("1", "12");
                required.Add("Month");
            }
            if (datatype is "gMonthDay" or "gDay")
            {
                properties["Day"] = IntegerType("1", "31");
                required.Add("Day");
            }
            properties["Timezone"] = new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Pattern(timezonePattern);

            var builder = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(properties)
                .Required(required)
                .AdditionalProperties(false);
            if (datatype == "gMonthDay")
            {
                builder.OneOf(
                    new JsonSchemaBuilder().Properties(
                        ("Month", new JsonSchemaBuilder().Enum(2)),
                        ("Day", IntegerType("1", "29"))),
                    new JsonSchemaBuilder().Properties(
                        ("Month", new JsonSchemaBuilder().Enum(4, 6, 9, 11)),
                        ("Day", IntegerType("1", "30"))),
                    new JsonSchemaBuilder().Properties(
                        ("Month", new JsonSchemaBuilder().Enum(1, 3, 5, 7, 8, 10, 12)),
                        ("Day", IntegerType("1", "31"))));
            }
            builder.Add("x-cogs-datatype", JsonValue.Create(datatype));
            return builder;
        }

        // JsonSchemaBuilder.False is a protected static singleton. Wrap it so
        // property descriptions and facet annotations can decorate a fresh builder.
        private static JsonSchemaBuilder UninhabitedSchema() =>
            new JsonSchemaBuilder().AllOf(JsonSchemaBuilder.False);

        private static void AddRawInteger(JsonSchemaBuilder builder, string keyword, string lexical) => AddRawNumber(builder, keyword, lexical);

        private static void AddRawNumber(JsonSchemaBuilder builder, string keyword, string? lexical)
        {
            if (!string.IsNullOrWhiteSpace(lexical)) builder.Add(keyword, JsonNode.Parse(lexical));
        }

        private static void AddCogsBound(JsonSchemaBuilder builder, string keyword, string datatype, string? lexical)
        {
            if (string.IsNullOrWhiteSpace(lexical)) return;
            builder.Add(keyword, new JsonObject { ["datatype"] = datatype, ["value"] = lexical });
        }

        private static JsonObject BuildMetaSchema() => new JsonObject
        {
            ["$schema"] = Draft202012,
            ["$id"] = "https://cogsdata.org/schema/meta/2.0",
            ["title"] = "COGS 2.0 JSON Schema extension vocabulary",
            ["description"] = "Describes the COGS datatype and temporal bound annotations. Standard JSON Schema processors may treat these as annotations; validate-instance enforces them.",
            ["x-cogs-vocabulary"] = CogsVocabulary,
            ["type"] = "object"
        };

        private static string DefinitionRef(string name) => "#/$defs/" + name;
        private static bool IsRequired(Property property) => property.MinCardinality != "0" && !string.IsNullOrWhiteSpace(property.MinCardinality);
        private static bool IsIntegerType(string type) => type is "nonPositiveInteger" or "negativeInteger" or "long" or "int" or "nonNegativeInteger" or "unsignedLong" or "positiveInteger";
        private static bool IsNumeric(string type) => IsIntegerType(type) || type is "decimal" or "float" or "double";
        private static bool IsTemporal(string type) => type is "duration" or "dateTime" or "time" or "date" or "gYearMonth" or "gYear" or "gMonthDay" or "gDay" or "gMonth";

        private sealed class SchemaEmissionPlan
        {
            public static SchemaEmissionPlan Empty { get; } = new(new HashSet<string>(StringComparer.Ordinal));

            public SchemaEmissionPlan(HashSet<string> modelTypeNames) => ModelTypeNames = modelTypeNames;

            public IReadOnlySet<string> ModelTypeNames { get; }
        }

        public bool IsInteger(string type) => IsIntegerType(type ?? string.Empty);
        public bool IsNumber(string type) => type is not null && IsNumeric(type);
        public bool IsBoolean(string type) => string.Equals(type, "boolean", StringComparison.OrdinalIgnoreCase);
    }
}
