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
            string path = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), ".."), ".."), ".."), "..");
            path = Path.Combine(path, "cogsburger");

            string dotLoc = null;
            if (File.Exists("dot.exe")) { dotLoc = Path.GetFullPath("dot.exe"); }
            else
            {
                var values = Environment.GetEnvironmentVariable("PATH");
                foreach (var exe in values.Split(Path.PathSeparator))
                {
                    var fullPath = Path.Combine(exe, "dot.exe");
                    if (File.Exists(fullPath)) { dotLoc = exe; }
                }
            }
            if (dotLoc == null) { throw new InvalidOperationException(); }

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
                    DotLocation = dotLoc,
                    Output = choices[i],
                    Format = "svg"
                };
                publisher.Publish(cogsModel);
            }
        }
    }
}
