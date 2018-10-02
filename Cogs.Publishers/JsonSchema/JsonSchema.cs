using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cogs.Publishers.JsonSchema
{
    public class JsonSchema
    {
        public string Title { get; set; }
        public string Type { get; set; }
        public string Id { get; set; }

        public bool AddProp { get; set; }
        public List<JsonSchemaProp> Properties { get; } = new List<JsonSchemaProp>();
        public List<string> Required { get; } = new List<string>();
    }
}
