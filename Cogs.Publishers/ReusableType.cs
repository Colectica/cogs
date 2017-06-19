using System.Collections.Generic;

namespace Cogs.Publishers
{
    public class ReusableType
    {
        public string Name { get; set; }
        public List<JsonSchemaProp> Properties { get; } = new List<JsonSchemaProp>();
    }
}