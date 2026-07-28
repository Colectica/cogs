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
using System.Text.Json;
using System.Globalization;
using Cogs.Common;
using VDS.RDF;

namespace Cogs.Publishers.Csharp
{
    public class CSharpPublisher
    {
        /// <summary>
        /// path to write output in
        /// </summary>
        public string TargetDirectory { get; set; }

        /// <summary>
        /// Desired namespace for xml serialization
        /// </summary>
        public string? TargetNamespace { get; set; }

        /// <summary>
        /// Desired namespace prefix for xml serialization
        /// </summary>
        public string? TargetNamespacePrefix { get; set; }

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
        private Dictionary<string, string>? Translator { get; set; }

        private static readonly (string Include, string Version)[] GeneratedProjectPackages =
        {
            ("dotNetRdf.Core", "3.5.1")
        };

        CogsModel model;

        public CSharpPublisher(CogsModel model, string targetDirectory)
        {
            this.model = model;
            TargetDirectory = targetDirectory;
            InitializeDictionary();
        }

        public void Publish()
        {
            string originalTarget = TargetDirectory;
            DirectoryPublication.Publish(originalTarget, Overwrite, stagingDirectory =>
            {
                TargetDirectory = stagingDirectory;
                try
                {
                    PublishCore();
                }
                finally
                {
                    TargetDirectory = originalTarget;
                }
            }, model.SourceDirectory);
        }

        private void PublishCore()
        {
            ValidateGeneratedNames();

            TargetNamespace ??= model.Settings.NamespaceUrl;
            TargetNamespacePrefix ??= model.Settings.NamespacePrefix;


            //get the project name
            string? csNamespace = model.Settings.CSharpNamespace;
            if (string.IsNullOrWhiteSpace(csNamespace))
            {
                csNamespace = "Cogs.Model";
            }
            ValidateTargetOptions(csNamespace);

            CreatePartialIIdentifiable(model, csNamespace);
            CreatePartialItemContainer(model, csNamespace);

            // Create the csproj project file
            if (WriteCsproj)
            {
                XDocument project = new XDocument(
                    new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                        new XElement("PropertyGroup", 
                            new XElement("TargetFramework", "net10.0"),
                            new XElement("PackageId", model.Settings.Slug),
                            new XElement("Version", model.Settings.Version),
                            new XElement("Description", model.Settings.Description),
                            new XElement("Authors", model.Settings.Author),
                            new XElement("Copyright", model.Settings.Copyright),
                            IsNullableEnabled ? new XElement("Nullable", "enable") : null),
                        new XElement("ItemGroup",
                            GeneratedProjectPackages.Select(x =>
                                new XElement("PackageReference", new XAttribute("Include", x.Include))))));
                SaveXmlDocument(project, Path.Combine(TargetDirectory, csNamespace + ".csproj"));

                XDocument directoryPackages = new XDocument(
                    new XElement("Project",
                        new XElement("PropertyGroup",
                            new XElement("ManagePackageVersionsCentrally", "true")),
                        new XElement("ItemGroup",
                            GeneratedProjectPackages.Select(x =>
                                new XElement("PackageVersion",
                                    new XAttribute("Include", x.Include),
                                    new XAttribute("Version", x.Version))))));
                SaveXmlDocument(directoryPackages, Path.Combine(TargetDirectory, "Directory.Packages.props"));
            }

            
            // Copy Types.cs file.
            using Stream? typeStream = (GetType()?.GetTypeInfo().Assembly.GetManifestResourceStream("Cogs.Publishers.Csharp.Types.txt")) 
                ?? throw new Exception("Could not find Types.txt resource");
            using StreamReader typeReader = new(typeStream);
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
        
            // Copy the DependantTypes.cs file.
            using Stream? stream = GetType().GetTypeInfo().Assembly.GetManifestResourceStream("Cogs.Publishers.Csharp.DependantTypes.txt")
                ?? throw new Exception("Could not find DependantTypes.txt resource");
            using StreamReader reader = new(stream);
            string fileContents = reader.ReadToEnd();

            fileContents = fileContents.Replace("__CogsGeneratedNamespace", csNamespace);
            fileContents = fileContents.Replace("__CogsXmlNamespace__", TargetNamespace ?? model.Settings.NamespaceUrl);
            fileContents = fileContents.Replace("__CogsXmlPrefix__", TargetNamespacePrefix ?? model.Settings.NamespacePrefix);
            fileContents = fileContents.Replace(
                "\"__CogsRdfInstanceBase__\"",
                QuoteCSharp(CogsRdfNaming.GetTermBase(TargetNamespace ?? model.Settings.NamespaceUrl) + "instance/"));
            fileContents = fileContents.Replace("\"__CogsVersionLiteral__\"", QuoteCSharp(model.Settings.CogsVersion));
            fileContents = fileContents.Replace("\"__CogsModelVersionLiteral__\"", QuoteCSharp(model.Settings.Version));
            fileContents = fileContents.Replace("\"__CogsSlugLiteral__\"", QuoteCSharp(model.Settings.Slug));

            if (!string.IsNullOrWhiteSpace(model.HeaderInclude))
            {
                fileContents = "/*" + Environment.NewLine + model.HeaderInclude + Environment.NewLine + "*/" + Environment.NewLine + fileContents;
            }

            File.WriteAllText(Path.Combine(TargetDirectory, "DependantTypes.cs"), fileContents, Encoding.UTF8);

            // Write a C# class for every item type.
            foreach (DataType? item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                if (item == null)
                {
                    continue;
                }
                string className = ToCSharpIdentifier(item.Name);

                StringBuilder classBuilder = new();

                if (!string.IsNullOrWhiteSpace(model.HeaderInclude))
                {
                    classBuilder.AppendLine("/*");
                    classBuilder.AppendLine(model.HeaderInclude);
                    classBuilder.AppendLine("*/");
                    classBuilder.AppendLine();
                }
                classBuilder.AppendLine("using System;");
                classBuilder.AppendLine("using System.Linq;");
                classBuilder.AppendLine("using System.Xml.Linq;");
                classBuilder.AppendLine("using Cogs.SimpleTypes;");
                classBuilder.AppendLine("using System.Reflection;");
                classBuilder.AppendLine("using System.Collections;");
                classBuilder.AppendLine("using Cogs.DataAnnotations;");
                classBuilder.AppendLine("using Cogs.Converters;");
                classBuilder.AppendLine("using System.Collections.Generic;");                
                classBuilder.AppendLine("using System.Numerics;");
                classBuilder.AppendLine("using System.ComponentModel.DataAnnotations;");
                classBuilder.AppendLine("using VDS.RDF;");
                classBuilder.AppendLine("using System.Globalization;");
                classBuilder.AppendLine();
                classBuilder.AppendLine($"namespace {csNamespace}");
                classBuilder.AppendLine("{");
                classBuilder.AppendLine( "    /// <summary>");
                foreach(var line in item.Description.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
                {
                    classBuilder.AppendLine($"    /// {line}");
                }
                
                classBuilder.AppendLine( "    /// <summary>");
                classBuilder.AppendLine($"    [CogsType({QuoteCSharp(item.Name)}, {model.ItemTypes.Contains(item).ToString().ToLowerInvariant()}, {item.IsAbstract.ToString().ToLowerInvariant()})]");
                classBuilder.Append("    public ");


                // Start building the ToXml method.
                StringBuilder toXml = new();
                string parameterStr = "";
                if (model.ReusableDataTypes.Contains(item)) { parameterStr = "string name"; }
                if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName) && !CogsTypes.SimpleTypeNames.Contains(item.ExtendsTypeName) )
                {
                    toXml.AppendLine($"        public override XElement ToXml({parameterStr})");
                }
                else
                {
                    toXml.AppendLine($"        public virtual XElement ToXml({parameterStr})");
                }
                toXml.AppendLine("        {");
                toXml.AppendLine($"            XNamespace ns = \"{TargetNamespace}\";");
                if (string.IsNullOrWhiteSpace(parameterStr))
                {
                    toXml.AppendLine($"""            XElement xEl = new XElement(ns + "{item.Name}");""");
                }
                else
                {
                    toXml.AppendLine($"            XElement xEl = new XElement(ns + name);");
                }
                
                // Start building the AddTriples method.
                StringBuilder addTriplesMethodBuilder = new();
                if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName) && !CogsTypes.SimpleTypeNames.Contains(item.ExtendsTypeName) )
                {
                    addTriplesMethodBuilder.AppendLine("        public override INode AddTriples(IGraph graph, INode? itemNode = null)");
                }
                else
                {
                    addTriplesMethodBuilder.AppendLine("        public virtual INode AddTriples(IGraph graph, INode? itemNode = null)");
                }
                addTriplesMethodBuilder.AppendLine("        {");


                bool typeExtendsAnotherType = !string.IsNullOrWhiteSpace(item.ExtendsTypeName);
                bool isReusableItem = model.ItemTypes.Contains(item);
                if (isReusableItem)
                {
                    addTriplesMethodBuilder.AppendLine($"            itemNode ??= graph.CreateUriNode(UriFactory.Create(RdfUriFactory.GetUri(this)));");
                }
                else
                {
                    addTriplesMethodBuilder.AppendLine($"            itemNode ??= graph.CreateBlankNode();");
                }
                string rdfClassIri = CogsRdfNaming.ClassIri(
                    TargetNamespace ?? model.Settings.NamespaceUrl,
                    item.Name);
                addTriplesMethodBuilder.AppendLine($$"""
                            IUriNode typePredicate = graph.CreateUriNode(UriFactory.Create("http://www.w3.org/1999/02/22-rdf-syntax-ns#type"));
                            IUriNode typeUriNode = graph.CreateUriNode(UriFactory.Create({{QuoteCSharp(rdfClassIri)}}));
                            graph.Assert(new Triple(itemNode, typePredicate, typeUriNode));
                """);

                if (typeExtendsAnotherType)
                {
                    addTriplesMethodBuilder.AppendLine();
                    addTriplesMethodBuilder.AppendLine($"            base.AddTriples(graph, itemNode);");
                    addTriplesMethodBuilder.AppendLine();
                }



                // Add abstract to class title if relevant
                if (item.IsAbstract) { classBuilder.Append("abstract "); }
                classBuilder.Append("partial class " + className);

                // Allow inheritance when relevant
                string nameArgument = model.ReusableDataTypes.Contains(item) ?
                    $"\"{item.ExtendsTypeName}\"" : string.Empty;
                if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                {
                    if(CogsTypes.SimpleTypeNames.Contains(item.ExtendsTypeName))
                    {
                        // TODO should we allow subclassing simple types? add others and handle serialization, or eliminate
                        classBuilder.AppendLine($"");
                        classBuilder.AppendLine("    {");
                        classBuilder.AppendLine("        /// <summary>");
                        classBuilder.AppendLine($"        /// The value of the item");
                        classBuilder.AppendLine("        /// <summary>");                        
                        if(string.Compare(item.ExtendsTypeName, "string") == 0)
                        {
                            classBuilder.AppendLine($"        public string Value {{ get; set; }}");
                        }
                        else
                        {
                            // TODO other types?
                            classBuilder.AppendLine($"        public string Value {{ get; set; }}");
                        }

                        classBuilder.AppendLine();

                    }
                    else
                    {
                        // This type extends another type.

                        // Add the inheritance to the class declaration.
                        classBuilder.AppendLine($" : {ToCSharpIdentifier(item.ExtendsTypeName)}");
                        classBuilder.AppendLine("    {");

                        // Add the base class descendants to the ToXml method.
                        toXml.AppendLine($"            foreach (var el in base.ToXml({nameArgument}).Elements())");
                        toXml.AppendLine("            {");
                        toXml.AppendLine("                xEl.Add(el);");
                        toXml.AppendLine("            }");
                    }

                }
                else if (!model.ReusableDataTypes.Contains(item))
                {
                    classBuilder.AppendLine(" : IIdentifiable");
                    classBuilder.AppendLine("    {");
                    classBuilder.AppendLine("        public string ReferenceId => CogsIdentity.Format(this);");

                }
                else { classBuilder.AppendLine($"{Environment.NewLine}    {{"); }


                classBuilder.AppendLine($"        public {className}() {{ Initialize(); }}");
                classBuilder.AppendLine();

                // For every property in the model, add a C# property.
                int propertyOrder = item.ParentTypes.Sum(parentType => parentType.Properties.Count);
                foreach (Property? sourceProperty in item!.Properties)
                {
                    if (sourceProperty == null)
                    {
                        continue;
                    }
                    Property prop = CloneProperty(sourceProperty);

                    AddPropertyToGetTriplesMethod(item, prop, addTriplesMethodBuilder);

                    // create documentation for property
                    classBuilder.AppendLine("        /// <summary>");
                    foreach (var line in prop.Description.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                    {
                        classBuilder.AppendLine($"        /// {line}");
                    }                    
                    classBuilder.AppendLine("        /// <summary>");

                    
                    // set c# datatype representation while saving original so can tell what type it is
                    string? origDataTypeName = null;
                    if (Translator?.ContainsKey(prop.DataTypeName) == true)
                    {
                        origDataTypeName = prop.DataTypeName;
                        prop.DataTypeName = Translator[prop.DataTypeName];                        
                    }
                    else
                    {
                        prop.DataTypeName = ToCSharpIdentifier(prop.DataTypeName);
                    }
                    string propertyName = ToCSharpIdentifier(prop.Name);

                    bool isIdentification = IsIdentificationProperty(prop);
                    string kind = model.ItemTypes.Contains(prop.DataType)
                        ? "CogsPropertyKind.ItemReference"
                        : model.ReusableDataTypes.Contains(prop.DataType)
                            ? "CogsPropertyKind.Composite"
                            : "CogsPropertyKind.Primitive";
                    string metadata = $"        [CogsProperty({QuoteCSharp(prop.Name)}, {QuoteCSharp(origDataTypeName ?? sourceProperty.DataTypeName)}, {kind}, {propertyOrder++}, {CogsTypeSystem.AllowsSubtypes(prop).ToString().ToLowerInvariant()}, {isIdentification.ToString().ToLowerInvariant()}, {QuoteCSharp(prop.MinCardinality)}, {QuoteCSharp(prop.MaxCardinality)}";
                    var metadataOptions = new List<string>();
                    if (prop.MinLength.HasValue) metadataOptions.Add($"MinLength = {prop.MinLength.Value}");
                    if (prop.MaxLength.HasValue) metadataOptions.Add($"MaxLength = {prop.MaxLength.Value}");
                    if (prop.Ordered) metadataOptions.Add("Ordered = true");
                    if (prop.Enumeration.Count > 0) metadataOptions.Add($"Enumeration = new string[] {{ {string.Join(", ", prop.Enumeration.Select(QuoteCSharp))} }}");
                    if (!string.IsNullOrEmpty(prop.Pattern)) metadataOptions.Add($"Pattern = {QuoteCSharp(prop.Pattern)}");
                    if (!string.IsNullOrEmpty(prop.MinInclusive)) metadataOptions.Add($"MinInclusive = {QuoteCSharp(prop.MinInclusive)}");
                    if (!string.IsNullOrEmpty(prop.MinExclusive)) metadataOptions.Add($"MinExclusive = {QuoteCSharp(prop.MinExclusive)}");
                    if (!string.IsNullOrEmpty(prop.MaxInclusive)) metadataOptions.Add($"MaxInclusive = {QuoteCSharp(prop.MaxInclusive)}");
                    if (!string.IsNullOrEmpty(prop.MaxExclusive)) metadataOptions.Add($"MaxExclusive = {QuoteCSharp(prop.MaxExclusive)}");
                    metadata += metadataOptions.Count == 0 ? ")]" : ", " + string.Join(", ", metadataOptions) + ")]";
                    classBuilder.AppendLine(metadata);

                    // if there can be at most one, create an instance variable
                    if (prop.MaxCardinality == "1")
                    {
                        if (Isboolintdoubleulong(prop.DataTypeName) || IsIdentificationProperty(prop))
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
                        // Nullable annotations are optional, but the wire-level distinction between an
                        // absent optional scalar and its CLR default value is not. Value types therefore
                        // always use Nullable<T> for 0..1 properties, even when nullable reference type
                        // annotations are disabled.
                        bool optionalSingleton = prop.MinCardinality == "0" && prop.MaxCardinality == "1";
                        bool clrValueType = Isboolintdoubleulong(prop.DataTypeName);
                        string nullableStr = optionalSingleton && (IsNullableEnabled || clrValueType) ? "?" : "";
                        //bool isIdentificationProperty = model.Identification.Contains(prop);
                        //string nullableStr = IsNullableEnabled && !isIdentificationProperty ? "?" : "";
                        string initializer = IsNullableEnabled && nullableStr.Length == 0 && !Isboolintdoubleulong(prop.DataTypeName)
                            ? " = null!;"
                            : string.Empty;
                        classBuilder.AppendLine($"        public {prop.DataTypeName}{nullableStr} {propertyName} {{ get; set; }}{initializer}");
                    }
                    // otherwise, create a list object to allow multiple
                    else
                    {
                        if (Isboolintdoubleulong(prop.DataTypeName) || IsIdentificationProperty(prop))
                        {
                            toXml.AppendLine($""""
                                xEl.Add(
                                    from item in {prop.Name}
                                    select new XElement(ns + "{prop.Name}", item));
                            """");
                        }
                        else if (origDataTypeName != null)
                        {
                            toXml.AppendLine($$"""
                                        if ({{prop.Name}} != null && {{prop.Name}}.Count > 0)
                                        {
                                            foreach (var item in {{prop.Name}})
                                            {
                                                {{SimpleToXml(origDataTypeName, "item", prop.Name, "xEl", true)}}
                                            }
                                        }
                            """);
                        }
                        else if (model.ReusableDataTypes.Contains(prop.DataType))
                        {
                            toXml.AppendLine($$"""
                                        if ({{prop.Name}} != null && {{prop.Name}}.Count > 0)
                                        {
                                            foreach (var item in {{prop.Name}})
                                            {
                                                xEl.Add(item.ToXml("{{prop.Name}}"));
                                            }
                                        }
                            """);
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

                        classBuilder.AppendLine($"        public List<{prop.DataTypeName}> {propertyName} {{ get; set; }} = new List<{prop.DataTypeName}>();");
                        classBuilder.AppendLine($"        public bool ShouldSerialize{propertyName}() {{ return {propertyName}.Count > 0; }}");

                    }
                }
                
                classBuilder.AppendLine("        partial void Initialize();");
                classBuilder.AppendLine();
                classBuilder.AppendLine("        /// <summary>");
                classBuilder.AppendLine("        /// Used to Serialize this object to XML");
                classBuilder.AppendLine("        /// <summary>");
                bool overridesToXml = !string.IsNullOrWhiteSpace(item.ExtendsTypeName)
                    && !CogsTypes.SimpleTypeNames.Contains(item.ExtendsTypeName);
                string xmlModifier = overridesToXml ? "override" : "virtual";
                classBuilder.AppendLine($"        public {xmlModifier} XElement ToXml({parameterStr})");
                classBuilder.AppendLine("        {");
                classBuilder.AppendLine(string.IsNullOrWhiteSpace(parameterStr)
                    ? $"            return CogsXmlCodec.WriteStandalone(this, {QuoteCSharp(item.Name)});"
                    : "            return CogsXmlCodec.WriteStandalone(this, name);");
                classBuilder.AppendLine("        }");
                classBuilder.AppendLine();
                classBuilder.Append(addTriplesMethodBuilder.ToString());
                classBuilder.AppendLine("            return itemNode;");
                classBuilder.AppendLine("        }");
                classBuilder.AppendLine("    }");
                classBuilder.AppendLine("}");
                classBuilder.AppendLine();

                // Write class to out folder
                File.WriteAllText(Path.Combine(TargetDirectory, className + ".cs"), classBuilder.ToString());
            }
        }

        private static void SaveXmlDocument(XDocument document, string path)
        {
            XmlWriterSettings xws = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true
            };
            using FileStream s = new(path, FileMode.Create, FileAccess.ReadWrite);
            using XmlWriter xw = XmlWriter.Create(s, xws);
            document.Save(xw);
        }

        private void AddPropertyToGetTriplesMethod(DataType item, Property prop, StringBuilder addTriplesMethodBuilder)
        {
            string memberName = ToCSharpIdentifier(prop.Name);

            bool isSingular = prop.MaxCardinality == "1";
            if (isSingular)
            {
                // We have itemNode accessible here already.

                // Make a predicate for the property.
                if (model.ItemTypes.Contains(prop.DataType))
                {
                    // For an item type, make a triple from this item, property-predicate, to the URI of the referenced item.
                    addTriplesMethodBuilder.AppendLine($$"""            
                                if ({{memberName}} != null)
                                {
                                    {{GetStringToAddTripleForReferencedItem(prop.Name, memberName)}}
                                }
                    """);
                }
                else if (model.ReusableDataTypes.Contains(prop.DataType))
                {
                    // For a nested, non-identified type, make a triple from this item, property-predicate, to ... something.
                    addTriplesMethodBuilder.AppendLine($$"""
                                if ({{memberName}} != null)
                                {
                                    {{GetStringToAddTripleForCompositeObject(prop.Name, memberName)}}
                                }
                    """);

                    addTriplesMethodBuilder.AppendLine();
                }
                else
                {
                    // This must be a primitive property. Put out the actual value.
                    addTriplesMethodBuilder.AppendLine($$"""
                                if ({{memberName}} != null)
                                {
                                    {{GetStringToAddTripleForPrimitive(prop.Name, memberName, prop.DataType)}}
                                }
                    """);
                    addTriplesMethodBuilder.AppendLine();
                }

            }
            else
            {
                // This is a list property.
                if (model.ItemTypes.Contains(prop.DataType))
                {
                    // If the reference is to a versionsed item, add a reference for each one.
                    addTriplesMethodBuilder.AppendLine($$"""            
                                foreach (var referencedItem in {{memberName}})
                                {
                                    if (referencedItem != null)
                                    {
                                        {{GetStringToAddTripleForReferencedItem(prop.Name, "referencedItem")}}
                                    }
                                }
                    """);
                }
                else if (model.ReusableDataTypes.Contains(prop.DataType))
                {
                    addTriplesMethodBuilder.AppendLine($$"""            
                                foreach (var referencedItem in {{memberName}})
                                {
                                    if (referencedItem != null)
                                    {
                                        {{GetStringToAddTripleForCompositeObject(prop.Name, "referencedItem")}}
                                    }
                                }
                    """);
                }
                else
                {
                    // This must be a primitive property. Put out the actual value.
                    addTriplesMethodBuilder.AppendLine($$"""
                                foreach (var obj in {{memberName}})
                                {
                                    if (obj != null)
                                    {
                                        {{GetStringToAddTripleForPrimitive(prop.Name, "obj", prop.DataType)}}
                                    }
                                }
                    """);
                    addTriplesMethodBuilder.AppendLine();
                }

            }
        }

        private string GetStringToAddTripleForReferencedItem(string predicateName, string variableName)
        {
            string predicateIri = GetRdfPropertyIri(predicateName);
            return $"""graph.Assert(new Triple(itemNode, graph.CreateUriNode(UriFactory.Create({QuoteCSharp(predicateIri)})), graph.CreateUriNode(UriFactory.Create(RdfUriFactory.GetUri({variableName})))));""";
        }

        private string GetStringToAddTripleForCompositeObject(string predicateName, string variableName)
        {
            string predicateIri = GetRdfPropertyIri(predicateName);
            return $"""
            INode node = {variableName}.AddTriples(graph);
                                graph.Assert(new Triple(itemNode, graph.CreateUriNode(UriFactory.Create({QuoteCSharp(predicateIri)})), node));
            """;
        }

        private string GetStringToAddTripleForPrimitive(string predicateName, string variableName, DataType dataType)
        {
            string predicateIri = GetRdfPropertyIri(predicateName);
            return $$"""graph.Assert(new Triple(itemNode, graph.CreateUriNode(UriFactory.Create({{QuoteCSharp(predicateIri)}})), CogsPrimitiveCodec.CreateRdfLiteral(graph, {{variableName}}, {{QuoteCSharp(dataType.Name)}})));""";
        }

        private string GetRdfPropertyIri(string propertyName) =>
            CogsRdfNaming.PropertyIri(TargetNamespace ?? model.Settings.NamespaceUrl, propertyName);

        private bool Isboolintdoubleulong(string name)
        {
            if (name.Equals("bool") || name.Equals("int") || name.Equals("double") || name.Equals("ulong") || name.Equals("long") || name.Equals("BigInteger") || name.Equals("float"))
            {
                return true;
            }
            return false;
        }

        private bool IsIdentificationProperty(Property property) =>
            model.Identification.Any(candidate => string.Equals(candidate.Name, property.Name, StringComparison.Ordinal));

        private void ValidateGeneratedNames()
        {
            CogsError? rdfError = RdfPublisherValidation.ValidatePropertyTermCollisions(
                    model,
                    "CSH1001",
                    "Generated C# RDF")
                .FirstOrDefault();
            if (rdfError is not null)
            {
                throw new CogsPublicationException($"{rdfError.Code}: {rdfError.Message}");
            }

            DataType[] types = model.ItemTypes.Concat(model.ReusableDataTypes).ToArray();
            var reservedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ItemContainer", "IIdentifiable", "CogsIdentity", "CogsModelMetadata", "RdfUriFactory",
                "ItemContainerJsonConverter", "CogsIdentityKey", "CogsIdentityMap", "CogsObjectState",
                "CogsPropertyMetadata", "CogsReflection", "CogsPrimitiveCodec", "CogsJsonCodec", "CogsXmlCodec",
                "LangString", "CogsDecimal", "CogsDate", "CogsDateTime", "CogsDateOnly", "CogsTime",
                "CogsDuration", "GYear", "GYearMonth", "GMonthDay", "GDay", "GMonth",
            };
            foreach (DataType type in types)
            {
                string generated = ToCSharpIdentifier(type.Name);
                if (reservedTypes.Contains(generated))
                    throw new InvalidOperationException($"COGS type '{type.Name}' conflicts with generated C# runtime type '{generated}'.");
            }
            foreach (IGrouping<string, DataType> collision in types.GroupBy(x => ToCSharpIdentifier(x.Name), StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
                throw new InvalidOperationException($"COGS types {string.Join(", ", collision.Select(x => $"'{x.Name}'"))} normalize to the same C# class '{collision.Key}'.");

            var reservedMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ReferenceId", "ToXml", "AddTriples", "GetUri", "GetType", "Initialize",
            };
            foreach (DataType type in types)
            {
                IEnumerable<Property> effective = type.ParentTypes.SelectMany(parent => parent.Properties).Concat(type.Properties);
                Property[] properties = effective.ToArray();
                foreach (Property property in properties)
                    if (reservedMembers.Contains(ToCSharpIdentifier(property.Name)))
                        throw new InvalidOperationException($"COGS property '{type.Name}.{property.Name}' conflicts with a generated C# member.");
                foreach (IGrouping<string, Property> collision in properties.GroupBy(x => ToCSharpIdentifier(x.Name), StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
                    throw new InvalidOperationException($"Properties on '{type.Name}' normalize to the same C# member '{collision.Key}'.");
                var memberNames = new HashSet<string>(properties.Select(x => ToCSharpIdentifier(x.Name)), StringComparer.OrdinalIgnoreCase);
                foreach (Property repeated in properties.Where(x => x.MaxCardinality != "1"))
                    if (memberNames.Contains("ShouldSerialize" + ToCSharpIdentifier(repeated.Name)))
                        throw new InvalidOperationException($"Generated ShouldSerialize member for '{type.Name}.{repeated.Name}' collides with a property.");
            }
        }

        private void ValidateTargetOptions(string csharpNamespace)
        {
            if (!Uri.TryCreate(TargetNamespace, UriKind.Absolute, out _))
                throw new InvalidOperationException($"C# publisher XML namespace '{TargetNamespace}' must be an absolute URI.");

            string targetPrefix = TargetNamespacePrefix
                ?? throw new InvalidOperationException("C# publisher XML namespace prefix is required.");
            try { XmlConvert.VerifyNCName(targetPrefix); }
            catch (XmlException exception)
            {
                throw new InvalidOperationException($"C# publisher XML namespace prefix '{targetPrefix}' must be an XML NCName.", exception);
            }
            if (targetPrefix is "xml" or "xmlns")
                throw new InvalidOperationException($"C# publisher XML namespace prefix '{targetPrefix}' is reserved.");

            string[] segments = csharpNamespace.Split('.');
            if (segments.Length == 0 || segments.Any(segment => !IsValidCSharpIdentifier(segment)))
                throw new InvalidOperationException($"CSharpNamespace '{csharpNamespace}' is not a valid C# namespace.");
        }

        private static bool IsValidCSharpIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || CSharpKeywords.Contains(value)) return false;
            bool first = true;
            foreach (Rune rune in value.EnumerateRunes())
            {
                UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                bool valid = first
                    ? Rune.IsLetter(rune) || category == UnicodeCategory.ConnectorPunctuation
                    : Rune.IsLetterOrDigit(rune) || category is UnicodeCategory.ConnectorPunctuation
                        or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                        or UnicodeCategory.Format;
                if (!valid) return false;
                first = false;
            }
            return !first;
        }

        private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while",
        };

        private static string ToCSharpIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value)) throw new InvalidOperationException("A COGS name cannot be empty.");
            var builder = new StringBuilder(value.Length);
            bool capitalize = true;
            foreach (Rune rune in value.Normalize(NormalizationForm.FormC).EnumerateRunes())
            {
                UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                bool allowed = Rune.IsLetterOrDigit(rune) || category is UnicodeCategory.ConnectorPunctuation
                    or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.Format;
                if (!allowed)
                {
                    capitalize = true;
                    continue;
                }
                Rune output = capitalize && Rune.IsLetter(rune) ? Rune.ToUpperInvariant(rune) : rune;
                builder.Append(output.ToString());
                capitalize = false;
            }
            if (builder.Length == 0) throw new InvalidOperationException($"COGS name '{value}' cannot be represented as a C# identifier.");
            return builder.ToString();
        }

        private string GetCSharpTypeName(string cogsTypeName) =>
            Translator != null && Translator.TryGetValue(cogsTypeName, out string? translated)
                ? translated
                : ToCSharpIdentifier(cogsTypeName);

        private static string QuoteCSharp(string value) => JsonSerializer.Serialize(value ?? string.Empty);

        private static Property CloneProperty(Property source) => new()
        {
            Name = source.Name,
            DataTypeName = source.DataTypeName,
            DataType = source.DataType,
            MinCardinality = source.MinCardinality,
            MaxCardinality = source.MaxCardinality,
            Description = source.Description,
            Ordered = source.Ordered,
            AllowSubtypes = source.AllowSubtypes,
            MinLength = source.MinLength,
            MaxLength = source.MaxLength,
            Enumeration = new List<string>(source.Enumeration),
            Pattern = source.Pattern,
            MinInclusive = source.MinInclusive,
            MinExclusive = source.MinExclusive,
            MaxInclusive = source.MaxInclusive,
            MaxExclusive = source.MaxExclusive,
            FromMixin = source.FromMixin,
        };

        private string SimpleToXml(string origDataTypeName, string name, string elname, string start, bool isInList)
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
                                        new XAttribute(XNamespace.Xml + ""lang"", {name}.LanguageTag)));";
            }

            if (origDataTypeName.ToLower().Equals("datetime")) { return $"{start}.Add(new XElement(ns + \"{elname}\", {name}.{nullableValueStr}ToString(\"yyyy-MM-dd\\\\THH:mm:ss.FFFFFFFK\")));"; }
            if (origDataTypeName.ToLower().Equals("time")) { return $"{start}.Add(new XElement(ns + \"{elname}\", {name}.{nullableValueStr}ToString(\"HH:mm:ss.FFFFFFFK\", CultureInfo.InvariantCulture)));"; }
            if (origDataTypeName.ToLower().Equals("date")){ return $"{start}.Add(new XElement(ns + \"{elname}\", {name}.{nullableValueStr}ToString(\"yyyy-MM-dd\", CultureInfo.InvariantCulture)));"; }
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
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Numerics;");
            builder.AppendLine("using Cogs.SimpleTypes;");
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
            foreach (var prop in model.Identification)
            {
                builder.AppendLine($"        {GetCSharpTypeName(prop.DataTypeName)} {ToCSharpIdentifier(prop.Name)} {{ get; set; }}");
            }
            builder.AppendLine("    }");
            builder.AppendLine("}");
            File.WriteAllText(Path.Combine(TargetDirectory, "IIdentifiable.Properties.cs"), builder.ToString());
        }


        // Creates the ItemContainer Class
        private void CreatePartialItemContainer(CogsModel model, string csNamespace)
        {
            string rdfTermBase = CogsRdfNaming.GetTermBase(TargetNamespace ?? model.Settings.NamespaceUrl);
            string rdfPrefix = TargetNamespacePrefix ?? model.Settings.NamespacePrefix;

            string clss = $$"""
using System;
using System.Xml.Linq;
using VDS.RDF;

namespace {{csNamespace}}
{
    /// <summary>
    /// Partial class implementation for RDF generation.
    /// <summary>
    public partial class ItemContainer
    {
        public IGraph MakeRdfGraph()
        {
            IGraph graph = new Graph();
            graph.NamespaceMap.AddNamespace({{QuoteCSharp(rdfPrefix)}}, UriFactory.Create({{QuoteCSharp(rdfTermBase)}}));

            foreach (var item in Items)
            {
                item.AddTriples(graph);
            }

            return graph;
        }
    }
}
""";
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
            Translator = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "string", "string" },
                { "boolean", "bool" },
                { "float", "float" },
                { "double", "double" },
                { "long", "long" },
                { "int", "int" },
                { "language", "string" },
                { "duration", "CogsDuration" },
                { "dateTime", "CogsDateTime" },
                { "time", "CogsTime" },
                { "date", "CogsDateOnly" },
                { "gYearMonth", "GYearMonth" },
                { "gMonthDay", "GMonthDay" },
                { "gYear", "GYear" },
                { "gMonth", "GMonth" },
                { "gDay", "GDay" },
                { "anyURI", "Uri" },
                { "nonPositiveInteger", "BigInteger" },
                { "negativeInteger", "BigInteger" },
                { "nonNegativeInteger", "BigInteger" },
                { "unsignedLong", "ulong" },
                { "positiveInteger", "BigInteger" },
                { "decimal", "CogsDecimal" },
                { "cogsDate", "CogsDate" },
                { "langString", "LangString" }
            };
        }
    }
}
