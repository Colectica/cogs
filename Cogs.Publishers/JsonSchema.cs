using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cogs.Publishers
{
    class JsonSchema
    {
        public string title;
        public string type;
        [JsonConverter(typeof(JsonScehmaPropConverter))]
        public List<JsonSchemaProp> properties { get; set; }
    }
}
