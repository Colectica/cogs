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
        public string Version { get; set; }
        public string Author { get; set; }
        public string Copyright { get; set; }
        public string NamespaceUrl { get; set; }
        public string NamespacePrefix { get; set; }
        public string CSharpNamespace { get; set; }

        public Dictionary<string, string> ExtraSettings { get; } = new Dictionary<string, string>();
    }
}
