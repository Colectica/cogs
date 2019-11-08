using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using Cogs.Publishers.JsonSchema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

namespace Cogs.Tests
{
    public class JsonSchemaTests
    {
        [Fact]
        public async System.Threading.Tasks.Task asyncJsonSchemaTestAsync()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "cogsburger"); 

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
            var schema = await NJsonSchema.JsonSchema.FromJsonAsync(schemaData);

            string[] tests = new string[]
            {
                "testing1_reference_reusable.json",
                "testing2_reference_Object.json",
                "test3_SimpleType.json",
                //"test4_invalid_json.json",
                "ToDo.json",
                "testing5_more.json",
                "jsonOut.json"
            };
            foreach (var test in tests)
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Cogs.Tests." + test))
                {
                    StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                    string jsonText = reader.ReadToEnd();
                    var result = schema.Validate(jsonText);
                    if (!test.Contains("invalid")) 
                    {                        
                        Assert.Empty(result); 
                    }
                    else 
                    { 
                        Assert.NotEmpty(result); 
                    }
                }
            }
        }

        [Fact]
        public void JsonSchema_Multiple_Properties_Same_Numeric_Type_Multiplicity_1()
        {
            var model = new CogsModel();

            var type = new Model.ItemType();
            type.Name = "Reusable1";
            type.Description = "Description One";

            var property1 = new Model.Property();
            property1.Name = "Property1";
            property1.Description = "Description One";
            property1.DataType = new Model.DataType { Name = "int" };
            property1.MinCardinality = "0";
            property1.MaxCardinality = "1";
            type.Properties.Add(property1);

            var property2 = new Model.Property();
            property2.Name = "Property2";
            property2.Description = "Description Two";
            property2.DataType = new Model.DataType { Name = "int" };
            property2.MinCardinality = "0";
            property2.MaxCardinality = "1";
            type.Properties.Add(property2);


            model.ItemTypes.Add(type);

            var jsonPublisher = new JsonPublisher();
            jsonPublisher.TargetDirectory = Environment.CurrentDirectory;
            jsonPublisher.Publish(model);

            Assert.True(true);
        }
    }
}
