using Cogs.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
            
            //create a list to store jsonschema for each itemtype
            List<JsonSchema> items = new List<JsonSchema>();

            foreach (ItemType item in model.ItemTypes)
            {
                JsonSchema temp = new JsonSchema();
                temp.name = item.Name;                          //get the name of the itemtype
                temp.type = "Object";                           //get the type of the itemtype which is usually Object
                var valStrings = new List<string>();            //get the properties of the item types
                foreach(var property in item.Properties)
                {
                    var prop = new JsonSchemaProp();
                    prop.name = property.Name;
                    prop.type = "int";
                    temp.properties.Add(prop);
                }
                items.Add(temp);
            }
            foreach(JsonSchema schema in items)
            {
                Console.WriteLine(JsonConvert.SerializeObject(schema, settings));
            }
            //Console.WriteLine(JsonConvert.SerializeObject(item, settings));
        }
    }
}
