// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Text;

namespace Cogs.Dto
{
    public class Property
    {
        public string Name { get; set; } = "";

        public string DataType { get; set; } = "";

        public string MinCardinality { get; set; } = "";
        public string MaxCardinality { get; set; } = "";

        public string Description { get; set; } = "";

        public string Ordered { get; set; } = "";
        public string AllowSubtypes { get; set; } = "";

        // simple string restrictions
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public string Enumeration { get; set; } = "";
        public string Pattern { get; set; } = "";
        // numeric restrictions
        public int? MinInclusive { get; set; }
        public int? MinExclusive { get; set; }
        public int? MaxInclusive { get; set; }
        public int? MaxExclusive { get; set; }

        public string DeprecatedNamespace { get; set; } = "";
        public string DeprecatedElementOrAttribute { get; set; } = "";
        public string DeprecatedChoiceGroup { get; set; } = "";

        public override string ToString()
        {
            return $"{Name} - {DataType} - {MinCardinality}..{MaxCardinality}";
        }
    }
}
