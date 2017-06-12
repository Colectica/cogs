using Newtonsoft.Json;
using System.Collections.Generic;

namespace Cogs.Publishers
{
    public class Allof
    {
        public string Ref {get; set;}
        [JsonConverter(typeof(JsonScehmaPropConverter))]
        public List<JsonSchemaProp> Properties { get; set; }
    }
}