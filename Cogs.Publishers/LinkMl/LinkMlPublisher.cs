using Cogs.Common;
using Cogs.Model;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;

namespace Cogs.Publishers.LinkMl
{
    public class LinkMlModel{
        public string id { get; set; }
        public string name { get; set; }
        public Dictionary<string, string> prefixes { get; set; }
        public string[] imports { get; set; }
        public string default_range { get; set; }
        public Dictionary<string, LinkMlClass> classes  { get; set; }
        public LinkMlModel(){
            prefixes = new Dictionary<string, string>();
            prefixes.Add("linkml", "https://w3id.org/linkml/");
            imports = new string[]{"linkml:types"};
        }
    }   
    public class LinkMlClass {
        public string description { get; set; }
        public string is_a   { get; set; }
        public Dictionary<string, LinkMlAttribute> attributes  { get; set; }
        public LinkMlClass(){
            attributes = new Dictionary<string, LinkMlAttribute>();
        }
    }

    public class LinkMlAttribute{
        public string description { get; set; }
        public string range   { get; set; }
        public int? minimum_value { get; set; }
        public bool? multivalued { get; set; }
    }
    public class LinkMlPublisher
    {
        public string Name { get; set; }
        public string NamespaceUriPrefix { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }
 
        public void Publish(CogsModel model)
        {
            if (Overwrite && Directory.Exists(TargetDirectory))
            {
                Directory.Delete(TargetDirectory, true);
            }
            Directory.CreateDirectory(TargetDirectory);

            var classes = new Dictionary<string, LinkMlClass>();

            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                var linkMlClass = new LinkMlClass{
                    description = item.Description
                };

                if(item.ExtendsTypeName.Length > 0){
                    linkMlClass.is_a = item.ExtendsTypeName;
                }

                foreach (var prop in item.Properties)
                {
                    var attr = new LinkMlAttribute{
                        description = prop.Description,
                        range = prop.DataType.Name
                    };
                    if(prop.MaxCardinality.Equals("n")){
                        attr.multivalued = true;
                    }
                    linkMlClass.attributes.Add(prop.Name, attr);
                }
                classes.Add(item.Name, linkMlClass);
            }

            var linkml = new LinkMlModel{
                default_range = "string",
                id = NamespaceUriPrefix,
                name = "model",
                classes = classes
            };

            var serializer = new SerializerBuilder()
                                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitEmptyCollections)
                                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                                    .Build();
            var yaml = serializer.Serialize(linkml);
            
            File.WriteAllText(Path.Combine(TargetDirectory, "linkml.yml"), yaml);
        }

    }
}