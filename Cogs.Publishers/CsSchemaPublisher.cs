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
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Reflection;

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
                new XElement("ItemGroup", new XElement("PackageReference", new XAttribute("Include","System.ComponentModel.Annotations"),
                    new XAttribute("Version", "4.4.0-preview2-25405-01")),
                    new XElement("PackageReference", new XAttribute("Include", "Microsoft.CSharp"), new XAttribute("Version", "4.4.0-preview2-25405-01")),
                    new XElement("PackageReference", new XAttribute("Include", "Newtonsoft.Json"), new XAttribute("Version", "10.0.3")))));
            XmlWriterSettings xws = new XmlWriterSettings { OmitXmlDeclaration = true };
            using (XmlWriter xw = XmlWriter.Create(Path.Combine(TargetDirectory, projName + ".csproj"), xws))
            {
                project.Save(xw);
            }
            // copy types file
            this.GetType().GetTypeInfo().Assembly.GetManifestResourceStream("Cogs.Publishers.Types.txt").CopyTo(new FileStream(Path.Combine(TargetDirectory, "Types.cs"), FileMode.Create));
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                // add class description using '$' for newline and '#' for tabs
                var newClass = new StringBuilder("using System;$using System.Linq;$using Newtonsoft.Json.Linq;$using Cogs.DataAnnotations;$using System.Collections.Generic;$" +
                    "using System.ComponentModel.DataAnnotations;$$namespace " + projName +"${$#/// <summary>$#/// " + item.Description + "$#/// <summary>");
                newClass.Append("$#public ");
                var jsonProperties = new StringBuilder();
                // add abstract to class title if relevant
                if (item.IsAbstract) { newClass.Append("abstract "); }
                newClass.Append("class " + item.Name);
                // allow inheritance when relevant
                if (!String.IsNullOrWhiteSpace(item.ExtendsTypeName)) newClass.Append(" : " + item.ExtendsTypeName);
                else if(!model.ReusableDataTypes.Contains(item)) { newClass.Append(" : IIdentifiable"); }
                newClass.Append("$#{");
                bool first = true;
                foreach(var prop in item.Properties)
                {
                    // set c# datatype representation
                    if (Translator.ContainsKey(prop.DataTypeName)) { prop.DataTypeName = Translator[prop.DataTypeName]; }
                    // create documentation for property
                    newClass.Append("$##/// <summary>$##/// " + prop.Description + "$##/// <summary>");
                    // create constraints
                    if (prop.DataTypeName.Equals("string") || prop.DataTypeName.Equals("Uri"))
                    {
                        if(prop.MinLength != null && prop.MaxLength != null)
                        {
                            newClass.Append("$##[StringLength(" + prop.MaxLength + ", MinimumLength = " + prop.MinLength + ")]");
                        }
                        else if(prop.MaxLength != null)
                        {
                            newClass.Append("$##[StringLength(" + prop.MaxLength + ")]");
                        }
                        else if (prop.MinLength != null)
                        {
                            newClass.Append("$##[StringLength(" + Int32.MaxValue + ", MinimumLength = " + prop.MinLength + ")]");
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
                                    if(useComma) { newClass.Append(", "); }
                                    newClass.Append("$###\"" + option + "\"");
                                    useComma = true;
                                }
                                if (!string.IsNullOrWhiteSpace(prop.Pattern)) { newClass.Append("$##}, " + prop.Pattern + ")]"); }
                                else { newClass.Append("$##})]"); }
                            }
                            else if(!string.IsNullOrWhiteSpace(prop.Pattern))
                            {
                                newClass.Append("$##[StringValidation(null, \"" + prop.Pattern + "\")]");
                            }
                        }
                    }else if(!prop.DataTypeName.Equals("bool") && !prop.DataTypeName.Equals("CogsDate"))
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
                    if (!first) { jsonProperties.Append(","); }
                    // if there can be at most one, create an instance variable
                    if (!prop.MaxCardinality.Equals("n") && Int32.Parse(prop.MaxCardinality) == 1)
                    {
                        newClass.Append("$##public " + prop.DataTypeName + " " + prop.Name + " { get; set; }");
                        if(model.ReusableDataTypes.Contains(prop.DataType)) { jsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", " + prop.Name + ".ToJson())"); }
                        else if (!model.ItemTypes.Contains(prop.DataType)) { jsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", " + prop.Name + ")"); }
                        else { jsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", new JArray($#####new JObject(new JProperty(\"!type\", \"ref\"), " +
                            "$######new JProperty(\"value\", new JArray($#######new JProperty(\"" + prop.DataTypeName + "\"), $#######new JProperty(" + prop.Name + ".ID))))))"); }
                    }
                    // otherwise, create a list object to allow multiple
                    else
                    {
                        newClass.Append("$##public List<" + prop.DataTypeName + "> " + prop.Name + "{ get; set; }  = new List<" + prop.DataTypeName + ">();");
                        if (!model.ItemTypes.Contains(prop.DataType))
                        {
                            jsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", $#####new JArray($######from item in " + prop.Name +
                                "$######select new JObject($#######new JProperty(\"" + prop.DataTypeName + "\", item))))");
                        }
                        else
                        {
                            jsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", $#####new JArray($######from item in " + prop.Name +
                                "$######select new JObject(new JProperty(\"!type\", \"ref\"), " +
                            "$#######new JProperty(\"value\", new JArray($########new JProperty(\"" + prop.DataTypeName + "\"), $########new JProperty(item.ID))))))"); 
                        }
                    }
                    first = false;
                }
                newClass.Append("$##/// <summary>$##/// Used to Serialize this object to Json $##/// <summary>");
                if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                {
                    newClass.Append("$##public new string ToJson()$##{");
                }
                else { newClass.Append("$##public string ToJson()$##{"); }
                if (!model.ReusableDataTypes.Contains(item))
                {
                    newClass.Append("$###JProperty json = new JProperty(ID, new JObject(");
                    newClass.Append(jsonProperties.ToString());
                    newClass.Append("));$###return json.ToString();$##}$#}$}");
                }
                else
                {
                    newClass.Append("$###JObject json = new JObject() {");
                    newClass.Append(jsonProperties.ToString());
                    newClass.Append("};$###return json.ToString();$##}$#}$}");
                }
                
                // write class to out folder
                File.WriteAllText(Path.Combine(TargetDirectory, item.Name + ".cs"), newClass.ToString().Replace("#", "    ").Replace("$", Environment.NewLine).Replace("!", "$"));
            }
        }



        // creates a file call IIdentifiable.cs which holds the IIdentifiable interface from which all item types descend
        private void CreateIIdentifiable(CogsModel model, string projName)
        {
            StringBuilder builder = new StringBuilder("using System;$using System.Collections.Generic;$$namespace " +
                    projName + "${$#/// <summary>$#/// IIdentifiable class which all object Inherit from. Used to Serialize to Json $#/// <summary>");
            builder.Append("$#public interface IIdentifiable$#{");
            foreach (var prop in model.Identification)
            {
                builder.Append("$##" + prop.DataTypeName + " " + prop.Name + " { get; set; }");
            }
            builder.Append("$##string ToJson();");
            builder.Append("$#}$}");
            File.WriteAllText(Path.Combine(TargetDirectory, "IIdentifiable.cs"), builder.ToString().Replace("#", "    ").Replace("$", Environment.NewLine));
        }



        // Creates the ItemContainer Class
        private void CreateItemContainer(CogsModel model, string projName)
        {
            StringBuilder builder = new StringBuilder("using System;$using System.Linq;$using System.Reflection;$using Newtonsoft.Json.Linq;"+
                "$using System.Collections.Generic;$$namespace " + projName + "${$#/// <summary>$#/// Class that contains a list of all items in the model $#/// <summary>");
            builder.Append("$#public class ItemContainer$#{$##public List<IIdentifiable> Items { get; } = new List<IIdentifiable>();");
            builder.Append("$##public List<IIdentifiable> TopLevelReferences { get; } = new List<IIdentifiable>();");
            builder.Append("$##public string Serialize()$##{");
            builder.Append("$###JObject builder = new JObject {new JProperty(\"Reference\", new JArray($####from obj in TopLevelReferences" +
                "$####select new JObject($#####new JProperty(\"!type\", \"ref\"), " +
                "$#####new JProperty(\"value\", new JArray($######new JProperty(obj.GetType().ToString()), $######new JProperty(obj.ID))))))};");
            builder.Append("$###foreach(var item in Assembly.GetExecutingAssembly().GetTypes())$###{$####var elements = Items.Where(x => x.GetType().Equals(item)).ToList();");
            builder.Append("$####if (elements.Count() > 0)$####{$#####var classType = new JObject();$#####foreach(var element in elements)$#####{" +
                "$######classType.Add(element.ToJson());$#####}$#####builder.Add(new JProperty(item.Name, new JObject(classType.ToString())));$####}");
            builder.Append("$###}$###return builder.ToString();$##}$#}$}");
            File.WriteAllText(Path.Combine(TargetDirectory, "ItemContainer.cs"), builder.ToString().Replace("#", "    ").Replace("$", Environment.NewLine).Replace("!", "$"));
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
                { "gYearMonth", "Tuple<int, int>" },
                { "gYear", "int" },
                { "gYearDay", "Tuple<int, int>" },
                { "gDay", "int" },
                { "gMonth", "int" },
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