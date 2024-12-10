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
                if (item.IsAbstract) { continue; } // json schema doesn't do inheritance

                var dataTypeDefSchema = GetJsonSchema(item);
                defs[item.Name] = dataTypeDefSchema;
            }

            foreach (var item in model.ItemTypes)
            {
                if (item.IsAbstract) { continue; } // json schema doesn't do inheritance

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
            var properties = new List<Property>();

            foreach (var parent in datatype.ParentTypes)
            {
                properties.AddRange(parent.Properties);
            }
            properties.AddRange(datatype.Properties);

            var jsonProperties = new Dictionary<string, Json.Schema.JsonSchema>();
            foreach (var property in properties)
            {
                var propBuilder = GetBuilderForProperty(property);
                jsonProperties[property.Name] = propBuilder;
            }            

            var builder = new JsonSchemaBuilder().Properties(jsonProperties);

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
                var typeBuilder = GetBuilderForType(property.DataTypeName);
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
                builder = GetBuilderForType(property.DataTypeName);
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

        public JsonSchemaBuilder GetBuilderForType(string dataTypeName)
        {
            if(CogsModel.ItemTypes.Any(t => t.Name == dataTypeName))
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
                    ("year", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("month", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("timezone", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern(timezonePattern))
                ).Required("year", "month").AdditionalProperties(false));
            results.Add("gYear", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("year", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("timezone", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern(timezonePattern))
                ).Required("year").AdditionalProperties(false));
            results.Add("gMonthDay", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("month", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("day", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("timezone", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern(timezonePattern))
                ).Required("month", "day").AdditionalProperties(false));
            results.Add("gDay", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("day", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("timezone", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern(timezonePattern))
                ).Required("day").AdditionalProperties(false));
            results.Add("gMonth", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("month", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                    ("timezone", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern(timezonePattern))
                ).Required("month").AdditionalProperties(false));

            results.Add("anyURI", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Uri));
            results.Add("cogsDate", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("dateTime", new JsonSchemaBuilder().Ref("#/$defs/dateTime")),
                    ("date", new JsonSchemaBuilder().Ref("#/$defs/date")),
                    ("gYearMonth", new JsonSchemaBuilder().Ref("#/$defs/gYearMonth")),
                    ("gYear", new JsonSchemaBuilder().Ref("#/$defs/gYear")),
                    ("duration", new JsonSchemaBuilder().Ref("#/$defs/duration"))
                ).AdditionalProperties(false));
            results.Add("language", new JsonSchemaBuilder().Type(SchemaValueType.String));
            results.Add("langString", new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(
                    ("languageTag", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                    ("value", new JsonSchemaBuilder().Type(SchemaValueType.String))
                ).Required("languageTag", "value").AdditionalProperties(false));

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
