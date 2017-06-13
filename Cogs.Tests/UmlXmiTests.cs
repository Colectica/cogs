using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Cogs.Tests
{
    public class UmlXmiTests
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

            var publisher = new UmlSchemaPublisher();
            publisher.TargetDirectory = outputPath;
            publisher.Publish(cogsModel);


            // TODO use xml importer to check that xml is properly formed.
            // For now we are just making sure there are no errors while running.


        }
    }
}
