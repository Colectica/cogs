using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Cogs.Tests
{
    public class JsonSchemaTests
    {
        [Fact]
        public void Jsontest()
        {
            string path = "..\\..\\..\\..\\cogsburger";

            string subdir = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            string outputPath = Path.Combine(Path.GetTempPath(), subdir);

            var directoryReader = new CogsDirectoryReader();
            var cogsDtoModel = directoryReader.Load(path);
            
            var modelBuilder = new CogsModelBuilder();
            var cogsModel = modelBuilder.Build(cogsDtoModel);

            var jsonPublisher = new JsonPublisher();
            jsonPublisher.TargetDirectory = outputPath;
            jsonPublisher.Publish(cogsModel);


            JSchema schema1 = JSchema.Parse(File.ReadAllText(@"C:\Users\clement\Desktop\res.json"));
            JObject o1 = JObject.Parse(File.ReadAllText(@"C:\Users\clement\Desktop\JsonFolder\testing1_reference_reusable.json"));
            JObject o2 = JObject.Parse(File.ReadAllText(@"C:\Users\clement\Desktop\JsonFolder\testing2_reference_Object.json"));
            //read JSON directly from a file
            bool valid1 = o1.IsValid(schema1);
            Assert.True(valid1);
            bool valid2 = o2.IsValid(schema1);
            Assert.True(valid2);
        }
    }
}
