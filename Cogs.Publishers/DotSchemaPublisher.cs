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

        private List<string> ClassList { get; set; }
        private List<string> ReusableList { get; set; }

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

            // create list of all classes so you know if a class is being referenced
            ClassList = new List<string>();
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                ClassList.Add(item.Name);
            }
            // create list of all reusable types so you know if a reusable type is being referenced
            ReusableList = new List<string>();
            foreach (var item in model.ReusableDataTypes)
            {
                ReusableList.Add(item.Name);
            }
            var header = "digraph G { fontname = \"Bitstream Vera Sans\" fontsize = 8 node [fontname = \"Bitstream Vera Sans\" " +
               "fontsize = 8 shape = \"record\"] edge [ fontname = \"Bitstream Vera Sans\" fontsize = 8 ] ";

            if (Output.Equals("all")) MakeGraphAll(model, header);
            else if (Output.Equals("topic")) MakeGraphTopic(model, header);
            else MakeGraphSingle(model, header);
        }

        private string MakeItem(DataType item)
        {
            var outputText = new StringBuilder();
            var classText = item.Name + "[ label=\"{" + item.Name + "| ";
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
                    if (ReusableList.Contains(property.DataTypeName))
                    {
                       // return MakeImbededNode(item);
                        outputText.Append("edge[ arrowhead = \"none\" headlabel = \"0...1\" taillabel = \"0...1\"] ");
                    }
                    else
                    {
                        outputText.Append("edge[ arrowhead = \"none\" headlabel = \"0...*\" taillabel = \"0...*\"] ");
                    }
                    outputText.Append(item.Name + " -> " + property.DataTypeName + "[ label = \"" + property.Name + "\"] ");
                }
            }
            if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
            {
                outputText.Append("edge [arrowhead = \"empty\"] ");
                outputText.Append(item.Name + "->" + item.ExtendsTypeName + " ");
            }
            outputText.Append(classText + "}\"] ");
            return outputText.ToString();
        }

        private string MakeImbededNode(DataType item)
        {
            var outputText = new StringBuilder();
            var classText = "subgraph " + item.Name + "{ label=\"" + item.Name + "| ";
            var innerClass = "";
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
                    if (ReusableList.Contains(property.DataTypeName))
                    {
                        innerClass = property.DataTypeName + "[ label=\"{" + property.DataTypeName + "| ";
                        // create class for reusable type (maybe have reusableList contain actual classes rather than just names)
                       // foreach(var prop in property.DataType)
                    }
                    else
                    {
                        outputText.Append("edge[ arrowhead = \"none\" headlabel = \"0...*\" taillabel = \"0...*\"] ");
                    }
                    outputText.Append(item.Name + " -> " + property.DataTypeName + "[ label = \"" + property.Name + "\"] ");
                }
            }
            if (!string.IsNullOrWhiteSpace(item.ExtendsTypeName))
            {
                outputText.Append("edge [arrowhead = \"empty\"] ");
                outputText.Append(item.Name + "->" + item.ExtendsTypeName + " ");
            }
            outputText.Append(classText + "}\"] ");
            return outputText.ToString();
        }

        public void MakeGraphAll(CogsModel model, string header)
        {
            var output = new StringBuilder(header);
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                output.Append(MakeItem(item));
            }
            output.Append("}");
            GenerateOutput("output", output.ToString());
        }

        public void MakeGraphTopic(CogsModel model, string header)
        {
            foreach(var topic in model.TopicIndices)
            {
                var output = new StringBuilder(header);
                foreach(var item in topic.ItemTypes)
                {
                    output.Append(MakeItem(item));
                }
                output.Append("}");
                GenerateOutput(topic.Name, output.ToString());
            }
        }

        public void MakeGraphSingle(CogsModel model, string header)
        {
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                GenerateOutput(item.Name, header + " " + MakeItem(item) + " }");
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