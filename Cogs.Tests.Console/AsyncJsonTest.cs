using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using Newtonsoft.Json;
using NJsonSchema;
using NJsonSchema.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cogs.Tests.Console
{
    public class AsyncJsonTest
    {
        public static async System.Threading.Tasks.Task MainAsync()
        {
            string path = @"C:\Users\clement\Documents\GitHub\cogs\cogsburger";

            string subdir = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            string outputPath = Path.Combine(Path.GetTempPath(), subdir);

            var directoryReader = new CogsDirectoryReader();
            var cogsDtoModel = directoryReader.Load(path);

            var modelBuilder = new CogsModelBuilder();
            var cogsModel = modelBuilder.Build(cogsDtoModel);

            var jsonPublisher = new JsonPublisher();
            jsonPublisher.TargetDirectory = outputPath;
            jsonPublisher.Publish(cogsModel);

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            };
            var schemaData = File.ReadAllText(Path.Combine(outputPath, "jsonSchema" + ".json"));
            var schema = await JsonSchema4.FromJsonAsync(schemaData);

            var jsondata1 = File.ReadAllText(@"testing1_reference_reusable.json");
            var jsondata2 = File.ReadAllText(@"testing2_reference_Object.json");
            var jsondata4 = File.ReadAllText(@"test4_invalid_json.json");

            var validate1 = schema.Validate(jsondata1);
            var validate2 = schema.Validate(jsondata2);
            var validate4 = schema.Validate(jsondata4);


            foreach (var error in validate1)
            {
                System.Console.WriteLine(error);
            }
            System.Console.WriteLine("JSON 1 validation done");
            foreach (var error in validate2)
            {
                System.Console.WriteLine(error);
            }
            System.Console.WriteLine("JSON 2 validation done");
            foreach (var error in validate4)
            {
                System.Console.WriteLine(error);
            }
            System.Console.WriteLine("JSON 4 validation done");
        }
    }
}
