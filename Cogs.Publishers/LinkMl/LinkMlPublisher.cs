using Cogs.Common;
using Cogs.Dto;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Core.Tokens;
using YamlDotNet.Serialization;

namespace Cogs.Publishers.LinkMl
{
    
    public class LinkMlPublisher
    {
        public string Name { get; set; }
        public string NamespaceUriPrefix { get; set; }
        public string NamespaceUri { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }
 
        public void Publish(CogsModel model)
        {
            var target = Path.Combine(TargetDirectory, "linkml.yml");

            if (Overwrite && Directory.Exists(TargetDirectory))
            {
                Directory.Delete(TargetDirectory, true);
            }
            Directory.CreateDirectory(TargetDirectory);


            // create slots
            var slots = new Dictionary<string, LinkMLSlot>();
            foreach (var item in model.ItemTypes.Union(model.ReusableDataTypes))
            {
                foreach (var property in item.Properties)
                {
                    var propertyName = property.Name.ToLowerFirstLetter();
                    if (!slots.ContainsKey(propertyName))
                    {
                        var slot = PropertyToSlot(property);
                        slots.Add(propertyName.ToLowerFirstLetter(), slot);
                    }
                }
            }

            var classes = new Dictionary<string, LinkMLClass>();
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                var linkMlClass = new LinkMLClass
                {
                    description = item.Description
                };

                if (string.IsNullOrEmpty(item.ExtendsTypeName) && item is Model.ItemType)
                {
                    var uniqueKeys = new LinkMLUniqueKeySlots();
                    uniqueKeys.unique_key_slots.AddRange(model.Identification.Select(x => x.Name.ToLowerFirstLetter()));
                    linkMlClass.unique_keys.Add("identification", uniqueKeys);
                }
                else if (item.ExtendsTypeName.Length > 0)
                {
                    linkMlClass.is_a = item.ExtendsTypeName;
                }

                if(item.IsAbstract)
                {
                    linkMlClass.IsAbstract = true;
                }

                foreach (var prop in item.Properties)
                {
                    var slot = PropertyToSlot(prop);//for shared definition

                    if (prop.MinCardinality != "0")
                    {
                        slot.required = true;
                    }
                    if(prop.MaxCardinality != "1")
                    {
                        slot.multivalued = true;
                    }

                    linkMlClass.slot_usage.Add(prop.Name.ToLowerFirstLetter(), slot);
                    linkMlClass.slots.Add(prop.Name.ToLowerFirstLetter());
                }
                classes.Add(item.Name, linkMlClass);
            }

            var types = new Dictionary<string, LinkMLType>();
            var cogsDate = new LinkMLType();
            cogsDate.description = "A union of the xsd date types";
            cogsDate.union_of.Add("date");
            cogsDate.union_of.Add("dateTime");
            cogsDate.union_of.Add("duration");
            cogsDate.union_of.Add("gYear");
            cogsDate.union_of.Add("gYearMonth");
            types.Add("cogsDate", cogsDate);

            var language = new LinkMLType();
            language.description = "A BCP47 language tag code";
            language.uri = "xsd:string";
            types.Add("language", language);

            var integer = new LinkMLType();
            integer.description = "An integer";
            integer.uri = "xsd:integer";
            types.Add("int", integer);

            var dateTime = new LinkMLType();
            dateTime.description = "A dateTime";
            dateTime.uri = "xsd:dateTime";
            types.Add("dateTime", dateTime);

            var uri = new LinkMLType();
            uri.description = "A URI";
            uri.uri = "xsd:anyURI";
            types.Add("anyURI", uri);

            var langString = new LinkMLType();
            langString.description = "A language tagged string";
            langString.uri = "rdf:langString";
            types.Add("langString", uri);

            var nonNegativeInteger = new LinkMLType();
            nonNegativeInteger.description = "A nonNegativeInteger";
            nonNegativeInteger.uri = "xsd:nonNegativeInteger";
            types.Add("nonNegativeInteger", uri);

            var modelName = "model";
            if (model.Settings.Slug != null)
            {
                modelName = model.Settings.Slug;
            }
            var linkml = new LinkMLModel
            {
                default_range = "string",
                id = NamespaceUri,
                name = modelName,
                classes = classes,
                slots = slots,
                default_prefix = NamespaceUriPrefix,
                types = types
            };
            linkml.prefixes.Add(NamespaceUriPrefix, NamespaceUri);
            linkml.prefixes.Add("rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#");



            var serializer = new SerializerBuilder()
                                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitEmptyCollections)
                                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                                    .Build();
            var yaml = serializer.Serialize(linkml);
            
            File.WriteAllText(target, yaml);
        }

        private LinkMLSlot PropertyToSlot(Model.Property property)
        {
            var slot = new LinkMLSlot
            {
                description = property.Description,
                range = property.DataType.Name
            };

            if (property.Ordered)
            {
                // https://w3id.org/linkml/list_elements_ordered Doesn't appear to change owl generation

                slot.list_elements_ordered = true;
                slot.inlined_as_list = true;
            }
            return slot;
        }
    }
}