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
                var obj2 = new JObject();
                foreach (var prop in schema)
                {
                    var obj = new JObject();
                    foreach (var inner_prop in prop.Properties)
                    {
                        obj.Add(new JProperty(inner_prop.Name,
                            new JObject(new JProperty("type", inner_prop.Type), new JProperty("$ref", inner_prop.Reference), new JProperty("minCardinality", inner_prop.MinCardinality), new JProperty("maxCardinality", inner_prop.MaxCardinality), new JProperty("Description", inner_prop.Description))));
                    }
                    obj2.Add(new JProperty(prop.Title,
                       new JObject(new JProperty("type", prop.Type), new JProperty( "property" ,obj), new JProperty("required", prop.Required))));
                }
                obj2.WriteTo(writer);
            }
        }
    }
}