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
        private JsonSerializerSettings settings = new JsonSerializerSettings();

        public string CogsLocation { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }

        public string TargetNamespace { get; set; } = "ddi:3_4";
        public List<DataType> ReusableStorage { get; set; }
        public List<ItemType> ItemTypeStorage { get; set; }

        public void Publish(CogsModel model)
        {
            //if (CogsLocation == null)
            //{
            //    throw new InvalidOperationException("Cogs location must be specified");
            //}
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
            settings.Converters.Add(new JsonSchemaConverter());
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            settings.DefaultValueHandling = DefaultValueHandling.Ignore;

            ReusableStorage = model.ReusableDataTypes;
            ItemTypeStorage = model.ItemTypes;
            //create a list to store jsonschema for each itemtype
            var root = new SchemaList();
            List<JsonSchema> items = new List<JsonSchema>();

            JsonSchema reference_node = new JsonSchema();
            reference_node.Title = "~~reference~~";
            items.Add(reference_node);
            List<ReusableType> define = Iteratereusable(model);

            Iterate(model, items);

            root.Schema = "http://json-schema.org/draft-04/schema#";
            root.Id = "#root";
            root.Properties = items;


            root.definitions = define;
            Console.WriteLine(JsonConvert.SerializeObject(root, settings));
            string res = JsonConvert.SerializeObject(root, settings);
            File.WriteAllText(@"C:\Users\clement\Desktop\res.json", res);
        }

        public List<ReusableType> Iteratereusable(CogsModel model)
        {
            List<ReusableType> res = new List<ReusableType>();
            foreach (var reuseabletype in model.ReusableDataTypes)
            {
                ReusableType define = new ReusableType();
                define.Name = reuseabletype.Name;
                foreach(var prop in reuseabletype.Properties)
                {
                    var temp = new JsonSchemaProp();
                    temp.MultiplicityElement = new Cardinality();
                    temp.Name = prop.Name;
                    if (IsReusableType(prop.DataType.Name))
                    {
                        temp.Reference = "#/definitions/" + prop.DataType.Name;
                    }
                    else
                    {
                        if (prop.DataType.Name == "int")
                        {
                            temp.Type = "integer";
                        }
                        else if (prop.DataType.Name == "double" || prop.DataType.Name == "decimal")
                        {
                            temp.Type = "number";
                        }
                        else
                        {
                            temp.Type = prop.DataType.Name.ToLower();
                        }
                    }
                    temp.MultiplicityElement.MinCardinality = prop.MinCardinality;
                    temp.MultiplicityElement.MaxCardinality = prop.MaxCardinality;
                    temp.Description = prop.Description;
                    define.Properties.Add(temp);
                }
                res.Add(define);
            }
            return res;
        }

        public void Iterate(CogsModel model, List<JsonSchema> items)
        {
            foreach (ItemType item in model.ItemTypes)
            {
                JsonSchema temp = new JsonSchema();
                temp.Title = item.Name;                          //get the name of the itemtype
                temp.Type = "object";                           //get the type of the itemtype which is usually Object
                temp.Id = "#" + item.Name;
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
        }

        public void SetJsonSchemaProp(JsonSchema temp, Property property)
        {
            var prop = new JsonSchemaProp();
            prop.MultiplicityElement = new Cardinality();
            prop.Name = property.Name;
            if (IsReusableType(property.DataType.Name))
            {
                prop.Reference = "#/definitions/" + property.DataType.Name;
            }
            else if (IsItemType(property.DataType.Name))
            {
                prop.Reference = "#/properties/Reference";
            }
            else
            {

                if (property.DataType.Name == "int")
                {
                    prop.Type = "integer";
                }
                else if (property.DataType.Name == "double" || property.DataType.Name == "decimal")
                {
                    prop.Type = "number";
                }
                else
                {
                    prop.Type = property.DataType.Name.ToLower();
                }
            }
            prop.MultiplicityElement.MinCardinality = property.MinCardinality;
            if (property.MinCardinality == "1")
            {
                temp.Required.Add(property.Name);
            }
            prop.MultiplicityElement.MaxCardinality = property.MaxCardinality;
            prop.Description = property.Description;
            temp.Properties.Add(prop);
        }

        public Boolean IsReusableType(string type)
        {
            foreach(var reusable in ReusableStorage)
            {
                if(type == reusable.Name)
                {
                    return true;
                }
            }
            return false;
        }

        public Boolean IsItemType(string type)
        {
            foreach(var item in ItemTypeStorage)
            {
                if(type == item.Name)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
