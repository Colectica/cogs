using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System.IO;
using Xunit;
using System.Reflection;
using System.IO.Compression;
using System;

namespace Cogs.Tests
{
    public class DotSchemaTests
    {
        [Fact]
        public void SvgForHamburgersTest()
        {
            var testDir = Path.Combine(Directory.GetCurrentDirectory(), "testing");
            Directory.CreateDirectory(Path.Combine(testDir, "temp"));

            string path = null;
            using (Stream resFilestream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Cogs.Tests.cogsburger.zip"))
            {
                path = Path.Combine(testDir, "cogsburger");
                var temp = Path.Combine(Path.Combine(testDir, "temp"), "cogsburger.zip");
                using (var stream = new FileStream(path + ".zip", FileMode.Create)) { resFilestream.CopyTo(stream); }
                ZipFile.ExtractToDirectory(path + ".zip", path);
            };

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
                publisher.Output = choices[i];
                publisher.Format = "svg";
                publisher.Publish(cogsModel);
            }
            Directory.Delete(testDir, true);
        }
    }
}
