using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Cogs.Publishers.JsonSchema
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
                var obj2 = new JObject();
                foreach (var prop in schema)
                {
                    var obj = new JObject();
                    if (prop.Title == "~~reference~~")
                    {
                        obj2.Add(new JProperty("topLevelReference",
                            new JObject(new JProperty("type", "array"),
                            new JProperty("items", new JObject(new JProperty("$ref", "#/definitions/reference"), new JProperty("minItems", 0))))));

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
                                                temp = CreateNonPositiveInteger();
                                                break;
                                            case "negativeinteger":
                                                temp = CreateNegativeInteger();
                                                break;
                                            case "int":
                                                temp = CreateInt();
                                                break;
                                            case "nonnegativeinteger":
                                                temp = CreateNonNegativeInteger();
                                                break;
                                            case "positiveinteger":
                                                temp = CreatePositiveInteger();
                                                break;
                                            case "unsignedlong":
                                                temp = CreateUnsignedLong();
                                                break;
                                            case "long":
                                                temp = CreateLong();
                                                break;
                                            case "integer":
                                                temp = CreateInteger();
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
                        var inner_object = new JObject();
                        inner_object.Add(new JProperty("type", "object"));
                        var inner_inner_object = new JObject();
                        inner_inner_object.Add(new JProperty("type", prop.Type));
                        inner_inner_object.Add(new JProperty("id", prop.Id));
                        inner_inner_object.Add(new JProperty("properties", obj));
                        inner_inner_object.Add(new JProperty("required", prop.Required));
                        if (!prop.AddProp)
                        {
                            inner_inner_object.Add(new JProperty("additionalProperties", false));
                        }
                        inner_object.Add(new JProperty("patternProperties", new JObject(new JProperty(@"^(?!\s*$).+", inner_inner_object))));
                        obj2.Add(new JProperty(prop.Title,  inner_object));
                    }
                }
                obj2.WriteTo(writer);
            }
        }
        //Integer
        private JObject CreateInteger()
        {
            var prop_integer = new JObject();
            prop_integer.Add(new JProperty("type", "integer"));
            return prop_integer;
        }
        //nonpositive Integer
        private JObject CreateNonPositiveInteger()
        {
            var prop_int_npint = new JObject();
            prop_int_npint.Add(new JProperty("type", "integer"));
            prop_int_npint.Add(new JProperty("maximum", 0));
            prop_int_npint.Add(new JProperty("exclusiveMaximum", false));
            return prop_int_npint;
        }
        //negatvie integer
        private JObject CreateNegativeInteger()
        {
            var prop_int_negativeint = new JObject();
            prop_int_negativeint.Add(new JProperty("type", "integer"));
            prop_int_negativeint.Add(new JProperty("maximum", 0));
            prop_int_negativeint.Add(new JProperty("exclusiveMaximum", true));
            return prop_int_negativeint;
        }
        //long 
        private JObject CreateLong()
        {
            var prop_num_long = new JObject();
            prop_num_long.Add(new JProperty("type", "integer"));
            prop_num_long.Add(new JProperty("minimum", -9223372036854775808));
            prop_num_long.Add(new JProperty("maximum", 9223372036854775808));
            prop_num_long.Add(new JProperty("exclusiveMinimum", false));
            prop_num_long.Add(new JProperty("exclusiveMaximum", false));
            return prop_num_long;
        }
        //int 
        private JObject CreateInt()
        {
            var prop_num_int = new JObject();
            prop_num_int.Add(new JProperty("type", "integer"));
            prop_num_int.Add(new JProperty("minimum", -2147483648));
            prop_num_int.Add(new JProperty("maximum", 2147483648));
            prop_num_int.Add(new JProperty("exclusiveMinimum", false));
            prop_num_int.Add(new JProperty("exclusiveMaximum", false));
            return prop_num_int;
        }
        //non negative integer
        private JObject CreateNonNegativeInteger()
        {
            var prop_num_nonnegativeint = new JObject();
            prop_num_nonnegativeint.Add(new JProperty("type", "integer"));
            prop_num_nonnegativeint.Add(new JProperty("minimum", 0));
            prop_num_nonnegativeint.Add(new JProperty("exlusiveMinimum", false));
            return prop_num_nonnegativeint;
        }
        //unsigned long 
        private JObject CreateUnsignedLong() {
            var prop_num_ulong = new JObject();
            prop_num_ulong.Add(new JProperty("type", "integer"));
            prop_num_ulong.Add(new JProperty("minimum", 0));
            prop_num_ulong.Add(new JProperty("maximum", 18446744073709551615));
            prop_num_ulong.Add(new JProperty("exclusiveMinimum", false));
            prop_num_ulong.Add(new JProperty("exclusiveMaximum", false));
            return prop_num_ulong;
        }
        //positive integer
        private JObject CreatePositiveInteger()
        {
            var prop_num_pveint = new JObject();
            prop_num_pveint.Add(new JProperty("type", "integer"));
            prop_num_pveint.Add(new JProperty("minimum", 0));
            prop_num_pveint.Add(new JProperty("exlusiveMinimum", true));
            return prop_num_pveint;
        }
    }
}