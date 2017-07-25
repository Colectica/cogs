using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System.IO;
using Xunit;
using System;

namespace Cogs.Tests
{
    public class DotSchemaTests
    {
        [Fact]
        public void SvgForHamburgersTest()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "cogsburger");

            string subdir = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            string outputPath = Path.Combine(Path.GetTempPath(), subdir);

            var directoryReader = new CogsDirectoryReader();
            var cogsDtoModel = directoryReader.Load(path);

            var modelBuilder = new CogsModelBuilder();
            var cogsModel = modelBuilder.Build(cogsDtoModel);

            var choices = new string[3] { "all", "type", "single" };
            for (int i = 0; i < 3; i++) {
                var publisher = new DotSchemaPublisher
                {
                    TargetDirectory = outputPath,
                    Output = choices[i],
                    Format = "svg"
                };
                publisher.Publish(cogsModel);
            }
        }
    }
}
