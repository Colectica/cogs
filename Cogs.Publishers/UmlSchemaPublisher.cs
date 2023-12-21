// Copyright (c) 2024 Colectica. All rights reserved
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
using System.Reflection;
using Cogs.Common;
using Cogs.Dto;

namespace Cogs.Publishers
{
    /// <summary>
    /// Generate an uml schema using
    /// </summary>
    public class UmlSchemaPublisher
    {
        /// <summary>
        /// path to write output in
        /// </summary>
        public string TargetDirectory { get; set; }

        /// <summary>
        /// name of file to create
        /// </summary>
        public string TargetFilename { get; set; }

        /// <summary>
        /// boolean to determine whether to replace existing or not
        /// </summary>
        public bool Overwrite { get; set; }

        /// <summary>
        /// boolean to determine whether to output normative xmi. If false, outputs xmi 2.5.1
        /// </summary>
        public bool Normative { get; set; }

        /// <summary>
        /// string that specifies path to dot executable file
        /// </summary>
        public string DotLocation { get; set; }

        // list of all IDs created. Used to ensure no duplicates
        private HashSet<string> IdList = new HashSet<string>();

        XNamespace xmins = "http://www.omg.org/spec/XMI/20131001";
        XNamespace umlns = "http://www.omg.org/spec/UML/20131001";

        private XElement CreatePackageElement(string id, string name, string description)
        {
            XElement result = new XElement("packagedElement", 
                new XAttribute(xmins + "type", "uml:Package"),
                new XAttribute(xmins + "id", id));

            if(description != null)
            {
                var ownedComment = new XElement("ownedComment",
                    new XAttribute(xmins + "type", "uml:Comment"),
                    new XAttribute(xmins + "id", CreateId(id + "-ownedComment")),
                    new XElement(xmins + "annotatedElement", new XAttribute(xmins + "idref", id)),
                    new XElement(xmins + "body", description));
                result.Add(ownedComment);
            }

            result.Add(new XElement("name", name));

            return result;
        }

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



            var itemPackage = CreatePackageElement("ItemTypes", "ItemTypes", "Identified items within the model.");
            var complexPackage = CreatePackageElement("ComplexDataTypes", "ComplexDataTypes", "ComplexDataTypes are classes that are not identified and contain relationships to identified items or other complex data types.");
            var datatypePackage = CreatePackageElement("DataTypes", "DataTypes", "DataTypes are classes that are not identified and contain no relations to identified items or complex types.");


            lowerPrimatives = CogsTypes.SimpleTypeNames.Select(x => x.ToLower()).ToHashSet();

            itemTypeNames = model.ItemTypes.Select(x => x.Name).ToHashSet();
            dataTypeNames = model.ReusableDataTypes.Select(x => x.Name).ToHashSet();
            complexTypeNames = new HashSet<string>();

            // find all complex types that reference an item type, or contain a datatype whose children reference an item type
            int lastCount = -1;
            while (lastCount != complexTypeNames.Count)
            {
                lastCount = complexTypeNames.Count;

                foreach (var dataType in model.ReusableDataTypes)
                {
                    foreach (var property in dataType.Properties)
                    {
                        if (itemTypeNames.Contains(property.DataTypeName))
                        {
                            dataTypeNames.Remove(dataType.Name);
                            complexTypeNames.Add(dataType.Name);
                        }

                        if (complexTypeNames.Contains(property.DataTypeName))
                        {
                            dataTypeNames.Remove(dataType.Name);
                            complexTypeNames.Add(dataType.Name);
                        }
                    }
                }
            }




            // loop through classes and reusable data types
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                bool isDatatype = dataTypeNames.Contains(item.Name);

                if(isDatatype)
                {
                    datatypePackage.Add(CreateClassDescription(item, model, isDatatype));
                }
                else if(item is Model.ItemType)
                {
                    itemPackage.Add(CreateClassDescription(item, model, isDatatype));
                }
                else 
                {
                    complexPackage.Add(CreateClassDescription(item, model, isDatatype));
                }

            }

            //create document header based on format specified
            XDocument xDoc;
            var headerElement = string.IsNullOrEmpty(model.HeaderInclude) ? null : new XComment(model.HeaderInclude);

            XElement modelOwnedComment = null;
            if (!string.IsNullOrEmpty(model.Settings.Description))
            {
                modelOwnedComment = new XElement("ownedComment",
                    new XAttribute(xmins + "type", "uml:Comment"),
                    new XAttribute(xmins + "id", CreateId(model.Settings.Slug + "-ownedComment")),
                    new XElement("annotatedElement", new XAttribute(xmins + "idref", model.Settings.Slug)),
                    new XElement("body", model.Settings.Description));
            }

            var xmiModel = new XElement(umlns + "Model",
                            new XAttribute(xmins + "id", CreateId(model.Settings.Slug)),
                            modelOwnedComment,
                            new XElement("name", model.Settings.Slug));

            xDoc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                headerElement,
                new XElement(xmins + "XMI", 
                    new XAttribute(XNamespace.Xmlns + "uml", "http://www.omg.org/spec/UML/20131001"),
                    new XAttribute(XNamespace.Xmlns + "xmi", "http://www.omg.org/spec/XMI/20131001"),
                    xmiModel));

            xmiModel.Add(itemPackage, complexPackage, datatypePackage);


            //write collection to file
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(TargetDirectory, model.Settings.Slug + ".xmi")))
            {
                XmlTextWriter writer = new XmlTextWriter(outputFile);
                writer.Formatting = Formatting.Indented;
                xDoc.WriteTo(writer);
                writer.Flush();
            }
        }

        HashSet<string> itemTypeNames = new HashSet<string>();
        HashSet<string> dataTypeNames = new HashSet<string>();
        HashSet<string> complexTypeNames = new HashSet<string>();
        HashSet<string> lowerPrimatives = new HashSet<string>();
        private List<XElement> CreateClassDescription(Model.DataType item, CogsModel model, bool isDatatype = false)
        {
            var results = new List<XElement>();

            string umlType = "uml:Class";
            if(isDatatype)
            {
                umlType = "uml:DataType";
            }

            // Create class
            var newItem = new XElement("packagedElement", 
                new XAttribute(xmins + "type", umlType),
                new XAttribute(xmins + "id", CreateId(item.Name)),
                new XElement("ownedComment",
                    new XAttribute(xmins + "type", "uml:Comment"),
                    new XAttribute(xmins + "id", CreateId(item.Name + "-ownedComment")),
                    new XElement("annotatedElement", new XAttribute(xmins + "idref", item.Name)),
                    new XElement("body", item.Description)),
                new XElement("name", item.Name)
                );                        

            // adds pointers for inheritance where applicable
            if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
            {
                newItem.Add(new XElement("generalization",
                    new XAttribute(xmins + "type", "uml:Generalization"),
                    new XAttribute(xmins + "id", CreateId(item.Name + ".Generalization")),
                    new XElement("general", 
                        new XAttribute(xmins + "idref", item.ExtendsTypeName))));
            }

            // loop through properties of class and add to class
            foreach (var property in item.Properties)
            {
                // TODO define cogsDate as a datatype
                if (string.Equals(property.DataTypeName, "cogsDate", StringComparison.OrdinalIgnoreCase)) { property.DataTypeName = "string"; }

                var id = item.Name + "." + property.Name;
                bool isPrimative = lowerPrimatives.Contains(property.DataTypeName.ToLower());
                // if this property should be an association, ie an item or a complex datatype. Not a datatype with no relations
                if (dataTypeNames.Contains(property.DataTypeName) || isPrimative)
                {                    
                    // add as a uml:Property
                    var newProperty = new XElement("ownedAttribute", 
                        new XAttribute(xmins + "type", "uml:Property"),
                        new XAttribute(xmins + "id", CreateId(id)),
                        new XElement("ownedComment",
                            new XAttribute(xmins + "type", "uml:Comment"),
                            new XAttribute(xmins + "id", CreateId(id + "-ownedComment")),
                            new XElement("annotatedElement", new XAttribute(xmins + "idref", id)),
                            new XElement("body", property.Description)));

                    if (property.MinCardinality != null)
                    {
                        newProperty.Add(new XElement("lowerValue", new XAttribute(xmins + "type", "uml:LiteralInteger"),
                            new XAttribute(xmins + "id", CreateId(id + ".lowerValue")),
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
                            new XAttribute(xmins + "id", CreateId(id + ".upperValue")),
                            attribute));
                    }

                    newProperty.Add(new XElement("name", property.Name));
                    if (isPrimative)
                    {
                        newProperty.Add(new XElement("type", 
                            new XAttribute("href", $"http://www.w3.org/2001/XMLSchema#{property.DataTypeName.ToLowerInvariant()}"),
                            new XAttribute(xmins + "type", "uml:PrimitiveType")));
                    }
                    else
                    {
                        newProperty.Add(new XElement("type", new XAttribute(xmins + "idref", property.DataTypeName)));
                    }
                    

                    newItem.Add(newProperty);
                }
                else
                {
                    // add as a uml:Association
                    // create link association
                    var classLink = new XElement("packagedElement", 
                        new XAttribute(xmins + "type", "uml:Association"),
                        new XAttribute(xmins + "id", CreateId("Association.from" + id + ".to." + property.DataTypeName)));

                    classLink.Add(new XElement("name", property.Name));

                    classLink.Add(new XElement("memberEnd", 
                        new XAttribute(xmins + "idref", id + ".association")));
                    classLink.Add(new XElement("memberEnd",
                        new XAttribute(xmins + "idref", "Association.from" + id + ".to." + property.DataTypeName + ".ownedEnd")));

                    var isOrdered = "false";
                    if(property.Ordered) { isOrdered = "true"; }

                    var ownedEnd = new XElement("ownedEnd", 
                        new XAttribute(xmins + "type", "uml:Property"),
                        new XAttribute(xmins + "id", CreateId("Association.from" + id + ".to." + property.DataTypeName + ".ownedEnd")));

                    
                    // item types are always 0..* since they can be referenced
                    var min = "0";
                    var max = "*";
                    // check to see if item being referenced is a ReusableDataType
                    if (dataTypeNames.Contains(property.DataTypeName) || complexTypeNames.Contains(property.DataTypeName))
                    {
                        min = "1";
                        max = "1";
                    }
                    ownedEnd.Add(new XElement("lowerValue", new XAttribute(xmins + "type", "uml:LiteralInteger"),
                        new XAttribute(xmins + "id", CreateId("Association.from" + id + ".to." + property.DataTypeName + ".ownedEnd.MinCardinality")),
                        new XAttribute("value", min)));
                    ownedEnd.Add(new XElement("upperValue", new XAttribute(xmins + "type", "uml:LiteralUnlimitedNatural"),
                        new XAttribute(xmins + "id", CreateId("Association.from" + id + ".to." + property.DataTypeName + ".ownedEnd.MaxCardinality")),
                        new XAttribute("value", max)));

                    ownedEnd.Add(new XElement("association", new XAttribute(xmins + "idref", "Association.from" + id + ".to." + property.DataTypeName)));
                    ownedEnd.Add(new XElement("type", new XAttribute(xmins + "idref", item.Name)));

                    classLink.Add(ownedEnd);

                    results.Add(classLink);


                    // reference link from current class as attribute
                    var link = new XElement("ownedAttribute", new XAttribute(xmins + "type", "uml:Property"),
                        new XAttribute(xmins + "id", CreateId(id + ".association")),
                        new XAttribute("name", property.Name),
                        new XAttribute("association", "Association.from" + property.Name + ".to." + property.DataTypeName),
                        new XAttribute("isOrdered", "true"),
                        new XElement("ownedComment",
                            new XAttribute(xmins + "type", "uml:Comment"),
                            new XAttribute(xmins + "id", CreateId(id + ".association" + "-ownedComment")),
                            new XElement("annotatedElement", new XAttribute(xmins + "idref", id + ".association")),
                            new XElement("body", property.Description)));

                    
                    link.Add(new XElement("lowerValue", new XAttribute(xmins + "type", "uml:LiteralInteger"),
                        new XAttribute(xmins + "id", CreateId(id + ".association.lowerValue")),
                        new XAttribute("value", property.MinCardinality)));
                    link.Add(new XElement("upperValue", new XAttribute(xmins + "type", "uml:LiteralUnlimitedNatural"),
                        new XAttribute(xmins + "id", CreateId(id + ".association.upperValue")),
                        new XAttribute("value", property.MaxCardinality)));
                    link.Add(new XElement("name", property.Name));
                    link.Add(new XElement("isOrdered", isOrdered));
                    link.Add(new XElement("type", new XAttribute(xmins + "idref", property.DataTypeName)));
                    newItem.Add(link);
                }


            }

            // add class to model
            results.Insert(0, newItem);

            return results;
        }


        private XElement CreateDiagram(string diagramName, List<Model.ItemType> items, CogsModel model)
        {

            // run svg publisher to create svg file to use for positioning
            DotSchemaPublisher publisher = new DotSchemaPublisher
            {
                TargetDirectory = TargetDirectory,
                Overwrite = Overwrite,
                Format = "svg",
                Output = "all",
                Inheritance = true,
                DotLocation = DotLocation,
                ShowReusables = true
            };
            publisher.Publish(model);

            var xOff = 0.0;
            var yOff = 0.0;
            var offset = 2.5;
            XNamespace ns = "http://www.w3.org/2000/svg";
            // get current date and time for when setting created and last modified settings
            var currentTime = DateTime.UtcNow.Year + "-" + DateTime.UtcNow.Month + "-" + DateTime.UtcNow.Day + " " + DateTime.UtcNow.Hour + ":" +
                DateTime.UtcNow.Minute + ":" + DateTime.UtcNow.Second;

            // read created svg file
            List<XElement> nodes = XDocument.Load(Path.Combine(TargetDirectory, "output.svg")).Root.Descendants(ns + "g")
                .Where(x => x.Attribute("class").Value == "node").ToList();
            File.Delete(Path.Combine(TargetDirectory, "output.svg"));

            //get leftmost  and topmost value to shift graph accordingly 
            foreach (var item in nodes.Descendants(ns + "title").ToList())
            {
                var node = XElement.Parse(item.NextNode.ToString());
                if (Convert.ToDouble(node.Attribute("cx").Value) < xOff)
                {
                    xOff = Convert.ToDouble(node.Attribute("cx").Value);
                }
                if (Convert.ToDouble(node.Attribute("cy").Value) < yOff)
                {
                    yOff = Convert.ToDouble(node.Attribute("cy").Value);
                }
                xOff = Math.Abs(xOff);
                yOff = Math.Abs(yOff);
            }


            XElement diagramElements = new XElement("elements");

            int seqno = 1;
            foreach (var item in model.ItemTypes)
            {
                try
                {
                    // add class to diagram
                    var node = XElement.Parse(nodes.Descendants(ns + "text").Where(x => x.FirstNode.ToString().Contains(item.Name)).ToList()[0].PreviousNode.ToString());
                    var left = (Double.Parse(node.Attribute("cx").Value) - Double.Parse(node.Attribute("rx").Value) + xOff) * offset;
                    var right = (Double.Parse(node.Attribute("cx").Value) + Double.Parse(node.Attribute("rx").Value) + xOff) * offset;
                    var top = (Double.Parse(node.Attribute("cy").Value) - Double.Parse(node.Attribute("ry").Value) + yOff) * offset;
                    var bottom = (Double.Parse(node.Attribute("cy").Value) + Double.Parse(node.Attribute("ry").Value) + yOff) * offset;
                    diagramElements.Add(new XElement("element",
                        new XAttribute("geometry", "Left=" + left + ";Top=" + top + ";Right=" + right + ";Bottom=" + bottom + ";"),
                        new XAttribute("subject", item.Name),
                        new XAttribute("seqno", seqno.ToString()),
                        new XAttribute("style", "DUID=" + item.Name + ";NSL=0;BCol=-1;BFol=-1;LCol=-1;LWth=-1;fontsz=0;bold=0;black=0;italic=0;ul=0;charset=0;pitch=0;));")));



                    seqno++;
                }
                catch 
                { 

                }
            }

            int diagramNumber = 1;

            var diagram = new XElement("diagrams", 
                new XElement("diagram", 
                    new XAttribute(xmins + "id", CreateId("ModelDiagram" + diagramNumber)),
                    new XElement("model", 
                        new XAttribute("package", model.Settings.Slug), new XAttribute("localID", diagramNumber), new XAttribute("owner", model.Settings.Slug)),
                        new XElement("properties", 
                            new XAttribute("name", model.Settings.Slug), new XAttribute("type", "Logical")),
                            new XElement("project", 
                                new XAttribute("author", "computer"), new XAttribute("version", "1.0"), new XAttribute("created", currentTime), new XAttribute("modified", currentTime)),
                                new XElement("style1", 
                                    new XAttribute("value", "ShowPrivate=1;ShowProtected=1;ShowPublic=1;HideRelationships=0;Locked=0;Border=1;HighlightForeign=1;" +
                                       "PackageContents=1;SequenceNotes=0;ScalePrintImage=0;PPgs.cx=1;PPgs.cy=1;DocSize.cx=815;DocSize.cy=1067;ShowDetails=0;Orientation=P;" +
                                       "Zoom=100;ShowTags=0;OpParams=1;VisibleAttributeDetail=0;ShowOpRetType=1;ShowIcons=1;CollabNums=0;HideProps=0;ShowReqs=0;ShowCons=0;PaperSize=1;" +
                                       "HideParents=0;UseAlias=0;HideAtts=0;HideOps=0;HideStereo=0;HideElemStereo=0;ShowTests=0;ShowMaint=0;ConnectorNotation=UML 2.1;ExplicitNavigability=0;" +
                                       "ShowShape=1;AdvancedElementProps=1;AdvancedFeatureProps=1;AdvancedConnectorProps=1;m_bElementClassifier=1;ShowNotes=0;SuppressBrackets=0;SuppConnectorLabels=0;" +
                                       "PrintPageHeadFoot=0;ShowAsList=0;")),
                                new XElement("style2", 
                                    new XAttribute("value", "ExcludeRTF=0;DocAll=0;HideQuals=0;AttPkg=1;ShowTests=0;ShowMaint=0;" +
                                       "SuppressFOC=1;MatrixActive=0;SwimlanesActive=1;KanbanActive=0;MatrixLineWidth=1;MatrixLineClr=0;MatrixLocked=0;TConnectorNotation=UML 2.1;TExplicitNavigability=0;" +
                                       "AdvancedElementProps=1;AdvancedFeatureProps=1;AdvancedConnectorProps=1;m_bElementClassifier=1;ProfileData=;MDGDgm=;STBLDgm=;ShowNotes=0;VisibleAttributeDetail=0;" +
                                       "ShowOpRetType=1;SuppressBrackets=0;SuppConnectorLabels=0;PrintPageHeadFoot=0;ShowAsList=0;SuppressedCompartments=;Theme=:119;SaveTag=D7ED2A20;")),
                                new XElement("swimlanes", 
                                    new XAttribute("value", "locked=false;orientation=0;width=0;inbar=false;names=false;color=-1;bold=false;fcol=0;tcol=-1;ofCol=-1;ufCol=-1;" +
                                        "hl=0;ufh=0;cls=0;SwimlaneFont=lfh:-10,lfw:0,lfi:0,lfu:0,lfs:0,lfface:Calibri,lfe:0,lfo:0,lfchar:1,lfop:0,lfcp:0,lfq:0,lfpf=0,lfWidth=0;")),
                                new XElement("matrixitems", 
                                    new XAttribute("value", "locked=false;matrixactive=false;swimlanesactive=true;kanbanactive=false;width=1;clrLine=0;")),
                                new XElement("extendedProperties"), 
                                    diagramElements));

            return diagram;
        }

        // helper method that takes a string and checks that the ID has not been previously created
        // returns the string if valid, otherwise throws InvalidOperationException
        private string CreateId(string name)
        {
            if (IdList.Contains(name))
            {
                Console.WriteLine("ERROR: name '{0}' used twice", name);
                throw new InvalidOperationException();
            }
            IdList.Add(name);
            return name;
        }
    }
}