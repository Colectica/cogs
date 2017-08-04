using System;
using System.Collections.Generic;
using System.Text;

namespace Cogs.Model
{
    public class Settings
    {
        public string Title { get; set; }
        public string ShortTitle { get; set; }
        public string Slug { get; set; }
        public string Description { get; set; }
        public string NamespaceUrl { get; set; }
        public string NamespacePrefix { get; set; }

        public Dictionary<string, string> ExtraSettings { get; } = new Dictionary<string, string>();
    }
}
