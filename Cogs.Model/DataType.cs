// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cogs.Model
{
    public class DataType
    {
        public bool IsXmlPrimitive { get; set; }
        public string Path { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }

        public List<AdditionalText> AdditionalText = new List<AdditionalText>();

        public string ExtendsTypeName { get; set; }
        public List<DataType> ParentTypes { get; } = new List<DataType>();
        public List<DataType> ChildTypes { get; } = new List<DataType>();
        public bool IsSubstitute { get; set; }

        public List<Relationship> Relationships { get; } = new List<Relationship>();

        public bool IsAbstract { get; set; }
        public bool IsPrimitive { get; set; }

        public List<Property> Properties { get; set; } = new List<Property>();

        public string DeprecatedNamespace { get; set; }
        public bool IsDeprecated { get; set; }

    }
}
