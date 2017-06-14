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
                    var obj2 = new JObject();
                    foreach(var prop in reuse.Properties)
                    {
                        obj2.Add(new JProperty(prop.Name,
                            new JObject(new JProperty("type", prop.Type), new JProperty("$ref", prop.Reference), new JProperty("minCardinality", prop.MinCardinality), new JProperty("maxCardinality", prop.MaxCardinality), new JProperty("Description", prop.Description))));
                    }
                    obj.Add(new JProperty(reuse.Name, obj2));
                }
                obj.WriteTo(writer);
            }
        }
    }
}