// Copyright (c) 2017 Colectica. All rights reservedbstr
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
using Cogs.Common;
using Cogs.Publishers.JsonSchema;

namespace Cogs.Publishers.Csharp
{
    public class CsSchemaPublisher
    {
        /// <summary>
        /// path to write output in
        /// </summary>
        public string TargetDirectory { get; set; }

        /// <summary>
        /// Desired namespace for xml serialization
        /// </summary>
        public string TargetNamespace { get; set; }

        /// <summary>
        /// Desired namespace prefix for xml serialization
        /// </summary>
        public string TargetNamespacePrefix { get; set; }

        /// <summary>
        /// boolean to determine whether to replace existing or not
        /// </summary>
        public bool Overwrite { get; set; }
        
        /// <summary>
        /// Determines whether a .csproj file should be written
        /// </summary>
        public bool WriteCsproj { get; set; }
        
        /// <summary>
        /// Determines whether nullable types should be used
        /// </summary>
        public bool IsNullableEnabled { get; set; }

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

            TargetNamespace = model.Settings.NamespaceUrl;
            TargetNamespacePrefix = model.Settings.NamespacePrefix;

            InitializeDictionary();

            //get the project name
            string csNamespace = model.Settings.CSharpNamespace;
            if (string.IsNullOrWhiteSpace(csNamespace))
            {
                csNamespace = "Cogs.Model";
            }

            CreatePartialIIdentifiable(model, csNamespace);
            CreatePartialItemContainer(model, csNamespace);

            // Create the csproj project file
            if (WriteCsproj)
            {
                XDocument project = new XDocument(
                    new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                        new XElement("PropertyGroup", 
                            new XElement("TargetFramework", "net6"),
                            IsNullableEnabled ? new XElement("Nullable", "enable") : null),
                        new XElement("ItemGroup", 
                            new XElement("PackageReference", new XAttribute("Include", "System.ComponentModel.Annotations"), new XAttribute("Version", "5.0.0")),
                            new XElement("PackageReference", new XAttribute("Include", "Microsoft.CSharp"), new XAttribute("Version", "4.7.0")),
                            new XElement("PackageReference", new XAttribute("Include", "Newtonsoft.Json"), new XAttribute("Version", "13.0.3")))));
                XmlWriterSettings xws = new XmlWriterSettings
                {
                    OmitXmlDeclaration = true,
                    Indent = true                
                };
                using (FileStream s = new FileStream(Path.Combine(TargetDirectory, csNamespace + ".csproj"), FileMode.Create, FileAccess.ReadWrite))
                using (XmlWriter xw = XmlWriter.Create(s, xws))
                {
                    project.Save(xw);
                }
            }

            
            // copy types file
            using (Stream typeStream = this.GetType().GetTypeInfo().Assembly.GetManifestResourceStream("Cogs.Publishers.Csharp.Types.txt"))
            using (StreamReader typeReader = new StreamReader(typeStream))
            {
                string typesContent = typeReader.ReadToEnd();
                var typesBuilder = new StringBuilder();

                if (!string.IsNullOrWhiteSpace(model.HeaderInclude))
                {
                    typesBuilder.AppendLine("/*");
                    typesBuilder.AppendLine(model.HeaderInclude);
                    typesBuilder.AppendLine("*/");
                    typesBuilder.AppendLine();
                }

                typesBuilder.AppendLine(typesContent);
                File.WriteAllText(Path.Combine(TargetDirectory, "Types.cs"), typesBuilder.ToString());
            }

            
            using (Stream stream = this.GetType().GetTypeInfo().Assembly.GetManifestResourceStream("Cogs.Publishers.Csharp.DependantTypes.txt"))
            using (StreamReader reader = new StreamReader(stream))
            {
                string fileContents = reader.ReadToEnd();

                fileContents = fileContents.Replace("__CogsGeneratedNamespace", csNamespace);

                if (!string.IsNullOrWhiteSpace(model.HeaderInclude))
                {
                    fileContents = "/*" + Environment.NewLine + model.HeaderInclude + Environment.NewLine + "*/" + Environment.NewLine + fileContents;
                }

                File.WriteAllText(Path.Combine(TargetDirectory, "DependantTypes.cs"), fileContents, Encoding.UTF8);
            }

            

            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                // add class description using '$' for newline and '#' for tabs
                var newClass = new StringBuilder();

                if (!string.IsNullOrWhiteSpace(model.HeaderInclude))
                {
                    newClass.AppendLine("/*");
                    newClass.AppendLine(model.HeaderInclude);
                    newClass.AppendLine("*/");
                    newClass.AppendLine();
                }
                newClass.AppendLine("using System;");
                newClass.AppendLine("using System.Linq;");
                newClass.AppendLine("using Newtonsoft.Json;");
                newClass.AppendLine("using System.Xml.Linq;");
                newClass.AppendLine("using Cogs.SimpleTypes;");
                newClass.AppendLine("using System.Reflection;");
                newClass.AppendLine("using System.Collections;");
                newClass.AppendLine("using Newtonsoft.Json.Linq;");
                newClass.AppendLine("using Cogs.DataAnnotations;");
                newClass.AppendLine("using Cogs.Converters;");
                newClass.AppendLine("using System.Collections.Generic;");                
                newClass.AppendLine("using System.ComponentModel.DataAnnotations;");
                newClass.AppendLine();
                newClass.AppendLine($"namespace {csNamespace}");
                newClass.AppendLine("{");
                newClass.AppendLine( "    /// <summary>");
                foreach(var line in item.Description.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                {
                    newClass.AppendLine($"    /// {line}");
                }
                
                newClass.AppendLine( "    /// <summary>");
                newClass.Append("    public ");
                

                var helpers = new StringBuilder();
                var toXml = new StringBuilder();
                string n = "";
                if (model.ReusableDataTypes.Contains(item)) { n = "string name"; }
                if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName) && !CogsTypes.SimpleTypeNames.Contains(item.ExtendsTypeName) )
                {
                    toXml.AppendLine($"        public override XElement ToXml({n})");
                }
                else
                {
                    toXml.AppendLine($"        public virtual XElement ToXml({n})");
                }
                toXml.AppendLine("        {");
                toXml.AppendLine($"            XNamespace ns = \"{TargetNamespace}\";");
                if (n.Equals(""))
                {
                    toXml.AppendLine($"            XElement xEl = new XElement(ns + \"{item.Name}\");");
                }
                else
                {
                    toXml.AppendLine($"            XElement xEl = new XElement(ns + name);");
                }
                

                // add abstract to class title if relevant
                if (item.IsAbstract) { newClass.Append("abstract "); }
                newClass.Append("partial class " + item.Name);

                // allow inheritance when relevant
                string nameArgument = model.ReusableDataTypes.Contains(item) ? "\"" + item.ExtendsTypeName + "\"" : string.Empty;
                if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                {
                    if(CogsTypes.SimpleTypeNames.Contains(item.ExtendsTypeName))
                    {
                        // TODO should we allow subclassing simple types? add others and handle serialization, or eliminate
                        newClass.AppendLine($"");
                        newClass.AppendLine("    {");
                        newClass.AppendLine("        /// <summary>");
                        newClass.AppendLine($"        /// The value of the item");
                        newClass.AppendLine("        /// <summary>");                        
                        if(string.Compare(item.ExtendsTypeName, "string") == 0)
                        {
                            newClass.AppendLine($"        public string Value {{ get; set; }}");
                        }
                        else
                        {
                            // TODO other types?
                            newClass.AppendLine($"        public string Value {{ get; set; }}");
                        }

                        newClass.AppendLine();

                    }
                    else
                    {
                        newClass.AppendLine($" : {item.ExtendsTypeName}");
                        newClass.AppendLine("    {");
                        toXml.AppendLine($"            foreach (var el in base.ToXml({nameArgument}).Descendants())");
                        toXml.AppendLine("            {");
                        toXml.AppendLine("                xEl.Add(el);");
                        toXml.AppendLine("            }");
                    }

                }
                else if (!model.ReusableDataTypes.Contains(item))
                {
                    newClass.AppendLine(" : IIdentifiable");
                    newClass.AppendLine("    {");
                    newClass.AppendLine("        [JsonIgnore]");                    
                    string format = string.Join(":", model.Identification.Select(x => "{" + x.Name + "}"));
                    newClass.AppendLine($"        public string ReferenceId {{ get {{ return $\"{format}\"; }} }}");                    

                }
                else { newClass.AppendLine($"{Environment.NewLine}    {{"); }


                // insert a type descriminator that will be output for json serialization
                if (item.IsSubstitute && item is DataType)
                {
                    bool isFirst = true;
                    if(item.ParentTypes.Count > 0)
                    {
                        var directParent = item.ParentTypes.Last();
                        if (directParent.IsSubstitute)
                        {
                            // not first in inheritance chain with a type descriminator
                            isFirst = false;
                        }
                    }
                    if (isFirst)
                    {
                        newClass.AppendLine();
                        newClass.AppendLine("        /// <summary>");
                        newClass.AppendLine("        /// Set the TypeDescriminator");
                        newClass.AppendLine("        /// <summary>");
                        newClass.AppendLine("        public " + item.Name + "() { this.TypeDescriminator = this.GetType().Name; }");
                        newClass.AppendLine();
                        newClass.AppendLine("        /// <summary>");
                        newClass.AppendLine("        /// Type descriminator for json serialization");
                        newClass.AppendLine("        /// <summary>");
                        newClass.AppendLine("        [JsonProperty(\"$type\")]");
                        newClass.AppendLine("        public string TypeDescriminator { get; set; }");
                        newClass.AppendLine();
                    }
                }


                foreach (var prop in item.Properties)
                {

                    // create documentation for property
                    newClass.AppendLine("        /// <summary>");
                    foreach (var line in prop.Description.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                    {
                        newClass.AppendLine($"        /// {line}");
                    }                    
                    newClass.AppendLine("        /// <summary>");

                    
                    // Add type converters for specific serialization
                    if(prop.DataTypeName.Equals("date", StringComparison.CurrentCultureIgnoreCase))
                    {
                        newClass.AppendLine("        [JsonConverter(typeof(DateConverter))]");
                    }
                    else if (prop.DataTypeName.Equals("time", StringComparison.CurrentCultureIgnoreCase))
                    {
                        newClass.AppendLine("        [JsonConverter(typeof(TimeConverter))]");
                    }
                    else if (prop.DataTypeName.Equals("datetime", StringComparison.CurrentCultureIgnoreCase))
                    {
                        newClass.AppendLine("        [JsonConverter(typeof(DateTimeConverter))]");
                    }
                    else if (prop.DataTypeName.Equals("gday", StringComparison.CurrentCultureIgnoreCase))
                    {
                        newClass.AppendLine("        [JsonConverter(typeof(GDayConverter))]");
                    }
                    else if (prop.DataTypeName.Equals("gmonth", StringComparison.CurrentCultureIgnoreCase))
                    {
                        newClass.AppendLine("        [JsonConverter(typeof(GMonthConverter))]");
                    }
                    else if (prop.DataTypeName.Equals("gmonthday", StringComparison.CurrentCultureIgnoreCase))
                    {
                        newClass.AppendLine("        [JsonConverter(typeof(GMonthDayConverter))]");
                    }
                    else if (prop.DataTypeName.Equals("gyear", StringComparison.CurrentCultureIgnoreCase))
                    {
                        newClass.AppendLine("        [JsonConverter(typeof(GYearConverter))]");
                    }
                    else if (prop.DataTypeName.Equals("gyearmonth", StringComparison.CurrentCultureIgnoreCase))
                    {
                        newClass.AppendLine("        [JsonConverter(typeof(GYearMonthConverter))]");
                    }
                    else if (prop.DataTypeName.Equals("duration", StringComparison.CurrentCultureIgnoreCase))
                    {
                        newClass.AppendLine("        [JsonConverter(typeof(DurationConverter))]");
                    }

                    // set c# datatype representation while saving original so can tell what type it is
                    string origDataTypeName = null;
                    if (Translator.ContainsKey(prop.DataTypeName))
                    {
                        origDataTypeName = prop.DataTypeName;
                        prop.DataTypeName = Translator[prop.DataTypeName];                        
                    }

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
                                newClass.AppendLine($"        [StringValidation(null, @\"{prop.Pattern}\")]");
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
                    
                    
                    if (model.ItemTypes.Contains(prop.DataType) && !item.IsAbstract)
                    {
                        newClass.AppendLine("        [JsonConverter(typeof(IIdentifiableConverter))]");
                    }

                    // allow substitution of reusable datatypes
                    if (prop.AllowSubtypes && model.ReusableDataTypes.Contains(prop.DataType))
                    {
                        newClass.AppendLine("        [JsonConverter(typeof(SubstitutionConverter))]");
                    }


                    // if there can be at most one, create an instance variable
                    if (!prop.MaxCardinality.Equals("n") && int.Parse(prop.MaxCardinality) == 1)
                    {
                        if (Isboolintdoubleulong(prop.DataTypeName) || model.Identification.Contains(prop))
                        {
                            // If the property is optional (min cardinality is 0), and nullable is enabled, then only write an element when one exists.
                            bool propertyIsOptional = prop.MinCardinality == "0" && IsNullableEnabled;
                            string tab = "";
                            if (propertyIsOptional)
                            {
                                toXml.AppendLine($"            if ({prop.Name} != null)");
                                toXml.AppendLine("            {");
                                tab = "    ";
                            }
                            toXml.AppendLine($"            {tab}xEl.Add(new XElement(ns + \"{prop.Name}\", {prop.Name}));");
                            if (propertyIsOptional)
                            {
                                toXml.AppendLine("            }");
                            }
                        }
                        else if (origDataTypeName != null)
                        {
                            if (!prop.DataTypeName.Equals("string"))
                            {
                                //newClass.AppendLine("        [JsonConverter(typeof(SimpleTypeConverter))]");
                            }
                            if (prop.DataTypeName.Equals("CogsDate"))
                            {
                                toXml.AppendLine($"            if ({prop.Name} != null && {prop.Name}.UsedType != CogsDateType.None)");
                            }
                            else if (prop.DataTypeName.Equals("LangString"))
                            {
                                toXml.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Value != null && {prop.Name}.LanguageTag != null)");
                            }
                            else if (prop.DataTypeName.Equals("DateTimeOffset") || prop.DataTypeName.Equals("TimeSpan"))
                            {
                                toXml.AppendLine($"            if ({prop.Name} != null &&{prop.Name} != default({prop.DataTypeName}))");
                            }
                            else
                            {
                                toXml.AppendLine($"            if ({prop.Name} != null)");
                            }
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                {SimpleToXml(origDataTypeName, prop.Name, prop.Name, "xEl", false)}");
                            toXml.AppendLine("            }");
                        }
                        else if (model.ReusableDataTypes.Contains(prop.DataType))
                        {
                            toXml.AppendLine($"            if ({prop.Name} != null) {{ xEl.Add({prop.Name}.ToXml(\"{prop.Name}\")); }}");
                        }
                        else if (!model.ItemTypes.Contains(prop.DataType))
                        {                            
                            toXml.AppendLine($"            if ({prop.Name} != null)");
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                xEl.Add(new XElement(ns + \"{prop.Name}\", {prop.Name}));");
                            toXml.AppendLine("            }");
                        }
                        else
                        {
                            toXml.AppendLine($"            if ({prop.Name} != null)");
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                xEl.Add(new XElement(ns + \"{prop.Name}\", ");
                            foreach (var part in model.Identification)
                            {
                                toXml.AppendLine($"                    new XElement(ns + \"{part.Name}\", {prop.Name}.{part.Name}), ");
                            }
                            toXml.AppendLine($"                    new XElement(ns + \"TypeOfObject\", {prop.Name}.GetType().Name)));");
                            toXml.AppendLine("            }");
                        }

                        // TODO Consider whether Identification properties in C# generator should be non-nullable 
                        string nullableStr = IsNullableEnabled && prop.MinCardinality == "0" && prop.MaxCardinality == "1" ? "?" : "";
                        //bool isIdentificationProperty = model.Identification.Contains(prop);
                        //string nullableStr = IsNullableEnabled && !isIdentificationProperty ? "?" : "";
                        newClass.AppendLine($"        public {prop.DataTypeName}{nullableStr} {prop.Name} {{ get; set; }}");
                    }
                    // otherwise, create a list object to allow multiple
                    else
                    {
                        if (Isboolintdoubleulong(prop.DataTypeName) || model.Identification.Contains(prop))
                        {
                            toXml.AppendLine($"            xEl.Add(");
                            toXml.AppendLine($"                from item in {prop.Name}");
                            toXml.AppendLine($"                select new XElement(ns + \"{prop.Name}\", item));");
                        }
                        else if (origDataTypeName != null)
                        {
                            toXml.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Count > 0)");
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                foreach (var item in {prop.Name})");
                            toXml.AppendLine("                {");
                            toXml.AppendLine($"                    {SimpleToXml(origDataTypeName, "item", prop.Name, "xEl", true)}");
                            toXml.AppendLine("                }");
                            toXml.AppendLine("            }");

                        }
                        else if (model.ReusableDataTypes.Contains(prop.DataType))
                        {
                            toXml.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Count > 0)");
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                foreach (var item in {prop.Name})");
                            toXml.AppendLine("                {");
                            toXml.AppendLine($"                    xEl.Add(item.ToXml(\"{prop.Name}\"));");
                            toXml.AppendLine("                }");
                            toXml.AppendLine("            }");
                        }
                        else if (!model.ItemTypes.Contains(prop.DataType))
                        {
                            toXml.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Count > 0)");
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                xEl.Add(");
                            toXml.AppendLine($"                    from item in {prop.Name}");
                            toXml.AppendLine($"                    select new XElement(ns + \"{prop.Name}\", item.ToString()));");
                            toXml.AppendLine("            }");
                        }
                        else
                        {
                            toXml.AppendLine($"            if ({prop.Name} != null && {prop.Name}.Count > 0)");
                            toXml.AppendLine("            {");
                            toXml.AppendLine($"                foreach (var item in {prop.Name})");
                            toXml.AppendLine("                {");
                            toXml.AppendLine($"                    xEl.Add(new XElement(ns + \"{prop.Name}\", ");
                            foreach (var part in model.Identification)
                            {
                                toXml.AppendLine($"                        new XElement(ns + \"{part.Name}\", item.{part.Name}), ");
                            }
                            toXml.AppendLine($"                        new XElement(ns + \"TypeOfObject\", item.GetType().Name)));");
                            toXml.AppendLine("                }");
                            toXml.AppendLine("            }");
                        }

                        newClass.AppendLine($"        public List<{prop.DataTypeName}> {prop.Name} {{ get; set; }} = new List<{prop.DataTypeName}>();");
                        newClass.AppendLine($"        public bool ShouldSerialize{prop.Name}() {{ return {prop.Name}.Count > 0; }}");

                    }
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
                File.WriteAllText(Path.Combine(TargetDirectory, item.Name + ".cs"), newClass.ToString());
            }
        }

        private bool Isboolintdoubleulong(string name)
        {
            if (name.Equals("bool") || name.Equals("int") || name.Equals("double") || name.Equals("ulong") || name.Equals("long") || name.Equals("decimal") || name.Equals("float"))
            {
                return true;
            }
            return false;
        }

        private object SimpleToXml(string origDataTypeName, string name, string elname, string start, bool isInList)
        {
            // TODO Consider whether Identification properties in C# generator should be non-nullable 
            string nullableValueStr = IsNullableEnabled && !isInList ? "Value." : "";
            //bool isIdentificationProperty = model.Identification.Contains(prop);
            //string nullableValueStr = IsNullableEnabled && !isIdentificationProperty ? "Value. : "";

            if (origDataTypeName.ToLower().Equals("duration"))
            {
                
                return $"{start}.Add(new XElement(ns + \"{elname}\", string.Format(\"P{{00}}DT{{00}}H{{00}}M{{00}}S\", {Environment.NewLine}                    " +
                    $"{name}.{nullableValueStr}ToString(\"%d\"), {name}.{nullableValueStr}ToString(\"%h\"), {name}.{nullableValueStr}ToString(\"%m\"), {name}.{nullableValueStr}ToString(\"%s\"))));";
            }
            if (origDataTypeName.ToLower().Equals("langstring"))
            {
                return $@"{start}.Add(new XElement(ns + ""{elname}"", {name}.Value, 
                                        new XAttribute(""lang"", {name}.LanguageTag)));";
            }

            if (origDataTypeName.ToLower().Equals("datetime")) { return $"{start}.Add(new XElement(ns + \"{elname}\", {name}.{nullableValueStr}ToString(\"yyyy-MM-dd\\\\THH:mm:ss.FFFFFFFK\")));"; }
            if (origDataTypeName.ToLower().Equals("time")) { return $"{start}.Add(new XElement(ns + \"{elname}\", {name}.{nullableValueStr}ToString(\"u\").Split(' ')[1]));"; }
            if (origDataTypeName.ToLower().Equals("date")){ return $"{start}.Add(new XElement(ns + \"{elname}\", {name}.{nullableValueStr}ToString(\"u\").Split(' ')[0]));"; }
            if (origDataTypeName.ToLower().Equals("gyearmonth")) { return $"xEl.Add(new XElement(ns + \"{elname}\", {name}.ToString()));"; }
            if (origDataTypeName.ToLower().Equals("gmonthday")) { return $"xEl.Add(new XElement(ns + \"{elname}\", {name}.ToString()));"; }
            if (origDataTypeName.ToLower().Equals("gyear")) { return $"xEl.Add(new XElement(ns + \"{elname}\", {name}.ToString()));"; }
            if (origDataTypeName.ToLower().Equals("gmonth")) { return $"xEl.Add(new XElement(ns + \"{elname}\", {name}.ToString()));"; }
            if (origDataTypeName.ToLower().Equals("gday")) { return $"xEl.Add(new XElement(ns + \"{elname}\", {name}.ToString()));"; }
            if (origDataTypeName.ToLower().Equals("cogsdate")) { return $"{start}.Add(new XElement(ns + \"{elname}\", {name}.ToString()));"; }
            return $"{start}.Add(new XElement(ns + \"{elname}\", {name}));";
        }

        
        // creates a file called IIdentifiable.cs which holds the IIdentifiable interface from which all item types descend
        private void CreatePartialIIdentifiable(CogsModel model, string csNamespace)
        {
            StringBuilder builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(model.HeaderInclude))
            {
                builder.AppendLine("/*");
                builder.AppendLine(model.HeaderInclude);
                builder.AppendLine("*/");
                builder.AppendLine();
            }
            builder.AppendLine("using System;");
            builder.AppendLine();
            builder.AppendLine("using System.Xml.Linq;");
            builder.AppendLine("using Newtonsoft.Json.Linq;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine();
            builder.AppendLine($"namespace {csNamespace}");
            builder.AppendLine("{");
            builder.AppendLine("    /// <summary>");
            builder.AppendLine("    /// IIdentifiable class which all object Inherit from. Used to Serialize to Json");
            builder.AppendLine("    /// <summary>");
            builder.AppendLine("    public partial interface IIdentifiable");
            builder.AppendLine("    {");

            // TODO Consider whether Identification properties in C# generator should be non-nullable
            // If so, then don't make this nullable here.
            string nullableStr = IsNullableEnabled ? "?" : "";
            foreach (var prop in model.Identification)
            {
                builder.AppendLine($"        {prop.DataTypeName}{nullableStr} {prop.Name} {{ get; set; }}");
            }
            builder.AppendLine("    }");
            builder.AppendLine("}");
            File.WriteAllText(Path.Combine(TargetDirectory, "IIdentifiable.Properties.cs"), builder.ToString());
        }


        // Creates the ItemContainer Class
        private void CreatePartialItemContainer(CogsModel model, string csNamespace)
        {

            string clss = $@"using System;
using System.Xml.Linq;

namespace {csNamespace}
{{
    /// <summary>
    /// Partial class implementation for XML generation 
    /// <summary>
    public partial class ItemContainer
    {{ 
        public XDocument MakeXml()
        {{
            XNamespace ns = ""{TargetNamespace}"";
            XDocument xDoc = new XDocument(new XElement(ns + ""ItemContainer"", new XAttribute(XNamespace.Xmlns + "
                + $@"""{TargetNamespacePrefix}"", ""{TargetNamespace}"")));
            if (xDoc.Root != null)
            {{
                if (TopLevelReferences != null && TopLevelReferences.Count > 0)
                {{
                    foreach (var item in TopLevelReferences)
                    {{
                        xDoc.Root.Add(new XElement(ns + ""TopLevelReference"", new XElement(ns + ""ID"", item.ID), new XElement(ns + ""TypeOfObject"", item.GetType())));
                    }}
                }}
                foreach (var item in Items)
                {{
                    xDoc.Root.Add(item.ToXml());
                }}
            }}
            return xDoc;
        }}
    }}
}}";
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(model.HeaderInclude))
            {
                builder.AppendLine("/*");
                builder.AppendLine(model.HeaderInclude);
                builder.AppendLine("*/");
                builder.AppendLine();
            }

            builder.AppendLine(clss);
            File.WriteAllText(Path.Combine(TargetDirectory, "ItemContainer.Xml.cs"), builder.ToString());
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
                { "gYearMonth", "GYearMonth" },
                { "gMonthDay", "GMonthDay" },
                { "gYear", "GYear" },
                { "gMonth", "GMonth" },
                { "gDay", "GDay" },
                { "anyURI", "Uri" },
                { "nonPositiveInteger", "int" },
                { "negativeInteger", "int" },
                { "nonNegativeInteger", "int" },
                { "unsignedLong", "ulong" },
                { "positiveInteger", "int" },
                { "cogsDate", "CogsDate" },
                { "langString", "LangString" }
            };
        }
    }
}