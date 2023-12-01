// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cogs.Model
{
    public class CogsModel
    {
        public List<ItemType> ItemTypes { get; } = new List<ItemType>();
        public List<DataType> ReusableDataTypes { get; } = new List<DataType>();
        public List<TopicIndex> TopicIndices { get; } = new List<TopicIndex>();

        public List<Property> Identification { get; } = new List<Property>();
        public Settings Settings { get; set; }
        public string HeaderInclude { get; set; }

        public string ArticlesPath { get; set; }
        public List<string> ArticleTocEntries { get; } = new List<string>();
    }
}
