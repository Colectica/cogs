using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System.IO;
using Xunit;
using System.Diagnostics;
using System.Reflection;
using System.IO.Compression;

namespace Cogs.Tests
{
    public class CsSchemaTests
    {

        [Fact]
        public void CsForHamburgersTest()
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

            var publisher = new CsSchemaPublisher();
            publisher.TargetDirectory = outputPath;
            publisher.Publish(cogsModel);

            //set up executable
            using (Stream resFilestream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Cogs.Tests.dotnet.exe"))
            {
                byte[] b = new byte[resFilestream.Length];
                resFilestream.Read(b, 0, b.Length);
                File.WriteAllBytes(Path.Combine(outputPath, "dotnet.exe"), b);
            }
            Build("cogsBurger", outputPath);
            Directory.Delete(testDir, true);
        }

        // builds the created project
        private void Build(string filename, string outputPath)
        {

            Run(outputPath, "restore " + Path.Combine(outputPath, filename + ".csproj"));
            Run(outputPath, "build " + Path.Combine(outputPath, filename + ".csproj"));
        }

         private void Run(string path, string arguments)
        {

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(path, "dotnet.exe"),
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
