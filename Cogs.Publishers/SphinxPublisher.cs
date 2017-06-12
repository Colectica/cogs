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

            var builder = new BuildSphinxDocumentation();
            builder.Build(model, TargetDirectory);
        }
    }
}
