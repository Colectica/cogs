// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Text;

namespace Cogs.Model
{
    public class Property
    {
        public string Name { get; set; }

        public string DataTypeName { get; set; }
        public DataType DataType { get; set; }

        public string MinCardinality { get; set; }
        public string MaxCardinality { get; set; }

        public string Description { get; set; }

        public string DeprecatedNamespace { get; set; }
        public string DeprecatedElementOrAttribute { get; set; }
        public string DeprecatedChoiceGroup { get; set; }
        public bool IsPrimitive
        {
            get
            {
                // temporary until model contains IsPrimitive definition
                if (DataTypeName.ToLower().Contains("string") || DataTypeName.ToLower().Contains("date") || DataTypeName.ToLower().Contains("codeValue"))
                {
                    return true;
                }
                return false;
            }
        }

        public override string ToString()
        {
            return $"{Name} - {DataType} - {MinCardinality}..{MaxCardinality}";
        }
    }
}
