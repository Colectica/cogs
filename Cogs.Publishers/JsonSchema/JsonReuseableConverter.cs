using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Cogs.Publishers.JsonSchema
{
    internal class JsonReuseableConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(List<ReusableType>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if(value is List<ReusableType> define)
            {
                var obj = new JObject();
                foreach(var reuse in define)
                {
                    if (reuse.Name == "~~reference~~")
                    {   
                        var referenceBody = 
                            new JObject(
                                new JProperty("type", "object"),
                                new JProperty("properties", 
                                    new JObject(
                                        new JProperty("$type", 
                                            new JObject(new JProperty("type", "string"))), 
                                        new JProperty("value", new JObject(new JProperty("type", "array"), 
                                        new JProperty("items", new JObject(new JProperty("type", "string"))))))));
                                                
                        if(!reuse.AddProp)
                        {
                            referenceBody.Add(new JProperty("additionalProperties", false));
                        }

                        var referenceHead = new JProperty("reference", referenceBody);
                        obj.Add(referenceHead);
                    }
                    else
                    {
                        var obj2 = new JObject();
                        foreach (var prop in reuse.Properties)
                        {
                           if (prop.Reference != null)
                            {
                                if (prop.MultiplicityElement.MaxCardinality == "1")
                                {
                                    obj2.Add(new JProperty(prop.Name,
                                    new JObject(new JProperty("$ref", prop.Reference),
                                            new JProperty("MultiplicityElement", (new JObject(new JProperty("lower", Convert.ToInt32(prop.MultiplicityElement.MinCardinality)), new JProperty("upper", Convert.ToInt32(prop.MultiplicityElement.MaxCardinality))))),
                                                    new JProperty("Description", prop.Description))));
                                }
                                else
                                {
                                    obj2.Add(new JProperty(prop.Name,
                                    new JObject(new JProperty("type", "array"), new JProperty("items", new JObject(new JProperty("$ref", prop.Reference))),
                                            new JProperty("minItems", Convert.ToInt32(prop.MultiplicityElement.MinCardinality)),
                                                    new JProperty("Description", prop.Description))));
                                }
                            }
                            else
                            {
                                if (prop.MultiplicityElement.MaxCardinality == "1")
                                {
                                    obj2.Add(new JProperty(prop.Name,
                                    new JObject(new JProperty("type", prop.Type),
                                            new JProperty("MultiplicityElement", (new JObject(new JProperty("lower", Convert.ToInt32(prop.MultiplicityElement.MinCardinality)), new JProperty("upper", Convert.ToInt32(prop.MultiplicityElement.MaxCardinality))))),
                                                    new JProperty("Description", prop.Description))));
                                }
                                else
                                {
                                    obj2.Add(new JProperty(prop.Name,
                                    new JObject(new JProperty("type", "array"), new JProperty("items", new JObject(new JProperty("type", prop.Type))),
                                            new JProperty("minItems", Convert.ToInt32(prop.MultiplicityElement.MinCardinality)),
                                                    new JProperty("Description", prop.Description))));
                                }
                            }
                        }
                        var inner_object = new JObject();
                        inner_object.Add(new JProperty("type", "object"));
                        inner_object.Add(new JProperty("properties", obj2));
                        if (!reuse.AddProp)
                        {
                            inner_object.Add(new JProperty("additionalProperties", false));
                        }
                        obj.Add(new JProperty(reuse.Name, inner_object));
                    }
                }
                obj.WriteTo(writer);
            }
        }
    }
}