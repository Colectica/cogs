using System;
using System.Collections.Generic;
using System.Text;
using CsvHelper.Configuration.Attributes;

namespace Cogs.Dto
{
    public class Setting
    {
        [Ignore]
        public string SourcePath { get; set; }
        [Ignore]
        public int? SourceLine { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
    }
}
