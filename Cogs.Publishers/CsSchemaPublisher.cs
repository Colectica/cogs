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
            try
            {
                File.Copy(Path.Combine(Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), ".."), "copiedFiles"), "Types.cs"), Path.Combine(TargetDirectory, "Types.cs"), true);
            }
            catch (DirectoryNotFoundException)
            {
                // when testing, filepath is different
                File.Copy(Directory.GetCurrentDirectory() + @"..\..\..\..\..\copiedFiles\Types.cs", Path.Combine(TargetDirectory, "Types.cs"), true);
            }
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                // add class description using '$' for newline and '#' for tabs
                var newClass = new StringBuilder("using System;$using System.Linq;$using Newtonsoft.Json.Linq;$using System.Collections.Generic;$" +
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
                        jsonProperties.Append("$####new JProperty(\"" + prop.DataTypeName + "\", " + prop.Name + ")");
                    }
                    // otherwise, create a list object to allow multiple
                    else
                    {
                        newClass.Append("$##public List<" + prop.DataTypeName + "> " + prop.Name + "{ get; set; }  = new List<" + prop.DataTypeName + ">();");
                        jsonProperties.Append("$####new JProperty(\"" + prop.DataTypeName + "\", $#####new JArray($######from item in " + prop.Name +
                            "$######select new JObject($#######new JProperty(\"" + prop.DataTypeName + "\", item))))");
                    }
                    first = false;
                }
                newClass.Append("$##/// <summary>$##/// Used to Serialize this object to Json $##/// <summary>");
                if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                {
                    newClass.Append("$##public new string ToJson()$##{");
                }
                else { newClass.Append("$##public string ToJson()$##{"); }
                newClass.Append("$###JProperty json = new JProperty(\"ID\", new JObject(");
                newClass.Append(jsonProperties.ToString());
                newClass.Append("));$###return json.ToString();$##}$#}$}");
                // write class to out folder
                File.WriteAllText(Path.Combine(TargetDirectory, item.Name + ".cs"), newClass.ToString().Replace("#", "    ").Replace("$", Environment.NewLine));
            }
        }


        //


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
            builder.Append("$##public List<IIdentifiable> Reusables { get; } = new List<IIdentifiable>();");
            builder.Append("$##public string Serialize()$##{");
            builder.Append("$###JObject builder = new JObject$###{");
            builder.Append("$####new JProperty(\"schema\", \"http://json-schema.org/draft-04/schema@\"),$####new JProperty(\"id\", \"@root\")," +
                "$####new JProperty(\"simpleType\", new JObject(" +
                "$#####new JProperty(\"duration\", new JObject(" +
                    "$######new JProperty(\"type\", \"number\"), $######new JProperty(\"format\", \"utc-millisec\"))), " +
                "$#####new JProperty(\"dateTime\", new JObject(" +
                    "$######new JProperty(\"type\", \"string\")," +
                    "$######new JProperty(\"format\", \"date-time\"))), " +
                "$#####new JProperty(\"time\", new JObject(" +
                    "$######new JProperty(\"type\", \"string\"), " +
                    "$######new JProperty(\"format\", \"time\"))), " +
                "$#####new JProperty(\"date\", new JObject(" +
                    "$######new JProperty(\"type\", \"string\"), " +
                    "$######new JProperty(\"format\", \"date\"))), " +
                "$#####new JProperty(\"gYearMonth\", new JObject(" +
                    "$######new JProperty(\"type\", \"object\"), " +
                    "$######new JProperty(\"properties\", new JObject(" +
                        "$#######new JProperty(\"year\", new JObject(" +
                            "$########new JProperty(\"type\", \"integer\"))), " +
                        "$#######new JProperty(\"month\", new JObject(" +
                            "$########new JProperty(\"type\", \"integer\"))), " +
                        "$#######new JProperty(\"timezone\", new JObject(" +
                            "$########new JProperty(\"type\", \"string\"))))), " +
                    "$######new JProperty(\"required\", new JArray(" +
                        "$#######new JProperty(\"year\"), new JProperty(\"month\"))))))), " +
                "$#####new JProperty(\"gYear\", new JObject(" +
                    "$######new JProperty(\"type\", \"object\"), " +
                    "$######new JProperty(\"properties\", new JObject(" +
                        "$#######new JProperty(\"year\", new JObject(" +
                            "$########new JProperty(\"type\", \"integer\"))), " +
                        "$#######new JProperty(\"timezone\", new JObject(" +
                            "$########new JProperty(\"type\", \"string\"))))), " +
                "$#####new JProperty(\"gMonthDay\", new JObject(" +
                    "$######new JProperty(\"type\", \"object\"), " +
                    "$######new JProperty(\"properties\", new JObject(" +
                        "$#######new JProperty(\"month\", new JObject(" +
                            "$########new JProperty(\"type\", \"integer\"))), " +
                        "$#######new JProperty(\"day\", new JObject(" +
                            "$########new JProperty(\"type\", \"integer\"))), " +
                        "$#######new JProperty(\"timezone\", new JObject(" +
                            "$########new JProperty(\"type\", \"string\"))))), " +
                    "$######new JProperty(\"required\", new JArray(" +
                        "$#######new JProperty(\"month\"), new JProperty(\"day\"))))))), " +
                "$#####new JProperty(\"gDay\", new JObject(" +
                    "$######new JProperty(\"type\", \"object\"), " +
                    "$######new JProperty(\"properties\", new JObject(" +
                        "$#######new JProperty(\"day\", new JObject(" +
                            "$########new JProperty(\"type\", \"integer\"))), " +
                        "$#######new JProperty(\"timezone\", new JObject(" +
                            "$########new JProperty(\"type\", \"string\"))))))), " +
                "$#####new JProperty(\"gMonth\", new JObject(" +
                    "$######new JProperty(\"type\", \"object\"), " +
                    "$######new JProperty(\"properties\", new JObject(" +
                        "$#######new JProperty(\"month\", new JObject(" +
                            "$########new JProperty(\"type\", \"integer\"))), " +
                        "$#######new JProperty(\"timezone\", new JObject(" +
                            "$########new JProperty(\"type\", \"string\"))))))), " +
                "$#####new JProperty(\"anyURI\", new JObject(" +
                    "$######new JProperty(\"type\", \"string\"))), " +
                "$#####new JProperty(\"cogsDate\", new JObject(" +
                    "$######new JProperty(\"type\", \"object\"), " +
                    "$######new JProperty(\"properties\", new JObject(" +
                        "$#######new JProperty(\"dateTime\", new JObject(" +
                            "$########new JProperty(\"!ref\", \"@/simpleType/dateTime\"))), " +
                        "$#######new JProperty(\"date\", new JObject(" +
                            "$########new JProperty(\"!ref\", \"@/simpleType/date\"))), " +
                        "$#######new JProperty(\"gYearMonth\", new JObject(" +
                            "$########new JProperty(\"!ref\", \"@/simpleType/gYearMonth\"))), " +
                        "$#######new JProperty(\"gYear\", new JObject(" +
                            "$########new JProperty(\"!ref\", \"@/simpleType/gYear\"))), " +
                        "$#######new JProperty(\"duration\", new JObject(" +
                            "$########new JProperty(\"!ref\", \"@/simpleType/duration\"))))))), " +
                "$#####new JProperty(\"language\", new JObject(" +
                    "$######new JProperty(\"type\", \"string\")))};");
            builder.Append("$###var refTypes = new JObject();$###foreach(var item in Reusables)$###{$####refTypes.Add(item.ToJson());$###}$###builder.Add(new JProperty(\"definitions\", refTypes));");
            builder.Append("$###foreach(var item in Assembly.GetExecutingAssembly().GetTypes())$###{$####var elements = Items.Where(x => x.GetType().Equals(item)).ToList();");
            builder.Append("$####if (elements.Count() > 0)$####{$#####var classType = new JObject();$#####foreach(var element in elements)$#####{" +
                "$######classType.Add(element.ToJson());$#####}$#####builder.Add(new JProperty(item.Name, new JObject(classType.ToString())));$####}$###}$###return builder.ToString();$##}$#}$}");
            File.WriteAllText(Path.Combine(TargetDirectory, "ItemContainer.cs"), builder.ToString().Replace("#", "    ").Replace("$", Environment.NewLine).Replace("@", "#").Replace("!", "$"));
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