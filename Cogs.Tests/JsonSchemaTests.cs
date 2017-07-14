using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
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
        public async System.Threading.Tasks.Task asyncJsonSchemaTestAsync()
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

            var jsondata1 = File.ReadAllText(@"C:\Users\clement\Documents\GitHub\cogs\Cogs.Tests.Console\testing1_reference_reusable.json");
            var jsondata2 = File.ReadAllText(@"C:\Users\clement\Documents\GitHub\cogs\Cogs.Tests.Console\testing2_reference_Object.json");
            var jsondata3 = File.ReadAllText(@"C:\Users\clement\Documents\GitHub\cogs\Cogs.Tests.Console\test3_SimpleType.json");
            var jsondata4 = File.ReadAllText(@"C:\Users\clement\Documents\GitHub\cogs\Cogs.Tests.Console\test4_invalid_json.json");
            var jsondata5 = File.ReadAllText(@"C:\Users\clement\Documents\GitHub\cogs\Cogs.Tests.Console\ToDo.json");
            var jsondata6 = File.ReadAllText(@"C:\Users\clement\Documents\GitHub\cogs\Cogs.Tests.Console\jsonOut.json");
            var jsondata7 = File.ReadAllText(@"C:\Users\clement\Documents\GitHub\cogs\Cogs.Tests.Console\testing5_more.json");

            var validate1 = schema.Validate(jsondata1);
            var validate2 = schema.Validate(jsondata2);
            var validate3 = schema.Validate(jsondata3);
            var validate4 = schema.Validate(jsondata4);
            var validate5 = schema.Validate(jsondata5);
            var validate6 = schema.Validate(jsondata6);
            var validate7 = schema.Validate(jsondata7);

            Assert.Empty(validate1);
            Assert.Empty(validate2);
            Assert.Empty(validate3);
            Assert.NotEmpty(validate4);
            Assert.Empty(validate5);
            Assert.Empty(validate6);
            Assert.Empty(validate7);
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
            jsonPublisher.TargetDirectory = @"c:\out\test-cogs";
            jsonPublisher.Publish(model);

        }
    }
}
