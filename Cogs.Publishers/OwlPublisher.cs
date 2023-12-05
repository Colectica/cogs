// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Cogs.Publishers
{
    public class OwlPublisher
    {
        public string CogsLocation { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }
        public string TargetNamespacePrefix { get; set; }

        public string TargetNamespace { get; set; }

        public string VersionInfo { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }

        public List<DataType> ReusableStorage { get; set; }
        public List<ItemType> ItemTypeStorage { get; set; }
        public HashSet<string> set { get; set; }
        public Dictionary<string, List<string>> map { get; set; }

        private CogsModel model { get; set; }

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

            this.model = model;

            ReusableStorage = model.ReusableDataTypes;
            ItemTypeStorage = model.ItemTypes;
            set = new HashSet<string>();
            map = new Dictionary<string, List<string>>();

            //Start here
            var projName = model.Settings.Slug;
            StringBuilder res = new StringBuilder();
            res.AppendLine(@"<?xml version=""1.0""?>");

            if (!string.IsNullOrWhiteSpace(this.model.HeaderInclude))
            {
                res.AppendLine("<!--");
                res.AppendLine(this.model.HeaderInclude);
                res.AppendLine("-->");
            }

            res.AppendLine(@"<rdf:RDF xmlns=""" + TargetNamespace +@"""");
            res.AppendLine(@"   xml:base=""" + TargetNamespacePrefix + @"""");
            res.AppendLine(@"   xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""");
            res.AppendLine(@"   xmlns:owl=""http://www.w3.org/2002/07/owl#""");
            res.AppendLine(@"   xmlns:xml=""http://www.w3.org/XML/1998/namespace""");
            res.AppendLine(@"   xmlns:xsd=""http://www.w3.org/2001/XMLSchema#""");
            res.AppendLine(@"   xmlns:rdfs=""http://www.w3.org/2000/01/rdf-schema#"">");
            if(!String.IsNullOrWhiteSpace(VersionInfo) || !String.IsNullOrWhiteSpace(Description) || !String.IsNullOrWhiteSpace(Title))
            {
                res.AppendLine(@"   <owl:Ontology rdf:about=""" + TargetNamespace + @""">");
                if (!String.IsNullOrWhiteSpace(VersionInfo))
                {
                    res.AppendLine(@"       <owl:versionInfo>" + VersionInfo + "</owl:versionInfo>");
                }
                if (!String.IsNullOrWhiteSpace(Description))
                {
                    res.AppendLine(@"       <rdfs:comment>" + Description + "</rdfs:comment>");
                }
                if(!String.IsNullOrWhiteSpace(Title))
                {
                    res.AppendLine(@"       <rdfs:label>" + Title + "</rdfs:label>");
                }
                res.AppendLine(@"   </owl:Ontology>");
            }
            else
            {
                res.AppendLine(@"   <owl:Ontology rdf:about=""" + TargetNamespace +@"""/>");
            }
            res.AppendLine("\n");
            
            //Generate each ItemType class and add to result
            GenComment(res, "Annotation properties & Datatypes");
            PredefineSimple(res, projName);

            GenComment(res, "Classes");
            GenOwlClass(res, model.ItemTypes, null, projName);
            GenOwlClass(res, null, model.ReusableDataTypes, projName);

            GenComment(res, "SimpleType");
            GenSimpleClass(res, projName);

            GenComment(res, "DataProperty Type & Objectproperty");
            GenProperty(res, model.ItemTypes, null, projName);
            GenProperty(res, null, model.ReusableDataTypes, projName);

            res.AppendLine(@"</rdf:RDF>");
            File.WriteAllText(Path.Combine(TargetDirectory, projName + ".owl"), res.ToString());
        }

        public void PredefineSimple(StringBuilder res, String projName)
        {
            res.AppendLine(@"   <owl:AnnotationProperty rdf:about=""http://www.w3.org/2002/07/owl#minQualifiedCardinality""/>");
            res.AppendLine(@"   <owl:AnnotationProperty rdf:about = ""http://www.w3.org/2002/07/owl#maxQualifiedCardinality""/>");
            res.AppendLine(@"   <rdfs:Datatype rdf:about=""http://www.w3.org/2001/XMLSchema#date""/>");
            res.AppendLine(@"   <rdfs:Datatype rdf:about=""http://www.w3.org/2001/XMLSchema#duration""/>");
            res.AppendLine(@"   <rdfs:Datatype rdf:about=""http://www.w3.org/2001/XMLSchema#gDay""/>");
            res.AppendLine(@"   <rdfs:Datatype rdf:about=""http://www.w3.org/2001/XMLSchema#gMonth""/>");
            res.AppendLine(@"   <rdfs:Datatype rdf:about=""http://www.w3.org/2001/XMLSchema#gYear""/>");
            res.AppendLine(@"   <rdfs:Datatype rdf:about=""http://www.w3.org/2001/XMLSchema#gYearMonth""/>");
            res.AppendLine(@"   <rdfs:Datatype rdf:about=""http://www.w3.org/2001/XMLSchema#time""/>");
            res.AppendLine(@"   <rdfs:Datatype rdf:about=""http://www.w3.org/2001/XMLSchema#gMonthDay""/>");
            res.AppendLine(@"   <rdfs:Datatype rdf:about=""http://www.w3.org/2001/XMLSchema#cogsDate""/>");
            res.AppendLine(@"   <owl:DatatypeProperty rdf:about=""" + TargetNamespace +@"#CogsDate""/>");
            res.AppendLine("\n");
        }
        public void GenOwlClass(StringBuilder res, List<ItemType> itemType, List<DataType> reusable, String projName)
        {
            if (itemType != null)
            {
                //generate class for all itemtype
                foreach (var item in itemType)
                {
                    StringBuilder GenClass = new StringBuilder();
                    GenClass.AppendLine(@"  <owl:Class rdf:about="""+TargetNamespace + "#" + item.Name + @""">");
                    GenClass.AppendLine(@"    <rdfs:label xml:lang=""en"">"+ item.Name +"</rdfs:label>");
                    GenClass.AppendLine(@"      <rdfs:comment>"+item.Description+"</rdfs:comment>");
                    if(item.ExtendsTypeName == null || item.ExtendsTypeName.Equals(""))
                    {
                        GenClass.AppendLine(@"      <rdfs:subClassOf rdf:resource=""" + TargetNamespace +@"""/>");
                    }
                    else
                    {
                        GenClass.AppendLine(@"      <rdfs:subClassOf rdf:resource=""" + TargetNamespace +"#" + item.ExtendsTypeName +@"""/>");
                    }
                    var keyPropertiesList = model.Identification.Where(x => item.Properties.Any(y => y.Name == x.Name)).ToList();//if they are injected to a base class
                    if (keyPropertiesList.Count != 0)
                    {
                        GenClass.AppendLine(@"      <owl:hasKey rdf:parseType = ""Collection"">");
                        foreach(var prop in keyPropertiesList)
                        {                           
                            GenClass.AppendLine(@"          <rdf:Description rdf:about=""" + TargetNamespace + "#" + prop.Name + @"""/>");
                        }
                        GenClass.AppendLine(@"      </owl:hasKey>");
                    }
                    if (item.Properties.Count != 0)
                    {
                        foreach (var prop in item.Properties)
                        {
                            if (!map.ContainsKey(prop.Name))
                            {
                                map[prop.Name] = new List<String>();
                                map[prop.Name].Add(item.Name);
                            }
                            else
                            {
                                map[prop.Name].Add(item.Name);
                            }
                        }
                    }
                    GenClass.AppendLine(@"  </owl:Class>");
                    res.Append(GenClass.ToString());
                    res.AppendLine("\n");
                }
            }
            else
            {
                //generate class for all reusabletype
                foreach (var item in reusable)
                {
                    StringBuilder GenClass = new StringBuilder();
                    GenClass.AppendLine(@"  <owl:Class rdf:about=""" + TargetNamespace + "#" + item.Name + @""">");
                    GenClass.AppendLine(@"    <rdfs:label xml:lang=""en"">" + item.Name + "</rdfs:label>");
                    GenClass.AppendLine(@"      <rdfs:comment>" + item.Description + "</rdfs:comment>");
                    if (item.ExtendsTypeName == null || item.ExtendsTypeName.Equals(""))
                    {
                        GenClass.AppendLine(@"      <rdfs:subClassOf rdf:resource=""" + TargetNamespace + @"""/>");
                    }
                    else
                    {
                        GenClass.AppendLine(@"      <rdfs:subClassOf rdf:resource=""" + TargetNamespace + "#" + item.ExtendsTypeName + @"""/>");
                    }
                    if (item.Properties.Count != 0)
                    {
                        foreach (var prop in item.Properties)
                        {
                            if (!map.ContainsKey(prop.Name))
                            {
                                map[prop.Name] = new List<String>();
                                map[prop.Name].Add(item.Name);
                            }
                            else
                            {
                                map[prop.Name].Add(item.Name);
                            }
                        }
                    }
                    GenClass.AppendLine(@"  </owl:Class>");
                    res.AppendLine(GenClass.ToString());
                    res.AppendLine("\n");
                }
            }
        }

        public void GenProperty(StringBuilder res, List<ItemType> itemtype, List<DataType> reusable, String projName)
        {
            if (itemtype != null)
            {
                foreach (var props in itemtype)
                {
                    foreach (var prop in props.Properties)
                    {
                        StringBuilder DataProp = new StringBuilder();
                        StringBuilder ObjectProp = new StringBuilder();
                        //Set dataproperty, and objectproperty
                        if (!set.Contains(prop.Name))
                        {
                            set.Add(prop.Name);
                            if (IsItemType(prop.DataType.Name) || IsReusableType(prop.DataType.Name))
                            {
                                ObjectProp.AppendLine(@"  <owl:ObjectProperty rdf:about = """ + TargetNamespace + "#" + prop.Name + @""">");
                                ObjectProp.AppendLine(@"    <rdfs:label xml:lang=""en"">" + prop.Name + "</rdfs:label>");
                                ObjectProp.AppendLine(@"    <rdfs:comment>" + prop.Description + "</rdfs:comment>");
                                //ObjectProp.AppendLine(@"    <rdfs:domain rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + props.Name + @"""/>");
                                addRestrictionDomain(ObjectProp, prop, projName);
                                addRestrictionRange(ObjectProp, prop, projName);
                                ObjectProp.AppendLine(@"  </owl:ObjectProperty>");

                            }
                            else
                            {
                                if (prop.DataType.Name.Equals("cogsDate"))
                                {
                                    DataProp.AppendLine(@"      <owl:ObjectProperty rdf:about = """ + TargetNamespace + @"#CogsDate"">");
                                    //DataProp.AppendLine(@"          <rdfs:domain rdf:resource = ""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#" + props.Name + @"""/>");
                                    DataProp.AppendLine(@"      <rdfs:label xml:lang=""en"">CogsDate</rdfs:label>");
                                    addRestrictionDomain(DataProp, prop, projName);
                                    DataProp.AppendLine(@"          <rdfs:range>");
                                    DataProp.AppendLine(@"              <owl:Restriction>");
                                    DataProp.AppendLine(@"                  <owl:onProperty rdf:resource = """+ TargetNamespace + @"#CogsDate""/>");
                                    DataProp.AppendLine(@"                  <owl:minQualifiedCardinality rdf:datatype = ""http://www.w3.org/2001/XMLSchema#nonNegativeInteger"" > 0 </owl:minQualifiedCardinality>");
                                    DataProp.AppendLine(@"                  <owl:onDataRange rdf:resource = ""http://www/w3.org/2001/XMLSchema#cogsDate"" />");
                                    DataProp.AppendLine(@"              </owl:Restriction>");
                                    DataProp.AppendLine(@"          </rdfs:range>");
                                    DataProp.AppendLine(@"      </owl:ObjectProperty>");
                                }
                                else
                                {
                                    DataProp.AppendLine(@"  <owl:DatatypeProperty rdf:about = """+ TargetNamespace + "#" + prop.Name + @""">");
                                    DataProp.AppendLine(@"      <rdfs:label xml:lang=""en"">" + prop.Name + "</rdfs:label>");
                                    DataProp.AppendLine(@"      <rdfs:comment>" + prop.Description + "</rdfs:comment>");
                                    //DataProp.AppendLine(@"      <rdfs:domain rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + props.Name + @"""/>");
                                    addRestrictionDomain(DataProp, prop, projName);
                                    addRestrictionRange(DataProp, prop, projName);
                                    DataProp.AppendLine(@"  </owl:DatatypeProperty>");

                                }
                            }
                        }

                        if (DataProp.Length > 0)
                        {
                            res.Append(DataProp.ToString());
                            res.AppendLine("\n");
                        }
                        if (ObjectProp.Length > 0)
                        {
                            res.Append(ObjectProp.ToString());
                            res.AppendLine("\n");
                        }
                        //}
                    }
                }
            }
            else
            {
                foreach (var props in reusable)
                {
                    foreach (var prop in props.Properties)
                    {
                        StringBuilder DataProp = new StringBuilder();
                        StringBuilder ObjectProp = new StringBuilder();
                        if (!set.Contains(prop.Name))
                        {
                            set.Add(prop.Name);
                            if (IsItemType(prop.DataType.Name) || IsReusableType(prop.DataType.Name))
                            {
                                ObjectProp.AppendLine(@"  <owl:ObjectProperty rdf:about = """ + TargetNamespace + "#" + prop.Name + @""">");
                                ObjectProp.AppendLine(@"    <rdfs:label xml:lang=""en"">" + prop.Name + "</rdfs:label>");
                                ObjectProp.AppendLine(@"    <rdfs:comment>" + prop.Description + "</rdfs:comment>");
                                //ObjectProp.AppendLine(@"    <rdfs:domain rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + props.Name + @"""/>");
                                addRestrictionDomain(ObjectProp, prop, projName);
                                addRestrictionRange(ObjectProp, prop, projName);
                                ObjectProp.AppendLine(@"  </owl:ObjectProperty>");

                            }
                            else
                            {
                                if (prop.DataType.Name.Equals("cogsDate"))
                                {
                                    DataProp.AppendLine(@"      <owl:ObjectProperty rdf:about = """ + TargetNamespace + @"#CogsDate"">");
                                    DataProp.AppendLine(@"          <rdfs:label xml:lang=""en"">CogsDate</rdfs:label>");
                                    //DataProp.AppendLine(@"          <rdfs:domain rdf:resource = ""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#" + props.Name + @"""/>");
                                    addRestrictionDomain(DataProp, prop, projName);
                                    DataProp.AppendLine(@"          <rdfs:range>");
                                    DataProp.AppendLine(@"              <owl:Restriction>");
                                    DataProp.AppendLine(@"                  <owl:onProperty rdf:resource = """+ TargetNamespace + @"#CogsDate""/>");
                                    DataProp.AppendLine(@"                  <owl:minQualifiedCardinality rdf:datatype = ""http://www.w3.org/2001/XMLSchema#nonNegativeInteger"" > 0 </owl:minQualifiedCardinality>");
                                    DataProp.AppendLine(@"                  <owl:onDataRange rdf:resource = ""http://www/w3.org/2001/XMLSchema#cogsDate"" />");
                                    DataProp.AppendLine(@"              </owl:Restriction>");
                                    DataProp.AppendLine(@"          </rdfs:range>");
                                    DataProp.AppendLine(@"      </owl:ObjectProperty>");
                                }
                                else
                                {
                                    DataProp.AppendLine(@"  <owl:DatatypeProperty rdf:about = """ + TargetNamespace + "#" + prop.Name + @""">");
                                    DataProp.AppendLine(@"      <rdfs:label xml:lang=""en"">" + prop.Name + "</rdfs:label>");
                                    DataProp.AppendLine(@"      <rdfs:comment>" + prop.Description + "</rdfs:comment>");
                                    //DataProp.AppendLine(@"      <rdfs:domain rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + props.Name + @"""/>");
                                    addRestrictionDomain(DataProp, prop, projName);
                                    addRestrictionRange(DataProp, prop, projName);
                                    DataProp.AppendLine(@"  </owl:DatatypeProperty>");
                                }
                            }
                        }
                        if (DataProp.Length > 0)
                        {
                            res.Append(DataProp.ToString());
                            res.AppendLine("\n");
                        }
                        if (ObjectProp.Length > 0)
                        {
                            res.Append(ObjectProp.ToString());
                            res.AppendLine("\n");
                        } 
                    }
                }
            }
        }

        public void GenSimpleClass(StringBuilder res, String projName)
        {
            StringBuilder GenCogsDate = new StringBuilder();
            GenCogsDate.AppendLine(@"  <owl:ObjectProperty rdf:about=""" + TargetNamespace + @"#cogsDate"">");
            GenCogsDate.AppendLine(@"    <rdfs:label xml:lang=""en"">cogsDate</rdfs:label>");
            GenCogsDate.AppendLine(@"    <rdfs:comment>Simple Type for cogsDate</rdfs:comment>");
            GenCogsDate.AppendLine(@"       <rdfs:range>");
            GenCogsDate.AppendLine(@"           <owl:Restriction>");
            GenCogsDate.AppendLine(@"               <owl:onProperty rdf:resource = ""http://www.w3.org/2001/XMLSchema#date""/>");
            GenCogsDate.AppendLine(@"               <owl:minQualifiedCardinality rdf:datatype = ""http://www.w3.org/2001/XMLSchema#nonNegativeInteger""> 0 </owl:minQualifiedCardinality>");
            GenCogsDate.AppendLine(@"               <owl:onDataRange rdf:resource = ""http://www.w3.org/2001/XMLSchema#date""/>");
            GenCogsDate.AppendLine(@"           </owl:Restriction>  ");
            GenCogsDate.AppendLine(@"       </rdfs:range>");
            GenCogsDate.AppendLine(@"       <rdfs:range>");
            GenCogsDate.AppendLine(@"           <owl:Restriction>");
            GenCogsDate.AppendLine(@"               <owl:onProperty rdf:resource = ""http://www.w3.org/2001/XMLSchema#dateTime""/>");
            GenCogsDate.AppendLine(@"               <owl:minQualifiedCardinality rdf:datatype = ""http://www.w3.org/2001/XMLSchema#nonNegativeInteger"" > 0 </owl:minQualifiedCardinality>");
            GenCogsDate.AppendLine(@"               <owl:onDataRange rdf:resource = ""http://www.w3.org/2001/XMLSchema#dateTime""/>");
            GenCogsDate.AppendLine(@"           </owl:Restriction>");
            GenCogsDate.AppendLine(@"       </rdfs:range>");
            GenCogsDate.AppendLine(@"       <rdfs:range>");
            GenCogsDate.AppendLine(@"           <owl:Restriction>");
            GenCogsDate.AppendLine(@"               <owl:onProperty rdf:resource = ""http://www.w3.org/2001/XMLSchema#duration""/>");
            GenCogsDate.AppendLine(@"               <owl:minQualifiedCardinality rdf:datatype = ""http://www.w3.org/2001/XMLSchema#nonNegativeInteger""> 0 </owl:minQualifiedCardinality>");
            GenCogsDate.AppendLine(@"               <owl:onDataRange rdf:resource = ""http://www.w3.org/2001/XMLSchema#duration""/>");
            GenCogsDate.AppendLine(@"           </owl:Restriction>");
            GenCogsDate.AppendLine(@"       </rdfs:range>");
            GenCogsDate.AppendLine(@"       <rdfs:range>");
            GenCogsDate.AppendLine(@"           <owl:Restriction>");
            GenCogsDate.AppendLine(@"               <owl:onProperty rdf:resource = ""http://www.w3.org/2001/XMLSchema#gYear""/>");
            GenCogsDate.AppendLine(@"               <owl:minQualifiedCardinality rdf:datatype = ""http://www.w3.org/2001/XMLSchema#nonNegativeInteger""> 0 </owl:minQualifiedCardinality>");
            GenCogsDate.AppendLine(@"               <owl:onDataRange rdf:resource = ""http://www.w3.org/2001/XMLSchema#gYear""/>");
            GenCogsDate.AppendLine(@"           </owl:Restriction>");
            GenCogsDate.AppendLine(@"       </rdfs:range>");
            GenCogsDate.AppendLine(@"       <rdfs:range>");
            GenCogsDate.AppendLine(@"           <owl:Restriction>");
            GenCogsDate.AppendLine(@"               <owl:onProperty rdf:resource = ""http://www.w3.org/2001/XMLSchema#gYearMonth""/>");
            GenCogsDate.AppendLine(@"               <owl:minQualifiedCardinality rdf:datatype = ""http://www.w3.org/2001/XMLSchema#nonNegativeInteger""> 0 </owl:minQualifiedCardinality>");
            GenCogsDate.AppendLine(@"               <owl:onDataRange rdf:resource = ""http://www.w3.org/2001/XMLSchema#gYearMonth""/>");
            GenCogsDate.AppendLine(@"           </owl:Restriction>");
            GenCogsDate.AppendLine(@"        </rdfs:range>");
            GenCogsDate.AppendLine(@" </owl:ObjectProperty>");
            res.Append(GenCogsDate.ToString());
            res.AppendLine("\n");
            //date
            StringBuilder GenDate = new StringBuilder();
            GenDate.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#date"">");
            GenDate.AppendLine(@"    <rdfs:label xml:lang=""en"">date</rdfs:label>");
            GenDate.AppendLine(@"    <rdfs:comment>Simple Type for date</rdfs:comment>");
            GenDate.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#date""/>");
            GenDate.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GenDate.ToString());
            res.AppendLine("\n");
            //dateTime
            StringBuilder GenDateTime = new StringBuilder();
            GenDateTime.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#dateTime"">");
            GenDateTime.AppendLine(@"    <rdfs:label xml:lang=""en"">dateTime</rdfs:label>");
            GenDateTime.AppendLine(@"    <rdfs:comment>Simple Type for dateTime</rdfs:comment>");
            GenDateTime.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#dateTime""/>");
            GenDateTime.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GenDateTime.ToString());
            res.AppendLine("\n");
            //duration
            StringBuilder GenDuration = new StringBuilder();
            GenDuration.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#duration"">");
            GenDuration.AppendLine(@"    <rdfs:label xml:lang=""en"">duration</rdfs:label>");
            GenDuration.AppendLine(@"    <rdfs:comment>Simple Type for date</rdfs:comment>");
            GenDuration.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#duration""/>");
            GenDuration.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GenDuration.ToString());
            res.AppendLine("\n");
            //gYear
            StringBuilder GengYear = new StringBuilder();
            GengYear.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#gYear"">");
            GengYear.AppendLine(@"    <rdfs:label xml:lang=""en"">gYear</rdfs:label>");
            GengYear.AppendLine(@"    <rdfs:comment>Simple Type for gYear</rdfs:comment>");
            GengYear.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#gYear""/>");
            GengYear.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GengYear.ToString());
            res.AppendLine("\n");
            //gYearMonth
            StringBuilder GengYearMonth = new StringBuilder();
            GengYearMonth.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#gYearMonth"">");
            GengYearMonth.AppendLine(@"    <rdfs:label xml:lang=""en"">gYearMonth</rdfs:label>");
            GengYearMonth.AppendLine(@"    <rdfs:comment>Simple Type for gYearMonth</rdfs:comment>");
            GengYearMonth.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#gYearMonth""/>");
            GengYearMonth.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GengYearMonth.ToString());
            res.AppendLine("\n");
        }
        public void addRestrictionDomain(StringBuilder res, Property prop, string projName)
        {
            if (map.ContainsKey(prop.Name))
            {
                res.AppendLine(@"       <rdfs:domain>");
                res.AppendLine(@"           <owl:Restriction>");
                res.AppendLine(@"           <owl:onProperty rdf:resource = """ + TargetNamespace + "#" + prop.Name + @"""/>");
                res.AppendLine(@"               <owl:allValuesFrom >");
                res.AppendLine(@"                   <owl:Class>");
                res.AppendLine(@"                   <owl:unionOf rdf:parseType = ""Collection"">");
                foreach (var parent in map[prop.Name])
                {
                    res.AppendLine(@"                       <rdf:Description rdf:about = """ + TargetNamespace + "#" + parent + @"""/>");
                }
                res.AppendLine(@"                   </owl:unionOf>");
                res.AppendLine(@"                   </owl:Class>");
                res.AppendLine(@"               </owl:allValuesFrom>");
                res.AppendLine(@"           </owl:Restriction>");
                res.AppendLine(@"       </rdfs:domain>");
            }
        }

        public void addRestrictionRange(StringBuilder OProp, Property prop, String projName)
        {
            if (prop.MinCardinality.Equals("0") && prop.MaxCardinality.Equals("n"))
            {
                if (!IsReusableType(prop.DataType.Name) && !IsItemType(prop.DataType.Name))
                {
                    if(prop.DataType.Name.ToLower() == "langstring")
                    {
                        OProp.AppendLine(@"      <rdfs:range rdf:resource=""http://www.w3.org/1999/02/22-rdf-syntax-ns#PlainLiteral""/>");
                    }
                    else
                    {
                        OProp.AppendLine(@"      <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#" + prop.DataType.Name + @"""/>");
                    }
                    
                }
                else
                {
                    OProp.AppendLine(@"      <rdfs:range rdf:resource=""" + TargetNamespace + "#" + prop.DataType.Name + @"""/>");
                }
            }
            else
            {
                if (!prop.MinCardinality.Equals("0"))
                {
                    OProp.AppendLine(@"      <rdfs:range>");
                    OProp.AppendLine(@"        <owl:Restriction>");
                    OProp.AppendLine(@"            <owl:onProperty rdf:resource=""" + TargetNamespace + "#" + prop.Name + @"""/>");
                    OProp.AppendLine(@"            <owl:minQualifiedCardinality rdf:datatype = ""http://www.w3.org/2001/XMLSchema#nonNegativeInteger"">" + prop.MinCardinality + @"</owl:minQualifiedCardinality>");
                    //OProp.AppendLine(@"            <owl:onClass rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + prop.DataType.Name + @"""/>");
                    if (!IsReusableType(prop.DataType.Name) && !IsItemType(prop.DataType.Name))
                    {
                        if (prop.DataType.Name.ToLower() == "langstring")
                        {
                            OProp.AppendLine(@"      <owl:onDataRange rdf:resource=""http://www.w3.org/1999/02/22-rdf-syntax-ns#PlainLiteral""/>");
                        }
                        else
                        {
                            OProp.AppendLine(@"      <owl:onDataRange rdf:resource = ""http://www.w3.org/2001/XMLSchema#" + prop.DataType.Name + @"""/>");

                        }
                    }
                    else
                    {
                        OProp.AppendLine(@"            <owl:onClass rdf:resource=""" + TargetNamespace + "#" + prop.DataType.Name + @"""/>");
                    }
                    //OProp.AppendLine(@"        </owl:Restriction>");
                    OProp.AppendLine(@" </owl:Restriction>");
                    OProp.AppendLine(@"      </rdfs:range>");
                }
                if (!prop.MaxCardinality.Equals("n"))
                {
                    OProp.AppendLine(@"      <rdfs:range>");
                    OProp.AppendLine(@"        <owl:Restriction>");
                    OProp.AppendLine(@"            <owl:onProperty rdf:resource=""" + TargetNamespace + "#" + prop.Name + @"""/>");
                    OProp.AppendLine(@"            <owl:maxQualifiedCardinality rdf:datatype = ""http://www.w3.org/2001/XMLSchema#nonNegativeInteger"">" + prop.MaxCardinality + @"</owl:maxQualifiedCardinality>");
                    if (!IsReusableType(prop.DataType.Name) && !IsItemType(prop.DataType.Name))
                    {
                        if (prop.DataType.Name.ToLower() == "langstring")
                        {
                            OProp.AppendLine(@"      <owl:onDataRange rdf:resource=""http://www.w3.org/1999/02/22-rdf-syntax-ns#PlainLiteral""/>");
                        }
                        else
                        {
                            OProp.AppendLine(@"      <owl:onDataRange rdf:resource = ""http://www.w3.org/2001/XMLSchema#" + prop.DataType.Name + @"""/>");
                        }
                    }
                    else
                    {
                        OProp.AppendLine(@"            <owl:onClass rdf:resource="""+ TargetNamespace + "#" + prop.DataType.Name + @"""/>");
                    }
                    OProp.AppendLine(@"        </owl:Restriction>");
                    OProp.AppendLine(@"      </rdfs:range>");
                }
            }
        }

        public Boolean IsItemType(string type)
        {
            foreach (var item in ItemTypeStorage)
            {
                if (type == item.Name)
                {
                    return true;
                }
            }
            return false;
        }
        public Boolean IsSimpleType(string type)
        {
            for (int i = 0; i < CogsTypes.SimpleTypeNames.Length; i++)
            {
                if (type == "float" || type == "double" || type == "decimal" || type == "string" || type == "boolean" || type == "int")
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

        public Boolean IsReusableType(string type)
        {
            foreach (var reusable in ReusableStorage)
            {
                if (type == reusable.Name)
                {
                    return true;
                }
            }
            return false;
        }

        public void GenComment(StringBuilder res, String title)
        { 
            res.AppendLine(@"<!--");
            res.AppendLine(@"///////////////////////////////////////////////////////////////////////////////////////");
            res.AppendLine(@"//");
            res.AppendLine(@"//"+ title + @"");
            res.AppendLine(@"//");
            res.AppendLine(@"///////////////////////////////////////////////////////////////////////////////////////");
            res.AppendLine(@"-->");
        }
    }
}