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

        private List<string> idList = new List<string>();

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

            XNamespace xmins = "http://www.omg.org/spec/XMI/20110701";
            XNamespace umlns = "http://www.omg.org/spec/UML/20110701";
            XAttribute first = new XAttribute(XNamespace.Xmlns + "uml", "http://www.omg.org/spec/UML/20110701");
            XAttribute second = new XAttribute(XNamespace.Xmlns + "xmi", "http://www.omg.org/spec/XMI/20110701");
            XElement xmodel = new XElement("packagedElement", new XAttribute(xmins + "type", "uml:Package"), 
                new XAttribute(xmins + "id", "TestProject"), new XAttribute("name", "RestaurantMenu"));
            // loop through classes
            foreach (var item in model.ItemTypes)
            {
                // Create class
                var newItem = new XElement(new XElement("packagedElement", new XAttribute(xmins + "type", "uml:Class"),
                           new XAttribute(xmins + "id", createId(item.Name)),
                           new XAttribute("name", item.Name)));
                String extends = item.ExtendsTypeName;
                // loop through properties of class and add to class
                foreach(var property in item.Properties)
                {
                    var newProperty = new XElement("ownedAttribute", new XAttribute(xmins+ "type", "uml:Property"),
                           new XAttribute(xmins + "id", createId(item.Name + "." + property.Name)),
                           new XAttribute("name", property.Name));
                    newProperty.Add(new XElement("type", new XAttribute(xmins + "idref", property.DataTypeName)));
                    if(property.MinCardinality != null)
                    {
                        newProperty.Add(new XElement("lowerValue", new XAttribute(xmins + "type", "uml:LiteralInteger"),
                            new XAttribute(xmins + "id", createId(item.Name + "." + property.Name + ".MinCardinality")),
                            new XAttribute("value", property.MinCardinality)));
                    }
                    if (property.MaxCardinality != null)
                    {
                        // if max is "n" change to "*"
                        if (property.MaxCardinality.Equals("n"))
                        {
                            newProperty.Add(new XElement("upperValue", new XAttribute(xmins + "type", "uml:LiteralUnlimitedNatural"),
                                new XAttribute(xmins + "id", createId(item.Name + "." + property.Name + ".MaxCardinality")),
                                new XAttribute("value", "*")));
                        }
                        else
                        {
                            newProperty.Add(new XElement("upperValue", new XAttribute(xmins + "type", "uml:LiteralUnlimitedNatural"),
                                new XAttribute(xmins + "id", createId(item.Name + "." + property.Name + ".MaxCardinality")),
                                new XAttribute("value", property.MaxCardinality)));
                        }
                    }
                    newItem.Add(newProperty);
                }
                // adds pointers for inheritance where applicable
                if (!extends.Equals(""))
                {
                    try
                    {
                        newItem.Add(new XElement("generalization",
                            new XAttribute(xmins + "type", "uml:Generalization"),
                            new XAttribute(xmins + "id", createId(item.Name + ".Generalization")),
                            new XAttribute("general", extends)));
                    }
                    catch (ArgumentException e)
                    {
                        return;
                    }
                   
                }
                // add class to model
                xmodel.Add(newItem);
            }

            //create document header
            XDocument xDoc = new XDocument(
               new XDeclaration("1.0", "utf-8", null),
               new XElement(xmins + "XMI", first, second,
               new XElement(xmins + "Documentation", new XAttribute("exporter", "Enterprise Architect"), new XAttribute("exporterVersion", "6.5")),
               new XElement(umlns + "Model", new XAttribute(xmins + "type", "uml:Model"), new XAttribute("name", "EA_Model"), xmodel)));

            //write collection to file
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(TargetDirectory + "\\" + TargetNamespace + ".xmi.xml")))
            {
                XmlTextWriter writer = new XmlTextWriter(outputFile);
                writer.Formatting = Formatting.Indented;
                xDoc.WriteTo(writer);
                writer.Flush();
            }
            tester(Path.Combine(TargetDirectory + "\\" + TargetNamespace + ".xmi.xml"));
        }

        //takes a string and checks that the ID has not been created before, then returns it if valid, otherwise throws exception
        private String createId(String name)
        {
            if (idList.Contains(name))
            {
                Console.WriteLine("ERROR: name '%s' used twice", name);
                throw new ArgumentException();
            }
            idList.Add(name);
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