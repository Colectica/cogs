using System;
using System.Collections.Generic;
using System.Text;

namespace Cogs.Publishers
{
    public class DataInformation
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string MinCardinality { get; set; }
        public string MaxCardinality { get; set; }
        public string Description { get; set; }
        public string MinLength { get; set; }
        public string MaxLength { get; set; }
        public string Enumeration { get; set; }
        public string Pattern { get; set; }
        public string MinInclusive { get; set; }
        public string MinExclusive { get; set; }
        public string MaxInclusive { get; set; }
        public string MaxExclusive { get; set; }
        public string DeprecatedNamespace { get; set; }
        public string DeprecatedElementOrAttribute { get; set; }
        public string DeprecatedChoiceGroup { get; set; }
    }
}
