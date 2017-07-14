// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
            var builder = new DotSchemaPublisher
            {
                TargetDirectory = Path.Combine(Path.Combine(TargetDirectory, "source"), "images"),
                DotLocation = DotLocation,
                Overwrite = Overwrite,
                Format = "svg",
                Output = "single",
                Inheritance = false
            };
            builder.Publish(model);
            // create documentation
            var doc = new BuildSphinxDocumentation();
            doc.Build(model, TargetDirectory);
            //copy over image css file
            var path = Path.Combine(Path.Combine(Path.Combine(Path.Combine(TargetDirectory, "build"), "html"), "_static"), "css");
            Directory.CreateDirectory(path);
            Assembly.GetExecutingAssembly().GetManifestResourceStream("Cogs.Publishers.image.css").CopyTo(new FileStream(Path.Combine(path, "image.css"), FileMode.Create));
        }
    }
}
