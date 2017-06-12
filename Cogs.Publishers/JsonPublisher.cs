using Cogs.Model;
using Newtonsoft.Json;
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
                temp.title = item.Name;                          //get the name of the itemtype
                temp.type = "object";                           //get the type of the itemtype which is usually Object
                temp.properties = new List<JsonSchemaProp>();
                //temp.allOf = new List<Dictionary<string, JsonSchemaProp>>();
                foreach (var property in item.Properties)
                {
                    var prop = new JsonSchemaProp();
                    prop.name = property.Name;
                    prop.type = property.DataType.Name;
                    prop.MinCardinality = property.MinCardinality;
                    prop.MaxCardinality = property.MaxCardinality;
                    prop.Description = property.Description;
                    temp.properties.Add(prop); 
                    if (item.ExtendsTypeName != "")             //Check whether there it extends another class
                    {
                        //add allof in the json schema, to include the method and member of parent class
                        temp.allOf = new Allof();               
                        temp.allOf.Ref = "#" + item.Name;
                        temp.allOf.Properties = new List<JsonSchemaProp>();
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
                                        var parentprop = new JsonSchemaProp();
                                        parentprop.name = inner_prop.Name;
                                        parentprop.type = inner_prop.DataType.Name;
                                        parentprop.MinCardinality = inner_prop.MinCardinality;
                                        parentprop.MaxCardinality = inner_prop.MaxCardinality;
                                        parentprop.Description = inner_prop.Description;
                                        temp.allOf.Properties.Add(parentprop);
                                    }
                                }
                            }
                        }
                    }
                }
                items.Add(temp);
            }
            foreach (JsonSchema schema in items)
            {
                Console.WriteLine(JsonConvert.SerializeObject(schema, settings));
            }
            //Console.WriteLine(JsonConvert.SerializeObject(item, settings));
        }
    }
}
