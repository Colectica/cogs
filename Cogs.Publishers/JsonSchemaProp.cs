using Newtonsoft.Json;
using System.Collections.Generic;

namespace Cogs.Publishers
{
    public class JsonSchemaProp
    {
        public string Name { get; set; }
        public string Type { get; set; }
        [JsonProperty("$ref")]
        public string Reference { get; set; }

        public Cardinality MultiplicityElement { get; set; }

        public string MinCardinality { get; set; }
        public string MaxCardinality { get; set; }

        public string Description { get; set; }
    }
}