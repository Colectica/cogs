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
                    new XElement("PackageReference", new XAttribute("Include", "Microsoft.CSharp"), 
                    new XAttribute("Version", "4.4.0-preview2-25405-01")),
                    new XElement("PackageReference", new XAttribute("Include", "Newtonsoft.Json"), new XAttribute("Version", "10.0.3")))));
            XmlWriterSettings xws = new XmlWriterSettings { OmitXmlDeclaration = true };
            using (XmlWriter xw = XmlWriter.Create(Path.Combine(TargetDirectory, projName + ".csproj"), xws))
            {
                project.Save(xw);
            }
            CreateJsonConverter(model, projName);
            // copy types file
            this.GetType().GetTypeInfo().Assembly.GetManifestResourceStream("Cogs.Publishers.Types.txt").CopyTo(
                new FileStream(Path.Combine(TargetDirectory, "Types.cs"), FileMode.Create));
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                // add class description using '$' for newline and '#' for tabs
                var newClass = new StringBuilder("using System;");
                newClass.AppendLine("using System.Linq;");
                newClass.AppendLine("using Newtonsoft.Json;");
                newClass.AppendLine("using Newtonsoft.Json.Linq;");
                newClass.AppendLine("using Cogs.DataAnnotations;");
                newClass.AppendLine("using System.Collections.Generic;");
                newClass.AppendLine("using System.ComponentModel.DataAnnotations;");
                newClass.AppendLine();
                newClass.AppendLine($"namespace {projName}");
                newClass.AppendLine("{");
                newClass.AppendLine("    /// <summary>");
                newClass.AppendLine($"    /// {item.Description}");
                newClass.AppendLine("    /// <summary>");
                newClass.Append("    public ");
                var toJsonProperties = new StringBuilder();
                var initializeReferences = new StringBuilder();
                var initializeReusables = new StringBuilder();
                var reusableToJson = new StringBuilder();
                var helpers = new StringBuilder();
                int reusablesInitialized = item.Properties.Where(x => model.ReusableDataTypes.Contains(x.DataType)).ToList().Count;
                // add abstract to class title if relevant
                if (item.IsAbstract) { newClass.Append("abstract "); }
                newClass.Append("class " + item.Name);
                // allow inheritance when relevant
                if (!String.IsNullOrWhiteSpace(item.ExtendsTypeName))
                {
                    newClass.AppendLine($" : {item.ExtendsTypeName}");
                    newClass.AppendLine("    {");
                    newClass.AppendLine("        public new string ReferenceId { set; get; }");
                }
                else if (!model.ReusableDataTypes.Contains(item))
                {
                    newClass.AppendLine(" : IIdentifiable");
                    newClass.AppendLine("    {");
                    newClass.AppendLine("        public string ReferenceId { set; get; }");
                }
                else { newClass.AppendLine("    {"); }
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
                    newClass.AppendLine("        /// <summary");
                    newClass.AppendLine($"        /// {prop.Description}");
                    newClass.AppendLine("        /// <summary");
                    // create constraints
                    if (prop.DataTypeName.Equals("string") || prop.DataTypeName.Equals("Uri"))
                    {
                        if (prop.MinLength != null && prop.MaxLength != null)
                        {
                            newClass.AppendLine($"        [StringLength({prop.MaxLength}, MinimumLength = {prop.MinLength})]");
                        }
                        else if (prop.MaxLength != null)
                        {
                            newClass.AppendLine($"        [StringLength({prop.MaxLength})]");
                        }
                        else if (prop.MinLength != null)
                        {
                            newClass.AppendLine($"        [StringLength({int.MaxValue}, MinimumLength = {prop.MinLength})]");
                        }
                        if (prop.DataTypeName.Equals("string"))
                        {
                            // work with Enum and pattern
                            if (prop.Enumeration.Count > 0)
                            {
                                newClass.AppendLine("        [StringValidation(new string[] {");
                                bool useComma = false;
                                foreach (var option in prop.Enumeration)
                                {
                                    if (useComma) { newClass.Append(", "); }
                                    newClass.AppendLine($"            \"{option}\"");
                                    useComma = true;
                                }
                                if (!string.IsNullOrWhiteSpace(prop.Pattern)) { newClass.AppendLine($"        }}, {prop.Pattern})]"); }
                                else { newClass.AppendLine("        })]"); }
                            }
                            else if (!string.IsNullOrWhiteSpace(prop.Pattern))
                            {
                                newClass.AppendLine($"        [StringValidation(null, \"{prop.Pattern}\")]");
                            }
                        }
                    }
                    else if (!prop.DataTypeName.Equals("bool") && !prop.DataTypeName.Equals("CogsDate"))
                    {
                        if (prop.MinInclusive != null || prop.MaxInclusive != null)
                        {
                            newClass.AppendLine($"        [Range({prop.MinInclusive}, {prop.MaxInclusive})]");
                        }
                        if (prop.MinExclusive != null || prop.MaxExclusive != null)
                        {
                            newClass.AppendLine($"        [ExclusiveRange({prop.MinInclusive}, {prop.MaxInclusive})]");
                        }
                    }
                    if (!first && (model.Identification.Contains(prop) || Isboolintdoubleulong(prop.DataTypeName)))
                    {
                        toJsonProperties.AppendLine(",");
                    }
                    var start = "((JObject)json.First).Add(";
                    if (model.ReusableDataTypes.Contains(item)) { start = "json.Add("; }
                    if (model.ItemTypes.Contains(prop.DataType) && !item.IsAbstract)
                    {
                        newClass.AppendLine("        [JsonConverter(typeof(IIdentifiableConverter))]");
                    }
                    // if there can be at most one, create an instance variable
                    if (!prop.MaxCardinality.Equals("n") && int.Parse(prop.MaxCardinality) == 1)
                    {
                        if (Isboolintdoubleulong(prop.DataTypeName) || model.Identification.Contains(prop))
                        {
                            toJsonProperties.AppendLine();
                            toJsonProperties.Append($"                new JProperty(\"{prop.Name}\", {prop.Name})");
                            first = false;
                        }
                        else if (origDataTypeName != null)
                        {
                            newClass.AppendLine("        [JsonConverter(typeof(SimpleTypeConverter))]");
                            if (prop.DataTypeName.Equals("CogsDate"))
                            {
                                reusablesInitialized++;
                                reusableToJson.AppendLine($"            if ({prop.Name}.GetValue() != null)");
                                initializeReferences.AppendLine($"                else if (line.Equals(\"{prop.Name}\") && thisObj && " +
                                    $"{prop.Name}.GetUsedType().Equals(\"datetime\"))");
                                initializeReferences.AppendLine("                {");
                                initializeReferences.AppendLine($"                    string[] start = parts[i + 3].Trim().Replace" +
                                    $"(\"\\\"\", \"\").Split('-', 'T');");
                                initializeReferences.AppendLine($"                   {prop.Name} = new CogsDate(new DateTimeOffset(" +
                                    $"int.Parse(start[0]), int.Parse(start[1]), int.Parse(start[2]), int.Parse(start[3]), ");
                                initializeReferences.AppendLine("                        int.Parse(parts[i + 4]), int.Parse(parts[i + 5]" +
                                    ".Split('+')[0]), new TimeSpan(int.Parse(parts[i + 5].Split('+')[1]), ");
                                initializeReferences.AppendLine($"                       int.Parse(parts[i + 6].Replace(\"\\\"\", \"\")), 0)));");
                                initializeReferences.AppendLine("                }");
                            }
                            else if (prop.DataTypeName.Equals("DateTimeOffset") || prop.DataTypeName.Equals("TimeSpan"))
                            {
                                reusableToJson.AppendLine($"            if ({prop.Name} != default({prop.DataTypeName}))");
                                if (origDataTypeName.Equals("dateTime"))
                                {
                                    reusablesInitialized++;
                                    initializeReferences.AppendLine();
                                    initializeReferences.AppendLine($"                else if (line.Equals(\"{prop.Name}\") && thisObj) ");
                                    initializeReferences.AppendLine("                {");
                                    initializeReferences.AppendLine($"                    string[] start = parts[i + 1].Trim().Replace" +
                                        $"(\"\\\"\", \"\").Split('-', 'T');");
                                    initializeReferences.AppendLine($"                   {prop.Name} = new DateTimeOffset(" +
                                        $"int.Parse(start[0]), int.Parse(start[1]), int.Parse(start[2]), int.Parse(start[3]), ");
                                    initializeReferences.AppendLine("                        int.Parse(parts[i + 2]), int.Parse(parts[i + 3]" +
                                        ".Split('+')[0]), new TimeSpan(int.Parse(parts[i + 3].Split('+')[1]), ");
                                    initializeReferences.AppendLine($"                       int.Parse(parts[i + 4].Replace(\"\\\"\", \"\")), 0));");
                                    initializeReferences.AppendLine("                }");
                                }
                            }
                            else { reusableToJson.AppendLine($"            if ({prop.Name} != null)"); }
                            reusableToJson.AppendLine("            {");
                            reusableToJson.AppendLine($"                {SimpleToJson(origDataTypeName, prop.Name, start, false)});");
                            reusableToJson.AppendLine("            }");
                        }
                        else if (model.ReusableDataTypes.Contains(prop.DataType))
                        {
                            reusableToJson.AppendLine($"            if ({prop.Name} != null) {{ {start} new JProperty(\"{prop.Name}\", " +
                                $"{prop.Name}.ToJson())); }}");
                            if (model.ItemTypes.Contains(item)) { initializeReferences.Append(InitializeReusable(prop, model)); }
                        }
                        else if(!model.ItemTypes.Contains(prop.DataType))
                        {
                            reusableToJson.AppendLine($"            if ({prop.Name} != null)");
                            reusableToJson.AppendLine("            {");
                            reusableToJson.AppendLine($"                {start}new JProperty(\"{prop.Name}\", {prop.Name}));");
                            reusableToJson.AppendLine("            }");
                        }
                        else
                        {
                            if (model.ReusableDataTypes.Contains(item))
                            {
                                reusableToJson.AppendLine($"            if ({prop.Name} != null)");
                                reusableToJson.AppendLine("            {");
                                reusableToJson.AppendLine($"                {start}new JProperty(\"{prop.Name}\", new JObject(");
                                reusableToJson.AppendLine($"                    new JProperty(\"{prop.DataTypeName}\", new JObject(");
                                reusableToJson.AppendLine($"                        new JProperty(\"ID\", {prop.Name}.ID))))));");
                                reusableToJson.AppendLine("            }");
                            }
                            else
                            {
                                reusableToJson.AppendLine($"            if ({prop.Name} != null)");
                                reusableToJson.AppendLine("            {");
                                reusableToJson.AppendLine($"                {start}new JProperty(\"{prop.Name}\", new JObject(" +
                                    $"new JProperty(\"@type\", \"ref\"), ");
                                reusableToJson.AppendLine($"                    new JProperty(\"value\", new JArray(\"{prop.DataTypeName}\",");
                                reusableToJson.AppendLine($"                        {prop.Name}.ID)))));");
                                reusableToJson.AppendLine("            }");
                                initializeReusables.AppendLine($"            if ({prop.Name} != null) {{ {prop.Name} = ({prop.DataTypeName})" +
                                    $"dict[{prop.Name}.ReferenceId]; }}");
                            }
                        }
                        newClass.AppendLine($"        public {prop.DataTypeName} {prop.Name} {{ get; set; }}");
                    }
                    // otherwise, create a list object to allow multiple
                    else
                    {
                        if (Isboolintdoubleulong(prop.DataTypeName) || model.Identification.Contains(prop))
                        {
                            toJsonProperties.AppendLine();
                            toJsonProperties.AppendLine($"                new JProperty(\"{prop.Name}\", ");
                            toJsonProperties.AppendLine($"                    new JArray(");
                            toJsonProperties.AppendLine($"                        from item in {prop.Name}");
                            toJsonProperties.Append($"                        select item))");
                            first = false;
                        }
                        else if (origDataTypeName != null)
                        {
                            if (origDataTypeName.Equals("dateTime"))
                            {
                                initializeReferences.AppendLine();
                                initializeReferences.AppendLine($"                else if (line.Equals(\"{prop.Name}\") && thisObj)");
                                initializeReferences.AppendLine("                { ");
                                initializeReferences.AppendLine($"                    {prop.Name} = new List<{prop.DataTypeName}>();");
                                initializeReferences.AppendLine("                    i++;");
                                initializeReferences.AppendLine("                    while (i < parts.Length)");
                                initializeReferences.AppendLine("                    {");
                                initializeReferences.AppendLine("                        if (parts[i].Trim().Replace(\"\\\"\", \"\")" +
                                    ".Equals(\"]\")) { break; }");
                                initializeReferences.AppendLine("                        string[] start = parts[i + 1].Trim()" +
                                    ".Replace(\"\\\"\", \"\").Split('-', 'T');");
                                initializeReferences.AppendLine($"                        {prop.Name}.Add(new DateTimeOffset(" +
                                    "int.Parse(start[0]), int.Parse(start[1]), int.Parse(start[2]), int.Parse(start[3]), ");
                                initializeReferences.AppendLine("                            int.Parse(parts[i + 2]), " +
                                    "int.Parse(parts[i + 3].Split('+')[0]), ");
                                initializeReferences.AppendLine("                            new TimeSpan(int.Parse(parts[i + 3].Split('+')[1]), " +
                                    "int.Parse(parts[i + 4].Replace(\"\\\"\", \"\")), 0)));");
                                initializeReferences.AppendLine("                        i += 5;");
                                initializeReferences.AppendLine("                    }");
                                initializeReferences.AppendLine("                }");
                                reusablesInitialized++;
                            }
                            else if (origDataTypeName.Equals("cogsDate"))
                            {
                                initializeReferences.AppendLine($"                else if (line.Equals(\"{prop.Name}\") && thisObj && " +
                                    $"{prop.Name}.Where(x => x.GetUsedType().Equals(\"datetime\")).ToList().Count > 0)");
                                initializeReferences.AppendLine("                {");
                                initializeReferences.AppendLine("                    List<int> indices = new List<int>();");
                                initializeReferences.AppendLine($"                    for (int j = 0; j < {prop.Name}.Count; j++) ");
                                initializeReferences.AppendLine("                    {");
                                initializeReferences.AppendLine($"                        if ({prop.Name}[j].GetUsedType()" +
                                    $".Equals(\"datetime\")) {{ indices.Add(j); }}");
                                initializeReferences.AppendLine("                    }");
                                initializeReferences.AppendLine("                    int done = 0;");
                                initializeReferences.AppendLine("                    i++;");
                                initializeReferences.AppendLine("                    while (i < parts.Length && done < indices.Count)");
                                initializeReferences.AppendLine("                    {");
                                initializeReferences.AppendLine("                        if (parts[i].Trim().Replace(\"\\\"\", \"\")" +
                                    ".Equals(\"]\")) { break; }");
                                initializeReferences.AppendLine("                        else if (parts[i].Trim().Replace(\"\\\"\", \"\")" +
                                    ".Equals(\"datetime\"))");
                                initializeReferences.AppendLine("                        {");
                                initializeReferences.AppendLine("                            string[] start = parts[i + 1].Trim()" +
                                    ".Replace(\"\\\"\", \"\").Split('-', 'T');");
                                initializeReferences.AppendLine($"                            {prop.Name}[indices[done]] = new CogsDate(" +
                                    "new DateTimeOffset(int.Parse(start[0]), int.Parse(start[1]), int.Parse(start[2]), int.Parse(start[3]), ");
                                initializeReferences.AppendLine("                                int.Parse(parts[i + 2]), " +
                                    "int.Parse(parts[i + 3].Split('+')[0]), ");
                                initializeReferences.AppendLine("                                new TimeSpan(int.Parse(parts[i + 3]" +
                                    ".Split('+')[1]), int.Parse(parts[i + 4].Replace(\"\\\"\", \"\")), 0)));");
                                initializeReferences.AppendLine("                            i += 4;");
                                initializeReferences.AppendLine("                            done++;");
                                initializeReferences.AppendLine("                        }");
                                initializeReferences.AppendLine("                        i++;");
                                initializeReferences.AppendLine("                    }");
                                initializeReferences.AppendLine("                    reusablesInitialized++;");
                                initializeReferences.AppendLine("                }");
                                reusablesInitialized++;
                            }
                            newClass.AppendLine("        [JsonConverter(typeof(SimpleTypeConverter))]");
                            reusableToJson.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Count > 0)");
                            reusableToJson.AppendLine("            {");
                            reusableToJson.AppendLine($"                var prop = new JProperty(\"{prop.Name}\", new JArray());");
                            reusableToJson.AppendLine($"                foreach (var item in {prop.Name})");
                            reusableToJson.Append("                {");
                            reusableToJson.AppendLine($"                    {SimpleToJson(origDataTypeName, prop.Name, "", true)}");
                            reusableToJson.AppendLine("                }");
                            reusableToJson.AppendLine($"                {start}prop);");
                            reusableToJson.AppendLine("            }");
                        }
                        else if (model.ReusableDataTypes.Contains(prop.DataType))
                        {
                            reusableToJson.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Count > 0)");
                            reusableToJson.AppendLine("            {");
                            reusableToJson.AppendLine($"                {start}new JProperty(\"{prop.Name}\", new JArray(");
                            reusableToJson.AppendLine($"                    from item in {prop.Name}");
                            reusableToJson.AppendLine($"                    select new JObject(new JProperty(\"{prop.DataTypeName}\", " +
                                $"item.ToJson())))));");
                            reusableToJson.AppendLine("            }");
                            if (model.ItemTypes.Contains(item)) { initializeReferences.Append(InitializeReusable(prop, model, helpers)); }
                        }
                        else if (!model.ItemTypes.Contains(prop.DataType))
                        {
                            reusableToJson.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Count > 0)");
                            reusableToJson.AppendLine("            {");
                            reusableToJson.AppendLine($"                {start}new JProperty(\"{prop.Name}\", new JArray(");
                            reusableToJson.AppendLine($"                    from item in {prop.Name}");
                            reusableToJson.AppendLine("                    select item)));");
                            reusableToJson.AppendLine("            }");
                        }
                        else
                        {
                            if (model.ReusableDataTypes.Contains(item))
                            {
                                reusableToJson.AppendLine($"            if ({prop.Name} != null)");
                                reusableToJson.AppendLine("            {");
                                reusableToJson.AppendLine($"                {start}new JProperty(\"{prop.Name}\", new JArray(");
                                reusableToJson.AppendLine($"                    from item in {prop.Name}");
                                reusableToJson.AppendLine($"                    select new JObject(new JProperty(\"{prop.DataTypeName}\", ");
                                reusableToJson.AppendLine("                        new JObject(new JProperty(\"ID\", item.ID)))))));");
                                reusableToJson.AppendLine("            }");
                            }
                            else
                            {
                                reusableToJson.AppendLine($"            if ({prop.Name} != null)");
                                reusableToJson.AppendLine("            {");
                                reusableToJson.AppendLine($"                {start}new JProperty(\"{prop.Name}\", new JArray(");
                                reusableToJson.AppendLine($"                    from item in {prop.Name}");
                                reusableToJson.AppendLine("                    select new JObject(new JProperty(\"@type\", \"ref\"), ");
                                reusableToJson.AppendLine("                        new JProperty(\"value\", new JArray(" +
                                    "item.GetType().Name.ToString(), item.ID))))));");
                                reusableToJson.AppendLine("            }");
                                initializeReusables.AppendLine($"            if ({prop.Name} != null)");
                                initializeReusables.AppendLine("            {");
                                initializeReusables.AppendLine($"                for (int j = 0; j < {prop.Name}.Count; j++)");
                                initializeReusables.AppendLine("                {");
                                initializeReusables.AppendLine($"                    dynamic temp = dict[{prop.Name}[j].ReferenceId];");
                                initializeReusables.AppendLine($"                    {prop.Name}[j] = temp;");
                                initializeReusables.AppendLine("                }");
                                initializeReusables.AppendLine("            }");
                            }
                        }
                        newClass.AppendLine($"        public List<{prop.DataTypeName}> {prop.Name} {{ get; set; }} = " +
                            $"new List<{prop.DataTypeName}>();");
                    }
                }
                string returnType = "JProperty";
                if (model.ItemTypes.Contains(item))
                {
                    newClass.AppendLine("        /// <summary>");
                    newClass.AppendLine("        /// Used to Serialize this object to Json");
                    newClass.AppendLine("        /// <summary>");
                    if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                    {
                        newClass.AppendLine($"        public override {returnType} ToJson()");
                        newClass.AppendLine("        {");
                        newClass.AppendLine("            JProperty json = base.ToJson();");
                        newClass.Append("            ((JObject)json.First).Add(");
                    }
                    else
                    {
                        newClass.AppendLine($"        public virtual {returnType} ToJson()");
                        newClass.AppendLine("        {");
                        newClass.Append($"            JProperty json = new JProperty(ID, new JObject(");
                    }
                    newClass.Append(toJsonProperties.ToString());
                    if (string.IsNullOrWhiteSpace(item.ExtendsTypeName)) { newClass.Append(")"); }
                    newClass.Append(");" + reusableToJson.ToString());
                    newClass.AppendLine($"            return json;");
                    newClass.AppendLine("        }");
                    newClass.AppendLine();
                    newClass.AppendLine("        /// <summary>");
                    newClass.AppendLine("        /// Used to set this object's properties from Json");
                    newClass.AppendLine("        /// <summary>");
                    if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                    {
                        newClass.AppendLine("        public override void InitializeReferences(Dictionary<string, " +
                            "IIdentifiable> dict, string json)");
                        newClass.AppendLine("        {");
                        newClass.AppendLine("            base.InitializeReferences(dict, json);");
                    }
                    else
                    {
                        newClass.AppendLine($"        public virtual void InitializeReferences(Dictionary<string, " +
                            $"IIdentifiable> dict, string json)");
                        newClass.AppendLine("        {");
                    }
                    if (initializeReferences.ToString().Contains("parts[i"))
                    {
                        newClass.AppendLine("            string[] parts = json.Split(new string[] { \":\", \",\", Environment.NewLine }, " +
                            "StringSplitOptions.None);");
                        if (initializeReferences.ToString().Contains("thisObj")) { newClass.AppendLine("            bool thisObj = false;"); }
                        newClass.AppendLine("            int reusablesInitialized = 0;");
                        newClass.AppendLine("            for (int i = 0; i < parts.Length; i ++)");
                        newClass.AppendLine("            {");
                        newClass.AppendLine("                var line = parts[i].Trim().Replace(\"\\\"\", \"\");");
                        newClass.AppendLine($"                if (reusablesInitialized == {reusablesInitialized}) {{ return; }}");
                        if (initializeReferences.ToString().Contains("thisObj"))
                        {
                            newClass.AppendLine("                else if (line.Equals(ID)) { thisObj = true; }");
                        }
                        newClass.AppendLine(initializeReferences.ToString());
                        newClass.AppendLine("            }");
                        newClass.Append(initializeReusables.ToString());
                        newClass.AppendLine("        }");
                        newClass.Append(helpers.ToString());
                    }
                    else
                    {
                        newClass.AppendLine(initializeReferences.ToString());
                        newClass.AppendLine("        }");
                    }
                    newClass.AppendLine("    }");
                    newClass.AppendLine("}");
                    newClass.AppendLine();
                }
                else
                {
                    returnType = "JObject";
                    if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                    {
                        newClass.AppendLine($"        public override {returnType} ToJson()");
                        newClass.AppendLine("        {");
                        newClass.AppendLine("            JObject json = base.ToJson();");
                        newClass.AppendLine("            ((JObject)json.First).Add(");
                    }
                    else
                    {
                        newClass.AppendLine($"        public virtual {returnType} ToJson()");
                        newClass.AppendLine("        {");
                        newClass.AppendLine("            JObject json = new JObject() {");
                    }
                    newClass.Append(toJsonProperties.ToString());
                    newClass.Append($"}};{reusableToJson.ToString()}");
                    newClass.AppendLine("            return json;");
                    newClass.AppendLine("        }");
                    newClass.AppendLine("    }");
                    newClass.AppendLine("}");
                    newClass.AppendLine();
                }
                // write class to out folder
                File.WriteAllText(Path.Combine(TargetDirectory, item.Name + ".cs"), newClass.ToString()
                    .Replace("            ((JObject)json.First).Add();", ""));
            }
        }


        private bool Isboolintdoubleulong(string name)
        {
            if (name.Equals("bool") || name.Equals("int") || name.Equals("double") || name.Equals("ulong") || name.Equals("long"))
            {
                return true;
            }
            return false;
        }


        private string InitializeReusable(Property prop, CogsModel model, StringBuilder main = null)
        {
            var name = prop.Name;
            var type = prop.DataTypeName;
            StringBuilder builder = new StringBuilder($@"                else if (line.Equals(""{name}"") && thisObj)
                {{
                    ");
            if (main != null)
            {
                builder.Append($@"{name} = new List<{type}>();
                    Initialize{name}({name}, parts, i);
                }}");
                InitializeReusableList(prop, model, main, name, type);
            }
            else
            {
                builder.Append($@"{name} = new {type}();
                    int counter = 1;
                    i++;
                    while (i < parts.Length && counter > 0)
                    {{
                        line = parts[i].Trim().Replace(""\"""", """");
                        if (line.Equals(""{{"")) {{ counter++; }}
                        else if (line.Equals(""}}"")) {{ counter--; }}");
                foreach (var p in prop.DataType.Properties)
                {
                    builder.AppendLine();
                    if (p.DataTypeName.ToLower().Equals("cogsdate"))
                    {
                        builder.Append($"                        else if (line.Equals(\"{p.Name}\"))");
                    }
                    else { builder.Append($"                        else if (line.Equals(\"{p.Name}\"))"); }
                    if (!p.MaxCardinality.Equals("1"))
                    {
                        builder.Append($@"
                        {{
                            {name}.{p.Name} = new List<{p.DataTypeName}>();
                            i += 2;
                            int array = 1;
                            while (i < parts.Length && counter > 0 && array > 0)
                            {{
                                line = parts[i].Trim().Replace(""\"""", """");
                                if (line.Equals(""{{"")) {{ counter++; }}
                                else if (line.Equals(""}}"")) {{ counter--; }}
                                else if (line.Equals(""["")) {{ array++; }}
                                else if (line.Equals(""]"")) {{ array--; }}
                                else if (!string.IsNullOrWhiteSpace(line)) { InitializeObject(p, model, true, name, ".Add(")}
                                i++;
                            }}
                        }}");
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
            main.AppendLine($@"        private int Initialize{name}(List<{type}> list, string[] parts, int i)
        {{
            {type} obj = null;
            bool open = true;
            i += 3;
            while (i < parts.Length && open)
            {{
                var line = parts[i].Trim().Replace(""\"""", """");
                if (line.Equals(""]"")) {{ open = false; }}
                else if (line.Equals(""{type}""))
                {{
                    if (obj != null) {{ list.Add(obj); }}
                    obj = new {type}();
                }}");
            foreach (var p in prop.DataType.Properties)
            {
                if (p.DataTypeName.Equals("cogsDate")) { main.AppendLine("                else if (line.Equals(\"cogsDate\"))"); }
                else { main.Append($"                else if (line.Equals(\"{p.Name}\"))"); }
                if (!p.MaxCardinality.Equals("1"))
                {
                    if (p.DataTypeName.Equals(type))
                    {
                        main.Append($" {{ i = Initialize{name}(obj.{p.Name}, parts, i); }}");
                    }
                    else if (model.ReusableDataTypes.Contains(prop.DataType))
                    {
                        main.Append($" {{ i = Initialize{p.Name}(obj.{p.Name}, parts, i); }}");
                        InitializeReusableList(p, model, subs, p.Name, p.DataTypeName);
                    }
                    else
                    {
                        main.Append($@"
                        {{
                            i += 2;
                            int array = 1;
                            while (i < parts.Length && counter > 0 && array > 0)
                            {{
                                line = parts[i].Trim().Replace(""\"""", """");
                                if (line.Equals(""{{"")) {{ counter++; }}
                                else if (line.Equals(""}}"")) {{ counter--; }}
                                else if (line.Equals(""["")) {{ array++; }}
                                else if (line.Equals(""]"")) {{ array--; }}
                                else if (!string.IsNullOrWhiteSpace(line)) {InitializeObject(p, model, true, "obj", ".Add(")}
                                i++;
                            }}
                        }}");
                    }
                }
                else { main.Append($" {InitializeObject(p, model, false, "obj", " = ")}"); }
            }
            main.AppendLine("                i++;");
            main.AppendLine("            }");
            main.AppendLine("            if (obj != null) { list.Add(obj); }");
            main.AppendLine("            return i;");
            main.AppendLine($"        }}{subs}");
        }


        private string InitializeObject(Property p, CogsModel model, bool isList, string name, string operation)
        {
            if (p.DataTypeName.Equals("cogsDate"))
            {
                return $@"
                        {{
                            if (parts[i + 2].Trim().Replace(""\"""", """").Equals(""datetime""))
                            {{
                                {name}.{p.Name} = new CogsDate(parts[i + 2].Trim().Replace(""\"""", """"), (parts[i + 3] + "":"" + 
                                    parts[i + 4] + "":"" + parts[i + 5] + "":"" + parts[i + 6] + "":"" + 
                                    parts[i + 7]).Trim().Replace(""\"""", """"));
                            }}
                            else 
                            {{ 
                                {name}.{p.Name} = new CogsDate(parts[i + 2].Trim().Replace(""\"""", """"), 
                                    parts[i + 3].Trim().Replace(""\"""", """")); 
                            }}
                        }}";
            }
            if (operation.Equals(".Add(")) { return $"{{ {name}.{p.Name}{operation}{ReusableTypeConvert(p.DataTypeName, isList, model)}); }}"; }
            return $"{{ {name}.{p.Name}{operation}{ReusableTypeConvert(p.DataTypeName, isList, model)}; }}";
        }


        private string ReusableTypeConvert(string name, bool isList, CogsModel model)
        {
            string i = "i + 1";
            if(isList) { i = "i"; }
            if (name.Equals("int")) { return "int.Parse(parts[" + i + "].Trim().Replace(\"\\\"\", \"\"))"; }
            if (name.Equals("double")) { return "double.Parse(parts[" + i + "].Trim().Replace(\"\\\"\", \"\"))"; }
            if (name.Equals("decimal")) { return "decimal.Parse(parts[" + i + "].Trim().Replace(\"\\\"\", \"\"))"; }
            if (model.ItemTypes.Where(x => x.Name == name).ToList().Count > 0)
            {
                return $"({name})dict[parts[i + 5].Trim().Replace(\"\\\"\", \"\")]";
            }
            return "parts[" + i + "].Trim().Replace(\"\\\"\", \"\")";
        }

        private string SimpleToJson(string origDataTypeName, string name, string start, bool isList)
        { 
            if (origDataTypeName.ToLower().Equals("duration"))
            {
                if (!isList) { return $"{start}new JProperty(\"{name}\", {name}.Ticks)"; }
                return "((JArray)prop.First).Add(item.Ticks);";
            }
            if (origDataTypeName.ToLower().Equals("datetime"))
            {
                if (!isList)
                {
                    return $"{start}new JProperty(\"{name}\", {name}.ToString(\"yyyy-MM-dd\\\\THH:mm:ss.FFFFFFFK\"))";
                }
                return "((JArray)prop.First).Add(item.ToString(\"yyyy-MM-dd\\\\THH:mm:ss.FFFFFFFK\"));";
            }
            if (origDataTypeName.ToLower().Equals("time"))
            {
                if (!isList) { return $"{start}new JProperty(\"{name}\", {name}.ToString(\"u\").Split(' ')[1])"; }
                return "((JArray)prop.First).Add(item.ToString(\"u\").Split(' ')[1]);";
            }
            if (origDataTypeName.ToLower().Equals("date"))
            {
                if (!isList) { return $"{start}new JProperty(\"{name}\", {name}.ToString(\"u\").Split(' ')[0])"; }
                return "((JArray)prop.First).Add(item.ToString(\"u\").Split(' ')[0]);";
            }
            if (origDataTypeName.ToLower().Equals("gyearmonth"))
            {
                if (!isList)
                {
                    return $"var ym = new JProperty(\"{name}\", new JObject({Environment.NewLine}                    new JProperty(\"year\", " +
                        $"{name}.Item1),{Environment.NewLine}                    new JProperty(\"month\", {name}.Item2)));" +
                        $"{Environment.NewLine}                if ({name}.Item3 != null) {{ ((JObject)ym.First).Add(new " +
                        $"JProperty(\"timezone\", {name}.Item3)); }}{Environment.NewLine}                {start}ym";
                }
                return $"{Environment.NewLine}                    if (item.Item3 != null){Environment.NewLine}                    {{" +
                    $"{Environment.NewLine}                        ((JArray)prop.First).Add(new JObject({Environment.NewLine}        " +
                    $"                    new JProperty(\"year\", item.Item1), new JProperty(\"month\", item.Item2), " +
                    $"new JProperty(\"timezone\", item.Item3)));{Environment.NewLine}                    }}{Environment.NewLine}    " +
                    $"                else {{ ((JArray)prop.First).Add(new JObject(new JProperty(\"year\", item.Item1), " +
                    $"new JProperty(\"month\", item.Item2))); }}";
            }
            if (origDataTypeName.ToLower().Equals("gyear"))
            {
                if (!isList)
                {
                    return $"var y = new JProperty(\"{name}\", new JObject({Environment.NewLine}                    new JProperty(\"year\", " +
                        $"{name}.Item1)));{Environment.NewLine}                if ({name}.Item2 != null) {{ ((JObject)y.First).Add(" +
                        $"new JProperty(\"timezone\", {name}.Item2)); }}{Environment.NewLine}                {start}y";
                }
                return $"{Environment.NewLine}                    if (item.Item2 != null){Environment.NewLine}                    {{" +
                    $"{Environment.NewLine}                        ((JArray)prop.First).Add(new JObject({Environment.NewLine}        " +
                    $"                    new JProperty(\"year\", item.Item1), new JProperty(\"timezone\", item.Item2)));{Environment.NewLine}" +
                    $"                    }}{Environment.NewLine}                    else {{ ((JArray)prop.First).Add(new JObject(" +
                    $"new JProperty(\"year\", item.Item1))); }}";
            }
            if (origDataTypeName.ToLower().Equals("gmonthday"))
            {
                if (!isList)
                {
                    return $"var ym = new JProperty(\"{name}\", new JObject({Environment.NewLine}                    new JProperty(\"month\", " +
                        $"{name}.Item1),{Environment.NewLine}                    new JProperty(\"day\", {name}.Item2)));" +
                        $"{Environment.NewLine}                if ({name}.Item3 != null) {{ ((JObject)ym.First).Add(new " +
                        $"JProperty(\"timezone\", {name}.Item3)); }}{Environment.NewLine}                {start}ym";
                }
                return $"{Environment.NewLine}                    if (item.Item3 != null){Environment.NewLine}                    {{" +
                    $"{Environment.NewLine}                        ((JArray)prop.First).Add(new JObject({Environment.NewLine}        " +
                    $"                    new JProperty(\"month\", item.Item1), new JProperty(\"day\", item.Item2), " +
                    $"new JProperty(\"timezone\", item.Item3)));{Environment.NewLine}                    }}{Environment.NewLine}    " +
                    $"                else {{ ((JArray)prop.First).Add(new JObject(new JProperty(\"month\", item.Item1), " +
                    $"new JProperty(\"day\", item.Item2))); }}";
            }
            if (origDataTypeName.ToLower().Equals("gday"))
            {
                if (!isList)
                {
                    return $"var y = new JProperty(\"{name}\", new JObject({Environment.NewLine}                    new JProperty(\"day\", " +
                        $"{name}.Item1)));{Environment.NewLine}                if ({name}.Item2 != null) {{ ((JObject)y.First).Add(" +
                        $"new JProperty(\"timezone\", {name}.Item2)); }}{Environment.NewLine}                {start}y";
                }
                return $"{Environment.NewLine}                    if (item.Item2 != null){Environment.NewLine}                    {{" +
                    $"{Environment.NewLine}                        ((JArray)prop.First).Add(new JObject({Environment.NewLine}        " +
                    $"                    new JProperty(\"day\", item.Item1), new JProperty(\"timezone\", item.Item2)));{Environment.NewLine}" +
                    $"                    }}{Environment.NewLine}                    else {{ ((JArray)prop.First).Add(new JObject(" +
                    $"new JProperty(\"day\", item.Item1))); }}";

            }
            if (origDataTypeName.ToLower().Equals("gmonth"))
            {
                if (!isList)
                {
                    return $"var y = new JProperty(\"{name}\", new JObject({Environment.NewLine}                    new JProperty(\"month\", " +
                        $"{name}.Item1)));{Environment.NewLine}                if ({name}.Item2 != null) {{ ((JObject)y.First).Add(" +
                        $"new JProperty(\"timezone\", {name}.Item2)); }}{Environment.NewLine}                {start}y";
                }
                return $"{Environment.NewLine}                    if (item.Item2 != null){Environment.NewLine}                    {{" +
                    $"{Environment.NewLine}                        ((JArray)prop.First).Add(new JObject({Environment.NewLine}        " +
                    $"                    new JProperty(\"month\", item.Item1), new JProperty(\"timezone\", item.Item2)));{Environment.NewLine}" +
                    $"                    }}{Environment.NewLine}                    else {{ ((JArray)prop.First).Add(new JObject(" +
                    $"new JProperty(\"month\", item.Item1))); }}";
            }
            if (origDataTypeName.ToLower().Equals("anyuri"))
            {
                if (!isList) { return $"{start}new JProperty(\"{name}\", {name})"; }
                return $"{Environment.NewLine}                    ((JArray)prop.First).Add(item);";
            }
            if (origDataTypeName.ToLower().Equals("cogsdate"))
            {
                if (!isList)
                {
                    return $"{start}new JProperty(\"{name}\", new JObject({Environment.NewLine}                    new " +
                        $"JProperty({name}.GetUsedType(), {name}.GetValue())))";
                }
                return $"{Environment.NewLine}                    ((JArray)prop.First).Add(new JObject({Environment.NewLine}            " +
                    $"            new JProperty(item.GetUsedType(), item.GetValue())));";
            }
            if (!isList) { return $"{start}new JProperty(\"{name}\", {name})"; }
            return "{Environment.NewLine}                    ((JArray)prop.First).Add(item);";
        }

// -------------------------------
        // creates a file called IIdentifiable.cs which holds the IIdentifiable interface from which all item types descend
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
                        IIdentifiable obj = null;???
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
                ifs.Append("$######" + start + "if (clss.Equals(\"" + item.Name + "\")) { obj = JsonConvert.DeserializeObject<" + item.Name + 
                    ">(instance.Value.ToString()); }");
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
            else { return Translate(JToken.Load(reader), objectType); }
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
                if (values.Length == 7)
                {
                    return new DateTimeOffset(int.Parse(values[2]), int.Parse(values[0]), int.Parse(values[1]),
                        int.Parse(values[3]), int.Parse(values[4]), int.Parse(values[5]), new TimeSpan());
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
                if (values.Length == 7)
                {
                    return new CogsDate(new DateTimeOffset(int.Parse(values[2]), int.Parse(values[0]), int.Parse(values[1]),
                        int.Parse(values[3]), int.Parse(values[4]), int.Parse(values[5]), new TimeSpan()));
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