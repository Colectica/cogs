using Cogs.Model;
using Cogs.Publishers.JsonSchema;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cogs.Publishers
{
    public class GraphQLPublisher
    {
        private JsonSerializerSettings settings = new JsonSerializerSettings();

        public string CogsLocation { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }

        public string TargetNamespace { get; set; } = "ddi:3_4";

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
            GraphQLList root = new GraphQLList();
            root.List = new List<GraphQLItems>();

            IterateType(root.List, model);
            WriteToFile(root.List);
        }

        public void IterateType(List<GraphQLItems> items, CogsModel model)
        {
            foreach (var data in model.ReusableDataTypes)
            {
                GraphQLItems type = new GraphQLItems();
                type.Type = data.Name;
                type.Properties = new Dictionary<string, string>();
                GetExtendProp(type, data);
                foreach (var prop in data.Properties)
                {
                    if(prop.MaxCardinality == "1")
                    {
                        if(prop.DataType.Name == "double" || prop.DataType.Name == "decimal")
                        {
                            prop.DataType.Name = "Float";
                        }
                        type.Properties.Add(prop.Name, FirstCharToUpper(prop.DataType.Name));
                    }
                    else
                    {
                        if (prop.DataType.Name == "double" || prop.DataType.Name == "decimal")
                        {
                            prop.DataType.Name = "Float";
                        }
                        type.Properties.Add(prop.Name, "["+ FirstCharToUpper(prop.DataType.Name)+"]");
                    }
                }
                items.Add(type);
            }
            foreach (var data in model.ItemTypes)
            {
                GraphQLItems type = new GraphQLItems();
                type.Type = data.Name;
                type.Properties = new Dictionary<string, string>();
                GetExtendProp(type, data);
                foreach (var prop in data.Properties)
                {
                    if (prop.MaxCardinality == "1")
                    {
                        if (prop.DataType.Name == "double" || prop.DataType.Name == "decimal")
                        {
                            prop.DataType.Name = "Float";
                        }
                        type.Properties.Add(prop.Name, FirstCharToUpper(prop.DataType.Name));
                    }
                    else
                    {
                        if (prop.DataType.Name == "double" || prop.DataType.Name == "decimal")
                        {
                            prop.DataType.Name = "Float";
                        }
                        type.Properties.Add(prop.Name, "[" + FirstCharToUpper(prop.DataType.Name) + "]");
                    }
                }
                items.Add(type);
            }
        }
        public void WriteToFile(List<GraphQLItems> items)
        {
            FileStream fs = new FileStream(Path.Combine(TargetDirectory, "GraphQL" + ".graphqls"), FileMode.OpenOrCreate, FileAccess.Write);
            TextWriter tmp = Console.Out;
            StreamWriter sw = new StreamWriter(fs);
            Console.SetOut(sw);
            //writeout simple type
            //duration
            Console.WriteLine("type duration {");
            Console.WriteLine("\t years: Int");
            Console.WriteLine("\t months : Int");
            Console.WriteLine("\t days : Int");
            Console.WriteLine("\t hour: Int");
            Console.WriteLine("\t minutes: Int");
            Console.WriteLine("\t seconds: Int");
            Console.WriteLine("\t timezone: String");
            Console.WriteLine("}");
            //datetime
            Console.WriteLine("type datetime {");
            Console.WriteLine("\t date: date");
            Console.WriteLine("\t time: time");
            Console.WriteLine("\t timezone: String");
            Console.WriteLine("}");
            //time
            Console.WriteLine("type time {");
            Console.WriteLine("\t hour: Int");
            Console.WriteLine("\t minutes: Int");
            Console.WriteLine("\t second: Int");
            Console.WriteLine("\t timezone: String");
            Console.WriteLine("}");
            //date
            Console.WriteLine("type date {");
            Console.WriteLine("\t year: Int");
            Console.WriteLine("\t month: Int");
            Console.WriteLine("\t day: Int");
            Console.WriteLine("\t timezone: String");
            Console.WriteLine("}");
            //gyearmonth
            Console.WriteLine("type gYearMonth {");
            Console.WriteLine("\t Year: Int");
            Console.WriteLine("\t Month: Int");
            Console.WriteLine("\t timezone: String");
            Console.WriteLine("}");
            //gyear
            Console.WriteLine("type gYear {");
            Console.WriteLine("\t Year: Int");
            Console.WriteLine("\t timezone: String");
            Console.WriteLine("}");
            //gmonthday
            Console.WriteLine("type gMonthDay {");
            Console.WriteLine("\t Month: Int");
            Console.WriteLine("\t Day: Int");
            Console.WriteLine("\t timezone: String");
            Console.WriteLine("}");
            //gDay
            Console.WriteLine("type gDay {");
            Console.WriteLine("\t Day: Int");
            Console.WriteLine("\t timezone: String");
            Console.WriteLine("}");
            //gmonth
            Console.WriteLine("type gMonth {");
            Console.WriteLine("\t Month: Int");
            Console.WriteLine("\t timezone: String");
            Console.WriteLine("}");
            //cogdate
            Console.WriteLine("type cogsDate {");
            Console.WriteLine("\t dateTime: datetime");
            Console.WriteLine("\t date : date");
            Console.WriteLine("\t gYearMonth : gYearMonth");
            Console.WriteLine("\t gYear : gYear");
            Console.WriteLine("\t duration : duration");
            Console.WriteLine("}");
            //language
            Console.WriteLine("type language {");
            Console.WriteLine("\t lanugage: String");
            Console.WriteLine("}");
            foreach (var item in items)
            {
                Console.WriteLine("type " + item.Type + "{");
                foreach (var prop in item.Properties)
                {
                    Console.WriteLine("\t" + prop.Key + " : " + prop.Value);
                }
                Console.WriteLine("}");
            }
            Console.SetOut(tmp);
            Console.WriteLine("Done");
            sw.Close();
        }
        public void GetExtendProp(GraphQLItems type, DataType data)
        {
            if (data.ExtendsTypeName != "")             //Check whether there it extends another class
            {
                //get the Parent information
                if (data.ParentTypes != null)
                {
                    //traverse parent list, find the properties of the parents
                    foreach (var properti in data.ParentTypes)
                    {
                        if (properti.Properties != null)
                        {
                            //traverse the properties and get all the information regarding variable. 
                            foreach (var inner_prop in properti.Properties)
                            {
                                if (inner_prop.MaxCardinality == "1")
                                {
                                    if (inner_prop.DataType.Name == "double" || inner_prop.DataType.Name == "decimal")
                                    {
                                        inner_prop.DataType.Name = "Float";
                                    }
                                    type.Properties.Add(inner_prop.Name, FirstCharToUpper(inner_prop.DataType.Name));
                                }
                                else
                                {
                                    if (inner_prop.DataType.Name == "double" || inner_prop.DataType.Name == "decimal")
                                    {
                                        inner_prop.DataType.Name = "Float";
                                    }
                                    type.Properties.Add(inner_prop.Name, "[" + FirstCharToUpper(inner_prop.DataType.Name) + "]");
                                }
                            }
                        }
                    }
                }
            }
        }

        public string FirstCharToUpper(string type)
        {
            String res = "";
            res = char.ToUpper(type[0]) + type.Substring(1);
            return res;
        }
    }
}
