using System;
using System.Collections.Generic;
using System.Text;

namespace Cogs.Publishers
{
    class JsonSchema
    {
        public string name;
        public string type;
        public List<JsonSchemaProp> properties = new List<JsonSchemaProp>();
    }
}
