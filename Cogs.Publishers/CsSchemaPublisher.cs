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
using System.Reflection;
using System.Collections;

namespace Cogs.Publishers
{
    public class CsSchemaPublisher
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
        /// dictionary for translating names to c# datatype representations
        /// </summary>
        private Dictionary<string, string> Translator { get; set; }

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

            InitializeDictionary();
            //get the project name
            var projName = "cogsBurger";
            CreateIIdentifiable(model, projName);
            CreateItemContainer(model, projName);
            //create project file
            XDocument project = new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup", new XElement("TargetFramework", "netstandard2.0"),
                    new XElement("AssemblyName", projName), new XElement("RootNamespace", projName)),
                new XElement("ItemGroup", new XElement("PackageReference", new XAttribute("Include", "System.ComponentModel.Annotations"),
                    new XAttribute("Version", "4.4.0-preview2-25405-01")),
                    new XElement("PackageReference", new XAttribute("Include", "Microsoft.CSharp"), new XAttribute("Version", "4.4.0-preview2-25405-01")),
                    new XElement("PackageReference", new XAttribute("Include", "Newtonsoft.Json"), new XAttribute("Version", "10.0.3")))));
            XmlWriterSettings xws = new XmlWriterSettings { OmitXmlDeclaration = true };
            using (XmlWriter xw = XmlWriter.Create(Path.Combine(TargetDirectory, projName + ".csproj"), xws))
            {
                project.Save(xw);
            }
            CreateJsonConverter(model, projName);
            // copy types file
            this.GetType().GetTypeInfo().Assembly.GetManifestResourceStream("Cogs.Publishers.Types.txt").CopyTo(new FileStream(Path.Combine(TargetDirectory, "Types.cs"), FileMode.Create));
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                // add class description using '$' for newline and '#' for tabs
                var newClass = new StringBuilder("using System;$using System.Linq;$using Newtonsoft.Json;$using Newtonsoft.Json.Linq;$using Cogs.DataAnnotations;$using System.Collections.Generic;$" +
                    "using System.ComponentModel.DataAnnotations;$$namespace " + projName + "${$#/// <summary>$#/// " + item.Description + "$#/// <summary>");
                newClass.Append("$#public ");
                var toJsonProperties = new StringBuilder();
                var initializeReferences = new StringBuilder();
                var reusableToJson = new StringBuilder();
                // add abstract to class title if relevant
                if (item.IsAbstract) { newClass.Append("abstract "); }
                newClass.Append("class " + item.Name);
                // allow inheritance when relevant
                if (!String.IsNullOrWhiteSpace(item.ExtendsTypeName)) newClass.Append(" : " + item.ExtendsTypeName + "$#{$##public new string ReferenceId { set; get; }");
                else if (!model.ReusableDataTypes.Contains(item)) { newClass.Append(" : IIdentifiable$#{$##public string ReferenceId { set; get; }"); }
                else { newClass.Append("$#{"); }
                bool first = true;
                foreach (var prop in item.Properties)
                {
                    // set c# datatype representation while saving original so can tell what type it is
                    string origDataTypeName = null;
                    if (Translator.ContainsKey(prop.DataTypeName))
                    {
                        origDataTypeName = prop.DataTypeName;
                        prop.DataTypeName = Translator[prop.DataTypeName];
                        if (!prop.DataTypeName.Equals("bool")) { first = true; }
                    }
                    // create documentation for property
                    newClass.Append("$##/// <summary>$##/// " + prop.Description + "$##/// <summary>");
                    // create constraints
                    if (prop.DataTypeName.Equals("string") || prop.DataTypeName.Equals("Uri"))
                    {
                        if (prop.MinLength != null && prop.MaxLength != null)
                        {
                            newClass.Append("$##[StringLength(" + prop.MaxLength + ", MinimumLength = " + prop.MinLength + ")]");
                        }
                        else if (prop.MaxLength != null)
                        {
                            newClass.Append("$##[StringLength(" + prop.MaxLength + ")]");
                        }
                        else if (prop.MinLength != null)
                        {
                            newClass.Append("$##[StringLength(" + int.MaxValue + ", MinimumLength = " + prop.MinLength + ")]");
                        }
                        if (prop.DataTypeName.Equals("string"))
                        {
                            // work with Enum and pattern
                            if (prop.Enumeration.Count > 0)
                            {
                                newClass.Append("$##[StringValidation(new string[] {");
                                bool useComma = false;
                                foreach (var option in prop.Enumeration)
                                {
                                    if (useComma) { newClass.Append(", "); }
                                    newClass.Append("$###\"" + option + "\"");
                                    useComma = true;
                                }
                                if (!string.IsNullOrWhiteSpace(prop.Pattern)) { newClass.Append("$##}, " + prop.Pattern + ")]"); }
                                else { newClass.Append("$##})]"); }
                            }
                            else if (!string.IsNullOrWhiteSpace(prop.Pattern))
                            {
                                newClass.Append("$##[StringValidation(null, \"" + prop.Pattern + "\")]");
                            }
                        }
                    }
                    else if (!prop.DataTypeName.Equals("bool") && !prop.DataTypeName.Equals("CogsDate"))
                    {
                        if (prop.MinInclusive != null || prop.MaxInclusive != null)
                        {
                            newClass.Append("$##[Range(" + prop.MinInclusive + ", " + prop.MaxInclusive + ")]");
                        }
                        if (prop.MinExclusive != null || prop.MaxExclusive != null)
                        {
                            newClass.Append("$##[ExclusiveRange(" + prop.MinExclusive + ", " + prop.MaxExclusive + ")]");
                        }
                    }
                    if (!first && model.Identification.Contains(prop)) { toJsonProperties.Append(","); }
                    var start = "((JObject)json.First).Add(";
                    if (origDataTypeName != null && !"boolintstringulong".Contains(prop.DataTypeName))
                    {
                        newClass.Append("$##[JsonConverter(typeof(SimpleTypeConverter))]");
                        SimpleToJson(origDataTypeName, prop.Name, start, newClass);
                    }
                    if (model.ReusableDataTypes.Contains(item)) { start = "json.Add("; }
                    // if there can be at most one, create an instance variable
                    if (!prop.MaxCardinality.Equals("n") && int.Parse(prop.MaxCardinality) == 1)
                    {
                        if (model.ItemTypes.Contains(prop.DataType) && !item.IsAbstract) { newClass.Append("$##[JsonConverter(typeof(IIdentifiableConverter))]"); }
                        newClass.Append("$##public " + prop.DataTypeName + " " + prop.Name + " { get; set; }");
                        if (origDataTypeName != null && !prop.DataTypeName.Equals("bool"))
                        {
                            if (prop.DataTypeName.Equals("CogsDate"))
                            {
                                reusableToJson.Append("$###if (" + prop.Name + ".GetValue() != null)");
                            }
                            else if (prop.DataTypeName.Equals("DateTimeOffset") || prop.DataTypeName.Equals("TimeSpan"))
                            {
                                reusableToJson.Append("$###if (" + prop.Name + " != default(" + prop.DataTypeName + "))");
                            }
                            else { reusableToJson.Append("$###if (" + prop.Name + " != null)"); }
                            reusableToJson.Append("$###{$####" + SimpleToJson(origDataTypeName, prop.Name, start) + ");$###}");
                        }
                        else if (model.ReusableDataTypes.Contains(prop.DataType))
                        {
                            reusableToJson.Append("$###if (" + prop.Name + " != null) { ");
                            reusableToJson.Append(start + "new JProperty(\"" + prop.Name + "\", " + prop.Name + ".ToJson())); }");
                            initializeReferences.Append(InitializeReusable(prop, false, model));
                        }
                        else if (model.Identification.Contains(prop))
                        {
                            toJsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", " + prop.Name + ")");
                            first = false;
                        }
                        else if(!model.ItemTypes.Contains(prop.DataType))
                        {
                            reusableToJson.Append("$###if ( " + prop.Name + " != null) $###{$####" + start + "new JProperty(\"" + prop.Name + "\", " + prop.Name + "));$###}");
                        }
                        else
                        {
                            reusableToJson.Append("$###if ( " + prop.Name + " != null) $###{$####" + start + "new JProperty(\"" + prop.Name +  "\", new JObject(" +
                                "new JProperty(\"@type\", \"ref\"), $#####new JProperty(\"value\", new JArray($######\"" + prop.DataTypeName + "\", $######" + prop.Name + ".ID)))));$###}");
                            initializeReferences.Append("$###if (" + prop.Name + " != null) { " + prop.Name + " = (" + prop.DataTypeName +
                                ")dict[" + prop.Name + ".ReferenceId]; }");
                        }
                    }
                    // otherwise, create a list object to allow multiple
                    else
                    {
                        if (model.ItemTypes.Contains(prop.DataType) && !item.IsAbstract) { newClass.Append("$##[JsonConverter(typeof(IIdentifiableConverter))]"); }
                        newClass.Append("$##public List<" + prop.DataTypeName + "> " + prop.Name + "{ get; set; }");
                        if (origDataTypeName != null && !prop.DataTypeName.Equals("bool"))
                        {
                            if (prop.DataTypeName.Equals("CogsDate"))
                            {
                                reusableToJson.Append("$###if (" + prop.Name + ".GetValue() != null)");
                            }
                            else if(prop.DataTypeName.Equals("DateTimeOffset") || prop.DataTypeName.Equals("TimeSpan"))
                            {
                                reusableToJson.Append("$###if (" + prop.Name + " != default(" + prop.DataTypeName + "))");
                            }
                            else { reusableToJson.Append("$###if (" + prop.Name + " != null)"); }
                            reusableToJson.Append("$###{$####" + start + "new JProperty(\"" + prop.Name + "\", new JArray($#####from item in " + prop.Name +
                                "select " + SimpleToJson(origDataTypeName, prop.Name, start) + ");$###}");
                        }
                        else if (model.ReusableDataTypes.Contains(prop.DataType))
                        {
                            if (prop.DataTypeName.Equals("DateTimeOffset") || prop.DataTypeName.Equals("TimeSpan"))
                            {
                                reusableToJson.Append("$###if (" + prop.Name + " != default(" + prop.DataTypeName + "))");
                            }
                            else { reusableToJson.Append("$###if (" + prop.Name + " != null)"); }
                            reusableToJson.Append("$###{$####" + start + "new JProperty(\"" + prop.Name + "\", $#####new JArray($######from item in " + prop.Name +
                                "$######select new JObject($#######new JProperty(\"" + prop.DataTypeName + "\", item.ToJson()))))); $###}");
                            initializeReferences.Append(InitializeReusable(prop, true, model));
                        }
                        else if (model.Identification.Contains(prop))
                        {
                            toJsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", $#####new JArray($######from item in " + prop.Name +
                                "$######select item))");
                            first = false;
                        }
                        else if (!model.ItemTypes.Contains(prop.DataType))
                        {
                            reusableToJson.Append("$###if ( " + prop.Name + " != null) $###{$####" + start + "new JProperty(\"" + prop.Name + "\", " +
                                "new JArray($#####from item in " + prop.Name + "$#####select item)));$###}");
                        }
                        else
                        {
                            reusableToJson.Append("$###if (" + prop.Name + " != null)$###{$####" + start + "new JProperty(\"" + prop.Name + 
                                "\", $#####new JArray($######from item in " + prop.Name + "$######select new JObject(new JProperty(\"@type\", \"ref\"), " +
                            "$#######new JProperty(\"value\", new JArray($########item.GetType().Name.ToString(), $########item.ID))))));$###}");
                            initializeReferences.Append("$###if (" + prop.Name + " != null)$###{$####for (int i = 0; i < " + prop.Name + ".Count; i++)" +
                                "$####{$#####dynamic temp = dict[" + prop.Name + "[i].ReferenceId];$#####" + prop.Name + "[i] = temp;$####}$###}");
                        }
                    }
                }
                newClass.Append("$##/// <summary>$##/// Used to Serialize this object to Json $##/// <summary>");
                string returnType = "JProperty";
                if (model.ReusableDataTypes.Contains(item)) { returnType = "JObject"; }
                if (!model.ReusableDataTypes.Contains(item))
                {
                    if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                    {
                        newClass.Append("$##public override " + returnType + " ToJson()$##{$###JProperty json = base.ToJson();$###((JObject)json.First).Add(");
                    }
                    else { newClass.Append("$##public virtual " + returnType + " ToJson()$##{$###JProperty json = new JProperty(ID, new JObject("); }
                    newClass.Append(toJsonProperties.ToString());
                    if (string.IsNullOrWhiteSpace(item.ExtendsTypeName)) { newClass.Append(")"); }
                    newClass.Append(");" + reusableToJson.ToString() + "$###return json;$##}");
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                    {
                        newClass.Append("$##public override " + returnType + " ToJson()$##{$###JObject json = base.ToJson();$###((JObject)json.First).Add(");
                    }
                    else { newClass.Append("$##public virtual " + returnType + " ToJson()$##{$###JObject json = new JObject() {"); }
                    newClass.Append(toJsonProperties.ToString());
                    newClass.Append("};" + reusableToJson.ToString() + "$###return json;$##}");
                }
                newClass.Append("$$##/// <summary>$##/// Used to set this object's properties from Json $##/// <summary>");
                if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                {
                    newClass.Append("$##public override void InitializeReferences(Dictionary<string, IIdentifiable> dict, string json)$##{" +
                        "$###base.InitializeReferences(dict, json);");
                }
                else { newClass.Append("$##public virtual void InitializeReferences(Dictionary<string, IIdentifiable> dict, string json)$##{"); }
                if (initializeReferences.ToString().Contains("thisObj")) 
                {
                    newClass.Append("$###string[] parts = json.Split(new string[] { \":\", \"{\", \"}\", \"[\", \"]\", \",\", Environment.NewLine }, " +
                    "StringSplitOptions.None);$###bool thisObj = false;");
                }
                newClass.Append(initializeReferences.ToString() + "$##}$#}$}$");
                // write class to out folder
                File.WriteAllText(Path.Combine(TargetDirectory, item.Name + ".cs"), newClass.ToString().
                    Replace("$###((JObject)json.First).Add();", "").Replace("#", "    ").Replace("$", Environment.NewLine).Replace("@", "$"));
            }
        }

        private string InitializeReusable(Property prop, bool isList, CogsModel model)
        {
            var name = prop.Name;
            var type = prop.DataTypeName;
            StringBuilder builder = new StringBuilder(@"
            for (int i = 0; i < parts.Length; i ++)
            {
                if (parts[i].Contains(ID)) { thisObj = true; }
                else if(parts[i].Contains(""" + name + @""") && thisObj)
                {
                    ");
            if(isList)
            {
                builder.Append(name + " = new List<" + type + @">();
                    "+ type + " obj = null;");
            }
            else { builder.Append(name + " = new " + type + "();"); }
            builder.Append(@"
                    i++;
                    while (i < parts.Length && (string.IsNullOrWhiteSpace(parts[i].Trim().Replace(""\"""", """")) || (this.GetType().GetProperties().Where(x => parts[i].Trim().Replace(""\"""", """").
                    ToLower().Equals(x.Name.ToLower())).ToList().Count == 0 && !""yearmonthdaydatetimeanyuricogsdate"".Contains(parts[i].Trim().Replace(""\"""", """").ToLower()))))
                    {
                        if (parts[i].Contains(""" + type + @"""))
                        {
                            ");
            if(isList)
            {
                builder.Append("if(obj != null) { " + name + @".Add(obj); }
                            obj = new " + type + @"();
                        }");
                foreach (var p in prop.DataType.Properties)
                {
                    builder.Append(@"
                        if (parts[i].Contains(""" + p.Name + "\"))");
                    if (!p.MaxCardinality.Equals("1"))
                    {
                        builder.Append(@"
                        {
                            " + name + "." + p.Name + " = new List<" + p.DataTypeName + @">();
                            i++;
                            while (i < parts.Length && (string.IsNullOrWhiteSpace(parts[i].Trim().Replace(""\"""", """")) || (this.GetType().GetProperties().Where(x => parts[i].Trim().Replace(""\"""", """").
                                ToLower().Equals(x.Name.ToLower())).ToList().Count == 0 && !""yearmonthdaydatetimeanyuricogsdate"".Contains(parts[i].Trim().Replace(""\"""", """").ToLower()))))
                            {
                                if(!string.IsNullOrWhiteSpace(parts[i])) { obj." + p.Name + ".Add(" + ReusableTypeConvert(p.DataTypeName, true, model) + @"); }
                                i++;
                            }
                        }");
                    }
                    else
                    {
                        builder.Append(" { obj." + p.Name + " = " + ReusableTypeConvert(p.DataTypeName, false, model) + @"; }");

                    }
                }
                builder.Append(@"
                        i++;
                    }
                    if (obj != null) { " + name + @".Add(obj); }
                }
            }
            thisObj = false;");
            }
            else
            {
                builder.Append(name + " = new " + type + @"();
                        }");
                foreach (var p in prop.DataType.Properties)
                {
                    if (!p.MaxCardinality.Equals("1"))
                    {
                        builder.Append(@"
                        if (parts[i].Contains(""" + p.Name + @"""))
                        {
                            " + name + "." + p.Name + " = new List<" + p.DataTypeName + @">();
                            i++;
                            while (i < parts.Length && (string.IsNullOrWhiteSpace(parts[i].Trim().Replace(""\"""", """")) || (this.GetType().GetProperties().Where(x => parts[i].Trim().Replace(""\"""", """").
                                ToLower().Equals(x.Name.ToLower())).ToList().Count == 0 && !""yearmonthdaydatetimeanyuricogsdate"".Contains(parts[i].Trim().Replace(""\"""", """").ToLower()))))
                            {
                                if(!string.IsNullOrWhiteSpace(parts[i])) { " + name + "." + p.Name + ".Add(" + ReusableTypeConvert(p.DataTypeName, true, model) + @"); }
                                i++;
                            }
                        }");
                    }
                    else
                    {
                        builder.Append(@"
                        if (parts[i].Contains(""" + p.Name + "\")) { " + name + "." + p.Name + " = " + ReusableTypeConvert(p.DataTypeName, false, model) + @"; }");
                    }
                }
                builder.Append(@"
                        i++;
                    }
                }
            }
            thisObj = false;");
            }
            return builder.ToString();
        }

        private string ReusableTypeConvert(string name, bool isList, CogsModel model)
        {
            string i = "i + 1";
            if(isList) { i = "i"; }
            if (name.Equals("int")) { return "int.Parse(parts[" + i + "].Trim().Replace(\"\\\"\", \"\"))"; }
            if (name.Equals("double")) { return "double.Parse(parts[" + i + "].Trim().Replace(\"\\\"\", \"\"))"; }
            if (name.Equals("decimal")) { return "decimal.Parse(parts[" + i + "].Trim().Replace(\"\\\"\", \"\"))"; }
            if (model.ItemTypes.Where(x => x.Name == name).ToList().Count > 0) { return "(" + name + ")dict[parts[i + 11].Trim().Replace(\"\\\"\", \"\")]"; }
            return "parts[" + i + "].Trim().Replace(\"\\\"\", \"\")";
        }

        private string SimpleToJson(string origDataTypeName, string name, string start, StringBuilder builder = null)
        {

            if (origDataTypeName.Equals("duration")) { return start + "new JProperty(\"duration\", " + name + ".Ticks)"; }
            if (origDataTypeName.Equals("dateTime"))
            {
                if (builder != null) { builder.Append("$##[JsonProperty(\"datetime\")]"); }
                return start + "new JProperty(\"datetime\", " + name + ".ToString(\"s\") + \"+\" + " + name + ".Offset.ToString())";
            }
            if (origDataTypeName.Equals("time")) { return start + "new JProperty(\"time\", " + name + ".ToString(\"T\") + \"+\" + " + name + ".Offset.ToString())"; }
            if (origDataTypeName.Equals("date")) { return start + "new JProperty(\"date\", " + name + ".ToString(\"u\").Split(' ')[0])"; }
            if (origDataTypeName.Equals("gYearMonth"))
            {
                if (builder != null) { builder.Append("$##[JsonProperty(\"YearMonth\")]"); }
                return "var ym = new JProperty(\"YearMonth\", new JObject($#####new JProperty(\"year\", " + name + ".Item1),$#####new " +
                    "JProperty(\"month\", " + name + ".Item2)));$####if (" + name + ".Item3 != null) { ((JObject)ym.First).Add(new JProperty(\"timezone\", " + name + 
                    ".Item3)); }$####" + start + "ym";
            }
            if (origDataTypeName.Equals("gYear"))
            {
                if (builder != null) { builder.Append("$##[JsonProperty(\"year\")]"); }
                return "var y = new JProperty(\"year\", new JObject($#####new JProperty(\"year\", " + name + ".Item1)));$####if (" + name + ".Item2 != null) " +
                    "{ ((JObject)y.First).Add(new JProperty(\"timezone\", " + name + ".Item2)); }$####" + start + "y";
            }
            if (origDataTypeName.Equals("gMonthDay"))
            {
                if (builder != null) { builder.Append("$##[JsonProperty(\"MonthDay\")]"); }
                return "var md = new JProperty(\"MonthDay\", new JObject($#####new JProperty(\"month\", " + name + ".Item1),$#####new " +
                    "JProperty(\"day\", " + name + ".Item2)));$####if (" + name + ".Item3 != null) { ((JObject)md.First).Add(new JProperty(\"timezone\", " + name + 
                    ".Item3)); }$####" + start + "md"; 
            }
            if (origDataTypeName.Equals("gDay"))
            {
                if (builder != null) { builder.Append("$##[JsonProperty(\"day\")]"); }
                return "var d = new JProperty(\"day\", new JObject($#####new JProperty(\"day\", " + name + ".Item1)));$####if (" + name + ".Item2 != null) " +
                    "{ ((JObject)d.First).Add(new JProperty(\"timezone\", " + name + ".Item2)); }$####" + start + "d"; 
            }
            if (origDataTypeName.Equals("gMonth"))
            {
                if (builder != null) { builder.Append("$##[JsonProperty(\"month\")]"); }
                return "var m = new JProperty(\"month\", new JObject($#####new JProperty(\"month\", " + name + ".Item1)));$####if (" + name + ".Item2 != null) " +
                    "{ ((JObject)m.First).Add(new JProperty(\"timezone\", " + name + ".Item2)); }$####" + start + "m";
            }
            if (origDataTypeName.Equals("anyUri"))
            {
                if (builder != null) { builder.Append("$##[JsonProperty(\"anyuri\")]"); }
                return start + "new JProperty(\"anyuri\", " + name + ")";
            }
            if (origDataTypeName.Equals("cogsDate"))
            {
                if (builder != null) { builder.Append("$##[JsonProperty(\"cogsdate\")]"); }
                return start + "new JProperty(\"cogsDate\", new JObject($#####new JProperty(" + name + ".UsedType.ToString(), " + name +
                    ".GetValue())))";
            }
            return start + "new JProperty(\"" + name + "\", " + name + ")";
        }



        // creates a file call IIdentifiable.cs which holds the IIdentifiable interface from which all item types descend
        private void CreateIIdentifiable(CogsModel model, string projName)
        {
            StringBuilder builder = new StringBuilder("using System;$using Newtonsoft.Json.Linq;$using System.Collections.Generic;$$namespace " +
                    projName + "${$#/// <summary>$#/// IIdentifiable class which all object Inherit from. Used to Serialize to Json $#/// <summary>");
            builder.Append("$#public interface IIdentifiable$#{");
            foreach (var prop in model.Identification)
            {
                builder.Append("$##" + prop.DataTypeName + " " + prop.Name + " { get; set; }");
            }
            builder.Append("$##JProperty ToJson();$##string ReferenceId { get; set; }$##void InitializeReferences(Dictionary<string, IIdentifiable> dict, string json);$#}$}");
            File.WriteAllText(Path.Combine(TargetDirectory, "IIdentifiable.cs"), builder.ToString().Replace("#", "    ").Replace("$", Environment.NewLine));
        }



        // Creates the ItemContainer Class
        private void CreateItemContainer(CogsModel model, string projName)
        {
            string clss = @"using System;
using System.Linq;
using Newtonsoft.Json;
using System.Reflection;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace !!!
{
    /// <summary>
    /// Class that contains a list of all items in the model 
    /// <summary>
    public class ItemContainer
    {
        public List<IIdentifiable> Items { get; } = new List<IIdentifiable>();
        public List<IIdentifiable> TopLevelReferences { get; } = new List<IIdentifiable>();


        public string Serialize()
        {
            JObject builder = new JObject();
            if (TopLevelReferences.Count > 0)
            {
                builder.Add(new JProperty(""TopLevelReference"", new JArray(
                from obj in TopLevelReferences
                select new JObject(
                    new JProperty(""$type"", ""ref""),
                    new JProperty(""value"", new JArray(
                        obj.GetType().ToString(),
                        obj.ID))))));
            }
            foreach(var item in Assembly.GetExecutingAssembly().GetTypes())
            {
                var elements = Items.Where(x => x.GetType().Equals(item)).ToList();
                if (elements.Count() > 0)
                {
                    var classType = new JObject();
                    foreach(var element in elements)
                    {
                        classType.Add(element.ToJson());
                    }
                    builder.Add(new JProperty(item.Name, new JObject(classType)));
                }
            }
            return builder.ToString();
        }


        public void Parse(string json)
        {
            List<string> ids = new List<string>();
            JObject builder = JObject.Parse(json);
            Dictionary<string, IIdentifiable> dict = new Dictionary<string, IIdentifiable>();
            foreach (var type in builder)
            {
                if (type.Key.Equals(""TopLevelReference""))
                {
                    if (type.Value.First != null)
                    {
                        foreach (var reference in (JArray)type.Value)
                        {
                            ids.Add(reference.Last.First.Last.ToString());
                        }
                    }
                }
                else
                {
                    var clss = type.Key;
                    foreach (KeyValuePair<string, JToken> instance in (JObject)type.Value)
                    {
                        IIdentifiable obj = null;
                        ???
                        if (obj == null) { throw new InvalidOperationException(); }
                        obj.ReferenceId = instance.Key;
                        Items.Add(obj);
                        dict.Add(instance.Key, obj);
                        if (ids.Contains(obj.ID)) { TopLevelReferences.Add(obj); }
                    }
                }
            }
            foreach (var obj in dict.Values)
            {
                obj.InitializeReferences(dict, json);
            }
        }
    }
}";
            StringBuilder ifs = new StringBuilder();
            string start = "";
            foreach(var item in model.ItemTypes)
            {
                ifs.Append("$######" + start + "if (clss.Equals(\"" + item.Name + "\")) { obj = JsonConvert.DeserializeObject<" + item.Name + ">(instance.Value.ToString()); }");
                start = "else ";
            }
            File.WriteAllText(Path.Combine(TargetDirectory, "ItemContainer.cs"), clss.Replace("!!!", projName).Replace("???", ifs.ToString()
                .Replace("#", "    ").Replace("$", Environment.NewLine)));
        }


        public void CreateJsonConverter(CogsModel model, string projName)
        {
            string clss = @"using System;
using System.Linq;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Cogs.DataAnnotations;

namespace cogsBurger
{
    class IIdentifiableConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType is IIdentifiable;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            IIdentifiable single = null;
            List<IIdentifiable> list = null;
            JToken obj = null;
            if (typeof(IEnumerable).IsAssignableFrom(objectType) && !objectType.ToString().ToLower().Equals(""string""))
            {
                list = ((IEnumerable)Activator.CreateInstance(objectType)).Cast<IIdentifiable>().ToList();
                obj = JArray.Load(reader);
            }
            else
            {
                single = (IIdentifiable)Activator.CreateInstance(objectType);
                obj = JObject.Load(reader);
            }
            if (obj != null && obj.First != null)
            {
                var props = obj.Children();
                if (single != null)
                {
                    var id = props.ElementAt(1).First.Last.ToString();
                    single.ReferenceId = id;
                }
                else
                {
                    for (int i = 0; i < obj.Count(); i++)
                    {
                        var id = props.ElementAt(i).Last.Last.Last.ToString();
                        var type = Type.GetType(typeof(IIdentifiable).Namespace + ""."" + props.ElementAt(i).Last.Last.First.ToString());
                        IIdentifiable temp = (IIdentifiable)Activator.CreateInstance(type);
                        temp.ReferenceId = id;
                        list.Add(temp);
                    }
                }
            }
            if (single != null) { return single; }
            var t = objectType.GetGenericArguments()[0].Name;???
            return new InvalidOperationException();
        }

        private void MakeObject(IIdentifiable obj, JsonReader reader)
        {
            reader.Read();
            obj.ReferenceId = reader.Value.ToString();
            reader.Read();
        }
    }



    class SimpleTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (typeof(IEnumerable).IsAssignableFrom(objectType) && !objectType.ToString().ToLower().Equals(""string""))
            {
                JArray obj = JArray.Load(reader);
            }
            else
            {
                if (objectType == typeof(TimeSpan))
                {
                    JToken prop = JToken.Load(reader);
                    string[] values = prop.ToString().Split(new char[] { ':' });
                    if (values.Length == 1) { return new TimeSpan(int.Parse(values[0])); }
                    return new TimeSpan(int.Parse(values[0]), int.Parse(values[1]), int.Parse(values[2]));
                }
                if (objectType == typeof(DateTimeOffset))
                {
                    JToken prop = JToken.Load(reader);
                    string[] values = prop.ToString().Split(new char[] { ' ', '/', ':', '-', '+', 'T', 'Z' });
                    if (values.Length > 8)
                    {
                        return new DateTimeOffset(int.Parse(values[0]), int.Parse(values[1]), int.Parse(values[2]),
                            int.Parse(values[3]), int.Parse(values[4]), int.Parse(values[5]), 
                            new TimeSpan(int.Parse(values[6]), int.Parse(values[7]), int.Parse(values[8])));
                    }
                    if (values.Length == 3 && prop.ToString().Contains(""-""))
                    {
                        return new DateTimeOffset(int.Parse(values[0]), int.Parse(values[1]), int.Parse(values[2]), 0, 0, 0, new TimeSpan());
                    }
                    return new DateTimeOffset(1, 1, 1, int.Parse(values[0]), int.Parse(values[1]), int.Parse(values[2]),
                        new TimeSpan(int.Parse(values[4]), int.Parse(values[5]), int.Parse(values[6])));
                }
                if (objectType == typeof(Uri))
                {
                    JToken prop = JToken.Load(reader);
                    return new Uri(prop.ToString());
                }
                JObject obj = JObject.Load(reader);
                if (objectType == typeof(Tuple<int, int, string>))
                {
                    int a = int.Parse(((JProperty)obj.First).First.ToString());
                    int b = int.Parse(((JProperty)obj.First).Next.First.ToString());
                    if (((JProperty) obj.First).Next.First.ToString().Equals(((JProperty) obj.Last).Value.ToString()))
                    {
                        return new Tuple<int, int, string>(a, b, null);
                    }
                    return new Tuple<int, int, string>(a, b, ((JProperty) obj.Last).Value.ToString());
                }
                if (objectType == typeof(Tuple<int, string>))
                {
                    int a = int.Parse(((JProperty)obj.First).First.ToString());
                    if (((JProperty) obj.First).First.ToString().Equals(((JProperty) obj.Last).Value.ToString()))
                    {
                        return new Tuple<int, string>(a, null);
                    }
                    return new Tuple<int, string>(a, ((JProperty) obj.Last).Value.ToString());
                }
                if (objectType == typeof(CogsDate))
                {
                    string[] values = obj.First.First.ToString().Split(new char[] { ' ', '/', ':', '-', '+', 'T', 'Z' });
                    if (((JProperty)obj.First).Name.Equals(""Duration"")) { return new CogsDate(new TimeSpan(int.Parse(values[0]))); }
                    if (values.Length == 1) { return new CogsDate(new Tuple<int, string>(int.Parse(values[0]), null)); }
                    if (values.Length == 2)
                    {
                        if (obj.First.First.ToString().Contains(""-""))
                        {
                            return new CogsDate(new Tuple<int, int, string>(int.Parse(values[0]), int.Parse(values[1]), null));
                        }
                        return new CogsDate(new Tuple<int, string>(int.Parse(values[0]), values[1]));
                    }
                    if (values.Length == 3)
                    {
                        if (int.TryParse(values[2], out int i))
                        {
                            return new CogsDate(new DateTimeOffset(int.Parse(values[0]), int.Parse(values[1]), i, 0, 0, 0, new TimeSpan()), true);
                        }
                        return new CogsDate(new Tuple<int, int, string>(int.Parse(values[0]), int.Parse(values[1]), values[2]));
                    }
                    if (values.Length > 8)
                    {
                        return new CogsDate(new DateTimeOffset(int.Parse(values[0]), int.Parse(values[1]), int.Parse(values[2]),
                            int.Parse(values[3]), int.Parse(values[4]), int.Parse(values[5]),
                            new TimeSpan(int.Parse(values[6]), int.Parse(values[7]), int.Parse(values[8]))));
                    }
                }
            }
            return null;
        }
    }
}";
            StringBuilder ifs = new StringBuilder();
            foreach (var item in model.ItemTypes)
            {
                ifs.Append("$###if (t.Equals(\"" + item.Name + "\")) { return list.Cast<" + item.Name + ">().ToList(); }");
            }
            File.WriteAllText(Path.Combine(TargetDirectory, "JsonConverter.cs"), clss.Replace("!!!", projName).Replace("???", ifs.ToString()
                .Replace("$", Environment.NewLine).Replace("#", "    ")));
        }


        // initialize the Translator dictionary
        private void InitializeDictionary()
        {
            Translator = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "boolean", "bool" },
                { "integer", "int" },
                { "language", "string" },
                { "duration", "TimeSpan" },
                { "dateTime", "DateTimeOffset" },
                { "time", "DateTimeOffset" },
                { "date", "DateTimeOffset" },
                { "gYearMonth", "Tuple<int, int, string>" },
                { "gMonthDay", "Tuple<int, int, string>" },
                { "gYear", "Tuple<int, string>" },
                { "gYearDay", "Tuple<int, int, string>" },
                { "gDay", "Tuple<int, string>" },
                { "gMonth", "Tuple<int, string>" },
                { "anyURI", "Uri" },
                { "nonPositiveInteger", "int" },
                { "negativeInteger", "int" },
                { "nonNegativeInteger", "int" },
                { "unsignedLong", "ulong" },
                { "positiveInteger", "int" },
                { "cogsDate", "CogsDate" }
            };
        }
    }
}