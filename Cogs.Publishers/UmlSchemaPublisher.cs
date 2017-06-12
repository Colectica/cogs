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
            XDocument xDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(xmins + "XMI", first, second,
                new XElement(xmins + "Documentation", new XAttribute("exporter", "Enterprise Architect"), new XAttribute("exporterVersion","6.5")),
                xmodel));

            // loop through data and convert
            foreach (var item in model.ItemTypes)
            {
                try
                {
                    var x = File.GetAttributes(Path.GetPathRoot(item.Path));
                } 
                catch (FileNotFoundException e)
                {
                    //some sort of error catching
                    return;
                }
                if ((File.GetAttributes(Path.GetPathRoot(item.Path)) & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    // open folder and loop through files in it
                    // referenced https://stackoverflow.com/questions/4254339/how-to-loop-through-all-the-files-in-a-directory-in-c-net
                    String[] files = Directory.GetFiles(Path.GetPathRoot(item.Path));
                    String info = null;
                    String extends = null;
                    foreach(var file in files)
                    {
                        if (Path.GetExtension(file).Equals(".csv"))
                        {
                            info = file;
                        }
                        else if( Path.GetFileName(file).Contains("Extends")){
                                extends = file;
                        }
                    }
                    if(info != null)
                    {
                        xDoc.Add(new XElement("packagedElement", new XAttribute("xmi:type", "uml:Class"),
                                new XAttribute("xmi:id", createId(Path.GetFileName(info))),
                                new XAttribute("name", Path.GetFileName(info))));
                        String line1;
                        String line2;
                        StreamReader open = new StreamReader(info);
                        var lineCount = File.ReadLines(info).Count();
                        if (lineCount == 2)
                        {
                            line1 = open.ReadLine();
                            line2 = open.ReadLine();
                        }
                        else
                        {
                            //Error checking here
                            return;
                        }
                        if (line1.Length == line2.Length)
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
                            }
                        }
                        open.Close();
                    }
                    else
                    {
                        // some sory of error checking here
                        return;
                    }
                    if(extends != null)
                    {
                        String name = Path.GetFileName(extends).Split('.')[2];
                        xDoc.Add(new XElement("generalization",
                            new XAttribute("xmi:type", "uml:Generalization"),
                            new XAttribute("xmi:id", createId(name, "Generalization")),
                            new XAttribute("general", createId(name))));
                    }
                }
            }

            //write collection to file
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(TargetDirectory + "\\" + TargetNamespace + ".xmi.xml")))
            {
              //  XmlTextWriter writer = new XmlTextWriter(Console.Out);
                XmlTextWriter writer = new XmlTextWriter(outputFile);
                writer.Formatting = Formatting.Indented;
                xDoc.WriteTo(writer);
                writer.Flush();
                Console.WriteLine();
            }
            tester(Path.Combine(TargetDirectory + "\\" + TargetNamespace + ".xmi.xml"));
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

        private bool tester(String file)
        {
            StreamReader output = new StreamReader(file);
            StreamReader answer = new StreamReader("C:\\Users\\kevin\\Documents\\restaurant.xmi.xml");
            String outLine;
            String answerLine;
            while((outLine = output.ReadLine()) != null && (answerLine = answer.ReadLine()) != null)
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