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
    public class UmlDiagramSchemaPublisher
    {
        /// path to write output in
        public string TargetDirectory { get; set; }
        /// boolean to determine whether to replace existing or not
        public bool Overwrite { get; set; }

        // list of all IDs created. Used to ensure no duplicates
        private List<string> IdList = new List<string>();

        public void Publish(CogsModel model)
        {
            if (TargetDirectory == null)
            {
                throw new InvalidOperationException("Target directory must be specified");
            }
            if (Overwrite && Directory.Exists(TargetDirectory))
            {
                Directory.Delete(TargetDirectory, true);
            }
            // TODO: if Overwrite is false and Directory.Exists(TargetDirectory)) throw an error and exit
            Directory.CreateDirectory(TargetDirectory);
            XNamespace umlns = "omg.org/UML1.3";
            XElement xmodel = new XElement("XMI.content");




            //create document header
            XDocument xDoc = new XDocument(
               new XDeclaration("1.0", "utf-8", null),
               new XElement("XMI", new XAttribute(XNamespace.Xmlns + "UML", "omg.org/UML1.3"),
               new XAttribute("xmi.version", "1.1"),
               new XAttribute("timestamp", DateTime.Today.Year + "-" + DateTime.Today.Month + "-" + DateTime.Today.Day + " " + 
               DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" +DateTime.Now.Second),
               new XElement("XMI.header", new XElement("XMI.documentation",
               new XElement("XMI.exporter", "Enterprise Architect"),
               new XElement("XMI.exporterVersion", "2.5"))), xmodel, new XElement("XMI.difference"), new XElement("XMI.extensions",
               new XAttribute("xmi.extender", "Enterprise Architect 2.5"), new XElement("EAModel.paramSub"))));

            //write collection to file
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(TargetDirectory, "uml-diagram" + ".xmi.xml")))
            {
                XmlTextWriter writer = new XmlTextWriter(outputFile);
                writer.Formatting = Formatting.Indented;
                xDoc.WriteTo(writer);
                writer.Flush();
            }
        }
    }
}