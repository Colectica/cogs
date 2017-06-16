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
        /// <summary>
        /// path to write output in
        /// </summary>
        public string TargetDirectory { get; set; }
        /// <summary>
        /// boolean to determine whether to replace existing or not
        /// </summary>
        public bool Overwrite { get; set; }
        /// <summary>
        /// boolean to determine whether to output normative xmi. If false, outputs xmi 2.5.1
        /// </summary>
        public bool Normative { get; set; }
        /// <summary>
        /// path to dot.exe file
        /// </summary>
        public string DotLocation { get; set; }

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

            //TODO: get project name and set it here
            string projectName = "CogsBurger";

            // set namespaces based on output format
            XNamespace xmins = "http://www.omg.org/spec/XMI/20110701";
            XNamespace umlns = "http://www.omg.org/spec/UML/20110701";
            if (!Normative)
            {
                xmins = "http://www.omg.org/spec/XMI/20131001";
                umlns = "http://www.omg.org/spec/UML/20131001";
            }

            XElement xmodel = new XElement("packagedElement", new XAttribute(xmins + "type", "uml:Package"), 
                new XAttribute(xmins + "id", projectName), new XAttribute("name", projectName));
            XElement diagramElements = new XElement("elements");
            // create list of all classes so you know if a class is being referenced
            var classList = new List<string>();
            foreach(var item in model.ItemTypes.Concat(model.ReusableDataTypes)){
                classList.Add(item.Name);
            }
            // create list of all reusable types so you know if a reusable type is being referenced
            var reusableList = new List<string>();
            foreach(var item in model.ReusableDataTypes)
            {
                reusableList.Add(item.Name);
            }
            List<XElement> nodes = null;
            var xOff = 0.0;
            var yOff = 0.0;
            XNamespace ns = "http://www.w3.org/2000/svg";
            if (!Normative)
            {
                // run svg publisher to create svg file to use for positioning
                SvgSchemaPublisher publisher = new SvgSchemaPublisher();
                publisher.DotLocation = DotLocation;
                publisher.TargetDirectory = TargetDirectory;
                publisher.Overwrite = Overwrite;
                publisher.Publish(model);

                // read created svg file
                nodes = XDocument.Load(Path.Combine(TargetDirectory, "output.svg")).Root.Descendants(ns + "g")
                    .Where(x => x.Attribute("class").Value == "node").ToList();
                File.Delete(Path.Combine(TargetDirectory, "output.svg"));

                //get leftmost  and topmost value to shift graph accordingly 
                foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
                {
                    var node = XElement.Parse(nodes.Descendants(ns + "title").Where(x => x.FirstNode.ToString() == item.Name).ToList()[0].NextNode.ToString());
                    var cords = node.Attribute("points").Value.Split(',', ' ');
                    if (Convert.ToDouble(cords[4]) < xOff)
                    {
                        xOff = Convert.ToDouble(cords[4]);
                    }
                    if (Convert.ToDouble(cords[3]) < yOff)
                    {
                        yOff = Convert.ToDouble(cords[3]);
                    }
                }
                xOff = Math.Abs(xOff);
                yOff = Math.Abs(yOff);
            } 
            int count = classList.Count;
            // loop through classes and reusable data types
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                // Create class
                var newItem = new XElement(new XElement("packagedElement", new XAttribute(xmins + "type", "uml:Class"),
                           new XAttribute(xmins + "id", CreateId(item.Name)),
                           new XAttribute("name", item.Name)));
                // add class to diagram
                if (!Normative)
                {
                    // add class to graph
                    var cords = XElement.Parse(nodes.Descendants(ns + "title").Where(x => x.FirstNode.ToString() == item.Name).ToList()[0].NextNode.ToString())
                    .Attribute("points").Value.Split(',', ' ');
                    var left = (Convert.ToDouble(cords[4]) + xOff).ToString();
                    var right = (Convert.ToDouble(cords[0]) + xOff).ToString();
                    var top = (Convert.ToDouble(cords[1]) + yOff).ToString();
                    var bottom = (Convert.ToDouble(cords[3]) + yOff).ToString();
                    diagramElements.Add(new XElement("element", new XAttribute("geometry", "Left=" + left + ";Top=" + top + ";Right=" + right + ";Bottom=" + bottom + ";"),
                        new XAttribute("subject", item.Name), new XAttribute("seqno", count.ToString()), new XAttribute("style",
                        "DUID=" + "item.Name" + ";NSL=0;BCol=-1;BFol=-1;LCol=-1;LWth=-1;fontsz=0;bold=0;black=0;italic=0;ul=0;charset=0;pitch=0;));")));
                }
                string extends = item.ExtendsTypeName;
                // loop through properties of class and add to class
                foreach(var property in item.Properties)
                {
                    var newProperty = new XElement("ownedAttribute", new XAttribute(xmins+ "type", "uml:Property"),
                           new XAttribute(xmins + "id", CreateId(item.Name + "." + property.Name)),
                           new XAttribute("name", property.Name));
                    newProperty.Add(new XElement("type", new XAttribute(xmins + "idref", property.DataTypeName)));
                    if(property.MinCardinality != null)
                    {
                        newProperty.Add(new XElement("lowerValue", new XAttribute(xmins + "type", "uml:LiteralInteger"),
                            new XAttribute(xmins + "id", CreateId(item.Name + "." + property.Name + ".MinCardinality")),
                            new XAttribute("value", property.MinCardinality)));
                    }
                    if (property.MaxCardinality != null)
                    {
                        var attribute = new XAttribute("value", property.MaxCardinality);
                        // if max is "n" change to "*"
                        if (property.MaxCardinality.Equals("n"))
                        {
                            attribute = new XAttribute("value", "*");
                        }
                        newProperty.Add(new XElement("upperValue", new XAttribute(xmins + "type", "uml:LiteralUnlimitedNatural"),
                            new XAttribute(xmins + "id", CreateId(item.Name + "." + property.Name + ".MaxCardinality")),
                            attribute));
                    }
                    newItem.Add(newProperty);
                    // see if property is a type of class
                    if(classList.Contains(property.DataTypeName)){
                        // create link association
                        var classLink = new XElement("packagedElement", new XAttribute(xmins + "type", "uml:Association"),
                            new XAttribute(xmins + "id", CreateId("Association.from" + property.Name + ".to." + property.DataTypeName)));
                        classLink.Add(new XElement("memberEnd", new XAttribute(xmins + "idref", item.Name + "." + property.Name + ".association")));
                        classLink.Add(new XElement("memberEnd", 
                            new XAttribute(xmins + "idref", "Association.from" + property.Name + ".to." + property.DataTypeName + ".ownedEnd")));
                        var ownedEnd = new XElement("ownedEnd", new XAttribute(xmins + "type", "uml:Property"),
                            new XAttribute(xmins + "id", CreateId("Association.from" + property.Name + ".to." + property.DataTypeName + ".ownedEnd")),
                            new XAttribute("association", "Association.from" + property.Name + ".to." + property.DataTypeName),
                            new XAttribute("isOrdered", "true"));
                        ownedEnd.Add(new XElement("type", new XAttribute(xmins + "idref", item.Name)));
                        var min = "0";
                        var max = "*";
                        // check to see if item being referenced is a ReusableDataType
                        if (reusableList.Contains(property.DataTypeName))
                        {
                            min = "1";
                            max = "1";
                        }
                        ownedEnd.Add(new XElement("lowerValue", new XAttribute(xmins + "type", "uml:LiteralInteger"),
                            new XAttribute(xmins + "id", CreateId("Association.from" + property.Name + ".to." + property.DataTypeName + ".ownedEnd.MinCardinality")),
                            new XAttribute("value", min)));
                        ownedEnd.Add(new XElement("upperValue", new XAttribute(xmins + "type", "uml:LiteralUnlimitedNatural"),
                            new XAttribute(xmins + "id", CreateId("Association.from" + property.Name + ".to." + property.DataTypeName + ".ownedEnd.MaxCardinality")),
                            new XAttribute("value", max)));
                        classLink.Add(ownedEnd);
                        xmodel.Add(classLink);
                        // reference link from current class as attribute
                        var link = new XElement("ownedAttribute", new XAttribute(xmins + "type", "uml:Property"),
                            new XAttribute(xmins + "id", CreateId(item.Name + "." + property.Name+ ".association")),
                            new XAttribute("name", property.Name), 
                            new XAttribute("association", "Association.from" + property.Name + ".to." + property.DataTypeName),
                            new XAttribute("isOrdered", "true"));
                        link.Add(new XElement("type", new XAttribute(xmins + "idref", property.DataTypeName)));
                        link.Add(new XElement("lowerValue", new XAttribute(xmins + "type", "uml:LiteralInteger"),
                            new XAttribute(xmins + "id", CreateId(item.Name + "." + property.Name + ".association.MinCardinality")),
                            new XAttribute("value", min)));
                        link.Add(new XElement("upperValue", new XAttribute(xmins + "type", "uml:LiteralUnlimitedNatural"),
                            new XAttribute(xmins + "id", CreateId(item.Name + "." + property.Name + ".association.MaxCardinality")),
                            new XAttribute("value", max)));
                        newItem.Add(link);
                    }
                }
                // adds pointers for inheritance where applicable
                if (!string.IsNullOrWhiteSpace(extends))
                {
                    newItem.Add(new XElement("generalization",
                        new XAttribute(xmins + "type", "uml:Generalization"),
                        new XAttribute(xmins + "id", CreateId(item.Name + ".Generalization")),
                        new XAttribute("general", extends)));
                }
                // add class to model
                xmodel.Add(newItem);
                count -= 1;
            }
            //create document header based on format specified
            XDocument xDoc;
            if (Normative)
            {
                xDoc = new XDocument(
                   new XDeclaration("1.0", "utf-8", null),
                   new XElement(xmins + "XMI", new XAttribute(XNamespace.Xmlns + "uml", "http://www.omg.org/spec/UML/20110701"),
                   new XAttribute(XNamespace.Xmlns + "xmi", "http://www.omg.org/spec/XMI/20110701"),
                   new XElement(xmins + "Documentation", new XAttribute("exporter", "Enterprise Architect"), new XAttribute("exporterVersion", "6.5")),
                   new XElement(umlns + "Model", new XAttribute(xmins + "type", "uml:Model"), new XAttribute("name", "EA_Model"), xmodel)));
            }
            else
            {
                // get current date and time for when setting created and last modified settings
                var currentTime = DateTime.Today.Year + "-" + DateTime.Today.Month + "-" + DateTime.Today.Day + " " + DateTime.Now.Hour + ":" + 
                    DateTime.Now.Minute + ":" + DateTime.Now.Second;
                // create header + structure of xml 2.5.1 (chunky and unpleasing, but works)
                xDoc = new XDocument(
                   new XDeclaration("1.0", "utf-8", null),
                   new XElement(xmins + "XMI", new XAttribute(XNamespace.Xmlns + "uml", "http://www.omg.org/spec/UML/20131001"),
                   new XAttribute(XNamespace.Xmlns + "xmi", "http://www.omg.org/spec/XMI/20131001"),
                   new XElement(xmins + "Documentation", new XAttribute("exporter", "Enterprise Architect"), new XAttribute("exporterVersion", "6.5")),
                   new XElement(umlns + "Model", new XAttribute(xmins + "type", "uml:Model"), new XAttribute("name", "EA_Model"), xmodel),
                   new XElement(xmins + "Extension", new XAttribute("extender", "Enterprise Architect"), new XAttribute("extenderID", "6.5"),
                   new XElement("diagrams", new XElement("diagram", new XAttribute(xmins + "id", CreateId("ModelDiagram")),
                   new XElement("model", new XAttribute("package", projectName), new XAttribute("localID", "28"), new XAttribute("owner", projectName)),
                   new XElement("properties", new XAttribute("name", projectName), new XAttribute("type", "Logical")),
                   new XElement("project", new XAttribute("author", "computer"), new XAttribute("version", "1.0"), new XAttribute("created", currentTime), new XAttribute("modified", currentTime)),
                   new XElement("style1", new XAttribute("value", "ShowPrivate=1;ShowProtected=1;ShowPublic=1;HideRelationships=0;Locked=0;Border=1;HighlightForeign=1;" +
                   "PackageContents=1;SequenceNotes=0;ScalePrintImage=0;PPgs.cx=1;PPgs.cy=1;DocSize.cx=815;DocSize.cy=1067;ShowDetails=0;Orientation=P;" +
                   "Zoom=100;ShowTags=0;OpParams=1;VisibleAttributeDetail=0;ShowOpRetType=1;ShowIcons=1;CollabNums=0;HideProps=0;ShowReqs=0;ShowCons=0;PaperSize=1;" +
                   "HideParents=0;UseAlias=0;HideAtts=0;HideOps=0;HideStereo=0;HideElemStereo=0;ShowTests=0;ShowMaint=0;ConnectorNotation=UML 2.1;ExplicitNavigability=0;" +
                   "ShowShape=1;AdvancedElementProps=1;AdvancedFeatureProps=1;AdvancedConnectorProps=1;m_bElementClassifier=1;ShowNotes=0;SuppressBrackets=0;SuppConnectorLabels=0;" +
                   "PrintPageHeadFoot=0;ShowAsList=0;")), new XElement("style2", new XAttribute("value", "ExcludeRTF=0;DocAll=0;HideQuals=0;AttPkg=1;ShowTests=0;ShowMaint=0;" +
                   "SuppressFOC=1;MatrixActive=0;SwimlanesActive=1;KanbanActive=0;MatrixLineWidth=1;MatrixLineClr=0;MatrixLocked=0;TConnectorNotation=UML 2.1;TExplicitNavigability=0;" +
                   "AdvancedElementProps=1;AdvancedFeatureProps=1;AdvancedConnectorProps=1;m_bElementClassifier=1;ProfileData=;MDGDgm=;STBLDgm=;ShowNotes=0;VisibleAttributeDetail=0;" +
                   "ShowOpRetType=1;SuppressBrackets=0;SuppConnectorLabels=0;PrintPageHeadFoot=0;ShowAsList=0;SuppressedCompartments=;Theme=:119;SaveTag=D7ED2A20;")),
                   new XElement("swimlanes", new XAttribute("value", "locked=false;orientation=0;width=0;inbar=false;names=false;color=-1;bold=false;fcol=0;tcol=-1;ofCol=-1;ufCol=-1;" +
                   "hl=0;ufh=0;cls=0;SwimlaneFont=lfh:-10,lfw:0,lfi:0,lfu:0,lfs:0,lfface:Calibri,lfe:0,lfo:0,lfchar:1,lfop:0,lfcp:0,lfq:0,lfpf=0,lfWidth=0;")),
                   new XElement("matrixitems", new XAttribute("value", "locked=false;matrixactive=false;swimlanesactive=true;kanbanactive=false;width=1;clrLine=0;")),
                   new XElement("extendedProperties"), diagramElements)))));
            }


            //write collection to file
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(TargetDirectory, "uml" + ".xmi.xml")))
            {
                XmlTextWriter writer = new XmlTextWriter(outputFile);
                writer.Formatting = Formatting.Indented;
                xDoc.WriteTo(writer);
                writer.Flush();
            }
        }

        // helper method that takes a string and checks that the ID has not been previously created
        // returns the string if valid, otherwise throws InvalidOperationException
        private string CreateId(string name)
        {
            if (IdList.Contains(name))
            {
                Console.WriteLine("ERROR: name '%s' used twice", name);
                throw new InvalidOperationException();
            }
            IdList.Add(name);
            return name;
        }
    }
}