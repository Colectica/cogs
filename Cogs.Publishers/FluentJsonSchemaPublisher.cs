using Cogs.Common;
using Cogs.Model;
using Json.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cogs.Publishers.FluentJson
{
    public class FluentJsonSchemaPublisher
    {
        public string CogsLocation { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }

        public bool AdditionalProperties { get; set; }

        private CogsModel CogsModel { get; set; }

        private HashSet<string> LowerCaseSimpleTypes { get; set; }

        public void Publish(CogsModel model)
        {
            CogsModel = model;
            LowerCaseSimpleTypes = CogsTypes.SimpleTypeNames.Select(x => x.ToLower()).ToHashSet();

            if (TargetDirectory == null)
            {
                throw new InvalidOperationException("Target directory must be specified");
            }

            if (Overwrite && Directory.Exists(TargetDirectory))
            {
                Directory.Delete(TargetDirectory, true);
            }

            Directory.CreateDirectory(TargetDirectory);


            // create the schema
            var builder = new JsonSchemaBuilder();
            builder.Schema("https://json-schema.org/draft/2020-12/schema");
            builder.Comment(CogsModel.Settings.Title);

            // create the simple types
            var defs = SimpleTypeDefinitions();

            // create the datatypes and itemtypes
            foreach (var item in model.ReusableDataTypes)
            {
                //if (item.IsAbstract) { continue; } // json schema doesn't do inheritance

                var dataTypeDefSchema = GetJsonSchema(item);
                defs[item.Name] = dataTypeDefSchema;
            }

            foreach (var item in model.ItemTypes)
            {
                //if (item.IsAbstract) { continue; } // json schema doesn't do inheritance

                var dataTypeDefSchema = GetJsonSchema(item);
                defs[item.Name] = dataTypeDefSchema;
            }

            builder.Defs(defs);

            // create the top level reference and flat item container
            var container = new Dictionary<string, Json.Schema.JsonSchemaBuilder>();

            var topLevel = new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Ref("#/$defs/reference")).MinItems(0);
            container["topLevelReferences"] = topLevel;

            var itemAlternatives = new List<Json.Schema.JsonSchemaBuilder>();
            foreach (var item in model.ItemTypes)
            {
                if (item.IsAbstract) { continue; }

                itemAlternatives.Add(new JsonSchemaBuilder().Ref($"#/$defs/{item.Name}"));
            }

            container["items"] = new JsonSchemaBuilder()
                .Type(SchemaValueType.Array)
                .Items(new JsonSchemaBuilder().AnyOf(itemAlternatives))
                .MinItems(0);

            builder.Type(SchemaValueType.Object)
                .Properties(container)
                .Required("items")
                .AdditionalProperties(false);

            var schema = builder.Build();

            var outputFile = Path.Combine(TargetDirectory, "jsonSchema.json");
            var output = JsonSerializer.Serialize(schema, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(outputFile, output, Encoding.UTF8);
        }

        public Json.Schema.JsonSchemaBuilder GetJsonSchema(DataType datatype)
        {
            var builder = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Description(datatype.Description);

            var properties = new List<Property>();

            // This includes inherited properties inline
            /*            
            foreach (var parent in datatype.ParentTypes)
            {
                properties.AddRange(parent.Properties);
            }            
            */

            // This includes inherited properties using AllOf and refs            
            var parent = datatype.ParentTypes.Where(x => x.Name == datatype.ExtendsTypeName).FirstOrDefault();
            if (parent != null)
            {
                builder.AllOf(new JsonSchemaBuilder().Ref($"#/$defs/{parent.Name}"));
            }
            
            properties.AddRange(datatype.Properties);
            var jsonProperties = new Dictionary<string, Json.Schema.JsonSchemaBuilder>();

            if (ShouldAddTypeDiscriminator(datatype))
            {
                jsonProperties["$type"] = BuildTypeDiscriminatorSchema(datatype);
            }

            foreach (var property in properties)
            {
                var propBuilder = GetBuilderForProperty(property);
                jsonProperties[property.Name] = propBuilder;
            }            

            builder.Properties(jsonProperties);

            var required = properties.Where(x => x.MinCardinality != "0").Select(x => x.Name).ToList();
            if (ShouldAddTypeDiscriminator(datatype))
            {
                required.Insert(0, "$type");
            }
            builder.Required(required);

            return builder;
        }
        public JsonSchemaBuilder GetBuilderForProperty(Property property)
        {
            var builder = new JsonSchemaBuilder();
            if (property.MaxCardinality != "1")
            {
                builder.Type(SchemaValueType.Array);
                var typeBuilder = GetBuilderForType(property);
                builder.Items(typeBuilder);

                if(uint.TryParse(property.MinCardinality, out uint minCardinatlity))
                {
                    builder.MinItems(minCardinatlity);
                }
                if (uint.TryParse(property.MaxCardinality, out uint maxCardinatlity))
                {
                    builder.MaxItems(maxCardinatlity);
                }
            }
            else
            {
                builder = GetBuilderForType(property);
            }
            if(!string.IsNullOrWhiteSpace(property.Pattern))
            {
                try
                {
                    builder.Pattern(property.Pattern);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                }
                
            }

            builder.Description(property.Description);
            return builder;
        }

        public JsonSchemaBuilder GetBuilderForType(Property property)
        {
            var dataTypeName = property.DataTypeName;
            if (CogsModel.ItemTypes.Any(t => t.Name == dataTypeName))
            {
                return new JsonSchemaBuilder().Ref($"#/$defs/reference");
            }
            if(dataTypeName.ToLowerInvariant() == "string")
            {
                return new JsonSchemaBuilder().Type(SchemaValueType.String);
            }
            else if (IsInteger(dataTypeName))
            {
                 return new JsonSchemaBuilder().Type(SchemaValueType.Integer);
            }
            else if (IsNumber(dataTypeName))
            {
                return new JsonSchemaBuilder().Type(SchemaValueType.Number);
            }
            else if (IsBoolean(dataTypeName))
            {
                return new JsonSchemaBuilder().Type(SchemaValueType.Boolean);
            }
            else
            {
                // if a composite type, check to see if it has any subclasses and allow any of those
                if (property.AllowSubtypes)
                {
                    var dataType = property.DataType;
                    var subTypes = CogsModel.ReusableDataTypes.Where(t => t.ParentTypes.Contains(dataType)).ToList();
                    if (subTypes.Count > 0)
                    {
                        var anyOfBuilders = new List<Json.Schema.JsonSchemaBuilder>();
                        if(dataType.IsAbstract == false)
                        {
                            anyOfBuilders.Add(new JsonSchemaBuilder().Ref($"#/$defs/{dataTypeName}"));
                        }
                        
                        foreach (var subType in subTypes)
                        {
                            anyOfBuilders.Add(new JsonSchemaBuilder().Ref($"#/$defs/{subType.Name}"));
                        }
                        return new JsonSchemaBuilder().AnyOf(anyOfBuilders);
                    }
                }
                return new JsonSchemaBuilder().Ref($"#/$defs/{dataTypeName}");
            }
        }

        private Dictionary<string, Json.Schema.JsonSchemaBuilder> SimpleTypeDefinitions()
        {
            const string timezonePattern = "^(Z)|((\\+|\\-)(00|0[0-9]|1[0-9]|2[0-3]):([0-9]|[0-5][0-9]))$";

            var results = new Dictionary<string, Json.Schema.JsonSchemaBuilder>();
            results.Add("duration", new JsonSchemaBuilder().Type(SchemaValueType.Number).Format("utc-millisec"));
            results.Add("dateTime", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.DateTime));
            results.Add("time", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Time));
            results.Add("date", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date));
            results.Add("nonNegativeInteger", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0));
            results.Add("gYearMonth", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("Year", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("Month", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("Timezone", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern(timezonePattern))
                ).Required("Year", "Month").AdditionalProperties(false));
            results.Add("gYear", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("Year", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("Timezone", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern(timezonePattern))
                ).Required("Year").AdditionalProperties(false));
            results.Add("gMonthDay", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("Month", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("Day", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("Timezone", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern(timezonePattern))
                ).Required("Month", "Day").AdditionalProperties(false));
            results.Add("gDay", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("Day", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("Timezone", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern(timezonePattern))
                ).Required("Day").AdditionalProperties(false));
            results.Add("gMonth", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("Month", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("Timezone", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern(timezonePattern))
                ).Required("Month").AdditionalProperties(false));

            results.Add("anyURI", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Uri));
            results.Add("cogsDate", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("DateTime", new JsonSchemaBuilder().Ref("#/$defs/dateTime")),
                    ("Date", new JsonSchemaBuilder().Ref("#/$defs/date")),
                    ("GYearMonth", new JsonSchemaBuilder().Ref("#/$defs/gYearMonth")),
                    ("GYear", new JsonSchemaBuilder().Ref("#/$defs/gYear")),
                    ("Duration", new JsonSchemaBuilder().Ref("#/$defs/duration"))
                ).AdditionalProperties(false));
            results.Add("language", new JsonSchemaBuilder().Type(SchemaValueType.String));
            results.Add("langString", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("LanguageTag", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                    ("Value", new JsonSchemaBuilder().Type(SchemaValueType.String))
                ).Required("LanguageTag", "Value").AdditionalProperties(false));

            results.Add("reference", BuildReferenceSchema());
            return results;
        }

        private Json.Schema.JsonSchemaBuilder BuildReferenceSchema()
        {
            var properties = new Dictionary<string, Json.Schema.JsonSchemaBuilder>
            {
                ["$type"] = BuildItemTypeSchema()
            };

            foreach (var property in CogsModel.Identification)
            {
                properties[property.Name] = GetBuilderForProperty(property);
            }

            var required = new List<string> { "$type" };
            required.AddRange(CogsModel.Identification.Select(x => x.Name));

            return new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(properties)
                .Required(required)
                .AdditionalProperties(false);
        }

        private JsonSchemaBuilder BuildItemTypeSchema(string? exactItemType = null)
        {
            var builder = new JsonSchemaBuilder().Type(SchemaValueType.String);
            if (!string.IsNullOrWhiteSpace(exactItemType))
            {
                return builder.Enum(exactItemType);
            }

            var concreteItemTypes = CogsModel.ItemTypes
                .Where(x => !x.IsAbstract)
                .Select(x => x.Name)
                .ToList();

            if (concreteItemTypes.Count > 0)
            {
                builder.Enum(concreteItemTypes);
            }

            return builder;
        }

        private JsonSchemaBuilder BuildTypeDiscriminatorSchema(DataType dataType)
        {
            var concreteTypes = GetDiscriminatorTypes(dataType)
                .Select(x => x.Name)
                .ToList();

            var builder = new JsonSchemaBuilder().Type(SchemaValueType.String);
            if (concreteTypes.Count > 0)
            {
                return builder.Enum(concreteTypes);
            }

            return builder;
        }

        private IEnumerable<DataType> GetDiscriminatorTypes(DataType dataType)
        {
            IEnumerable<DataType> scope;
            if (dataType is ItemType)
            {
                scope = CogsModel.ItemTypes;
            }
            else
            {
                scope = CogsModel.ReusableDataTypes;
            }

            return scope.Where(x => !x.IsAbstract && (x == dataType || HasAncestor(x, dataType)));
        }

        private static bool HasAncestor(DataType dataType, DataType ancestor)
        {
            foreach (var parent in dataType.ParentTypes)
            {
                if (parent == ancestor || HasAncestor(parent, ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldAddTypeDiscriminator(DataType dataType)
        {
            if (dataType is ItemType itemType)
            {
                return !itemType.IsAbstract;
            }

            return dataType.IsSubstitute;
        }

        public bool IsInteger(string type)
        {
            type = type.ToLower();
            return type == "integer"
                || type == "nonpositiveinteger"
                || type == "negativeinteger"
                || type == "int"
                || type == "nonnegativeinteger"
                || type == "positiveinteger"
                || type == "unsignedlong"
                || type == "long";
        }

        public bool IsNumber(string type)
        {
            type = type.ToLower();
            return type == "float" || type == "double" || type == "decimal";
        }
        public bool IsBoolean(string type)
        {
            type = type.ToLower();
            return type == "bool" || type == "boolean";
        }
    }

            
}
