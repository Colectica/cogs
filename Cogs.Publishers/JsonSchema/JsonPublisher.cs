using Cogs.Common;
using Cogs.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Cogs.Publishers.JsonSchema
{
    public class JsonPublisher
    {
        private JsonSerializerSettings settings = new JsonSerializerSettings();

        public string CogsLocation { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }
        public bool AdditionalProp { get; set; }

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
            ReusableType reference_def = new ReusableType();
            reference_def.Name = "~~reference~~";

            reference_node.Title = "~~reference~~";
            items.Add(reference_node);
            List<ReusableType> define = Iteratereusable(model);
            define.Add(reference_def);

            Iterate(model, items);

            root.Schema = "http://json-schema.org/draft-04/schema#";
            root.Id = "#root";
            root.Properties = items;
            root.SimpleType = "root";
            root.definitions = define;

            if (!AdditionalProp)
            {
                foreach (var prop in root.Properties)
                {
                    prop.AddProp = false;
                }
                foreach (var prop in root.definitions)
                {
                    prop.AddProp = false;
                }
            }
            else
            {
                foreach (var prop in root.Properties)
                {
                    prop.AddProp = true;
                }
                foreach (var prop in root.definitions)
                {
                    prop.AddProp = true;
                }
            }
            //Console.WriteLine(JsonConvert.SerializeObject(root, settings));
            string res = JsonConvert.SerializeObject(root, settings);
            Directory.CreateDirectory(TargetDirectory);
            File.WriteAllText(Path.Combine(TargetDirectory, "jsonSchema" + ".json"), res);
        }

        public List<ReusableType> Iteratereusable(CogsModel model)
        {
            List<ReusableType> res = new List<ReusableType>();
            foreach (var reuseabletype in model.ReusableDataTypes)
            {
                ReusableType define = new ReusableType();
                define.Name = reuseabletype.Name.ToLowerFirstLetter();

                if (reuseabletype.IsSubstitute)
                {
                    var subTypeProperty = new JsonSchemaProp();
                    subTypeProperty.MultiplicityElement = new Cardinality() { MinCardinality = "1", MaxCardinality = "1" };
                    subTypeProperty.Name = "$type";
                    subTypeProperty.Description = "Discriminator specifies the data type name";
                    subTypeProperty.Type = "string";
                    define.Properties.Add(subTypeProperty);
                }

                foreach(var prop in reuseabletype.Properties)
                {
                    var temp = new JsonSchemaProp();
                    temp.MultiplicityElement = new Cardinality();
                    temp.Name = prop.Name.ToLowerFirstLetter();
                    if (IsReusableType(prop.DataType.Name))
                    {
                        temp.Reference = "#/definitions/" + prop.DataType.Name.ToLowerFirstLetter();
                    }
                    else if (IsSimpleType(prop.DataType.Name))
                    {
                        temp.Reference = "#/simpleType/" + prop.DataType.Name.ToLowerFirstLetter();
                    }
                    else if (IsItemType(prop.DataType.Name))
                    {
                        temp.Reference = "#/definitions/reference";
                    }
                    else
                    {
                        if (TypeBelongToInt(prop.DataType.Name))
                        {
                            temp.Type = "integer";
                            temp.original_type = prop.DataType.Name.ToLowerFirstLetter();
                        }
                        else if (TypeBelongToNum(prop.DataType.Name))
                        {
                            temp.Type = "number";
                            temp.original_type = prop.DataType.Name.ToLowerFirstLetter();
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
                temp.Title = item.Name.ToLowerFirstLetter();                          //get the name of the itemtype
                temp.Type = "object";                           //get the type of the itemtype which is usually Object
                temp.Id = "#" + item.Name.ToLowerFirstLetter();
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
                prop.Reference = "#/definitions/" + property.DataType.Name.ToLowerFirstLetter();
            }
            else if(IsSimpleType(property.DataType.Name))
            {
                prop.Reference = "#/simpleType/" + property.DataType.Name.ToLowerFirstLetter();
            }
            else if (IsItemType(property.DataType.Name))
            {
                prop.Reference = "#/definitions/reference";
            }
            else
            {
                if (TypeBelongToInt(property.DataType.Name))
                {
                    prop.Type = "integer";
                    prop.original_type = property.DataType.Name;
                }
                else if (TypeBelongToNum(property.DataType.Name))
                {
                    prop.Type = "number";
                    prop.original_type = property.DataType.Name;
                }
                else
                {
                    prop.Type = property.DataType.Name.ToLower();
                }
            }
            prop.MultiplicityElement.MinCardinality = property.MinCardinality;

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
        public Boolean IsSimpleType(string type)
        {
            for(int i = 0; i < CogsTypes.SimpleTypeNames.Length ; i++)
            {   
                if(type == "float" || type == "double" || type == "decimal" || type == "string" || type == "boolean" || type =="int")
                {
                    return false;
                }
                if (type == CogsTypes.SimpleTypeNames[i])
                {
                    return true;
                }
            }
            return false;
        }

        public Boolean TypeBelongToInt(string type)
        {
            type = type.ToLower();
            return type == "integer"
                || type == "nonpositiveinteger"
                || type == "negativeinteger"
                || type == "int"
                || type == "nonnegativeinteger"
                || type == "positiveinteger"
                || type == "unsignedlong"
                || type == "long";
        }

        public Boolean TypeBelongToNum(string type)
        {
            type = type.ToLower();
            return type == "float" || type == "double" || type == "decimal";
        }
    }
}
