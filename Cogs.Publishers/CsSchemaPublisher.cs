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
                newClass.AppendLine();
                newClass.AppendLine("using System.Linq;");
                newClass.AppendLine("using Newtonsoft.Json;");
                newClass.AppendLine("using System.Xml.Linq;");
                newClass.AppendLine("using System.Reflection;");
                newClass.AppendLine("using System.Collections;");
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
                var reusableToJson = new StringBuilder();
                var helpers = new StringBuilder();
                var toXml = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName)) { toXml.AppendLine("        public override XElement ToXml()"); }
                else { toXml.AppendLine("        public virtual XElement ToXml()"); }
                toXml.AppendLine("        {");
                toXml.AppendLine($"            var xEl = new XElement(\"{item.Name}\");");
                bool initialiseReusable = false;
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
                else { newClass.AppendLine($"{Environment.NewLine}    {{"); }
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
                    newClass.AppendLine("        /// <summary>");
                    newClass.AppendLine($"        /// {prop.Description}");
                    newClass.AppendLine("        /// <summary>");
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
                        toJsonProperties.Append(",");
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
                            toXml.AppendLine($"            xEl.Add(new XElement(\"{prop.Name}\", {prop.Name}));");
                            first = false;
                        }
                        else if (origDataTypeName != null)
                        {
                            if (!prop.DataTypeName.Equals("string"))
                            {
                                newClass.AppendLine("        [JsonConverter(typeof(SimpleTypeConverter))]");
                            }
                            if (prop.DataTypeName.Equals("CogsDate"))
                            {
                                reusableToJson.AppendLine($"            if ({prop.Name}.GetValue() != null)");
                                toXml.AppendLine($"            if ({prop.Name}.GetValue() != null)");
                            }
                            else if (prop.DataTypeName.Equals("DateTimeOffset") || prop.DataTypeName.Equals("TimeSpan"))
                            {
                                reusableToJson.AppendLine($"            if ({prop.Name} != default({prop.DataTypeName}))");
                                toXml.AppendLine($"            if ({prop.Name} != default({prop.DataTypeName}))");
                            }
                            else
                            {
                                reusableToJson.AppendLine($"            if ({prop.Name} != null)");
                                toXml.AppendLine($"            if ({prop.Name} != null)");
                            }
                            reusableToJson.AppendLine("            {");
                            reusableToJson.AppendLine($"                {SimpleToJson(origDataTypeName, prop.Name, start, false)});");
                            reusableToJson.AppendLine("            }");
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                {SimpleToXml(origDataTypeName, prop.Name, "xEl", false)}");
                            toXml.AppendLine("            }");
                        }
                        else if (model.ReusableDataTypes.Contains(prop.DataType))
                        {
                            initialiseReusable = true;
                            reusableToJson.AppendLine($"            if ({prop.Name} != null) {{ {start}new JProperty(\"{prop.Name}\", {prop.Name}.ToJson())); }}");
                            toXml.AppendLine($"            if ({prop.Name} != null) {{ xEl.Add({prop.Name}.ToXml()); }}");
                            newClass.AppendLine("        [JsonConverter(typeof(ReusableConverter))]");
                        }
                        else if (!model.ItemTypes.Contains(prop.DataType))
                        {
                            reusableToJson.AppendLine($"            if ({prop.Name} != null)");
                            reusableToJson.AppendLine("            {");
                            reusableToJson.AppendLine($"                {start}new JProperty(\"{prop.Name}\", {prop.Name}));");
                            reusableToJson.AppendLine("            }");
                            toXml.AppendLine($"            if ({prop.Name} != null)");
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                xEl.Add(new XElement(\"{prop.Name}\", {prop.Name}));");
                            toXml.AppendLine("            }");
                        }
                        else
                        {
                            reusableToJson.AppendLine($"            if ({prop.Name} != null)");
                            reusableToJson.AppendLine("            {");
                            if (model.ReusableDataTypes.Contains(item))
                            {
                                reusableToJson.AppendLine($"                {start}new JProperty(\"{prop.Name}\", new JObject(");
                                reusableToJson.AppendLine($"                    new JProperty(\"{prop.DataTypeName}\", new JObject(");
                                reusableToJson.AppendLine($"                        new JProperty(\"ID\", {prop.Name}.ID))))));");
                                reusableToJson.AppendLine("            }");
                            }
                            else
                            {
                                initialiseReusable = true;
                                reusableToJson.AppendLine($"                {start}new JProperty(\"{prop.Name}\", new JObject(" +
                                    $"new JProperty(\"@type\", \"ref\"), ");
                                reusableToJson.AppendLine($"                    new JProperty(\"value\", new JArray(\"{prop.DataTypeName}\",");
                                reusableToJson.AppendLine($"                        {prop.Name}.ID)))));");
                                reusableToJson.AppendLine("            }");
                                initializeReferences.AppendLine($"            if ({prop.Name} != null) {{ {prop.Name} = ({prop.DataTypeName})" +
                                    $"dict[{prop.Name}.ReferenceId]; }}");
                            }
                            toXml.AppendLine($"            if ({prop.Name} != null)");
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                xEl.Add(new XElement(\"{prop.Name}\", ");
                            foreach (var part in model.Identification)
                            {
                                toXml.AppendLine($"                    new XElement(\"{part.Name}\", {prop.Name}.{part.Name}), ");
                            }
                            toXml.AppendLine($"                    new XElement(\"ItemType\", {prop.Name}.GetType().Name)));");
                            toXml.AppendLine("            }");
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
                            toXml.AppendLine($"            xEl.Add(");
                            toXml.AppendLine($"                from item in {prop.Name}");
                            toXml.AppendLine($"                select new XElement(\"{prop.Name}\", item));");
                            first = false;
                        }
                        else if (origDataTypeName != null)
                        {
                            if (!prop.DataTypeName.Equals("string"))
                            {
                                newClass.AppendLine("        [JsonConverter(typeof(SimpleTypeConverter))]");
                            }
                            reusableToJson.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Count > 0)");
                            reusableToJson.AppendLine("            {");
                            reusableToJson.AppendLine($"                var prop = new JProperty(\"{prop.Name}\", new JArray());");
                            reusableToJson.AppendLine($"                foreach (var item in {prop.Name})");
                            reusableToJson.AppendLine("                {");
                            reusableToJson.AppendLine($"                    {SimpleToJson(origDataTypeName, prop.Name, "", true)}");
                            reusableToJson.AppendLine("                }");
                            reusableToJson.AppendLine($"                {start}prop);");
                            reusableToJson.AppendLine("            }");
                            toXml.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Count > 0)");
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                foreach (var item in {prop.Name})");
                            toXml.AppendLine("                {");
                            toXml.AppendLine($"                    var prop = new XElement(\"{prop.Name}\");");
                            toXml.AppendLine($"                    {SimpleToXml(origDataTypeName, "item", "prop", true)}");
                            toXml.AppendLine("                    xEl.Add(prop);");
                            toXml.AppendLine("                }");
                            toXml.AppendLine("            }");

                        }
                        else if (model.ReusableDataTypes.Contains(prop.DataType))
                        {
                            initialiseReusable = true;
                            newClass.AppendLine("        [JsonConverter(typeof(ReusableConverter))]");
                            reusableToJson.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Count > 0)");
                            reusableToJson.AppendLine("            {");
                            reusableToJson.AppendLine($"                {start}new JProperty(\"{prop.Name}\", new JArray(");
                            reusableToJson.AppendLine($"                    from item in {prop.Name}");
                            reusableToJson.AppendLine($"                    select new JObject(new JProperty(\"{prop.DataTypeName}\", item.ToJson())))));");
                            reusableToJson.AppendLine("            }");
                            toXml.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Count > 0)");
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                foreach (var item in {prop.Name})");
                            toXml.AppendLine("                {");
                            toXml.AppendLine($"                    xEl.Add(item.ToXml());");
                            toXml.AppendLine("                }");
                            toXml.AppendLine("            }");
                        }
                        else if (!model.ItemTypes.Contains(prop.DataType))
                        {
                            reusableToJson.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Count > 0)");
                            reusableToJson.AppendLine("            {");
                            reusableToJson.AppendLine($"                {start}new JProperty(\"{prop.Name}\", new JArray(");
                            reusableToJson.AppendLine($"                    from item in {prop.Name}");
                            reusableToJson.AppendLine("                    select item)));");
                            reusableToJson.AppendLine("            }");
                            toXml.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Count > 0)");
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                xEl.Add(");
                            toXml.AppendLine($"                    from item in {prop.Name}");
                            toXml.AppendLine($"                    select new XElement(\"{prop.Name}\", item.ToString()));");
                            toXml.AppendLine("            }");
                        }
                        else
                        {
                            reusableToJson.AppendLine($"            if ({prop.Name} != null)");
                            reusableToJson.AppendLine("            {");
                            reusableToJson.AppendLine($"                {start}new JProperty(\"{prop.Name}\", new JArray(");
                            reusableToJson.AppendLine($"                    from item in {prop.Name}");
                            if (model.ReusableDataTypes.Contains(item))
                            {
                                reusableToJson.AppendLine($"                    select new JObject(new JProperty(\"{prop.DataTypeName}\", ");
                                reusableToJson.AppendLine("                        new JObject(new JProperty(\"ID\", item.ID)))))));");
                                reusableToJson.AppendLine("            }");
                            }
                            else
                            {
                                initialiseReusable = true;
                                reusableToJson.AppendLine("                    select new JObject(new JProperty(\"@type\", \"ref\"), ");
                                reusableToJson.AppendLine("                        new JProperty(\"value\", new JArray(" +
                                    "item.GetType().Name.ToString(), item.ID))))));");
                                reusableToJson.AppendLine("            }");
                                initializeReferences.AppendLine($"            if ({prop.Name} != null)");
                                initializeReferences.AppendLine("            {");
                                initializeReferences.AppendLine($"                for (int j = 0; j < {prop.Name}.Count; j++)");
                                initializeReferences.AppendLine("                {");
                                initializeReferences.AppendLine($"                    dynamic temp = dict[{prop.Name}[j].ReferenceId];");
                                initializeReferences.AppendLine($"                    {prop.Name}[j] = temp;");
                                initializeReferences.AppendLine("                }");
                                initializeReferences.AppendLine("            }");
                            }
                            toXml.AppendLine($"            if ({prop.Name} != null)");
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                var prop = new XElement(\"{prop.Name}\");");
                            toXml.AppendLine($"                foreach (var item in {prop.Name})");
                            toXml.AppendLine("                {");
                            toXml.AppendLine($"                    prop.Add(new XElement(item.ToString(), ");
                            foreach (var part in model.Identification)
                            {
                                toXml.AppendLine($"                        new XElement(\"{part.Name}\", item.{part.Name}), ");
                            }
                            toXml.AppendLine($"                        new XElement(\"ItemType\", \"item.GetType()\")));");
                            toXml.AppendLine("                }");
                            toXml.AppendLine("            }");
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
                    newClass.AppendLine(");");
                    newClass.AppendLine(reusableToJson.ToString());
                    newClass.AppendLine($"            return json;");
                    newClass.AppendLine("        }");
                    newClass.AppendLine();
                    newClass.AppendLine("        /// <summary>");
                    newClass.AppendLine("        /// Used to set this object's properties from Json");
                    newClass.AppendLine("        /// <summary>");
                    if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                    {
                        newClass.AppendLine("        public override void InitializeReferences(Dictionary<string, " +
                            "IIdentifiable> dict)");
                        newClass.AppendLine("        {");
                        newClass.AppendLine("            base.InitializeReferences(dict);");
                    }
                    else
                    {
                        newClass.AppendLine($"        public virtual void InitializeReferences(Dictionary<string, " +
                            $"IIdentifiable> dict)");
                        newClass.AppendLine("        {");
                    }
                    if (initialiseReusable)
                    {
                        newClass.AppendLine(@"            Stack<PropertyInfo> properties = new Stack<PropertyInfo>();
            Dictionary<PropertyInfo, string> seen = new Dictionary<PropertyInfo, string>();
            foreach (var property in GetType().GetProperties().ToList())
            {
                properties.Push(property);
                seen.Add(property, property.Name);
            }
            while (properties.Count > 0)
            {
                var property = properties.Pop();
                if (property.PropertyType.GetInterfaces().Contains(typeof(IIdentifiable)))
                {
                    object super = this;
                    object actual = this;
                    string propName = null;
                    bool isNull = false;
                    foreach (var item in seen[property].Split('.'))
                    {
                        object next = null;
                        if (typeof(IEnumerable).IsAssignableFrom(actual.GetType()))
                        {
                            if (((IList)actual).Count > 0)
                            {
                                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(actual.GetType().GetGenericArguments()[0]));
                                foreach (var prop in ((IList)actual))
                                {
                                    var type = prop.GetType();
                                    if (type.Namespace.Equals(GetType().Namespace) && !type.IsPrimitive && type != typeof(string) && type != typeof(decimal) && 
                                         !seen.Keys.Contains(prop))
                                    {
                                        list.Add(dict[((IIdentifiable)prop).ReferenceId]);
                                    }
                                }
                                super.GetType().GetProperty(propName).SetValue(super, list);
                            }
                        }
                        else { next = actual.GetType().GetProperty(item).GetValue(actual); }
                        if (next == null)
                        {
                            isNull = true;
                            break;
                        }
                        super = actual;
                        actual = next;
                        propName = item;
                    }
                    if (propName != null && !isNull && ((IIdentifiable)actual).ReferenceId != null)
                    {
                        super.GetType().GetProperty(propName).SetValue(super, dict[((IIdentifiable)actual).ReferenceId]);
                    }
                }
                else
                {
                    foreach (var prop in property.PropertyType.GetProperties())
                    {
                        var type = prop.GetType();
                        if ((!type.IsPrimitive && type != typeof(string) && type != typeof(decimal)) && !seen.Keys.Contains(prop))
                        {
                            properties.Push(prop);
                            seen.Add(prop, seen[property] + ""."" + prop.Name);
                        }
                    }
                }
            }
        }");
                    }
                    else { newClass.AppendLine("        }"); }
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
                }
                newClass.AppendLine();
                newClass.AppendLine("        /// <summary>");
                newClass.AppendLine("        /// Used to Serialize this object to XML");
                newClass.AppendLine("        /// <summary>");
                newClass.Append(toXml.ToString());
                newClass.AppendLine("            return xEl;");
                newClass.AppendLine("        }");
                newClass.AppendLine("    }");
                newClass.AppendLine("}");
                newClass.AppendLine();
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

        private object SimpleToXml(string origDataTypeName, string name, string start, bool isList)
        {
            string indent = Environment.NewLine + "                ";
            if (isList) { indent += "    "; }
            if (origDataTypeName.ToLower().Equals("duration")){ return $"{start}.Add(new XElement(\"{name}\", {name}.Ticks));"; }
            if (origDataTypeName.ToLower().Equals("datetime")) { return $"{start}.Add(new XElement(\"{name}\", {name}.ToString(\"yyyy-MM-dd\\\\THH:mm:ss.FFFFFFFK\")));"; }
            if (origDataTypeName.ToLower().Equals("time")) { return $"{start}.Add(new XElement(\"{name}\", {name}.ToString(\"u\").Split(' ')[1]));"; }
            if (origDataTypeName.ToLower().Equals("date")){ return $"{start}.Add(new XElement(\"{name}\", {name}.ToString(\"u\").Split(' ')[0]));"; }
            if (origDataTypeName.ToLower().Equals("gyearmonth"))
            {
                return $"var ym = new XElement(\"yearmonth\");{indent}if ({name}.Item3 != null) {{ ym.Add(new XElement(" +
                    $"\"year\", {name}.Item1), new XElement(\"month\", {name}.Item2), new XElement(\"timezone\", {name}.Item3)); }}" +
                    $"{indent}else {{ ym.Add(new XElement(\"year\", {name}.Item1), new XElement(\"month\", {name}.Item2)); }}{indent}{start}.Add(ym);";
            }
            if (origDataTypeName.ToLower().Equals("gyear"))
            {
                return $"if ({name}.Item2 != null) {{ {start}.Add(new XElement(\"year\", {name}.Item1), new XElement(\"timezone\", {name}.Item2)); }}" +
                    $"{indent}else {{ {start}.Add(new XElement(\"year\", {name}.Item1)); }}";
            }
            if (origDataTypeName.ToLower().Equals("gmonthday"))
            {
                return $"var md = new XElement(\"monthday\");{indent}if ({name}.Item3 != null) {{ md.Add(new XElement(" +
                    $"\"month\", {name}.Item1), new XElement(\"day\", {name}.Item2), new XElement(\"timezone\", {name}.Item3)); }}" +
                    $"{indent}else {{ md.Add(new XElement(\"month\", {name}.Item1), new XElement(\"day\", {name}.Item2)); }}{indent}{start}.Add(md);";
            }
            if (origDataTypeName.ToLower().Equals("gday"))
            {
                return $"if ({name}.Item2 != null) {{ {start}.Add(new XElement(\"day\", {name}.Item1), new XElement(\"timezone\", {name}.Item2)); }}" +
                    $"{indent}else {{ {start}.Add(new XElement(\"day\", {name}.Item1)); }}";
            }
            if (origDataTypeName.ToLower().Equals("gmonth"))
            {
                return $"if ({name}.Item2 != null) {{ {start}.Add(new XElement(\"month\", {name}.Item1), new XElement(\"timezone\", {name}.Item2)); }}" +
                    $"{indent}else {{ {start}.Add(new XElement(\"month\", {name}.Item1)); }}";
            }
            if (origDataTypeName.ToLower().Equals("cogsdate"))
            {
                return $"{start}.Add(new XElement({name}.GetUsedType(), {name}.GetValue()));";
            }
            return $"{start}.Add(new XElement(\"{name}\", {name}));";
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


        // creates a file called IIdentifiable.cs which holds the IIdentifiable interface from which all item types descend
        private void CreateIIdentifiable(CogsModel model, string projName)
        {
            StringBuilder builder = new StringBuilder("using System;");
            builder.AppendLine();
            builder.AppendLine("using System.Xml.Linq;");
            builder.AppendLine("using Newtonsoft.Json.Linq;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine();
            builder.AppendLine($"namespace {projName}");
            builder.AppendLine("{");
            builder.AppendLine("    /// <summary>");
            builder.AppendLine("    /// IIdentifiable class which all object Inherit from. Used to Serialize to Json");
            builder.AppendLine("    /// <summary>");
            builder.AppendLine("    public interface IIdentifiable");
            builder.AppendLine("    {");
            foreach (var prop in model.Identification)
            {
                builder.AppendLine($"        {prop.DataTypeName} {prop.Name} {{ get; set; }}");
            }
            builder.AppendLine("        JProperty ToJson();");
            builder.AppendLine("        string ReferenceId { get; set; }");
            builder.AppendLine("        void InitializeReferences(Dictionary<string, IIdentifiable> dict);");
            builder.AppendLine("        XElement ToXml();");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            File.WriteAllText(Path.Combine(TargetDirectory, "IIdentifiable.cs"), builder.ToString());
        }


        // Creates the ItemContainer Class
        private void CreateItemContainer(CogsModel model, string projName)
        {
            string clss = @"using System;
using System.Linq;
using Newtonsoft.Json;
using System.Xml.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace !!!
{
    /// <summary>
    /// Class that contains a list of all items in the model 
    /// <summary>
    [JsonConverter(typeof(ItemContainerConverter))]
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


        public void Parse(JsonReader json)
        {
            json.DateParseHandling = DateParseHandling.None;
            List<string> ids = new List<string>();
            JObject builder = JObject.Load(json);
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
                obj.InitializeReferences(dict);
            }
        }

        public XDocument MakeXml()
        {
            XDocument xDoc = new XDocument(new XElement(GetType().Namespace));
            if (TopLevelReferences != null && TopLevelReferences.Count > 0)
            {
                XElement tops = new XElement(""TopLevelReferences"");
                foreach (var item in TopLevelReferences)
                {
                    tops.Add(new XElement(item.GetType().ToString(), item.ID));
                }
                xDoc.Root.Add(tops);
            }
            foreach (var item in Items)
            {
                xDoc.Root.Add(item.ToXml());
            }
            return xDoc;
        }
    }
}";
            StringBuilder ifs = new StringBuilder();
            string start = "";
            foreach(var item in model.ItemTypes)
            {
                ifs.AppendLine($"                        {start}if (clss.Equals(\"{item.Name}\")) {{ obj = " +
                    $"JsonConvert.DeserializeObject<{item.Name}>(instance.Value.ToString()); }}");
                start = "else ";
            }
            File.WriteAllText(Path.Combine(TargetDirectory, "ItemContainer.cs"), clss.Replace("!!!", projName).Replace("???", ifs.ToString()));
        }


        public void CreateJsonConverter(CogsModel model, string projName)
        {
            string clss = @"using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Collections;
using Newtonsoft.Json.Linq;
using Cogs.DataAnnotations;
using System.Globalization;
using System.Collections.Generic;

namespace !!!
{
    class ItemContainerConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ItemContainer);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JsonReader reader = new JsonTextReader(new StringReader(((ItemContainer)value).Serialize()))
            {
                DateParseHandling = DateParseHandling.None
            };
            JObject.Load(reader).WriteTo(writer);
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            ItemContainer container = new ItemContainer();
            container.Parse(reader);
            return container;
        }
    }

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
            var t = objectType.GetGenericArguments()[0].Name;

???
            return new InvalidOperationException();
        }

        private void MakeObject(IIdentifiable obj, JsonReader reader)
        {
            reader.Read();
            obj.ReferenceId = reader.Value.ToString();
            reader.Read();
        }
    }

    class ReusableConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return !(objectType is IIdentifiable);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => base.CanRead;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (typeof(IEnumerable).IsAssignableFrom(objectType) && objectType != typeof(string))
            {
                JArray json = JArray.Load(reader);
                IList values = (IList)Activator.CreateInstance(objectType);
                foreach (JObject child in json.Children())
                {
                    values.Add(Evaluate((JObject)child.First.First, objectType.GetGenericArguments()[0]));
                }
                return values;
            }
            else
            {
                return Evaluate(JObject.Load(reader), objectType);
            }
        }

        private object Evaluate (JObject json, Type objectType)
        {
            object obj = Activator.CreateInstance(objectType);
            foreach (JProperty child in json.Children())
            {
                var prop = objectType.GetProperties().Where(x => x.Name == child.Name).ToList()[0];
                if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
                {
                    IList values = (IList)Activator.CreateInstance(prop.PropertyType);
                    foreach (var subChild in child.First)
                    {
                        values.Add(GetObject(prop.PropertyType.GetGenericArguments()[0], subChild));
                    }
                    objectType.GetProperty(prop.Name).SetValue(obj, values);
                }
                else { objectType.GetProperty(prop.Name).SetValue(obj, GetObject(prop.PropertyType, child.First)); }
            }
            return obj;
        }

        private object GetObject(Type type, JToken child)
        {
            if (type.GetInterfaces().Contains(typeof(IIdentifiable)))
            {
                var item = Activator.CreateInstance(type);
                ((IIdentifiable)item).ReferenceId = child.First.First.First.First.ToString();
                return item;
            }
            else if (child.Type == JTokenType.Object && type != typeof(CogsDate))
            {
                return Evaluate((JObject)child.First.First, type);
            }
            else
            {
                if (type == typeof(int)) { return int.Parse(child.ToString()); }
                else if (type == typeof(double)) { return double.Parse(child.ToString()); }
                else if (type == typeof(decimal)) { return decimal.Parse(child.ToString()); }
                else if (type == typeof(bool)) { return bool.Parse(child.ToString()); }
                else if (type == typeof(long)) { return long.Parse(child.ToString()); }
                else if (type == typeof(string)) { return child.ToString(); }
                else
                {
                    var item = SimpleTypeTranslator.Translate(child, type);
                    if (item == null) { item = Activator.CreateInstance(type, child.ToString()); }
                    return item;
                }
            }
        }
    }

    static class SimpleTypeTranslator
    {
        public static object Translate(JToken prop, Type objectType)
        {
            if (objectType == typeof(TimeSpan)) { return TimeSpan.FromTicks(int.Parse(prop.ToString())); }
            if (objectType == typeof(DateTimeOffset))
            {
                string[] types = new string[] { ""yyyy-MM-dd\\THH:mm:ss.FFFFFFFK"", ""yyyy-MM-dd"", ""HH:mm:ss.FFFFFFFK"" };
                foreach (var type in types)
                {
                    if (DateTimeOffset.TryParseExact(prop.ToString(), type, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset dto))
                    { return dto; }
                }
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
                if (int.TryParse(obj.First.First.ToString(), out int t)) { return new CogsDate(TimeSpan.FromTicks(t)); }
                if (DateTimeOffset.TryParseExact(obj.First.First.ToString(), ""yyyy-MM-dd"", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out DateTimeOffset dto))
                {
                    return new CogsDate(dto, true);
                }
                if (DateTimeOffset.TryParseExact(obj.First.First.ToString(), ""yyyy-MM-dd\\THH:mm:ss.FFFFFFFK"",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out dto))
                {
                    return new CogsDate(dto);
                }
                if (int.TryParse(obj.First.First.First.First.ToString(), out int year))
                {
                    if (obj.First.First.First.Next == null) { return new CogsDate(new Tuple<int, string>(year, null)); }
                    if (int.TryParse(obj.First.First.First.Next.First.ToString(), out int month))
                    {
                        if (obj.First.First.First.Next.Next != null)
                        {
                            return new CogsDate(new Tuple<int, int, string>(year, month, obj.First.First.First.Next.Next.First.ToString()));
                        }
                        return new CogsDate(new Tuple<int, int, string>(year, month, null));
                    }
                    return new CogsDate(new Tuple<int, string>(year, obj.First.First.First.Next.First.ToString()));
                }
            }
            return null;
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
                    values.Add(SimpleTypeTranslator.Translate(current, objectType.GetGenericArguments()[0]));
                    current = current.Next;
                }
                return values;
            }
            else { return SimpleTypeTranslator.Translate(JToken.Load(reader), objectType); }
        }
    }
}";
            StringBuilder ifs = new StringBuilder();
            foreach (var item in model.ItemTypes)
            {
                ifs.AppendLine($"            if (t.Equals(\"{item.Name}\")) {{ return list.Cast<{item.Name}>().ToList(); }}");
            }
            File.WriteAllText(Path.Combine(TargetDirectory, "JsonConverter.cs"), clss.Replace("!!!", projName).Replace("???", ifs.ToString()));
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