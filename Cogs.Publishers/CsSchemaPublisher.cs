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
                var helpers = new StringBuilder();
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
                        if (!Isboolintdoubleulong(prop.DataTypeName)) { first = true; }
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
                    if (!first && (model.Identification.Contains(prop) || Isboolintdoubleulong(prop.DataTypeName))) { toJsonProperties.Append(","); }
                    var start = "((JObject)json.First).Add(";
                    if (model.ReusableDataTypes.Contains(item)) { start = "json.Add("; }
                    if (model.ItemTypes.Contains(prop.DataType) && !item.IsAbstract) { newClass.Append("$##[JsonConverter(typeof(IIdentifiableConverter))]"); }
                    // if there can be at most one, create an instance variable
                    if (!prop.MaxCardinality.Equals("n") && int.Parse(prop.MaxCardinality) == 1)
                    {
                        if (Isboolintdoubleulong(prop.DataTypeName) || model.Identification.Contains(prop))
                        {
                            toJsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", " + prop.Name + ")");
                            first = false;
                        }
                        else if (origDataTypeName != null)
                        {
                            newClass.Append("$##[JsonConverter(typeof(SimpleTypeConverter))]");
                            SimpleToJson(origDataTypeName, prop.Name, start, false, newClass);
                            if (prop.DataTypeName.Equals("CogsDate"))
                            {
                                reusableToJson.Append("$###if (" + prop.Name + ".GetValue() != null)");
                            }
                            else if (prop.DataTypeName.Equals("DateTimeOffset") || prop.DataTypeName.Equals("TimeSpan"))
                            {
                                reusableToJson.Append("$###if (" + prop.Name + " != default(" + prop.DataTypeName + "))");
                            }
                            else { reusableToJson.Append("$###if (" + prop.Name + " != null)"); }
                            reusableToJson.Append("$###{$####" + SimpleToJson(origDataTypeName, prop.Name, start, false) + ");$###}");
                        }
                        else if (model.ReusableDataTypes.Contains(prop.DataType))
                        {
                            reusableToJson.Append("$###if (" + prop.Name + " != null) { ");
                            reusableToJson.Append(start + "new JProperty(\"" + prop.Name + "\", " + prop.Name + ".ToJson())); }");
                            if (model.ItemTypes.Contains(item)) { initializeReferences.Append(InitializeReusable(prop, model)); }
                        }
                        else if(!model.ItemTypes.Contains(prop.DataType))
                        {
                            reusableToJson.Append("$###if ( " + prop.Name + " != null) $###{$####" + start + "new JProperty(\"" + prop.Name + "\", " + prop.Name + "));$###}");
                        }
                        else
                        {
                            if (model.ReusableDataTypes.Contains(item))
                            {
                                reusableToJson.Append("$###if ( " + prop.Name + " != null) $###{$####" + start + "new JProperty(\"" + prop.Name + "\", new JObject($#####" +
                                "new JProperty(\""+ prop.DataTypeName + "\", new JObject($######new JProperty(\"ID\", " + prop.Name + ".ID))))));$###}");
                            }
                            else
                            {
                                reusableToJson.Append("$###if ( " + prop.Name + " != null) $###{$####" + start + "new JProperty(\"" + prop.Name + "\", new JObject(" +
                               "new JProperty(\"@type\", \"ref\"), $#####new JProperty(\"value\", new JArray($######\"" + prop.DataTypeName + "\", $######" + prop.Name + ".ID)))));$###}");
                                initializeReferences.Append("$###if (" + prop.Name + " != null) { " + prop.Name + " = (" + prop.DataTypeName +
                                    ")dict[" + prop.Name + ".ReferenceId]; }");
                            }
                        }
                        newClass.Append("$##public " + prop.DataTypeName + " " + prop.Name + " { get; set; }");
                    }
                    // otherwise, create a list object to allow multiple
                    else
                    {
                        if (Isboolintdoubleulong(prop.DataTypeName) || model.Identification.Contains(prop))
                        {
                            toJsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", $#####new JArray($######from item in " + prop.Name +
                                "$######select item))");
                            first = false;
                        }
                        else if (origDataTypeName != null)
                        {
                            newClass.Append("$##[JsonConverter(typeof(SimpleTypeConverter))]");
                            SimpleToJson(origDataTypeName, prop.Name, "", true, newClass);
                            reusableToJson.Append("$###if (" + prop.Name + " != null && " + prop.Name + ".Count > 0)");
                            reusableToJson.Append("$###{$####var prop = new JProperty(\"" + prop.Name + "\", new JArray());$####foreach (var item in " +
                                prop.Name + ")$####{" + SimpleToJson(origDataTypeName, prop.Name, "", true) + "$####}$####" + start + "prop);$###}");
                        }
                        else if (model.ReusableDataTypes.Contains(prop.DataType))
                        {
                            reusableToJson.Append("$###if (" + prop.Name + " != null && " + prop.Name + ".Count > 0)$###{$####" + start + "new JProperty(\"" + 
                                prop.Name + "\", $#####new JArray($######from item in " + prop.Name + "$######select new JObject($#######new JProperty(\"" + 
                                prop.DataTypeName + "\", item.ToJson()))))); $###}");
                            if (model.ItemTypes.Contains(item)) { initializeReferences.Append(InitializeReusable(prop, model, helpers)); }
                        }
                        else if (!model.ItemTypes.Contains(prop.DataType))
                        {
                            reusableToJson.Append("$###if ( " + prop.Name + " != null && " + prop.Name + ".Count > 0) $###{$####" + start + "new JProperty(\"" + 
                                prop.Name + "\", new JArray($#####from item in " + prop.Name + "$#####select item)));$###}");
                        }
                        else
                        {
                            if (model.ReusableDataTypes.Contains(item))
                            {
                                reusableToJson.Append("$###if (" + prop.Name + " != null)$###{$####" + start + "new JProperty(\"" + prop.Name +
                                "\", $#####new JArray($######from item in " + prop.Name + "$######select new JObject(new JProperty(\"" + prop.DataTypeName + 
                                "\", new JObject($######new JProperty(\"ID\", item.ID)))))));$###}");
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
                        newClass.Append("$##public List<" + prop.DataTypeName + "> " + prop.Name + "{ get; set; } = new List<" + prop.DataTypeName + ">();");
                    }
                }
                string returnType = "JProperty";
                if (model.ItemTypes.Contains(item))
                {
                    newClass.Append("$##/// <summary>$##/// Used to Serialize this object to Json $##/// <summary>");
                    if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                    {
                        newClass.Append("$##public override " + returnType + " ToJson()$##{$###JProperty json = base.ToJson();$###((JObject)json.First).Add(");
                    }
                    else { newClass.Append("$##public virtual " + returnType + " ToJson()$##{$###JProperty json = new JProperty(ID, new JObject("); }
                    newClass.Append(toJsonProperties.ToString());
                    if (string.IsNullOrWhiteSpace(item.ExtendsTypeName)) { newClass.Append(")"); }
                    newClass.Append(");" + reusableToJson.ToString() + "$###return json;$##}");
                    newClass.Append("$$##/// <summary>$##/// Used to set this object's properties from Json $##/// <summary>");
                    if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                    {
                        newClass.Append("$##public override void InitializeReferences(Dictionary<string, IIdentifiable> dict, string json)$##{" +
                            "$###base.InitializeReferences(dict, json);");
                    }
                    else { newClass.Append("$##public virtual void InitializeReferences(Dictionary<string, IIdentifiable> dict, string json)$##{"); }
                    if (initializeReferences.ToString().Contains("thisObj"))
                    {
                        newClass.Append("$###string[] parts = json.Split(new string[] { \":\", \",\", Environment.NewLine }, " +
                        "StringSplitOptions.None);$###bool thisObj = false;$###int reusablesInitialized = 0;$###for (int i = 0; i < parts.Length; i ++)$###{$####" +
                        "if (reusablesInitialized == " + item.Properties.Where(x => model.ReusableDataTypes.Contains(x.DataType)).ToList().Count + ") { return; }$####" +
                        "else if (parts[i].Contains(ID)) { thisObj = true; }" + initializeReferences.ToString() + "$###}$##}" + helpers.ToString() + "$#}$}$");
                    }
                    else { newClass.Append(initializeReferences.ToString() + "$##}$#}$}$"); }
                }
                else
                {
                    returnType = "JObject";
                    if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                    {
                        newClass.Append("$##public override " + returnType + " ToJson()$##{$###JObject json = base.ToJson();$###((JObject)json.First).Add(");
                    }
                    else { newClass.Append("$##public virtual " + returnType + " ToJson()$##{$###JObject json = new JObject() {"); }
                    newClass.Append(toJsonProperties.ToString());
                    newClass.Append("};" + reusableToJson.ToString() + "$###return json;$##}$#}$}$");
                }
                // write class to out folder
                File.WriteAllText(Path.Combine(TargetDirectory, item.Name + ".cs"), newClass.ToString().
                    Replace("$###((JObject)json.First).Add();", "").Replace("#", "    ").Replace("$", Environment.NewLine).Replace("@", "$"));
            }
        }


        private bool Isboolintdoubleulong(string name)
        {
            if (name.Equals("bool") || name.Equals("int") || name.Equals("double") || name.Equals("ulong") || name.Equals("long")) { return true; }
            return false;
        }


        private string InitializeReusable(Property prop, CogsModel model, StringBuilder main = null)
        {
            var name = prop.Name;
            var type = prop.DataTypeName;
            StringBuilder builder = new StringBuilder(@"
                else if (parts[i].Contains(""" + name + @""") && thisObj)
                {
                    ");
            if (main != null)
            {
                builder.Append(name + " = new List<" + type + @">();
                    Initialize" + name + "(" + name + @", parts, i);
                }");
                InitializeReusableList(prop, model, main, name, type);
            }
            else
            {
                builder.Append(name + " = new " + type + @"();
                    int counter = 1;
                    i++;
                    while (i < parts.Length && counter > 0)
                    {
                        var line = parts[i].Trim().Replace(""\"""", """");
                        if (line.Equals(""{"")) { counter++; }
                        else if (line.Equals(""}"")) { counter--; }");
                foreach (var p in prop.DataType.Properties)
                {
                    if (p.DataTypeName.Equals("cogsDate")) { builder.Append("$######else if (line.Equals(\"cogsDate\"))"); }
                    else { builder.Append("$######else if (line.Equals(\"" + p.Name + "\"))"); }
                    if (!p.MaxCardinality.Equals("1"))
                    {
                        builder.Append(@"
                        {
                            " + name + "." + p.Name + " = new List<" + p.DataTypeName + @">();
                            i += 2;
                            int array = 1;
                            while (i < parts.Length && counter > 0 && array > 0)
                            {
                                line = parts[i].Trim().Replace(""\"""", """");
                                if (line.Equals(""{"")) { counter++; }
                                else if (line.Equals(""}"")) { counter--; }
                                else if (line.Equals(""["")) { array++; }
                                else if (line.Equals(""]"")) { array--; }
                                else if (!string.IsNullOrWhiteSpace(line)) " + InitializeObject(p, model, true, name, ".Add(") + @"
                                i++;
                            }
                        }");
                    }
                    else { builder.Append(InitializeObject(p, model, false, name, " = ")); }
                }
                builder.Append(@"
                        i++;
                    }
                    reusablesInitialized++;
                }");
            }
            return builder.ToString();
        }


        private void InitializeReusableList(Property prop, CogsModel model, StringBuilder main, string name, string type)
        {
            StringBuilder subs = new StringBuilder();
            main.Append("$##private int Initialize" + name + "(List<" + type + "> list, string[] parts, int i)$##{$###" + type + @" obj = null;
            bool open = true;
            i += 3;
            while (i < parts.Length && open)
            {
                var line = parts[i].Trim().Replace(""\"""", """");
                if (line.Equals(""]"")) { open = false; }
                else if (line.Equals(""" + type + @"""))
                {
                    if (obj != null) { list.Add(obj); }
                    obj = new " + type + @"();
                }");
            foreach (var p in prop.DataType.Properties)
            {
                if (p.DataTypeName.Equals("cogsDate")) { main.Append("$####else if (line.Equals(\"cogsDate\"))"); }
                else { main.Append("$####else if (line.Equals(\"" + p.Name + "\"))"); }
                if (!p.MaxCardinality.Equals("1"))
                {
                    if (p.DataTypeName.Equals(type))
                    {
                        main.Append(" { i = Initialize" + name + "(obj." + p.Name + @", parts, i); }");
                    }
                    else if (model.ReusableDataTypes.Contains(prop.DataType))
                    {
                        main.Append(" { i = Initialize" + p.Name + "(obj." + p.Name + ", parts, i, int counter); }");
                        InitializeReusableList(p, model, subs, p.Name, p.DataTypeName);
                    }
                    else
                    {
                        main.Append(@"
                        {
                            i += 2;
                            int array = 1;
                            while (i < parts.Length && counter > 0 && array > 0)
                            {
                                line = parts[i].Trim().Replace(""\"""", """");
                                if (line.Equals(""{"")) { counter++; }
                                else if (line.Equals(""}"")) { counter--; }
                                else if (line.Equals(""["")) { array++; }
                                else if (line.Equals(""]"")) { array--; }
                                else if (!string.IsNullOrWhiteSpace(line)) " + InitializeObject(p, model, true, "obj", ".Add(") + @"
                                i++;
                            }
                        }");
                    }
                }
                else { main.Append(" " + InitializeObject(p, model, false, "obj", " = ")); }
            }
            main.Append("$####i++;$###}$###if (obj != null) { list.Add(obj); }$###return i;$##}" + subs);
        }


        private string InitializeObject(Property p, CogsModel model, bool isList, string name, string operation)
        {
            if (p.DataTypeName.Equals("cogsDate"))
            {
                return @"
                        {
                            if (parts[i + 2].Trim().Replace(""\"""", """").Equals(""datetime""))
                            {
                                " + name + "." + p.Name + @" = new CogsDate(parts[i + 2].Trim().Replace(""\"""", """"), (parts[i + 3] + "":"" + parts[i + 4] + "":"" + parts[i + 5] +
                                   "":"" + parts[i + 6] + "":"" + parts[i + 7]).Trim().Replace(""\"""", """"));
                            }
                            else { " + name + "." + p.Name + @" = new CogsDate(parts[i + 2].Trim().Replace(""\"""", """"), parts[i + 3].Trim().Replace(""\"""", """")); }
                        }";
            }
            if (operation.Equals(".Add(")) { return "{ " + name + "." + p.Name + operation + ReusableTypeConvert(p.DataTypeName, isList, model) + @"); }"; }
            return "{ " + name + "." + p.Name + operation + ReusableTypeConvert(p.DataTypeName, isList, model) + @"; }";
        }


        private string ReusableTypeConvert(string name, bool isList, CogsModel model)
        {
            string i = "i + 1";
            if(isList) { i = "i"; }
            if (name.Equals("int")) { return "int.Parse(parts[" + i + "].Trim().Replace(\"\\\"\", \"\"))"; }
            if (name.Equals("double")) { return "double.Parse(parts[" + i + "].Trim().Replace(\"\\\"\", \"\"))"; }
            if (name.Equals("decimal")) { return "decimal.Parse(parts[" + i + "].Trim().Replace(\"\\\"\", \"\"))"; }
            if (model.ItemTypes.Where(x => x.Name == name).ToList().Count > 0) { return "(" + name + ")dict[parts[i + 5].Trim().Replace(\"\\\"\", \"\")]"; }
            return "parts[" + i + "].Trim().Replace(\"\\\"\", \"\")";
        }

        private string SimpleToJson(string origDataTypeName, string name, string start, bool isList, StringBuilder builder = null)
        { 
            if (origDataTypeName.ToLower().Equals("duration"))
            {
                if (!isList) { return start + "new JProperty(\"duration\", " + name + ".Ticks)"; }
                return "((JArray)prop.First).Add(item.Ticks);";
            }
            if (origDataTypeName.ToLower().Equals("datetime"))
            {
                if (!isList)
                {
                    if (builder != null) { builder.Append("$##[JsonProperty(\"datetime\")]"); }
                    return start + "new JProperty(\"datetime\", " + name + ".ToString(\"s\") + \"+\" + " + name + ".Offset.ToString())";
                }
                if (builder != null) { builder.Append("$##[JsonProperty(\"" + name + "\")]"); }
                return "((JArray)prop.First).Add(item.ToString(\"s\") + \"+\" + item.Offset.ToString());";
            }
            if (origDataTypeName.ToLower().Equals("time"))
            {
                if (!isList) { return start + "new JProperty(\"Time\", " + name + ".ToString(\"u\").Split(' ')[1])"; }
                return "((JArray)prop.First).Add(item.ToString(\"u\").Split(' ')[1]);";
            }
            if (origDataTypeName.ToLower().Equals("date"))
            {
                if (!isList) { return start + "new JProperty(\"date\", " + name + ".ToString(\"u\").Split(' ')[0])"; }
                return "((JArray)prop.First).Add(item.ToString(\"u\").Split(' ')[0]);";
            }
            if (origDataTypeName.ToLower().Equals("gyearmonth"))
            {
                if (!isList)
                {
                    if (builder != null) { builder.Append("$##[JsonProperty(\"YearMonth\")]"); }
                    return "var ym = new JProperty(\"YearMonth\", new JObject($#####new JProperty(\"year\", " + name + ".Item1),$#####new " +
                    "JProperty(\"month\", " + name + ".Item2)));$####if (" + name + ".Item3 != null) { ((JObject)ym.First).Add(new JProperty(\"timezone\", " + name +
                    ".Item3)); }$####" + start + "ym";
                }
                if (builder != null) { builder.Append("$##[JsonProperty(\"" + name + "\")]"); }
                return "$#####if (item.Item3 != null)$#####{$######((JArray)prop.First).Add(new JObject($#######new JProperty(\"year\", item.Item1), " +
                    "new JProperty(\"month\", item.Item2), new JProperty(\"timezone\", item.Item3)));$#####}$#####else { ((JArray)prop.First).Add(new JObject(" +
                    "new JProperty(\"year\", item.Item1), new JProperty(\"month\", item.Item2))); }";
            }
            if (origDataTypeName.ToLower().Equals("gyear"))
            {
                if (!isList)
                {
                    if (builder != null) { builder.Append("$##[JsonProperty(\"year\")]"); }
                    return "var y = new JProperty(\"year\", new JObject($#####new JProperty(\"year\", " + name + ".Item1)));$####if (" + name + ".Item2 != null) " +
                    "{ ((JObject)y.First).Add(new JProperty(\"timezone\", " + name + ".Item2)); }$####" + start + "y";
                }
                if (builder != null) { builder.Append("$##[JsonProperty(\"" + name + "\")]"); }
                return "$#####if (item.Item2 != null)$#####{$######((JArray)prop.First).Add(new JObject($#######new JProperty(\"year\", item.Item1), " +
                    "new JProperty(\"timezone\", item.Item2)));$#####}$#####else { ((JArray)prop.First).Add(new JObject(new JProperty(\"year\", item.Item1))); }";
            }
            if (origDataTypeName.ToLower().Equals("gmonthday"))
            {
                if(!isList)
                {
                    if (builder != null) { builder.Append("$##[JsonProperty(\"MonthDay\")]"); }
                    return "var md = new JProperty(\"MonthDay\", new JObject($#####new JProperty(\"month\", " + name + ".Item1),$#####new " +
                    "JProperty(\"day\", " + name + ".Item2)));$####if (" + name + ".Item3 != null) { ((JObject)md.First).Add(new JProperty(\"timezone\", " + name +
                    ".Item3)); }$####" + start + "md";
                }
                if (builder != null) { builder.Append("$##[JsonProperty(\"" + name + "\")]"); }
                return "$#####if (item.Item3 != null)$#####{$######((JArray)prop.First).Add(new JObject($#######new JProperty(\"month\", item.Item1), " +
                    "new JProperty(\"day\", item.Item2), new JProperty(\"timezone\", item.Item3)));$#####}$#####else { ((JArray)prop.First).Add(new JObject(" +
                    "new JProperty(\"month\", item.Item1), new JProperty(\"day\", item.Item2))); }";
            }
            if (origDataTypeName.ToLower().Equals("gday"))
            {
                if (!isList)
                {
                    if (builder != null) { builder.Append("$##[JsonProperty(\"day\")]"); }
                    return "var d = new JProperty(\"day\", new JObject($#####new JProperty(\"day\", " + name + ".Item1)));$####if (" + name + ".Item2 != null) " +
                    "{ ((JObject)d.First).Add(new JProperty(\"timezone\", " + name + ".Item2)); }$####" + start + "d";
                }
                if (builder != null) { builder.Append("$##[JsonProperty(\"" + name + "\")]"); }
                return "$#####if (item.Item2 != null) {((JArray)prop.First).Add(new JObject(new JProperty(\"day\", item.Item1), new JProperty(\"timezone\", item.Item2))); }" +
                    "$#####else { ((JArray)prop.First).Add(new JObject(new JProperty(\"day\", item.Item1))); }";

            }
            if (origDataTypeName.ToLower().Equals("gmonth"))
            {
                if (!isList)
                {
                    if (builder != null) { builder.Append("$##[JsonProperty(\"month\")]"); }
                    return "var m = new JProperty(\"month\", new JObject($#####new JProperty(\"month\", " + name + ".Item1)));$####if (" + name + ".Item2 != null) " +
                    "{ ((JObject)m.First).Add(new JProperty(\"timezone\", " + name + ".Item2)); }$####" + start + "m";
                }
                if (builder != null) { builder.Append("$##[JsonProperty(\"" + name + "\")]"); }
                return "$#####if (item.Item2 != null) { ((JArray)prop.First).Add(new JObject(new JProperty(\"month\", item.Item1), new JProperty(\"timezone\", item.Item2))); }" +
                    "$#####else { ((JArray)prop.First).Add(new JObject(new JProperty(\"month\", item.Item1))); }";
            }
            if (origDataTypeName.ToLower().Equals("anyuri"))
            {
                if (!isList)
                {
                    if (builder != null) { builder.Append("$##[JsonProperty(\"anyuri\")]"); }
                    return start + "new JProperty(\"anyuri\", " + name + ")";
                }
                if (builder != null) { builder.Append("$##[JsonProperty(\"" + name + "\")]"); }
                return "$#####((JArray)prop.First).Add(new JObject(new JPropert(\"anyuri\", item))); }";
            }
            if (origDataTypeName.ToLower().Equals("cogsdate"))
            {
                if (!isList)
                {
                    if (builder != null) { builder.Append("$##[JsonProperty(\"cogsdate\")]"); }
                    return start + "new JProperty(\"cogsDate\", new JObject($#####new JProperty(" + name + ".GetUsedType(), " + name +
                    ".GetValue())))";
                }
                if (builder != null) { builder.Append("$##[JsonProperty(\"" + name + "\")]"); }
                return "$#####((JArray)prop.First).Add(new JObject($######new JProperty(item.GetUsedType(), item.GetValue())));";
            }
            if (!isList) { return start + "new JProperty(\"" + name + "\", " + name + ")"; }
            return "$#####((JArray)prop.First).Add(item);";
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
                    string id = null;
                    if (props.ElementAt(0).First.ToString().Equals(""ref"")) { id = props.ElementAt(1).First.Last.ToString(); }
                    else { id = props.ElementAt(0).First.Last.ToString(); }
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
                IList values = (IList)Activator.CreateInstance(objectType);
                var current = obj.First;
                while (current != null)
                {
                    values.Add(Translate(current, objectType.GetGenericArguments()[0]));
                    current = current.Next;
                }
                return values;
            }
            else { return Translate(JToken.Load(reader), objectType);  }
        }

        private object Translate(JToken prop, Type objectType)
        {
            if (objectType == typeof(TimeSpan))
            {
                string[] values = prop.ToString().Split(new char[] { ':' });
                if (values.Length == 1) { return new TimeSpan(int.Parse(values[0])); }
                return new TimeSpan(int.Parse(values[0]), int.Parse(values[1]), int.Parse(values[2]));
            }
            if (objectType == typeof(DateTimeOffset))
            {
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
                return new DateTimeOffset(1, 1, 1, int.Parse(values[0]), int.Parse(values[1]), int.Parse(values[2]), new TimeSpan());
            }
            if (objectType == typeof(Uri))
            {
                return new Uri(prop.ToString());
            }
            JObject obj = (JObject)prop;
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
                if (((JProperty)obj.First).Name.Equals(""duration"")) { return new CogsDate(new TimeSpan(int.Parse(values[0]))); }
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