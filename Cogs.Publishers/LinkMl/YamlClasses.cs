using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Cogs.Publishers.LinkMl
{
    public class LinkMLModel
    {
        public string id { get; set; }
        public string name { get; set; }
        public Dictionary<string, string> prefixes { get; set; } = new Dictionary<string, string>();
        public string[] imports { get; set; } = new string[] { "linkml:types" };
        public string default_range { get; set; }
        public string default_prefix { get; set; }
        public Dictionary<string, LinkMLClass> classes { get; set; }
        public Dictionary<string, LinkMLSlot> slots { get; set; }

        public Dictionary<string, LinkMLType> types { get; set; }

        public LinkMLModel()
        {
            prefixes.Add("linkml", "https://w3id.org/linkml/");
        }
    }
    public class LinkMLClass
    {
        public string description { get; set; }
        public string is_a { get; set; }

        [YamlMember(Alias = "abstract")]
        public bool IsAbstract { get; set; }
        public Dictionary<string, LinkMLSlot> slot_usage { get; set; } = new Dictionary<string, LinkMLSlot>();
        public List<string> slots { get; set; } = new List<string>();
        public Dictionary<string, LinkMLUniqueKeySlots> unique_keys { get; set; } = new Dictionary<string, LinkMLUniqueKeySlots>();


        public LinkMLClass()
        {
        }
    }

    public class LinkMLSlot
    {
        public string description { get; set; }
        public string range { get; set; }
        public int? minimum_value { get; set; }
        public int? maximum_value { get; set; }

        public bool required { get; set; }
        public bool? multivalued { get; set; }
        public bool? inlined_as_list { get; set; }
        public bool? list_elements_ordered { get; set; }
    }

    public class LinkMLType
    {
        public string description { get; set; }
        public string type_uri { get; set; }
        public string uri { get; set; }

        [YamlMember(Alias = "base")]
        public string PythonType { get; set; } = "string";
        public List<string> union_of { get; set; }
    }

    public class LinkMLUniqueKeySlots
    {
        public List<string> unique_key_slots { get; set; } = new List<string>();
    }
}
