// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Model;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace Cogs.Publishers
{
    /// <summary>
    /// Generate an uml schema using the Garden of Eden approach, all elements and type definitions are declared globally
    /// </summary>
    public class UmlSchemaPublisher
    {
        public string CogsLocation { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }

        public string TargetNamespace { get; set; } = "ddi:3_4";

        Dictionary<string, string> createdElements = new Dictionary<string, string>();

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

            Console.WriteLine(model.ToString());

            // initialize data structure to hold all initialized objects
            // http://www.dotnetcurry.com/linq/564/linq-to-xml-tutorials-examples
            XAttribute first = new XAttribute(XNamespace.Xmlns + "uml", "http://www.omg.org/spec/UML/20110701");
            XAttribute second = new XAttribute(XNamespace.Xmlns + "xmi", "http://www.omg.org/spec/XMI/20110701");
            XDocument xDoc = new XDocument(
                new XDeclaration("1.0", "windows-1252", null),
                new XElement("xmi:XMI", first, second),
                new XElement("xmi:Documentation", new XAttribute("exporter", "Enterprise Architect"), new XAttribute("exporterVersion", "6.5")),
                new XElement("uml:Model", new XAttribute("xmi:type", "uml:Model"), new XAttribute("name", "EA_Model")));

            //create UML header



            // create built in types


            // loop through data and convert
            foreach (var item in model.ItemTypes)
            {
                if ((File.GetAttributes(item.ToString()) & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    //create new object
                    xDoc.Add(new XElement("packagedElement", new XAttribute("xmi:type", "uml:Package"),
                    new XAttribute("xmi:id", createId(item.Name)),
                    new XAttribute("name", item.Name)));

                    // open folder and loop through files in it
                    foreach (var file in item)
                    {
                        if (Path.GetExtension(file).Equals(".csv"))
                        {
                            xDoc.Add(new XElement("packagedElement", new XAttribute("xmi:type", "uml:Class"),
                                new XAttribute("xmi:id", createId(Path.GetFileName(file))),
                                new XAttribute("name", Path.GetFileName(file))));
                            String line1;
                            String line2;
                            StreamReader open = new StreamReader(file);
                            var lineCount = File.ReadLines(file).Count();
                            if(lineCount == 2)
                            {
                                line1 = open.ReadLine();
                                line2 = open.ReadLine();
                            }
                            else
                            {
                                //Error checking here
                                return;
                            }
                            if(line1.Length == line2.Length)
                            {
                                String[] names = line1.Split(',');
                                String[] values = line2.Split(',');
                                // check that 
                                if (Array.IndexOf(names, "Name") < 0 || Array.IndexOf(names, "DataType") < 0 || Array.IndexOf(names, "MinCardinality") < 0 
                                    || Array.IndexOf(names, "MaxCardinality") < 0 || Array.IndexOf(names, "Description") < 0 || 
                                    values[Array.IndexOf(names, "Name")].Equals("") || values[Array.IndexOf(names, "DataType")].Equals(""))
                                {
                                    // throw exception or some sort of error
                                }
                                else
                                {
                                    // name
                                    String name = values[Array.IndexOf(values, "name")];
                                    xDoc.Add(new XElement("ownedAttribute",
                                        new XAttribute("xmi:type", "uml: Property"),
                                        new XAttribute("xmi:id", createId(name)),
                                        new XAttribute("name", name)));

                                    // type
                                    xDoc.Add(new XElement("type", new XAttribute("xmi:idref", values[Array.IndexOf(names, "DataType")])));

                                    // description
                                    xDoc.Add(new XElement("description",
                                        new XAttribute("xmi:id", createId(name, "description")),
                                        new XAttribute("value", values[Array.IndexOf(names, "description")])));

                                    // MinCardinality (first check if relevent)
                                    if(!values[Array.IndexOf(names, "MinCardinality")].Equals(""))
                                    {
                                        xDoc.Add(new XElement("lowerValue", 
                                            new XAttribute("xmi:type", "uml:LiteralInteger"),
                                            new XAttribute("xmi:id", createId(name , "MinCardinality")),
                                            new XAttribute("value", values[Array.IndexOf(names, "MinCardinality")])));
                                    }

                                    // MaxCardinality (first check if relevent)
                                    if (!values[Array.IndexOf(names, "MinCardinality")].Equals(""))
                                    {
                                        if(values[Array.IndexOf(names, "MaxCardinality")].Equals("*"))
                                        {
                                            xDoc.Add(new XElement("upperValue",
                                                new XAttribute("xmi:type", "uml:LiteralUnlimitedNatural"),
                                                new XAttribute("xmi:id", createId(name, "MaxCardinality"))));
                                        }
                                        else
                                        {
                                            xDoc.Add(new XElement("upperValue",
                                                new XAttribute("xmi:type", "uml:LiteralInteger"),
                                                new XAttribute("xmi:id", createId(name, "MaxCardinality")),
                                                new XAttribute("value", values[Array.IndexOf(names, "MaxCardinality")])));
                                        }
                                    }



                                }



                            }
                            else
                            {
                                //Error checking here
                                return;
                            }

                            file.Close();
                        }
                    }
                }
                

                //add to collection

            }

            //write collection to file
            using (StreamWriter outputFile = new StreamWriter(TargetDirectory + @"\output.xmi.xml"))
            {
                foreach (string line in xDoc.Elements())
                    outputFile.WriteLine(line);
            }

        }

        //takes an object and sets its id field to something relevant/informative
        private String createId(String name)
        {
            return name;
        }

        private String createId(String name, String type)
        {
            return name + type;
        }
    }
}