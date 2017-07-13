using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System.IO;
using Xunit;
using System.Diagnostics;
using System.Reflection;
using System.IO.Compression;
using System;

namespace Cogs.Tests
{
    public class CsSchemaTests
    {

        [Fact]
        public void CsForHamburgersTest()
        {
            string path = Path.Combine(Path.Combine(Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), ".."), ".."), ".."), "..");
            path = Path.Combine(path, "cogsburger");

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
            string dotLoc = null;
            if (File.Exists("dotnet.exe")) { dotLoc = Path.GetFullPath("dotnet.exe"); }
            else
            {
                var values = Environment.GetEnvironmentVariable("PATH");
                foreach (var exe in values.Split(Path.PathSeparator))
                {
                    var fullPath = Path.Combine(exe, "dotnet.exe");
                    if (File.Exists(fullPath)) { dotLoc = fullPath; }
                }
            }
            if (dotLoc == null) { throw new InvalidOperationException(); }

            Build("cogsBurger", outputPath, dotLoc);
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
