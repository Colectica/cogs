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
using System.Diagnostics;


namespace Cogs.Tests
{
    public class CsSchemaTests
    {
        [Fact]
        public void CsForHamburgersTest()
        {
            string path = "..\\..\\..\\..\\cogsburger";

            string subdir = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            string outputPath = Path.Combine(Path.GetTempPath(), subdir);

            var directoryReader = new CogsDirectoryReader();
            var cogsDtoModel = directoryReader.Load(path);

            var modelBuilder = new CogsModelBuilder();
            var cogsModel = modelBuilder.Build(cogsDtoModel);

            var publisher = new CsSchemaPublisher();
            publisher.TargetDirectory = outputPath;
            publisher.Publish(cogsModel);
            Build("cogsBurger", outputPath);
        }

        // builds the created project
        public void Build(string filename, string outputPath)
        {
            Run(@"C:\Program Files\dotnet\dotnet.exe", "restore " + Path.Combine(outputPath, filename + ".csproj"));
            Run(@"C:\Program Files\dotnet\dotnet.exe", "build " + Path.Combine(outputPath, filename + ".csproj"));
        }
         public void Run(string exeLocation, string arguments)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exeLocation,
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
                if (line.Equals("Build FAILED.")) Assert.False(true);
            }
        }
    }
}
