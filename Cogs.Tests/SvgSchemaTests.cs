using System;
using System.Collections.Generic;
using System.Text;
using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System.IO;
using Xunit;

namespace Cogs.Tests
{
    public class SvgSchemaTests
    {
        [Fact]
        public void UmlForHamburgersTest()
        {
            string path = "..\\..\\..\\..\\cogsburger";

            string subdir = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            string outputPath = Path.Combine(Path.GetTempPath(), subdir);

            var directoryReader = new CogsDirectoryReader();
            var cogsDtoModel = directoryReader.Load(path);

            var modelBuilder = new CogsModelBuilder();
            var cogsModel = modelBuilder.Build(cogsDtoModel);

            var publisher = new SvgSchemaPublisher();
            publisher.TargetDirectory = outputPath;
            publisher.DotLocation = "C:\\Users\\kevin\\Downloads\\graphviz-2.38\\release\\bin";
            publisher.Publish(cogsModel);

            // TODO use xml importer to check that svg is properly formed.
            // For now we are just making sure there are no errors while running.


        }
    }
}
