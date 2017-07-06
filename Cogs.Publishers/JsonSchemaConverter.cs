using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Cogs.Publishers
{
    internal class JsonSchemaConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(List<JsonSchema>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value is List<JsonSchema> schema)
            {
                var prop_integer = new JObject();
                var prop_int_npint = new JObject();
                var prop_int_negativeint = new JObject();
                var prop_num_long = new JObject();
                var prop_num_int = new JObject();
                var prop_num_nonnegativeint = new JObject();
                var prop_num_ulong = new JObject();
                var prop_num_pveint = new JObject();
                //Integer
                prop_integer.Add(new JProperty("type", "integer"));
                //nonpositive Integer
                prop_int_npint.Add(new JProperty("type", "integer"));
                prop_int_npint.Add(new JProperty("maximum", 0));
                prop_int_npint.Add(new JProperty("exclusiveMaximum", false));
                //negatvie integer
                prop_int_negativeint.Add(new JProperty("type", "integer"));
                prop_int_negativeint.Add(new JProperty("maximum", 0));
                prop_int_negativeint.Add(new JProperty("exclusiveMaximum", true));
                //long 
                prop_num_long.Add(new JProperty("type", "integer"));
                prop_num_long.Add(new JProperty("minimum", -9223372036854775808));
                prop_num_long.Add(new JProperty("maximum", 9223372036854775808));
                prop_num_long.Add(new JProperty("exclusiveMinimum", false));
                prop_num_long.Add(new JProperty("exclusiveMaximum", false));
                //int 
                prop_num_int.Add(new JProperty("type", "integer"));
                prop_num_int.Add(new JProperty("minimum", -2147483648));
                prop_num_int.Add(new JProperty("maximum", 2147483648));
                prop_num_int.Add(new JProperty("exclusiveMinimum", false));
                prop_num_int.Add(new JProperty("exclusiveMaximum", false));
                //non negative integer
                prop_num_nonnegativeint.Add(new JProperty("type", "integer"));
                prop_num_nonnegativeint.Add(new JProperty("minimum", 0));
                prop_num_nonnegativeint.Add(new JProperty("exlusiveMinimum", false));
                //unsigned long 
                prop_num_ulong.Add(new JProperty("type", "integer"));
                prop_num_ulong.Add(new JProperty("minimum", 0));
                prop_num_ulong.Add(new JProperty("maximum", 18446744073709551615));
                prop_num_ulong.Add(new JProperty("exclusiveMinimum", false));
                prop_num_ulong.Add(new JProperty("exclusiveMaximum", false));
                //positive integer
                prop_num_pveint.Add(new JProperty("type", "integer"));
                prop_num_pveint.Add(new JProperty("minimum", 0));
                prop_num_pveint.Add(new JProperty("exlusiveMinimum", true));

                var obj2 = new JObject();
                foreach (var prop in schema)
                {
                    var obj = new JObject();
                    if (prop.Title == "~~reference~~")
                    {
                        obj2.Add(new JProperty("TopLevelReference",
                            new JObject(new JProperty("type", "array"),
                            new JProperty("items", new JObject(new JProperty("$ref", "#/definitions/Reference"), new JProperty("minItems", 0))))));

                    }
                    else
                    {
                        foreach (var inner_prop in prop.Properties)
                        {
                            if (inner_prop.Reference != null)
                            {
                                if (inner_prop.MultiplicityElement.MaxCardinality == "1")
                                {
                                    obj.Add(new JProperty(inner_prop.Name,
                                    new JObject(new JProperty("$ref", inner_prop.Reference),
                                            new JProperty("MultiplicityElement", (new JObject(new JProperty("lower", Convert.ToInt32(inner_prop.MultiplicityElement.MinCardinality)), new JProperty("upper", Convert.ToInt32(inner_prop.MultiplicityElement.MaxCardinality))))),
                                                    new JProperty("Description", inner_prop.Description))));
                                }
                                else
                                {
                                    obj.Add(new JProperty(inner_prop.Name,
                                    new JObject(new JProperty("type", "array"), new JProperty("items", new JObject(new JProperty("$ref", inner_prop.Reference))),
                                            new JProperty("minItems", Convert.ToInt32(inner_prop.MultiplicityElement.MinCardinality)),
                                                    new JProperty("Description", inner_prop.Description))));
                                }
                            }
                            else
                            {
                                if (inner_prop.MultiplicityElement.MaxCardinality == "1")
                                {
                                    var temp = new JObject();
                                    if(inner_prop.original_type == null )
                                    {
                                        temp = null;
                                    }
                                    else
                                    {
                                        switch (inner_prop.original_type.ToLower())
                                        {
                                            case "nonpositiveinteger":
                                                temp = prop_int_npint;
                                                break;
                                            case "negativeinteger":
                                                temp = prop_int_negativeint;
                                                break;
                                            case "int":
                                                temp = prop_num_int;
                                                break;
                                            case "nonnegativeinteger":
                                                temp = prop_num_nonnegativeint;
                                                break;
                                            case "positiveinteger":
                                                temp = prop_num_pveint;
                                                break;
                                            case "unsignedlong":
                                                temp = prop_num_ulong;
                                                break;
                                            case "long":
                                                temp = prop_num_long;
                                                break;
                                            case "integer":
                                                temp = prop_integer;
                                                break;
                                            default:
                                                temp = null;
                                                break;
                                        }
                                    }
                                    if(temp == null)
                                    {
                                        obj.Add(
                                        new JProperty(inner_prop.Name,
                                        new JObject(
                                            new JProperty("type", inner_prop.Type),
                                            new JProperty("MultiplicityElement",
                                            (new JObject(
                                                new JProperty("lower", Convert.ToInt32(inner_prop.MultiplicityElement.MinCardinality)),
                                                new JProperty("upper", Convert.ToInt32(inner_prop.MultiplicityElement.MaxCardinality))))),
                                            new JProperty("Description", inner_prop.Description))));
                                    }
                                    else
                                    {
                                        temp.Add(new JProperty("Description", inner_prop.Description));
                                        obj.Add(new JProperty(inner_prop.Name, temp));
                                    }
                                }
                                else
                                {
                                    obj.Add(
                                        new JProperty(inner_prop.Name,
                                        new JObject(
                                            new JProperty("type", "array"), 
                                            new JProperty("items", 
                                            new JObject(
                                                new JProperty("type", inner_prop.Type))),
                                            new JProperty("minItems", Convert.ToInt32(inner_prop.MultiplicityElement.MinCardinality)),
                                            new JProperty("Description", inner_prop.Description))));
                                }
                            }
                        }
                        obj2.Add(new JProperty(prop.Title, new JObject(new JProperty("type", "object"), new JProperty("patternProperties", new JObject(new JProperty(@"^(?!\s*$).+", new JObject(new JProperty("type", prop.Type), new JProperty("id", prop.Id), new JProperty("properties", obj), new JProperty("required", prop.Required))))))));
                    }
                }
                obj2.WriteTo(writer);
            }
        }
    }
}