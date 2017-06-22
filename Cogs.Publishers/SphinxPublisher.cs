// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cogs.Publishers
{
    public class SphinxPublisher
    {
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }
        public string DotLocation { get; set; }

        public void Publish(CogsModel model)
        {
            if (TargetDirectory == null)
            {
                throw new InvalidOperationException("Target directory must be specified");
            }

            if (Overwrite && Directory.Exists(TargetDirectory))
            {
                Directory.Delete(TargetDirectory, true);
                System.Threading.Thread.Sleep(1000);
            }

            Directory.CreateDirectory(TargetDirectory);
            // create graphs for each item
            var builder = new DotSchemaPublisher();
            builder.DotLocation = DotLocation;
            builder.TargetDirectory = TargetDirectory;
            builder.Overwrite = Overwrite;
            builder.Format = "svg";
            builder.Output = "single";
            builder.Publish(model);
            // create documentation
            var doc = new BuildSphinxDocumentation();
            doc.Build(model, TargetDirectory);
        }
    }
}
