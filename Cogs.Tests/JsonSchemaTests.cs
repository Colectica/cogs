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


            //var schema = File.ReadAllText(@"C:\Users\clement\Desktop\res.json"));
            //var valid1 = schema.Validate();
            //read JSON directly from a file
            //Assert.True(valid1);
            //Assert.True(valid2);
        }
    }
}
