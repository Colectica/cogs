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

namespace Cogs.Tests
{
    public class SvgSchemaTests
    {
        [Fact]
        public void SvgForHamburgersTest()
        {
            string path = "..\\..\\..\\..\\cogsburger";

            string subdir = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            string outputPath = Path.Combine(Path.GetTempPath(), subdir);

            var directoryReader = new CogsDirectoryReader();
            var cogsDtoModel = directoryReader.Load(path);

            var modelBuilder = new CogsModelBuilder();
            var cogsModel = modelBuilder.Build(cogsDtoModel);

            var publisher = new SvgSchemaPublisher();
            publisher.TargetDirectory = outputPath;
            publisher.DotLocation = "C:\\Users\\kevin\\Downloads\\graphviz-2.38\\release\\bin";
            publisher.Publish(cogsModel);
            // svg schema is being created now but no final version is available yet
        //    Validate(Path.Combine(outputPath, "output.svg"), Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\SVG.xsd"));
        }

        // takes filename of created xml document and filename for schema and validates the schema
        private static void Validate(string filename, string schemaFile)
        {
            // used https://msdn.microsoft.com/en-us/library/system.xml.schema.validationeventargs.severity(v=vs.110).aspx
            Console.WriteLine();
            Console.WriteLine("\r\nValidating XML file {0}...", filename);

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Parse;
            XmlSchemaSet schemaSet = new XmlSchemaSet();
            schemaSet.Add(null, XmlReader.Create(schemaFile, settings));

            XmlSchema compiledSchema = null;

            foreach (XmlSchema schema in schemaSet.Schemas())
            {
                compiledSchema = schema;
            }

            settings = new XmlReaderSettings();
            settings.Schemas.Add(compiledSchema);
            settings.DtdProcessing = DtdProcessing.Parse;
            settings.ValidationEventHandler += new ValidationEventHandler(ValidationCallBack);
            settings.ValidationType = ValidationType.Schema;

            //Create the schema validating reader.
            XmlReader vreader = XmlReader.Create(filename, settings);

            while (vreader.Read()) { }

            //Close the reader.
            vreader.Close();
        }

        //Display any warnings or errors.
        private static void ValidationCallBack(object sender, ValidationEventArgs args)
        {
            if (args.Severity == XmlSeverityType.Warning)
                Console.WriteLine("\tWarning: Matching schema not found.  No validation occurred." + args.Message);
            else
            {
                Console.WriteLine("\tValidation error: " + args.Message);
                Assert.False(true);
            }
        }
    }
}
