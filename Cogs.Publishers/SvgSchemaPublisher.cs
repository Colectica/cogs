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
    public class SvgSchemaPublisher
    {
        /// <summary>
        /// path to write output in
        /// </summary>
        public string TargetDirectory { get; set; }
        /// <summary>
        /// boolean to determine whether to replace existing or not
        /// </summary>
        public bool Overwrite { get; set; }

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
            var classList = new List<string>();
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                classList.Add(item.Name);
            }
            // create list of all reusable types so you know if a reusable type is being referenced
            var reusableList = new List<string>();
            foreach (var item in model.ReusableDataTypes)
            {
                reusableList.Add(item.Name);
            }
            var outputText = "digraph G { fontname = \"Bitstream Vera Sans\" fontsize = 8 node [fontname = \"Bitstream Vera Sans\" " +
                "fontsize = 8 shape = \"record\"] edge [ fontname = \"Bitstream Vera Sans\" fontsize = 8 ] ";

            // loop through classes and reusable data types
            foreach (var item in model.ItemTypes.Concat(model.ReusableDataTypes))
            {
                // Create class
                var classText = item.Name + "[ label=\"{" + item.Name + "| ";

                // add class properties
                foreach (var property in item.Properties)
                {
                    classText += property.Name + " : " + property.DataTypeName;
                    if (!string.IsNullOrWhiteSpace(property.MinCardinality) && !string.IsNullOrWhiteSpace(property.MaxCardinality))
                    {
                        classText += "[" + property.MinCardinality + "..." + property.MaxCardinality + "] ";
                    }
                    classText += "\\l";
                    // check for association
                    if(classList.Contains(property.DataTypeName))
                    {
                        if (reusableList.Contains(property.DataTypeName))
                        {
                            outputText += "edge[ arrowhead = \"none\" headlabel = \"0..1\" taillabel = \"0..1\"] ";
                        }
                        else
                        {
                            outputText += "edge[ arrowhead = \"none\" headlabel = \"0..*\" taillabel = \"0..*\"] ";
                        }
                        outputText += item.Name + " -> " + property.DataTypeName + "[ label = \"" + property.Name + "\"] ";
                    }
                }
                if(!string.IsNullOrWhiteSpace(item.ExtendsTypeName)){
                    outputText += "edge [arrowhead = \"empty\"] ";
                    outputText += item.Name + "->" + item.ExtendsTypeName + " ";
                }
                outputText += classText + "}\"] ";
            }
            outputText += "}";

            File.WriteAllText(Path.Combine(TargetDirectory, "input.dot"), outputText);
        }
    }
}