using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cogs.Publishers
{
    class SchemaList
    {
        public string Schema { get; set; }
        [JsonConverter(typeof(JsonReuseableConverter))]
        public List<ReusableType> definitions { get; set; }
        [JsonConverter(typeof(JsonSchemaConverter))]
        public List<JsonSchema> Properties { get; set; }
    }
}
