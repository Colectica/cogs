using Cogs.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Cogs.Publishers
{
    public class JsonPublisher
    {
        private JsonSerializerSettings settings= new JsonSerializerSettings();

        public string CogsLocation { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }

        public string TargetNamespace { get; set; } = "ddi:3_4";

        //Dictionary<string, string> createdElements = new Dictionary<string, string>();

        public void Publish(CogsModel model)
        {
            if (CogsLocation == null)
            {
                throw new InvalidOperationException("Cogs location must be specified");
            }
            if (TargetDirectory == null)
            {
                throw new InvalidOperationException("Target directory must be specified");
            }

            if (Overwrite && Directory.Exists(TargetDirectory))
            {
                Directory.Delete(TargetDirectory, true);
            }

            Directory.CreateDirectory(TargetDirectory);
            settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            settings.Formatting = Formatting.Indented;
            settings.Converters.Add(new JsonScehmaPropConverter());
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            settings.DefaultValueHandling = DefaultValueHandling.Ignore;

            //create a list to store jsonschema for each itemtype
            List<JsonSchema> items = new List<JsonSchema>();

            //foreach (ItemType item in model.ItemTypes)
            //{
            //   Console.WriteLine(JsonConvert.SerializeObject(item, settings));
            //}
            foreach (ItemType item in model.ItemTypes)
            {
                JsonSchema temp = new JsonSchema();
                temp.Title = item.Name;                          //get the name of the itemtype
                temp.Type = "object";                           //get the type of the itemtype which is usually Object
                if (item.ExtendsTypeName != "")             //Check whether there it extends another class
                {
                    //get the Parent information
                    if (item.ParentTypes != null)
                    {
                        //traverse parent list, find the properties of the parents
                        foreach (var properti in item.ParentTypes)
                        {
                            if (properti.Properties != null)
                            {
                                //traverse the properties and get all the information regarding variable. 
                                foreach (var inner_prop in properti.Properties)
                                {
                                    SetJsonSchemaProp(temp, inner_prop);
                                }
                            }
                        }
                    }
                }
                foreach (var property in item.Properties)
                {
                    SetJsonSchemaProp(temp, property);
                }
                items.Add(temp);
            }
            foreach (JsonSchema schema in items)
            {
                Console.WriteLine(JsonConvert.SerializeObject(schema, settings));
            }
            //Console.WriteLine(JsonConvert.SerializeObject(item, settings));
        }

        public void SetJsonSchemaProp(JsonSchema temp, Property property)
        {
            var prop = new JsonSchemaProp();
            prop.Name = property.Name;
            prop.Type = property.DataType.Name;
            prop.MinCardinality = property.MinCardinality;
            if (property.MinCardinality == "1")
            {
                temp.Required.Add(property.Name);
            }
            prop.MaxCardinality = property.MaxCardinality;
            prop.Description = property.Description;
            temp.Properties.Add(prop);
        }
    }
}
