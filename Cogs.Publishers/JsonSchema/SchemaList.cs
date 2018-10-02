using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cogs.Publishers.JsonSchema
{
    class SchemaList
    {
        public string Schema { get; set; }
        public string Id { get; set; }
        [JsonConverter(typeof(JsonSimpleConverter))]
        public string SimpleType { get; set; }
        [JsonConverter(typeof(JsonReuseableConverter))]
        public List<ReusableType> definitions { get; set; }
        [JsonConverter(typeof(JsonSchemaConverter))]
        public List<JsonSchema> Properties { get; set; }
    }
}
