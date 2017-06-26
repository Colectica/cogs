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
        /// path to dot.exe file
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

        private List<string> ClassList { get; set; }
        private List<DataType> ReusableList { get; set; }

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

            // create list of all class names so you know if a class is being referenced
            ClassList = new List<string>();
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                ClassList.Add(item.Name);
            }
            // create list of all reusable types so you know if a reusable type is being referenced and can get information about it
            ReusableList = new List<DataType>();
            foreach (var item in model.ReusableDataTypes)
            {
                ReusableList.Add(item);
            }
            var header = "digraph G { compound = true rankdir=LR fontsize = 8 node [ " +
               "fontsize = 8 shape = \"oval\"] edge [ fontsize = 8 ] ";

            if (Output.Equals("all")) { MakeGraphAll(model, header); }
            else if (Output.Equals("topic")) { MakeGraphTopic(model, header); }
            else { MakeGraphSingle(model, header); }
        }

        private string MakeItem(DataType item)
        {
            return MakeNode(item, item.Name + "[ shape = \"record\" color = blue label=\"{" + item.Name + "| ");
        }

        private string MakeCluster(DataType item, DataType reusable)
        {
            StringBuilder output = new StringBuilder();
            StringBuilder outerClass = new StringBuilder("subgraph cluster" + item.Name + " { color = blue label=\"" + item.Name + "\" ");
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
                if (ClassList.Contains(property.DataTypeName))
                {
                    if (ReusableList.Contains(property.DataType))
                    {
                        // add reusableTypes within that type to reusable list as well
                        Stack<DataType> stack = new Stack<DataType>();
                        stack.Push(property.DataType);
                        reusablesPresent.Add(property.DataType);
                        while(stack.Count > 0)
                        {
                            var type = stack.Pop();
                            foreach (var prop in type.Properties)
                            {
                                // checks: item is a reusable type, item is not already seen, item isn't current item, item isn't primitive
                                if (ReusableList.Contains(prop.DataType) && !reusablesPresent.Contains(type) &&
                                    !type.Name.Equals(prop.DataTypeName) && !prop.IsPrimitive && !prop.DataTypeName.Equals(item.Name))
                                {
                                    stack.Push(prop.DataType);
                                    reusablesPresent.Add(prop.DataType);

                                }
                            }
                        }
                    }
                    else
                    {
                        output.Append("edge[ arrowhead = \"none\" headlabel = \"0...*\" taillabel = \"0...*\"] ");
                    }
                    output.Append(item.Name + "Properties -> " + item.Name + property.DataTypeName + "[ arrowhead = \"none\" label = \"" + property.Name + "\"] ");
                }
            }
            if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName) && Inheritance)
            {
                output.Append("edge [arrowhead = \"empty\"] ");
                output.Append(item.Name + "Properties ->" + item.ExtendsTypeName + "[ltail=cluster" + item.Name + "] ");
            }
            outerClass.Append("}\"] ");
            foreach(var reused in reusablesPresent)
            {
                outerClass.Append(item.Name + reused.Name + " [ shape = \"record\" label=\"{ " + reused.Name + " | ");
                foreach(var property in reused.Properties)
                {
                    outerClass.Append(property.Name + " : " + property.DataTypeName);
                    if (!string.IsNullOrWhiteSpace(property.MinCardinality) && !string.IsNullOrWhiteSpace(property.MaxCardinality))
                    {
                        outerClass.Append("[" + property.MinCardinality + "..." + property.MaxCardinality + "] ");
                    }
                    outerClass.Append("\\l");
                    if (ClassList.Contains(property.DataTypeName))
                    {
                        if (reusablesPresent.Contains(property.DataType))
                        {
                            output.Append(item.Name + reused.Name + " -> " + item.Name + property.DataTypeName + "[ arrowhead = \"none\" label = \"" + property.Name + "\"] ");
                        }
                        else if (!property.IsPrimitive)
                        {
                            output.Append("edge[ arrowhead = \"none\" headlabel = \"0...*\" taillabel = \"0...*\"] ");
                            output.Append(item.Name + reused.Name + " -> " + property.DataTypeName + "[ label = \"" + property.Name + "\"] ");
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(reused.ExtendsTypeName) && Inheritance)
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

        private string MakeNode(DataType item, string classText)
        {
            StringBuilder outputText = new StringBuilder();
            foreach (var property in item.Properties)
            {
                classText += property.Name + " : " + property.DataTypeName;
                if (!string.IsNullOrWhiteSpace(property.MinCardinality) && !string.IsNullOrWhiteSpace(property.MaxCardinality))
                {
                    classText += "[" + property.MinCardinality + "..." + property.MaxCardinality + "] ";
                }
                classText += "\\l";
                if (ClassList.Contains(property.DataTypeName))
                {
                    if (ReusableList.Contains(property.DataType))
                    {
                        return MakeCluster(item, property.DataType);
                    }
                    else
                    {
                        outputText.Append("edge[ arrowhead = \"none\" headlabel = \"0...*\" taillabel = \"0...*\"] ");
                    }
                    outputText.Append(item.Name + " -> " + property.DataTypeName + "[ label = \"" + property.Name + "\"] ");
                }
            }
            if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName) && Inheritance)
            {
                outputText.Append("edge [arrowhead = \"empty\" headlabel = \"\" taillabel = \"\"] ");
                outputText.Append(item.Name + "->" + item.ExtendsTypeName + " ");
            }
            outputText.Append(classText + "}\"] ");
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
                    if (ClassList.Contains(property.DataTypeName))
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
                        output.Append(item.Name + " -> " + item.ExtendsTypeName + " [arrowhead=\"empty\"]");
                    }
                    foreach (var property in item.Properties)
                    {
                        if (ClassList.Contains(property.DataTypeName) && !seen.Contains(property.DataTypeName))
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
                var arrows = "";
                bool ifCluster = false;
                if (item.Properties.Where(x => ReusableList.Contains(x.DataType)).ToList().Count > 0) ifCluster = true;
                foreach(var clss in model.ItemTypes.Concat(model.ReusableDataTypes))
                {
                    if (clss.ExtendsTypeName.Equals(item.Name) && Inheritance)
                    {
                        if (ifCluster) { arrows += clss.Name + " -> " + item.Name + "Properties" + "[arrowhead=\"empty\" lhead = cluster" + item.Name + "] "; }
                        else arrows += clss.Name + " -> " + item.Name + "[arrowhead=\"empty\"] ";


                    }
                    foreach (var property in clss.Properties)
                    {
                        if (property.DataTypeName.Equals(item.Name))
                        {
                            if (clss.Name.Equals(item.Name) && ifCluster)
                            {
                                arrows += clss.Name + "Properties -> " + item.Name + "Properties [ arrowhead=\"none\" label=" + property.Name + " ] ";
                            } 
                            else if (ifCluster) arrows += clss.Name + " -> " + item.Name + "Properties [ arrowhead=\"none\" label=" + property.Name + " lhead = cluster" + item.Name + "] ";
                            else arrows += clss.Name + " -> " + item.Name + "[ arrowhead=\"none\" label=" + property.Name + "] ";
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
            ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(DotLocation, "dot.exe"));
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = "-T " + Format + " -o " + Path.Combine(TargetDirectory, filename.Replace(" ", "") + "." + Format) + " " + Path.Combine(TargetDirectory, "input.dot");
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();

            // delete the intermediate file
            File.Delete(Path.Combine(TargetDirectory, "input.dot"));
        }
    }
}