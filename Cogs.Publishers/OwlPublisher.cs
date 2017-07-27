// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Model;
using Cogs.Publishers;
using System;
using System.IO;

namespace Cogs.Console
{
    public class OwlPublisher : GraphQLPublisher
    {
        public string CogsLocation { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }

        public string TargetNamespace { get; set; } = "ddi:3_4";

        public void Publish(CogsModel model)
        {
            //if (CogsLocation == null)
            //{
            //    throw new InvalidOperationException("Cogs location must be specified");
            //}
            if (TargetDirectory == null)
            {
                throw new InvalidOperationException("Target directory must be specified");
            }

            if (Overwrite && Directory.Exists(TargetDirectory))
            {
                Directory.Delete(TargetDirectory, true);
            }

            //Start here
        }
    }
}