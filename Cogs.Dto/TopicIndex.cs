// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cogs.Dto
{
    public class TopicIndex
    {
        public string SourcePath { get; set; }
        public string IndexSourcePath { get; set; }
        public int? SourceLine { get; set; }
        public int? SourceColumn { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> ItemTypes { get; set; } = new List<string>();
        public List<SourceTextEntry> ItemTypeSources { get; } = new List<SourceTextEntry>();
        public string ArticlesPath { get; set; }
        public List<string> ArticleTocEntries { get; } = new List<string>();
        public List<SourceTextEntry> ArticleTocEntrySources { get; } = new List<SourceTextEntry>();
    }
}
