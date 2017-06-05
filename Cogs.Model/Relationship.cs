// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cogs.Model
{
    public class Relationship
    {
        public string PropertyName { get; set; }
        public DataType TargetItemType { get; set; }
    }
}
