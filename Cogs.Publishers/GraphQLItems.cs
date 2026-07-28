using System;
using System.Collections.Generic;
using System.Text;

namespace Cogs.Publishers
{
    public class GraphQLItems
    {
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, string> Properties { get; set; } = new();
    }
}
