using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cogs.Publishers
{
    public class JsonSchema
    {
        public string Title { get; set; }
        public string Type { get; set; }
        [JsonConverter(typeof(JsonScehmaPropConverter))]
        public List<JsonSchemaProp> Properties { get; } = new List<JsonSchemaProp>();
        public List<string> Required { get; } = new List<string>();
    }
}
