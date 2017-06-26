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
    public class SphinxTests
    {
        [Fact]
        public void SphinxForHamburgersTest()
        {
            string path = "..\\..\\..\\..\\cogsburger";

            string subdir = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            string outputPath = Path.Combine(Path.GetTempPath(), subdir);

            var directoryReader = new CogsDirectoryReader();
            var cogsDtoModel = directoryReader.Load(path);

            var modelBuilder = new CogsModelBuilder();
            var cogsModel = modelBuilder.Build(cogsDtoModel);

            var sphinxPublisher = new SphinxPublisher();
            sphinxPublisher.TargetDirectory = outputPath;
            sphinxPublisher.DotLocation = "C:\\Users\\kevin\\Downloads\\graphviz-2.38\\release\\bin";
            sphinxPublisher.Publish(cogsModel);


            // TODO Inspect the sphinx directory to make sure it has some things we expect.
            // For now we are just making sure there are no errors while running.


        }
    }
}
