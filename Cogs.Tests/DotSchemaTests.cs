using System;
using System.Collections.Generic;
using System.Text;
using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System.IO;
using Xunit;
using System.Xml;
using System.Xml.Schema;

namespace Cogs.Tests
{
    public class DotSchemaTests
    {
        [Fact]
        public void SvgForHamburgersTest()
        {
            string path = "..\\..\\..\\..\\cogsburger";

            string subdir = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            string outputPath = Path.Combine(Path.GetTempPath(), subdir);

            var directoryReader = new CogsDirectoryReader();
            var cogsDtoModel = directoryReader.Load(path);

            var modelBuilder = new CogsModelBuilder();
            var cogsModel = modelBuilder.Build(cogsDtoModel);

            var choices = new string[3] { "all", "type", "single" };
            for (int i = 0; i < 3; i++) {
                var publisher = new DotSchemaPublisher();
                publisher.TargetDirectory = outputPath;
                publisher.DotLocation = "C:\\Users\\kevin\\Downloads\\graphviz-2.38\\release\\bin";
                publisher.Output = choices[i];
                publisher.Publish(cogsModel);
            }
        }
    }
}
