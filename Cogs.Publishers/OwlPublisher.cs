// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
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
            GenOwlClass(res, model.ItemTypes, null, projName);
            GenOwlClass(res, null, model.ReusableDataTypes, projName);
            GenSimpleClass(res, projName); 

            GenProperty(res, model.ItemTypes, null, projName);
            GenProperty(res, null, model.ReusableDataTypes, projName);

            res.AppendLine(@"</rdf:RDF>");
            File.WriteAllText(Path.Combine(TargetDirectory, projName + ".owl"), res.ToString());
        }

        public void GenOwlClass(StringBuilder res, List<ItemType> itemType, List<DataType> reusable, String projName)
        {
            if (itemType != null)
            {
                foreach (var item in itemType)
                {
                    StringBuilder GenClass = new StringBuilder();
                    GenClass.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + item.Name + @""">");
                    GenClass.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/owl#Thing""/>");
                    GenClass.AppendLine(@"  </owl:Class>");
                    res.Append(GenClass.ToString());
                }
            }
            else
            {
                foreach (var item in reusable)
                {
                    StringBuilder GenClass = new StringBuilder();
                    GenClass.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + item.Name + @""">");
                    GenClass.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/owl#Thing""/>");
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

                        if (!set.Contains(prop.Name))
                        {
                            set.Add(prop.Name);
                            if (IsItemType(prop.DataType.Name) || IsSimpleType(prop.DataType.Name) || IsReusableType(prop.DataType.Name))
                            {
                                ObjectProp.AppendLine(@"  <owl:ObjectProperty rdf:about = ""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + prop.Name + @""">");
                                ObjectProp.AppendLine(@"    <rdfs:domain rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + props.Name + @"""/>");
                                ObjectProp.AppendLine(@"    <rdfs:range rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + prop.DataType.Name + @"""/>");
                                ObjectProp.AppendLine(@"  </owl:ObjectProperty>");
                            }
                            else
                            {
                                DataProp.AppendLine(@"  <owl:DatatypeProperty rdf:about = ""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + prop.Name + @""">");
                                DataProp.AppendLine(@"    <rdfs:domain rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + "#" + props.Name + @"""/>");
                                DataProp.AppendLine(@"    <rdfs:range rdf:resource=""http://www.w3.org/2001/XMLSchema#" + prop.DataType.Name + @"""/>");
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
            GenanyURI.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/"+projName+@"#anyURI"">");
            GenanyURI.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/owl#Thing""/>");
            GenanyURI.AppendLine(@"  </owl:Class>");
            res.Append(GenanyURI.ToString());
            //cogsDate
            StringBuilder GenCogsDate = new StringBuilder();
            GenCogsDate.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/"+projName+@"#cogsDate"">");
            GenCogsDate.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/owl#Thing""/>");
            GenCogsDate.AppendLine(@"  </owl:Class>");
            res.Append(GenCogsDate.ToString());
            //date
            StringBuilder GenDate = new StringBuilder();
            GenDate.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + @"#date"">");
            GenDate.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/owl#Thing""/>");
            GenDate.AppendLine(@"  </owl:Class>");
            res.Append(GenDate.ToString());
            //dateTime
            StringBuilder GenDateTime = new StringBuilder();
            GenDateTime.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + @"#dateTime"">");
            GenDateTime.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/owl#Thing""/>");
            GenDateTime.AppendLine(@"  </owl:Class>");
            res.Append(GenDateTime.ToString());
            //duration
            StringBuilder GenDuration = new StringBuilder();
            GenDuration.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + @"#duration"">");
            GenDuration.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/owl#Thing""/>");
            GenDuration.AppendLine(@"  </owl:Class>");
            res.Append(GenDuration.ToString());
            //gDay
            StringBuilder GengDay = new StringBuilder();
            GengDay.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + @"#gDay"">");
            GengDay.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/owl#Thing""/>");
            GengDay.AppendLine(@"  </owl:Class>");
            res.Append(GengDay.ToString());
            //gMonth
            StringBuilder GengMonth = new StringBuilder();
            GengMonth.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + @"#gMonth"">");
            GengMonth.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/owl#Thing""/>");
            GengMonth.AppendLine(@"  </owl:Class>");
            res.Append(GengMonth.ToString());
            //gMonthDay
            StringBuilder GengMonthDay = new StringBuilder();
            GengMonthDay.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + @"#gMonthDay"">");
            GengMonthDay.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/owl#Thing""/>");
            GengMonthDay.AppendLine(@"  </owl:Class>");
            res.Append(GengMonthDay.ToString());
            //gYear
            StringBuilder GengYear = new StringBuilder();
            GengYear.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + @"#gYear"">");
            GengYear.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/owl#Thing""/>");
            GengYear.AppendLine(@"  </owl:Class>");
            res.Append(GengYear.ToString());
            //gYearMonth
            StringBuilder GengYearMonth = new StringBuilder();
            GengYearMonth.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + @"#gYearMonth"">");
            GengYearMonth.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/owl#Thing""/>");
            GengYearMonth.AppendLine(@"  </owl:Class>");
            res.Append(GengYearMonth.ToString());
            //time
            StringBuilder Gentime = new StringBuilder();
            Gentime.AppendLine(@"  <owl:Class rdf:about=""http://www.semanticweb.org/clement/ontologies/2017/6/" + projName + @"#time"">");
            Gentime.AppendLine(@"      <rdfs:subClassOf rdf:resource=""http://www.semanticweb.org/clement/ontologies/2017/6/owl#Thing""/>");
            Gentime.AppendLine(@"  </owl:Class>");
            res.Append(Gentime.ToString());
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