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

            // create the top level reference, item container with pattern properties
            var container = new Dictionary<string, Json.Schema.JsonSchema>();

            var topLevel = new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Ref("#/$defs/reference")).MinItems(0);
            container["topLevelReference"] = topLevel;

            foreach (var item in model.ItemTypes)
            {
                if(item.IsAbstract) { continue; }

                var itemType = new JsonSchemaBuilder().PatternProperties(("^(?!\\s*$).+", new JsonSchemaBuilder().Ref($"#/$defs/{item.Name}")));
                container[item.Name] = itemType;
            }

            builder.Properties(container).AdditionalProperties(false);

            var schema = builder.Build();

            var outputFile = Path.Combine(TargetDirectory, "jsonSchema.json");
            var output = JsonSerializer.Serialize(schema);
            File.WriteAllText(outputFile, output, Encoding.UTF8);
        }

        public Json.Schema.JsonSchema GetJsonSchema(DataType datatype)
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
            var parent = datatype.ParentTypes.FirstOrDefault();
            if (parent != null)
            {
                builder.AllOf(new JsonSchemaBuilder().Ref($"#/$defs/{parent.Name}"));
            }

            properties.AddRange(datatype.Properties);
            var jsonProperties = new Dictionary<string, Json.Schema.JsonSchema>();
            foreach (var property in properties)
            {
                var propBuilder = GetBuilderForProperty(property);
                jsonProperties[property.Name] = propBuilder;
            }            

            builder.Properties(jsonProperties);

            var required = properties.Where(x => x.MinCardinality != "0").Select(x => x.Name).ToList();
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
                        var anyOfBuilders = new List<Json.Schema.JsonSchema>();
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

        private Dictionary<string, Json.Schema.JsonSchema> SimpleTypeDefinitions()
        {
            const string timezonePattern = "^(Z)|((\\+|\\-)(00|0[0-9]|1[0-9]|2[0-3]):([0-9]|[0-5][0-9]))$";

            var results = new Dictionary<string, Json.Schema.JsonSchema>();

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

            results.Add("reference", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("$type", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                    ("value", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)))
                ).Required("$type", "value").AdditionalProperties(false));
            return results;
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
