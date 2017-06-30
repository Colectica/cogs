using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
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

            var schemaData = File.ReadAllText(Path.Combine(outputPath, "jsonSchema" + ".json"));

            //var schema = await JsonSchema4.FromFileAsync(Path.Combine(outputPath, "jsonSchema" + ".json"));

            var schema = await JsonSchema4.FromJsonAsync(schemaData);
            //schema = await JsonSchema4.FromJsonAsync(schemaData);
            var jsondata = File.ReadAllText(@"testing1_reference_reusable.json");
            //var validator = new JsonSchemaValidator();
            //ICollection<ValidationError> validate = validator.Validate(jsondata, schema);
            var validate = schema.Validate(jsondata);
            //var validate = schema.Validate(jsondata);

            if (validate == null)
            {
                System.Console.WriteLine("JSON Match");
            }
            else
            {
                System.Console.WriteLine("JSON does not match");
            }
        }
    }
}
