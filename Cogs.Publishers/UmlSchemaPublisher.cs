// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Model;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using System.Xml;
using System.Diagnostics;

namespace Cogs.Publishers
{
    /// <summary>
    /// Generate an uml schema using the Garden of Eden approach, all elements and type definitions are declared globally
    /// </summary>
    public class UmlSchemaPublisher
    {
        // path to outer file to read into 
        public string CogsLocation { get; set; }
        // path to write output in
        public string TargetDirectory { get; set; }
        // boolean to determine whether to replace existing or not
        public bool Overwrite { get; set; }

        public string TargetNamespace { get; set; } = "ddi3_4";

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
            // what about Overwrite being false and Directory.Exists(TargetDirectory))?

            Directory.CreateDirectory(TargetDirectory);

            //create UML header
            // referenced http://www.dotnetcurry.com/linq/564/linq-to-xml-tutorials-examples
            XNamespace xmins = "http://www.omg.org/spec/XMI/20110701";
            XNamespace umlns = "http://www.omg.org/spec/UML/20110701";
            XAttribute first = new XAttribute(XNamespace.Xmlns + "uml", "http://www.omg.org/spec/UML/20110701");
            XAttribute second = new XAttribute(XNamespace.Xmlns + "xmi", "http://www.omg.org/spec/XMI/20110701");
            XElement xmodel = new XElement(umlns + "Model", new XAttribute(xmins + "type", "uml:Model"), new XAttribute("name", "EA_Model"));
            var packageName = "restaurant Menu";
            // loop through data and convert
            foreach (var item in model.ItemTypes)
            {
                var newItem = new XElement(new XElement("packagedElement", new XAttribute(xmins + "type", umlns + "Class"),
                           new XAttribute(xmins + "id", createId(item.Name)),
                           new XAttribute("name", item.Name)));
                var info = item.Properties;
                String extends = item.ExtendsTypeName;
                foreach(var property in info)
                {
                    var newProperty = new XElement("ownedAttribute", new XAttribute(xmins+ "type", umlns+ "Class"),
                           new XAttribute(xmins + "id", createId(property.Name)),
                           new XAttribute("name", property.Name));
                    newProperty.Add(new XElement("type", new XAttribute(xmins + "idref", property.DataType)));
                    if(property.MinCardinality != null)
                    {
                        newProperty.Add(new XElement("lowerValue", new XAttribute(xmins + "type", umlns + "LiteralInteger"),
                            new XAttribute(xmins + "id", createId(item.Name + "." + property.Name + ".MinCardinality")),
                            new XAttribute("value", property.MinCardinality)));
                    }
                    if (property.MaxCardinality != null)
                    {
                        if (property.MaxCardinality.Equals("*"))
                        {
                            newProperty.Add(new XElement("lowerValue", new XAttribute(xmins + "type", umlns + "LiteralUnlimitedNatural"),
                                 new XAttribute(xmins + "id", createId(item.Name + "." + property.Name + ".MaxCardinality")),
                                 new XAttribute("value", "1")));
                        }
                        else
                        {
                            newProperty.Add(new XElement("lowerValue", new XAttribute(xmins + "type", umlns + "LiteralInteger"),
                                 new XAttribute(xmins + "id", createId(item.Name + "." + property.Name + ".MaxCardinality")),
                                 new XAttribute("value", property.MaxCardinality)));
                        }
                    }

                    newItem.Add(newProperty);
                }
                if (extends != null)
                {
                    newItem.Add(new XElement("generalization",
                        new XAttribute(xmins+ "type", umlns + "Generalization"),
                        new XAttribute(xmins +"id", createId(item.Name + ".Generalization")),
                        new XAttribute("general", createId(extends))));
                }
                xmodel.Add(newItem);
            }
            //create document header
            XDocument xDoc = new XDocument(
               new XDeclaration("1.0", "utf-8", null),
               new XElement(xmins + "XMI", first, second,
               new XElement(xmins + "Documentation", new XAttribute("exporter", "Enterprise Architect"), new XAttribute("exporterVersion", "6.5")),
               xmodel));

            //write collection to file
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(TargetDirectory + "\\" + TargetNamespace + ".xmi.xml")))
            {
                //  XmlTextWriter writer = new XmlTextWriter(Console.Out);
                XmlTextWriter writer = new XmlTextWriter(outputFile);
                writer.Formatting = Formatting.Indented;
                xDoc.WriteTo(writer);
                writer.Flush();
            }
            tester(Path.Combine(TargetDirectory + "\\" + TargetNamespace + ".xmi.xml"));
        }

        /*
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
                    if (!values[Array.IndexOf(names, "MinCardinality")].Equals(""))
                    {
                        xDoc.Add(new XElement("lowerValue",
                            new XAttribute("xmi:type", "uml:LiteralInteger"),
                            new XAttribute("xmi:id", createId(name, "MinCardinality")),
                            new XAttribute("value", values[Array.IndexOf(names, "MinCardinality")])));
                    }

                    // MaxCardinality (first check if relevent)
                    if (!values[Array.IndexOf(names, "MinCardinality")].Equals(""))
                    {
                        if (values[Array.IndexOf(names, "MaxCardinality")].Equals("*"))
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
                }*/

        //takes an object and sets its id field to something relevant/informative
        private String createId(String name)
        {
            return name;
        }
        private bool tester(String file)
        {
            StreamReader output = new StreamReader(file);
            StreamReader answer = new StreamReader("C:\\Users\\kevin\\Documents\\restaurant.xmi.xml");
            String outLine;
            String answerLine;
            while((outLine = output.ReadLine().Trim()) != null && (answerLine = answer.ReadLine().Trim()) != null)
            {
                if (!outLine.Equals(answerLine))
                {
                    Console.WriteLine("Saw: " + outLine);
                    Console.WriteLine("Expected: " + answerLine);
                    return false;
                }
            }
            if((outLine =  output.ReadLine()) != null)
            {
                Console.WriteLine("Saw: " + outLine);
                Console.WriteLine("Expected nothing");
                return false;
            }
            else if((answerLine = answer.ReadLine()) != null)
            {
                Console.WriteLine("Saw nothing");
                Console.WriteLine("Expected: " + answerLine);
                return false;
            }
            return true;
        }
    }
}