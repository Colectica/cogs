using Newtonsoft.Json;
using System.Collections.Generic;

namespace Cogs.Publishers.JsonSchema
{
    public class JsonSchemaProp
    {
        public string Name { get; set; }
        public string Type { get; set; }
        [JsonProperty("$ref")]
        public string Reference { get; set; }

        public Cardinality MultiplicityElement { get; set; }

        //string properties
        public int Maxlength { get; set; }
        public int Minlength { get; set; }
        public string[] Enumeration { get; set; }
        public string pattern { get; set; }

        //number and integer properties
        public string original_type { get; set; }
        //public bool ExclusiveMinimum { get; set; }
        //public bool ExclusiveMaximum { get; set; }

        //public string MinCardinality { get; set; }
        //public string MaxCardinality { get; set; }

        public string Description { get; set; }
    }
}