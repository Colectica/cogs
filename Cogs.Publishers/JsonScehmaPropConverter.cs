using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Cogs.Publishers
{
    internal class JsonScehmaPropConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(List<JsonSchemaProp>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is List<JsonSchemaProp> prop_list)
            {
                var obj = new JObject();
                foreach (var prop in prop_list)
                {
                    obj.Add(new JProperty(prop.Name,
                        new JObject(new JProperty("type", prop.Type), new JProperty("minCardinality", prop.MinCardinality), new JProperty("maxCardinality", prop.MaxCardinality), new JProperty("Description", prop.Description))));
                }
                obj.WriteTo(writer);
            }
        }
    }
}