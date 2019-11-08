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
using System.Reflection;
using Cogs.Common;

namespace Cogs.Publishers
{
    /// <summary>
    /// Generate an uml schema using the Garden of Eden approach, all elements and type definitions are declared globally
    /// </summary>
    public class DotSchemaPublisher
    {
        /// <summary>
        /// path to write output in
        /// </summary>
        public string TargetDirectory { get; set; }
        /// <summary>
        /// path to dot executable file
        /// </summary>
        public string DotLocation { get; set; }
        /// <summary>
        /// boolean to determine whether to replace existing or not
        /// </summary>
        public bool Overwrite { get; set; }
        /// <summary>
        /// string specifying output format
        /// </summary>
        public string Format { get; set; }
        /// <summary>
        /// string specifying at what level to create the diagram(s). 
        /// Options are single (diagram for each item), all (one diagram for everything) or topic (diagrams for each itemType).
        /// </summary>
        public string Output { get; set; }
        /// <summary>
        /// bool to determine whether to allow inheritance or not in graph(s)
        /// </summary>
        public bool Inheritance { get; set; }
        /// <summary>
        /// bool to determine whether to allow reusable types and their properties in graphs or not
        /// </summary>
        public bool ShowReusables { get; set; }

        private List<ItemType> ClassList { get; set; }
        private List<DataType> ReusableList { get; set; }
        public List<CogsError> Errors { get; set; } = new List<CogsError>();

        public int Publish(CogsModel model)
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

            if (DotLocation == null)
            {
                if (File.Exists("dot.exe")) { DotLocation = Path.GetFullPath("dot.exe"); }
                else if (File.Exists("dot")) { DotLocation = Path.GetFullPath("dot"); }
                else
                {
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        foreach (var exe in Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator))
                        {
                            var fullPath = Path.Combine(exe, "dot.exe");
                            if (File.Exists(fullPath)) 
                            { 
                                DotLocation = fullPath;
                                break;
                            }
                        }
                    }
                    else if (Environment.OSVersion.Platform == PlatformID.Unix)
                    {
                        DotLocation = "dot";
                    }
                }
                if (DotLocation == null)
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, "Could not find dot file: please specify path"));
                    return -1;
                }
            }
            // create list of all class names so you know if a class is being referenced
            ClassList = model.ItemTypes;
            // create list of all reusable types so you know if a reusable type is being referenced and can get information about it
            ReusableList = model.ReusableDataTypes;
            var header = "digraph G { compound = true rankdir=LR fontsize = 8 node [fontsize = 8 shape = \"oval\" " +
                "style = \"filled\" fillcolor = \"#f7b733\"] edge [ fontsize = 8 ] ";

            if (Output.Equals("all")) { MakeGraphAll(model, header); }
            else if (Output.Equals("topic")) { MakeGraphTopic(model, header); }
            else { MakeGraphSingle(model, header); }
            return 0;
        }

        private string MakeItem(DataType item)
        {
            return MakeNode(item, new StringBuilder($"{item.Name} [ shape = \"record\" label = \"{{ {item.Name} | "));
        }

        private string MakeCluster(DataType item)
        {
            StringBuilder output = new StringBuilder();
            StringBuilder outerClass = new StringBuilder($"subgraph cluster{item.Name} {{ style = \"filled\" " +
                $"fillcolor = \"#4abdac\" label = \"{item.Name}\" ");
            List<DataType> reusablesPresent = new List<DataType>();
            outerClass.Append(item.Name + "Properties [ shape = \"record\" label=\"{Properties |");
            foreach (var property in item.Properties)
            {
                outerClass.Append(property.Name + " : " + property.DataTypeName);
                if (!string.IsNullOrWhiteSpace(property.MinCardinality) && !string.IsNullOrWhiteSpace(property.MaxCardinality))
                {
                    outerClass.Append("[" + property.MinCardinality + "..." + property.MaxCardinality + "] ");
                }
                outerClass.Append("\\l");
                if (ClassList.Contains(property.DataType))
                {
                    output.Append("edge[ arrowhead = \"none\" headlabel = \"0...*\" taillabel = \"0...*\"] ");
                    output.Append(item.Name + "Properties -> " + property.DataTypeName + "[ arrowhead = \"none\" label = \"" + property.Name + "\"] ");
                }
                else if (ReusableList.Contains(property.DataType))
                {
                    // add reusableTypes within that type to reusable list as well
                    Stack<DataType> stack = new Stack<DataType>();
                    if (!property.DataTypeName.Equals(item.Name) && !reusablesPresent.Contains(property.DataType))
                    {
                        stack.Push(property.DataType);
                        reusablesPresent.Add(property.DataType);
                        while (stack.Count > 0)
                        {
                            var type = stack.Pop();
                            foreach (var prop in type.Properties)
                            {
                                // checks: item is a reusable type, item is not already seen, item isn't current item, item isn't primitive
                                if (ReusableList.Contains(prop.DataType) && !reusablesPresent.Contains(type) &&
                                    !type.Name.Equals(prop.DataTypeName) && !prop.DataTypeName.Equals(item.Name))
                                {
                                    stack.Push(prop.DataType);
                                    reusablesPresent.Add(prop.DataType);
                                }
                            }
                        }
                        output.Append(item.Name + "Properties -> " + item.Name + property.DataTypeName + "[ arrowhead = \"none\" label = \"" + property.Name + "\"] ");
                    }
                }
            }
            if (Inheritance && !string.IsNullOrWhiteSpace(item.ExtendsTypeName))
            {
                output.Append("edge [arrowhead = \"empty\"] ");
                output.Append(item.Name + "Properties ->" + item.ExtendsTypeName + "[ltail=cluster" + item.Name + "] ");
            }
            outerClass.Append("}\"] ");
            foreach(var reused in reusablesPresent)
            {
                outerClass.Append(item.Name + reused.Name + " [ shape = \"record\" fillcolor = \"#fc4a1a\" label=\"{ " + reused.Name + " | ");
                foreach(var property in reused.Properties)
                {
                    outerClass.Append(property.Name + " : " + property.DataTypeName);
                    if (!string.IsNullOrWhiteSpace(property.MinCardinality) && !string.IsNullOrWhiteSpace(property.MaxCardinality))
                    {
                        outerClass.Append("[" + property.MinCardinality + "..." + property.MaxCardinality + "] ");
                    }
                    outerClass.Append("\\l");
                    if (ClassList.Contains(property.DataType) && !property.IsPrimitive)
                    {
                        output.Append("edge[ arrowhead = \"none\" headlabel = \"0...*\" taillabel = \"0...*\"] ");
                        output.Append(item.Name + reused.Name + " -> " + property.DataTypeName + "[ label = \"" + property.Name + "\"] ");
                    }
                    else if (reusablesPresent.Contains(property.DataType))
                    {
                        output.Append(item.Name + reused.Name + " -> " + item.Name + property.DataTypeName + "[ arrowhead = \"none\" label = \"" + property.Name + "\"] ");
                    }
                }
                if (Inheritance && !string.IsNullOrWhiteSpace(reused.ExtendsTypeName))
                {
                    output.Append("edge [arrowhead = \"empty\"] ");
                    output.Append(item.Name + reused.Name + "->" + reused.ExtendsTypeName + " ");
                }
                outerClass.Append("}\"] ");
            }
            outerClass.Append("}");
            output.Append(outerClass);
            return output.ToString();
        }

        private string MakeNode(DataType item, StringBuilder classText)
        {
            StringBuilder outputText = new StringBuilder();
            foreach (var property in item.Properties)
            {
                classText.Append(property.Name + " : " + property.DataTypeName);
                if (!string.IsNullOrWhiteSpace(property.MinCardinality) && !string.IsNullOrWhiteSpace(property.MaxCardinality))
                {
                    classText.Append("[" + property.MinCardinality + "..." + property.MaxCardinality + "] ");
                }
                classText.Append("\\l");
                if (ClassList.Contains(property.DataType))
                {
                   outputText.Append("edge[ arrowhead = \"none\" headlabel = \"0...*\" taillabel = \"0...*\"] ");
                   outputText.Append(item.Name + " -> " + property.DataTypeName + "[ label = \"" + property.Name + "\"] ");
                }
                if (ReusableList.Contains(property.DataType) && ShowReusables)
                {
                    return MakeCluster(item);
                }
            }
            if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName) && Inheritance)
            {
                outputText.Append("edge [arrowhead = \"empty\" headlabel = \"\" taillabel = \"\"] ");
                outputText.Append(item.Name + "->" + item.ExtendsTypeName + " ");
            }
            outputText.Append(classText.ToString() + "}\"] ");
            return outputText.ToString();
        }

        public void MakeGraphAll(CogsModel model, string header)
        {
            var output = new StringBuilder(header);
            foreach (var item in model.ItemTypes)
            {
                if (Inheritance && !String.IsNullOrWhiteSpace(item.ExtendsTypeName))
                {
                    output.Append(item.Name + " -> " + item.ExtendsTypeName + " [arrowhead=\"empty\"]");
                }
                foreach (var property in item.Properties)
                {
                    if (ClassList.Contains(property.DataType))
                    {
                        output.Append(item.Name + " -> " + property.DataTypeName + " ");
                    }
                }
            }
            output.Append("}");
            GenerateOutput("output", output.ToString());
        }

        public void MakeGraphTopic(CogsModel model, string header)
        {
            foreach(var topic in model.TopicIndices)
            {
                var output = new StringBuilder(header);
                Stack<DataType> stack = new Stack<DataType>();
                List<string> seen = new List<string>();
                foreach (var item in topic.ItemTypes)
                {
                    stack.Push(item);
                }
                while(stack.Count > 0)
                {
                    var item = stack.Pop();
                    seen.Add(item.Name);
                    if(Inheritance && !String.IsNullOrWhiteSpace(item.ExtendsTypeName))
                    {
                        output.Append($"{item.Name} -> {item.ExtendsTypeName} [ arrowhead = \"empty\"]");
                    }
                    foreach (var property in item.Properties)
                    {
                        if (ClassList.Contains(property.DataType) && !seen.Contains(property.DataTypeName))
                        {
                            output.Append(item.Name + " -> " + property.DataTypeName + " ");
                            stack.Push(property.DataType);
                        }
                    }
                }
                output.Append("}");
                GenerateOutput(topic.Name, output.ToString());
            }
        }

        public void MakeGraphSingle(CogsModel model, string header)
        {
            int previousOutput = 0;
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                if (previousOutput > item.Name.Length)
                {
                    Console.Write("\rCreating Graph for " + item.Name + string.Join("", Enumerable.Repeat(" ", previousOutput - item.Name.Length)));
                }
                else { Console.Write("\rCreating Graph for " + item.Name); }
                previousOutput = item.Name.Length;
                StringBuilder arrows = new StringBuilder();
                bool isCluster = false;
                if (ShowReusables && item.Properties.Where(x => ReusableList.Contains(x.DataType)).ToList().Count > 0) { isCluster = true; }
                foreach(var clss in model.ItemTypes.Concat(model.ReusableDataTypes))
                {
                    if (Inheritance && clss.ExtendsTypeName.Equals(item.Name))
                    {
                        if (isCluster) { arrows.Append($"{clss.Name} -> {item.Name}Properties [arrowhead=\"empty\" lhead = cluster{item.Name}] "); }
                        else { arrows.Append($"{clss.Name} -> {item.Name} [ arrowhead=\"empty\"] "); }
                    }
                    foreach (var property in clss.Properties)
                    {
                        if (property.DataTypeName.Equals(item.Name))
                        {
                            if (isCluster && clss.Name.Equals(item.Name))
                            {
                                arrows.Append($"{clss.Name}Properties -> {item.Name}Properties [ arrowhead=\"none\" label = {property.Name}] ");
                            }
                            else if (isCluster) { arrows.Append($"{clss.Name} -> {item.Name}Properties [ arrowhead=\"none\" label= {property.Name} lhead = cluster{item.Name}] "); }
                            else { arrows.Append($"{clss.Name} -> {item.Name}[ arrowhead=\"none\" label={property.Name}] "); }
                        }
                    }
                }
                GenerateOutput(item.Name, header + " " + MakeItem(item) + arrows + " }");
            }
        }

        private void GenerateOutput(string filename, string outputText)
        {
            // create text file containing input for graphviz dot
            File.WriteAllText(Path.Combine(TargetDirectory, "input.dot"), outputText);
            // run graphviz dot
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(DotLocation))
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = "-T " + Format + " -o " + Path.Combine(TargetDirectory, filename.Replace(" ", "") + "." + Format) + " " + Path.Combine(TargetDirectory, "input.dot")
            };
            process.StartInfo = startInfo;
            try
            {
                if (process.Start() == false)
                {
                    if (Errors.Where(x => x.Message.Equals("Error launching dot executable")).ToList().Count == 0)
                    {
                        Errors.Add(new CogsError(ErrorLevel.Error, "Error launching dot executable"));
                    }
                    return;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                if (Errors.Where(x => x.Message.Equals("dot executable not found: please check path")).ToList().Count == 0)
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, "dot executable not found: please check path"));
                }
                return;
            }
            process.WaitForExit();

            // delete the intermediate file
            File.Delete(Path.Combine(TargetDirectory, "input.dot"));
            AddShadow(Path.Combine(TargetDirectory, filename.Replace(" ", "") + "." + Format));
        }

        private void AddShadow(string file)
        {
            StringBuilder newFile = new StringBuilder();
            using (StreamReader reader = new StreamReader(file))
            {
                string line;
                bool isFirst = true;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("viewBox"))
                    {
                        newFile.Append(line + Environment.NewLine + "<filter id = \"dropshadow\" height = \"130%\" ><feGaussianBlur in= \"SourceAlpha\" stdDeviation = \"3\"/>" +
                            "<!--stdDeviation is how much to blur--><feOffset dx = \"2\" dy = \"2\" result = \"offsetblur\"/><!--how much to offset-->" +
                            "<feMerge><feMergeNode/><!--this contains the offset blurred image--><feMergeNode in= \"SourceGraphic\"/>" +
                            "<!--this contains the element that the filter is applied to --></feMerge></filter>" + Environment.NewLine);
                    }
                    else if (line.Contains("id=\"node"))
                    {
                        for (int i = 0; i < 7; i++) { newFile.Append(line[i]); }
                        var s = "";
                        for (int i = 12; i < line.Length; i++)
                        {
                            if (s.Contains("<title>"))
                            {
                                var name = "";
                                while (!line[i].Equals('<'))
                                {
                                    name += line[i];
                                    i++;
                                }
                                if (ClassList.Where(x => x.Name == name).ToList().Count > 0 || ClassList.Where(x => x.Name + "Properties" == name).ToList().Count > 0)
                                {
                                    newFile.Append("ItemType" + name);
                                }
                                else { newFile.Append("ReusableType" + name); }
                                while (i < line.Length)
                                {
                                    s += line[i];
                                    i++;
                                }
                            }
                            else { s += line[i]; }
                        }
                        newFile.Append(s);
                    }
                    else if ((line.Contains("polygon") || line.Contains("ellipse")) && !line.Contains("fill=\"none\"") && !line.Contains("fill=\"black\""))
                    {
                        if(!isFirst)
                        {
                            foreach (var chr in line)
                            {
                                if (!chr.Equals('/')) { newFile.Append(chr); }
                                else { newFile.Append(" style=\"filter: url(#dropshadow)\"" + chr); }
                            }
                            newFile.Append(Environment.NewLine);
                        }
                        else { isFirst = false; }
                    }
                    else { newFile.Append(line.ToString() + Environment.NewLine); }
                }
            }
            File.Delete(file);
            File.WriteAllText(file, newFile.ToString());
        }
    }
}