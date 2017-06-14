using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Cogs.Publishers
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
                    if(reuse.Name == "~~reference~~")
                    {
                        obj.Add(new JProperty("Reference", new JObject(new JProperty("$type", "ref"), new JProperty("value",new JArray(new JObject(new JProperty("type", "string")), new JObject(new JProperty("ID" , "number")))))));
                    } 
                    else
                    {
                        var obj2 = new JObject();
                        foreach (var prop in reuse.Properties)
                        {
                            if (prop.Reference != null)
                            {
                                obj2.Add(new JProperty(prop.Name,
                                new JObject(new JProperty("$ref", prop.Reference),
                                            new JProperty("MultiplicityElement", (new JObject(new JProperty("lower", Convert.ToInt32(prop.MultiplicityElement.MinCardinality)), new JProperty("upper", prop.MultiplicityElement.MaxCardinality)))),
                                                    new JProperty("Description", prop.Description))));
                            }
                            else
                            {
                                obj2.Add(new JProperty(prop.Name,
                                    new JObject(new JProperty("type", prop.Type),
                                            new JProperty("MultiplicityElement", (new JObject(new JProperty("lower", Convert.ToInt32(prop.MultiplicityElement.MinCardinality)), new JProperty("upper", prop.MultiplicityElement.MaxCardinality)))),
                                                    new JProperty("Description", prop.Description))));
                            }
                        }
                        obj.Add(new JProperty(reuse.Name, obj2));
                    }
                }
                obj.WriteTo(writer);
            }
        }
    }
}