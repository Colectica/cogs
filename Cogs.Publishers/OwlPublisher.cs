// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
using Cogs.Publishers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cogs.Publisher
{
    public class OwlPublisher
    {
        public string CogsLocation { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }

        public string TargetNamespace { get; set; } = "ddi:3_4";

        public List<DataType> ReusableStorage { get; set; }
        public List<ItemType> ItemTypeStorage { get; set; }
        public HashSet<string> set { get; set; }

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

            ReusableStorage = model.ReusableDataTypes;
            ItemTypeStorage = model.ItemTypes;
            set = new HashSet<string>();
            //Start here
            var projName = "cogsburger";
            StringBuilder res = new StringBuilder();
            res.AppendLine(@"<?xml version=""1.0""?>");
            res.AppendLine(@"<rdf:RDF xmlns=""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#""");
            res.AppendLine(@"   xml:base=""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger""");
            res.AppendLine(@"   xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""");
            res.AppendLine(@"   xmlns:owl=""http://www.w3.org/2002/07/owl#""");
            res.AppendLine(@"   xmlns:xml=""http://www.w3.org/XML/1998/namespace""");
            res.AppendLine(@"   xmlns:xsd=""http://www.w3.org/2001/XMLSchema#""");
            res.AppendLine(@"   xmlns:rdfs=""http://www.w3.org/2000/01/rdf-schema#"">");
            res.AppendLine(@"   <owl:Ontology rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + @"""/>");

            //Generate each ItemType class and add to result
            PredefineSimple(res);
            GenOwlClass(res, model.ItemTypes, null, projName);
            GenOwlClass(res, null, model.ReusableDataTypes, projName);
            GenSimpleClass(res, projName); 

            GenProperty(res, model.ItemTypes, null, projName);
            GenProperty(res, null, model.ReusableDataTypes, projName);

            res.AppendLine(@"</rdf:RDF>");
            File.WriteAllText(Path.Combine(TargetDirectory, projName + ".owl"), res.ToString());
        }

        public void PredefineSimple(StringBuilder res)
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
            res.AppendLine(@"   <owl:DatatypeProperty rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#CogsDate""/>");
        }
        public void GenOwlClass(StringBuilder res, List<ItemType> itemType, List<DataType> reusable, String projName)
        {
            if (itemType != null)
            {
                //generate class for all itemtype
                foreach (var item in itemType)
                {
                    StringBuilder GenClass = new StringBuilder();
                    GenClass.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + item.Name + @""">");
                    GenClass.AppendLine(@"      <rdfs:comment>"+item.Description+"</rdfs:comment>");
                    GenClass.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + @"""/>");
                    GenClass.AppendLine(@"  </owl:Class>");
                    res.Append(GenClass.ToString());
                }
            }
            else
            {
                //generate class for all reusabletype
                foreach (var item in reusable)
                {
                    StringBuilder GenClass = new StringBuilder();
                    GenClass.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + item.Name + @""">");
                    GenClass.AppendLine(@"      <rdfs:comment>" + item.Description + "</rdfs:comment>");
                    GenClass.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/"+projName+@"""/>");
                    GenClass.AppendLine(@"  </owl:Class>");
                    res.AppendLine(GenClass.ToString());
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
                                ObjectProp.AppendLine(@"  <owl:ObjectProperty rdf:about = ""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + prop.Name + @""">");
                                ObjectProp.AppendLine(@"    <rdfs:comment>" + prop.Description + "</rdfs:comment>");
                                ObjectProp.AppendLine(@"    <rdfs:domain rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + props.Name + @"""/>");
                                ObjectProp.AppendLine(@"    <rdfs:range rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + prop.DataType.Name + @"""/>");
                                //addRestrictionRange(ObjectProp, prop, prop.Name);   
                                //added here
                                ObjectProp.AppendLine(@"  </owl:ObjectProperty>");
                               
                            }
                            else
                            {
                                if (prop.DataType.Name.Equals("cogsDate"))
                                {
                                    DataProp.AppendLine(@"      <owl:ObjectProperty rdf:about = ""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#CogsDate"">");
                                    DataProp.AppendLine(@"          <rdfs:domain rdf:resource = ""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#" + props.Name + @"""/>");
                                    DataProp.AppendLine(@"          <rdfs:range>");
                                    DataProp.AppendLine(@"              <owl:Restriction>");
                                    DataProp.AppendLine(@"                  <owl:onProperty rdf:resource = ""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#CogsDate""/>");
                                    DataProp.AppendLine(@"                  <owl:minQualifiedCardinality rdf:datatype = ""http://www.w3.org/2001/XMLSchema#nonNegativeInteger"" > 0 </owl:minQualifiedCardinality>");
                                    DataProp.AppendLine(@"                  <owl:onDataRange rdf:resource = ""http://www/w3.org/2001/XMLSchema#cogsDate"" />");
                                    DataProp.AppendLine(@"              </owl:Restriction>");
                                    DataProp.AppendLine(@"          </rdfs:range>");
                                    DataProp.AppendLine(@"      </owl:ObjectProperty>");
                                }
                                else
                                {
                                    DataProp.AppendLine(@"  <owl:DatatypeProperty rdf:about = ""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + prop.Name + @""">");
                                    DataProp.AppendLine(@"      <rdfs:comment>" + prop.Description + "</rdfs:comment>");
                                    DataProp.AppendLine(@"      <rdfs:domain rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + props.Name + @"""/>");
                                    DataProp.AppendLine(@"      <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#" + prop.DataType.Name + @"""/>");
                                    //addRestrictionRange(DataProp, prop, prop.Name);
                                    //added here
                                    DataProp.AppendLine(@"  </owl:DatatypeProperty>");
                                   
                                }
                            }

                            if (DataProp.Length > 0)
                            {
                                res.Append(DataProp.ToString());
                            }
                            if (ObjectProp.Length > 0)
                            {
                                res.Append(ObjectProp.ToString());
                            }
                        }
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
                        if(IsItemType(prop.DataType.Name) || IsReusableType(prop.DataType.Name))
                        {
                            ObjectProp.AppendLine(@"  <owl:ObjectProperty rdf:about = ""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + prop.Name + @""">");
                            ObjectProp.AppendLine(@"    <rdfs:comment>" + prop.Description + "</rdfs:comment>");
                            ObjectProp.AppendLine(@"    <rdfs:domain rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + props.Name + @"""/>");
                            ObjectProp.AppendLine(@"    <rdfs:range rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + prop.DataType.Name + @"""/>");
                            //addRestrictionRange(ObjectProp, prop, prop.Name);
                            //added here 
                            ObjectProp.AppendLine(@"  </owl:ObjectProperty>");
                            
                        }
                        else
                        {
                            if (prop.DataType.Name.Equals("cogsDate"))
                            {
                                DataProp.AppendLine(@"      <owl:ObjectProperty rdf:about = ""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#CogsDate"">");
                                DataProp.AppendLine(@"          <rdfs:domain rdf:resource = ""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#" + props.Name + @"""/>");
                                DataProp.AppendLine(@"          <rdfs:range>");
                                DataProp.AppendLine(@"              <owl:Restriction>");
                                DataProp.AppendLine(@"                  <owl:onProperty rdf:resource = ""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#CogsDate""/>");
                                DataProp.AppendLine(@"                  <owl:minQualifiedCardinality rdf:datatype = ""http://www.w3.org/2001/XMLSchema#nonNegativeInteger"" > 0 </owl:minQualifiedCardinality>");
                                DataProp.AppendLine(@"                  <owl:onDataRange rdf:resource = ""http://www/w3.org/2001/XMLSchema#cogsDate"" />");
                                DataProp.AppendLine(@"              </owl:Restriction>");
                                DataProp.AppendLine(@"          </rdfs:range>");
                                DataProp.AppendLine(@"      </owl:ObjectProperty>");
                            }
                            else
                            {
                                DataProp.AppendLine(@"  <owl:DatatypeProperty rdf:about = ""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + prop.Name + @""">");
                                DataProp.AppendLine(@"      <rdfs:comment>" + prop.Description + "</rdfs:comment>");
                                DataProp.AppendLine(@"      <rdfs:domain rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + props.Name + @"""/>");
                                DataProp.AppendLine(@"      <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#" + prop.DataType.Name + @"""/>");
                                //addRestrictionRange(DataProp, prop, prop.Name);
                                DataProp.AppendLine(@"  </owl:DatatypeProperty>");
                            }
                        }
                        if (DataProp.Length > 0)
                        {
                            res.Append(DataProp.ToString());
                        }
                        if (ObjectProp.Length > 0)
                        {
                            res.Append(ObjectProp.ToString());
                        } 
                    }
                }
            }
        }

        public void GenSimpleClass(StringBuilder res, String projName)
        {
            //anyURI
            StringBuilder GenanyURI = new StringBuilder();
            GenanyURI.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#anyURI"">");
            GenanyURI.AppendLine(@"    <rdfs:comment>Simple Type for anyURI</rdfs:comment>");
            GenanyURI.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#anyURI""/>");
            GenanyURI.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GenanyURI.ToString());
            //cogsDate
            StringBuilder GenCogsDate = new StringBuilder();
            GenCogsDate.AppendLine(@"  <owl:ObjectProperty rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + @"#cogsDate"">");
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
            //date
            StringBuilder GenDate = new StringBuilder();
            GenDate.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#date"">");
            GenDate.AppendLine(@"    <rdfs:comment>Simple Type for date</rdfs:comment>");
            GenDate.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#date""/>");
            GenDate.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GenDate.ToString());
            //dateTime
            StringBuilder GenDateTime = new StringBuilder();
            GenDateTime.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#dateTime"">");
            GenDateTime.AppendLine(@"    <rdfs:comment>Simple Type for dateTime</rdfs:comment>");
            GenDateTime.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#dateTime""/>");
            GenDateTime.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GenDateTime.ToString());
            //duration
            StringBuilder GenDuration = new StringBuilder();
            GenDuration.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#duration"">");
            GenDuration.AppendLine(@"    <rdfs:comment>Simple Type for date</rdfs:comment>");
            GenDuration.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#duration""/>");
            GenDuration.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GenDuration.ToString());
            //gDay
            StringBuilder GengDay = new StringBuilder();
            GengDay.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#gDay"">");
            GengDay.AppendLine(@"    <rdfs:comment>Simple Type for gDay</rdfs:comment>");
            GengDay.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#gDay""/>");
            GengDay.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GengDay.ToString());
            //gMonth
            StringBuilder GengMonth = new StringBuilder();
            GengMonth.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#gMonth"">");
            GengMonth.AppendLine(@"    <rdfs:comment>Simple Type for gMonth</rdfs:comment>");
            GengMonth.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#gMonth""/>");
            GengMonth.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GengMonth.ToString());
            //gMonthDay
            StringBuilder GengMonthDay = new StringBuilder();
            GengMonthDay.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#gMonthDay"">");
            GengMonthDay.AppendLine(@"    <rdfs:comment>Simple Type for gMonthDay</rdfs:comment>");
            GengMonthDay.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#gMonthDay""/>");
            GengMonthDay.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GengMonthDay.ToString());
            //gYear
            StringBuilder GengYear = new StringBuilder();
            GengYear.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#gYear"">");
            GengYear.AppendLine(@"    <rdfs:comment>Simple Type for gYear</rdfs:comment>");
            GengYear.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#gYear""/>");
            GengYear.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GengYear.ToString());
            //gYearMonth
            StringBuilder GengYearMonth = new StringBuilder();
            GengYearMonth.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#gYearMonth"">");
            GengYearMonth.AppendLine(@"    <rdfs:comment>Simple Type for gYearMonth</rdfs:comment>");
            GengYearMonth.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#gYearMonth""/>");
            GengYearMonth.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(GengYearMonth.ToString());
            //time
            StringBuilder Gentime = new StringBuilder();
            Gentime.AppendLine(@"  <owl:DatatypeProperty rdf:about=""http://www.w3.org/2001/XMLSchema#time"">");
            Gentime.AppendLine(@"    <rdfs:comment>Simple Type for time</rdfs:comment>");
            Gentime.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#time""/>");
            Gentime.AppendLine(@"  </owl:DatatypeProperty>");
            res.Append(Gentime.ToString());
        }
        public void addRestrictionDomain(StringBuilder res, string propname, List<OwlPropAndLink> list)
        {
            res.AppendLine(@"< rdfs:domain >");
            res.AppendLine(@"   < owl:Restriction >");
            res.AppendLine(@"       < owl:onProperty rdf:resource = ""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#"+ propname +@""" />");
            res.AppendLine(@"           < owl:allValuesFrom >");
            res.AppendLine(@"           < owl:Class >");
            res.AppendLine(@"               < owl:unionOf rdf:parseType = ""Collection"" >");
            foreach(var prop in list)
            {
                if(prop.Prop_name.Equals(propname))
                {
                    foreach (var type in prop.link_class)
                    {
                        res.AppendLine(@"                   < rdf:Description rdf:about = ""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#" +type+ @"""/>");
                    }
                }
            }
            res.AppendLine(@"           </ owl:unionOf >");
            res.AppendLine(@"           </ owl:Class >");
            res.AppendLine(@"       </ owl:allValuesFrom >");
            res.AppendLine(@"   </ owl:Restriction >");
            res.AppendLine(@"</ rdfs:domain >");
        }

        public void addRestrictionRange(StringBuilder res, Property prop)
        {
            res.AppendLine(@"<rdfs:range>");
            res.AppendLine(@"   <owl:Restriction>");
            res.AppendLine(@"       <owl:onProperty rdf:resource = ""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#" + prop.Name + @"""/>");
            if (!prop.MinCardinality.Equals("0"))
            {
                res.AppendLine(@"           <owl:minQualifiedCardinality rdf:datatype = ""http://www.w3.org/2001/XMLSchema#nonNegativeInteger"">" + prop.MinCardinality + @"</owl:minQualifiedCardinality>");
            }
            if (!prop.MinCardinality.Equals("n"))
            {
                res.AppendLine(@"           <owl:maxQualifiedCardinality rdf:datatype = ""http://www.w3.org/2001/XMLSchema#nonNegativeInteger"">" + prop.MaxCardinality + @"</owl:maxQualifiedCardinality>");
            }
            //res.AppendLine(@"       <owl:onClass rdf:resource = ""http://www.semanticweb.org/clement/ontologies/2017/6/cogsburger#" + classname + @"""/>");
            res.AppendLine(@"   </owl:Restriction>");
            res.AppendLine(@"</rdfs:range>");
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
    }
}