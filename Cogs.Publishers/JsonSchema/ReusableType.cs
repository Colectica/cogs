using System.Collections.Generic;

namespace Cogs.Publishers.JsonSchema
{
    public class ReusableType
    {
        public string Name { get; set; }
        public bool AddProp {get; set;}
        public List<JsonSchemaProp> Properties { get; } = new List<JsonSchemaProp>();
    }
}