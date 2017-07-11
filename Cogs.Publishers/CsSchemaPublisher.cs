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
            CreateJsonConverter(model, projName);
            // copy types file
            this.GetType().GetTypeInfo().Assembly.GetManifestResourceStream("Cogs.Publishers.Types.txt").CopyTo(new FileStream(Path.Combine(TargetDirectory, "Types.cs"), FileMode.Create));
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                // add class description using '$' for newline and '#' for tabs
                var newClass = new StringBuilder("using System;$using System.Linq;$using Newtonsoft.Json;$using Newtonsoft.Json.Linq;$using Cogs.DataAnnotations;$using System.Collections.Generic;$" +
                    "using System.ComponentModel.DataAnnotations;$$namespace " + projName +"${$#/// <summary>$#/// " + item.Description + "$#/// <summary>");
                newClass.Append("$#public ");
                var toJsonProperties = new StringBuilder();
                var initializeReferences = new StringBuilder();
                var reusableToJson = new StringBuilder();
                // add abstract to class title if relevant
                if (item.IsAbstract) { newClass.Append("abstract "); }
                newClass.Append("class " + item.Name);
                // allow inheritance when relevant
                if (!String.IsNullOrWhiteSpace(item.ExtendsTypeName)) newClass.Append(" : " + item.ExtendsTypeName);
                else if(!model.ReusableDataTypes.Contains(item)) { newClass.Append(" : IIdentifiable"); }
                newClass.Append("$#{");
                newClass.Append("$##public string ReferenceId { set; get; }");
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
                    if (!first && !model.ReusableDataTypes.Contains(prop.DataType)) { toJsonProperties.Append(","); }
                    // if there can be at most one, create an instance variable
                    if (!prop.MaxCardinality.Equals("n") && Int32.Parse(prop.MaxCardinality) == 1)
                    {
                        if (model.ItemTypes.Contains(prop.DataType) && !item.IsAbstract) { newClass.Append("$##[JsonConverter(typeof(IIdentifiableConverter))]"); }
                        newClass.Append("$##public " + prop.DataTypeName + " " + prop.Name + " { get; set; }");
                        if(model.ReusableDataTypes.Contains(prop.DataType))
                        {
                            reusableToJson.Append("$###if (" + prop.Name + " != null) {");
                            if(!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                            {
                                reusableToJson.Append("((JObject)json.First).Add(new JProperty(\"" + prop.Name + "\", " + prop.Name + ".ToJson())); }");
                            }
                            else { reusableToJson.Append("json.Add(new JProperty(\"" + prop.Name + "\", " + prop.Name + ".ToJson())); }"); }
                        }
                        else if (!model.ItemTypes.Contains(prop.DataType)) { toJsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", " + prop.Name + ")"); }
                        else
                        {
                            toJsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", new JObject(new JProperty(\"@type\", \"ref\"), " +
                            "$#####new JProperty(\"value\", new JArray($######\"" + prop.DataTypeName + "\", $######" + prop.Name + ".ID))))");
                            initializeReferences.Append("$###if (" + prop.Name + ".ReferenceId != null) { " + prop.Name + " = (" + prop.DataTypeName +
                                ")dict[" + prop.Name + ".ReferenceId]; }");
                        }
                    }
                    // otherwise, create a list object to allow multiple
                    else
                    {
                        if (model.ItemTypes.Contains(prop.DataType) && !item.IsAbstract) { newClass.Append("$##[JsonConverter(typeof(IIdentifiableConverter))]"); }
                        newClass.Append("$##public List<" + prop.DataTypeName + "> " + prop.Name + "{ get; set; }  = new List<" + prop.DataTypeName + ">();");
                        if(model.ReusableDataTypes.Contains(prop.DataType))
                        {
                            reusableToJson.Append("$###if (" + prop.Name + " != null) $###{");
                            if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                            {
                                reusableToJson.Append("$####((JObject)json.First).Add(new JProperty(\"" + prop.Name + "\", $#####new JArray($######from item in " + prop.Name +
                                "$######select new JObject($#######new JProperty(\"" + prop.DataTypeName + "\", item.ToJson()))))); $###}");
                            }
                            else { reusableToJson.Append("$####json.Add(new JProperty(\"" + prop.Name + "\", $#####new JArray($######from item in " + prop.Name +
                                "$######select new JObject($#######new JProperty(\"" + prop.DataTypeName + "\", item.ToJson()))))); $###}"); }
                        }
                        else if (!model.ItemTypes.Contains(prop.DataType))
                        {
                            if(model.ReusableDataTypes.Contains(item))
                            {
                                toJsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", $#####new JArray($######from item in " + prop.Name +
                                "$######select item))");
                            }
                            else
                            {
                                toJsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", $#####new JArray($######from item in " + prop.Name +
                                "$######select new JObject($#######new JProperty(\"" + prop.DataTypeName + "\", item))))");
                            }
                        }
                        else
                        {
                            toJsonProperties.Append("$####new JProperty(\"" + prop.Name + "\", $#####new JArray($######from item in " + prop.Name +
                                "$######select new JObject(new JProperty(\"@type\", \"ref\"), " +
                            "$#######new JProperty(\"value\", new JArray($########item.GetType().Name.ToString(), $########item.ID)))))");
                            initializeReferences.Append("$###if (" + prop.Name + " != null)$###{$####for (int i = 0; i < " + prop.Name + ".Count; i++)" +
                                "$####{$#####dynamic temp = dict[" + prop.Name + "[i].ReferenceId];$#####" + prop.Name + "[i] = temp;$####}$###}");
                        }
                    }
                    first = false;
                }
                newClass.Append("$##/// <summary>$##/// Used to Serialize this object to Json $##/// <summary>");
                string returnType = "JProperty";
                if(model.ReusableDataTypes.Contains(item)) { returnType = "JObject"; }
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
                    newClass.Append(toJsonProperties.ToString() + reusableToJson.ToString());
                    newClass.Append("};" + reusableToJson.ToString() + "$###return json;$##}");
                }
                newClass.Append("$$##/// <summary>$##/// Used to set this object's properties from Json $##/// <summary>");
                if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
                {
                    newClass.Append("$##public new void InitializeReferences(Dictionary<string, IIdentifiable> dict)$##{");
                }
                else { newClass.Append("$##public void InitializeReferences(Dictionary<string, IIdentifiable> dict)$##{"); }
                newClass.Append(initializeReferences + "$##}$#}$}$");
                
                // write class to out folder
                File.WriteAllText(Path.Combine(TargetDirectory, item.Name + ".cs"), newClass.ToString().
                    Replace("$###((JObject)json.First).Add();", "").Replace("#", "    ").Replace("$", Environment.NewLine).Replace("@", "$"));
            }
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
            builder.Append("$##JProperty ToJson();$##string ReferenceId { get; set; }$##void InitializeReferences(Dictionary<string, IIdentifiable> dict);$#}$}");
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
            JObject builder = new JObject {new JProperty(""TopLevelReference"", new JArray(
                from obj in TopLevelReferences
                select new JObject(
                    new JProperty(""$type"", ""ref""),
                    new JProperty(""value"", new JArray(
                        obj.GetType().ToString(),
                        obj.ID)))))};
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
            string id = """";
            JObject builder = JObject.Parse(json);
            Dictionary<string, IIdentifiable> dict = new Dictionary<string, IIdentifiable>();
            foreach (var type in builder)
            {
                if (type.Key.Equals(""TopLevelReference""))
                {
                    id = type.Value.First.Last.First.Last.ToString();
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
                        if (obj.ID.Equals(id)) { TopLevelReferences.Add(obj); }
                    }
                }
            }
            foreach (var obj in dict.Values)
            {
                obj.InitializeReferences(dict);
            }
        }
    }
}";
            StringBuilder ifs = new StringBuilder();
            foreach(var item in model.ItemTypes)
            {
                ifs.Append("$######if (clss.Equals(\"" + item.Name + "\")) { obj = JsonConvert.DeserializeObject<" + item.Name + ">(instance.Value.ToString()); }");
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
}";
            StringBuilder ifs = new StringBuilder();
            foreach (var item in model.ItemTypes)
            {
                ifs.Append("$###if (t.Equals(\"" + item.Name + "\")) { return list.Cast<" + item.Name + ">().ToList(); }");
            }
            File.WriteAllText(Path.Combine(TargetDirectory, "IIdentifiableConverter.cs"), clss.Replace("!!!", projName).Replace("???", ifs.ToString()
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