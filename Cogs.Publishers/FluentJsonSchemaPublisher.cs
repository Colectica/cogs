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
                if (EmissionPlan.UntaggedCompositeNames.Contains(composite.Name))
                {
                    definitions[composite.Name] = GetJsonSchema(composite, tagged: false);
                }
                if (EmissionPlan.TaggedCompositeNames.Contains(composite.Name))
                {
                    definitions[TaggedName(composite)] = GetJsonSchema(composite, tagged: true);
                }
            }
            foreach (var item in model.ItemTypes.Where(x => !x.IsAbstract))
            {
                definitions[item.Name] = GetJsonSchema(item, tagged: true);
            }
            foreach (var reference in EmissionPlan.ReferenceDefinitions)
            {
                definitions[reference.Name] = BuildReferenceSchema(reference.ConcreteTypes);
            }
            definitions["Reference"] = BuildReferenceSchema(model.ItemTypes.Where(x => !x.IsAbstract));

            var itemAlternatives = model.ItemTypes
                .Where(x => !x.IsAbstract)
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .Select(x => new JsonSchemaBuilder().Ref(DefinitionRef(x.Name)))
                .ToArray();

            var containerProperties = new Dictionary<string, JsonSchemaBuilder>
            {
                ["topLevelReferences"] = new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Ref(DefinitionRef("Reference")))
                    .MinItems(0),
                ["items"] = new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(itemAlternatives.Length == 0 ? JsonSchemaBuilder.False : new JsonSchemaBuilder().OneOf(itemAlternatives))
                    .MinItems(0)
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

        public JsonSchemaBuilder GetJsonSchema(DataType datatype) =>
            GetJsonSchema(datatype, datatype is ItemType && !datatype.IsAbstract);

        private JsonSchemaBuilder GetJsonSchema(DataType datatype, bool tagged)
        {
            var properties = CogsTypeSystem.EffectiveProperties(datatype);
            var jsonProperties = new Dictionary<string, JsonSchemaBuilder>();
            if (tagged)
            {
                jsonProperties["$type"] = new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(datatype.Name);
            }
            foreach (var property in properties)
            {
                jsonProperties[property.Name] = GetBuilderForProperty(property);
            }

            var builder = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Description(datatype.Description ?? string.Empty)
                .Properties(jsonProperties)
                .AdditionalProperties(false);

            var required = properties.Where(IsRequired).Select(x => x.Name).ToList();
            if (tagged) required.Insert(0, "$type");
            if (required.Count > 0) builder.Required(required);
            return builder;
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
                var use = new ItemReferenceUse(item.Name, CogsTypeSystem.AllowsSubtypes(property));
                if (EmissionPlan.ItemReferenceTargets.TryGetValue(use, out string? plannedReference))
                {
                    return plannedReference is null
                        ? UninhabitedSchema()
                        : new JsonSchemaBuilder().Ref(DefinitionRef(plannedReference));
                }

                string reference = use.AllowsSubtypes ? AssignableReferenceName(item) : ReferenceName(item);
                return new JsonSchemaBuilder().Ref(DefinitionRef(reference));
            }
            if (property.DataType != null && !property.DataType.IsXmlPrimitive)
            {
                if (!CogsTypeSystem.AllowsSubtypes(property))
                {
                    return new JsonSchemaBuilder().Ref(DefinitionRef(property.DataType.Name));
                }

                var alternatives = CogsTypeSystem.ConcreteAssignableTypes(CogsModel, property.DataType)
                    .Where(x => x is not ItemType)
                    .Select(x => new JsonSchemaBuilder().Ref(DefinitionRef(TaggedName(x))))
                    .ToArray();
                return alternatives.Length == 0
                    ? UninhabitedSchema()
                    : new JsonSchemaBuilder().OneOf(alternatives);
            }

            return new JsonSchemaBuilder().Ref(DefinitionRef(property.DataTypeName));
        }

        private SchemaEmissionPlan BuildEmissionPlan(CogsModel model)
        {
            var untaggedComposites = new HashSet<string>(StringComparer.Ordinal);
            var taggedComposites = new HashSet<string>(StringComparer.Ordinal);
            var traversedComposites = new HashSet<string>(StringComparer.Ordinal);
            var itemReferenceUses = new HashSet<ItemReferenceUse>();

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
                if (property.DataType is ItemType item)
                {
                    itemReferenceUses.Add(new ItemReferenceUse(item.Name, CogsTypeSystem.AllowsSubtypes(property)));
                    return;
                }

                if (property.DataType == null || property.DataType.IsXmlPrimitive)
                {
                    return;
                }

                DataType composite = property.DataType;
                if (!CogsTypeSystem.AllowsSubtypes(property))
                {
                    untaggedComposites.Add(composite.Name);
                    TraverseProperties(composite);
                    return;
                }

                foreach (var concrete in CogsTypeSystem.ConcreteAssignableTypes(model, composite)
                    .Where(x => x is not ItemType))
                {
                    taggedComposites.Add(concrete.Name);
                    TraverseProperties(concrete);
                }
            }

            // Every concrete item remains a legal full object in ItemContainer.items.
            // Its flattened effective properties are therefore the roots of the
            // model-defined value-type and item-reference reachability graph.
            foreach (var item in model.ItemTypes.Where(x => !x.IsAbstract))
            {
                foreach (var property in CogsTypeSystem.EffectiveProperties(item))
                {
                    TraverseProperty(property);
                }
            }

            return BuildReferencePlan(
                model,
                untaggedComposites,
                taggedComposites,
                itemReferenceUses);
        }

        private SchemaEmissionPlan BuildReferencePlan(
            CogsModel model,
            HashSet<string> untaggedComposites,
            HashSet<string> taggedComposites,
            HashSet<ItemReferenceUse> itemReferenceUses)
        {
            var targets = new Dictionary<ItemReferenceUse, string?>();
            var definitionsByShape = new Dictionary<string, ReferenceDefinitionPlan>(StringComparer.Ordinal);
            DataType[] allConcreteItems = model.ItemTypes
                .Where(x => !x.IsAbstract)
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .Cast<DataType>()
                .ToArray();
            string globalShape = BuildReferenceShapeKey(allConcreteItems);
            var itemByName = model.ItemTypes.ToDictionary(x => x.Name, StringComparer.Ordinal);

            foreach (var use in itemReferenceUses
                .OrderBy(x => x.ItemName, StringComparer.Ordinal)
                .ThenBy(x => x.AllowsSubtypes))
            {
                ItemType declared = itemByName[use.ItemName];
                DataType[] permitted = (use.AllowsSubtypes
                        ? CogsTypeSystem.ConcreteAssignableTypes(model, declared)
                        : declared.IsAbstract ? Array.Empty<DataType>() : new DataType[] { declared })
                    .OfType<ItemType>()
                    .OrderBy(x => x.Name, StringComparer.Ordinal)
                    .Cast<DataType>()
                    .ToArray();

                if (permitted.Length == 0)
                {
                    // An abstract item without a concrete descendant is
                    // uninhabited and needs no named definition.
                    targets[use] = null;
                    continue;
                }

                string shape = BuildReferenceShapeKey(permitted);
                if (string.Equals(shape, globalShape, StringComparison.Ordinal))
                {
                    targets[use] = "Reference";
                    continue;
                }

                if (!definitionsByShape.TryGetValue(shape, out ReferenceDefinitionPlan? definition))
                {
                    string definitionName = GetCanonicalReferenceName(model, permitted);
                    definition = new ReferenceDefinitionPlan(definitionName, permitted);
                    definitionsByShape.Add(shape, definition);
                }
                targets[use] = definition.Name;
            }

            return new SchemaEmissionPlan(
                untaggedComposites,
                taggedComposites,
                targets,
                definitionsByShape.Values
                    .OrderBy(x => x.Name, StringComparer.Ordinal)
                    .ToArray());
        }

        private string BuildReferenceShapeKey(IReadOnlyList<DataType> concreteTypes)
        {
            var key = new StringBuilder();
            AppendShapeSegment(key, "types");
            foreach (var type in concreteTypes.OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                AppendShapeSegment(key, type.Name);
            }

            // Reference objects use the same complete identification contract,
            // but include it in the key so future item-local identity changes
            // cannot accidentally cause structurally different schemas to merge.
            AppendShapeSegment(key, "identity");
            foreach (var property in CogsModel.Identification)
            {
                AppendShapeSegment(key, property.Name);
                AppendShapeSegment(key, property.DataTypeName);
                AppendShapeSegment(key, property.MinCardinality);
                AppendShapeSegment(key, property.MaxCardinality);
                AppendShapeSegment(key, property.Description);
                AppendShapeSegment(key, property.Pattern);
                AppendShapeSegment(key, property.MinLength?.ToString(CultureInfo.InvariantCulture));
                AppendShapeSegment(key, property.MaxLength?.ToString(CultureInfo.InvariantCulture));
                AppendShapeSegment(key, property.MinInclusive);
                AppendShapeSegment(key, property.MinExclusive);
                AppendShapeSegment(key, property.MaxInclusive);
                AppendShapeSegment(key, property.MaxExclusive);
                foreach (var value in property.Enumeration)
                {
                    AppendShapeSegment(key, value);
                }
            }
            return key.ToString();
        }

        private static void AppendShapeSegment(StringBuilder builder, string? value)
        {
            if (value is null)
            {
                builder.Append("-1:");
                return;
            }
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(value);
        }

        private static string GetCanonicalReferenceName(CogsModel model, IReadOnlyList<DataType> concreteTypes)
        {
            if (concreteTypes.Count == 1)
            {
                return ReferenceName(concreteTypes[0]);
            }

            string[] permittedNames = concreteTypes
                .Select(x => x.Name)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            ItemType? canonicalBase = model.ItemTypes
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .FirstOrDefault(candidate =>
                    CogsTypeSystem.ConcreteAssignableTypes(model, candidate)
                        .OfType<ItemType>()
                        .Select(x => x.Name)
                        .OrderBy(x => x, StringComparer.Ordinal)
                        .SequenceEqual(permittedNames, StringComparer.Ordinal));

            // Every multi-type reference set originates from an assignable
            // closure, so a canonical base is expected for a validated model.
            // Retain a deterministic fallback for manually constructed models.
            return canonicalBase is null
                ? AssignableReferenceName(concreteTypes.OrderBy(x => x.Name, StringComparer.Ordinal).First())
                : AssignableReferenceName(canonicalBase);
        }

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

        private static string TaggedName(DataType type) => type.Name + "__Tagged";
        private static string ReferenceName(DataType type) => type.Name + "__Reference";
        private static string AssignableReferenceName(DataType type) => type.Name + "__AssignableReference";
        private static string DefinitionRef(string name) => "#/$defs/" + name;
        private static bool IsRequired(Property property) => property.MinCardinality != "0" && !string.IsNullOrWhiteSpace(property.MinCardinality);
        private static bool IsIntegerType(string type) => type is "nonPositiveInteger" or "negativeInteger" or "long" or "int" or "nonNegativeInteger" or "unsignedLong" or "positiveInteger";
        private static bool IsNumeric(string type) => IsIntegerType(type) || type is "decimal" or "float" or "double";
        private static bool IsTemporal(string type) => type is "duration" or "dateTime" or "time" or "date" or "gYearMonth" or "gYear" or "gMonthDay" or "gDay" or "gMonth";

        private readonly record struct ItemReferenceUse(string ItemName, bool AllowsSubtypes);

        private sealed record ReferenceDefinitionPlan(
            string Name,
            IReadOnlyList<DataType> ConcreteTypes);

        private sealed class SchemaEmissionPlan
        {
            public static SchemaEmissionPlan Empty { get; } = new(
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<ItemReferenceUse, string?>(),
                Array.Empty<ReferenceDefinitionPlan>());

            public SchemaEmissionPlan(
                HashSet<string> untaggedCompositeNames,
                HashSet<string> taggedCompositeNames,
                Dictionary<ItemReferenceUse, string?> itemReferenceTargets,
                IReadOnlyList<ReferenceDefinitionPlan> referenceDefinitions)
            {
                UntaggedCompositeNames = untaggedCompositeNames;
                TaggedCompositeNames = taggedCompositeNames;
                ItemReferenceTargets = itemReferenceTargets;
                ReferenceDefinitions = referenceDefinitions;
            }

            public IReadOnlySet<string> UntaggedCompositeNames { get; }
            public IReadOnlySet<string> TaggedCompositeNames { get; }
            public IReadOnlyDictionary<ItemReferenceUse, string?> ItemReferenceTargets { get; }
            public IReadOnlyList<ReferenceDefinitionPlan> ReferenceDefinitions { get; }
        }

        public bool IsInteger(string type) => IsIntegerType(type ?? string.Empty);
        public bool IsNumber(string type) => type is not null && IsNumeric(type);
        public bool IsBoolean(string type) => string.Equals(type, "boolean", StringComparison.OrdinalIgnoreCase);
    }
}
