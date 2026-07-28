// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Text;
using CsvHelper.Configuration.Attributes;

namespace Cogs.Dto
{
    public class Property
    {
        [Ignore]
        public string SourcePath { get; set; } = "";

        [Ignore]
        public int? SourceLine { get; set; }

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
        public string MinInclusive { get; set; } = "";
        public string MinExclusive { get; set; } = "";
        public string MaxInclusive { get; set; } = "";
        public string MaxExclusive { get; set; } = "";

        public string DeprecatedNamespace { get; set; } = "";
        public string DeprecatedElementOrAttribute { get; set; } = "";
        public string DeprecatedChoiceGroup { get; set; } = "";

        public override string ToString()
        {
            return $"{Name} - {DataType} - {MinCardinality}..{MaxCardinality}";
        }
    }
}
