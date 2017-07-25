using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System;
using System.IO;
using Xunit;

namespace Cogs.Tests
{
    public class SphinxTests
    {
        [Fact]
        public void SphinxForHamburgersTest()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "cogsburger");

            string subdir = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            string outputPath = Path.Combine(Path.GetTempPath(), subdir);

            var directoryReader = new CogsDirectoryReader();
            var cogsDtoModel = directoryReader.Load(path);

            var modelBuilder = new CogsModelBuilder();
            var cogsModel = modelBuilder.Build(cogsDtoModel);

            var sphinxPublisher = new SphinxPublisher
            {
                TargetDirectory = outputPath
            };
            sphinxPublisher.Publish(cogsModel);


            // TODO Inspect the sphinx directory to make sure it has some things we expect.
            // For now we are just making sure there are no errors while running.


        }
    }
}
