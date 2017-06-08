// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Model;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cogs.Publishers
{
    /// <summary>
    /// Generate an uml schema using the Garden of Eden approach, all elements and type definitions are declared globally
    /// </summary>
    public class UmlSchemaPublisher
    {
        public string CogsLocation { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }

        public string TargetNamespace { get; set; } = "ddi:3_4";

        Dictionary<string, string> createdElements = new Dictionary<string, string>();

        public void Publish(CogsModel model)
        {
            if (CogsLocation == null)
            {
                throw new InvalidOperationException("Cogs location must be specified");
            }
            if (TargetDirectory == null)
            {
                throw new InvalidOperationException("Target directory must be specified");
            }
            if (Overwrite && Directory.Exists(TargetDirectory))
            {
                Directory.Delete(TargetDirectory, true);
            }
            Directory.CreateDirectory(TargetDirectory);


            //create UML header
           

            // create built in types
            



            // initialize data structure to hold all initialized objects
            foreach (var item in model.ItemTypes)
            {
                //create new variable


                //add to collection

            }

            //write collection to file



        }
    }
}