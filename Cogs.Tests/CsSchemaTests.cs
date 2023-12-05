using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers.Csharp;
using System.IO;
using Xunit;
using System.Diagnostics;
using System;

namespace Cogs.Tests
{
    public class CsSchemaTests
    {

        [Fact]
        public void CsForHamburgersTest()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "cogsburger");

            string subdir = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            string outputPath = Path.Combine(Path.GetTempPath(), subdir);

            var directoryReader = new CogsDirectoryReader();
            var cogsDtoModel = directoryReader.Load(path);

            var modelBuilder = new CogsModelBuilder();
            var cogsModel = modelBuilder.Build(cogsDtoModel);

            var publisher = new CsSchemaPublisher
            {
                TargetDirectory = outputPath
            };
            publisher.Publish(cogsModel);

            // get the dotnet filepath
            Build("cogsburger", outputPath, "dotnet");
        }

        // builds the created project
        private void Build(string filename, string outputPath, string dotnet)
        {

            Run(dotnet, "restore " + Path.Combine(outputPath, filename + ".csproj"));
            Run(dotnet, "build " + Path.Combine(outputPath, filename + ".csproj"));
        }

         private void Run(string path, string arguments)
        {

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            while (!proc.StandardOutput.EndOfStream)
            {
                string line = proc.StandardOutput.ReadLine();
                Debug.WriteLine(line);
                if (line.Equals("Build FAILED.")) { Assert.False(true); }
            }
        }
    }
}
