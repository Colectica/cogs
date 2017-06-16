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
    public class UmlXmiTests
    {
        [Fact]
        public void UmlForHamburgersTest()
        {
            string path = "..\\..\\..\\..\\cogsburger";

            string subdir = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            string outputPath = Path.Combine(Path.GetTempPath(), subdir);

            var directoryReader = new CogsDirectoryReader();
            var cogsDtoModel = directoryReader.Load(path);

            var modelBuilder = new CogsModelBuilder();
            var cogsModel = modelBuilder.Build(cogsDtoModel);

            // test both normative and not normative outputs
            var publisher = new UmlSchemaPublisher();
            publisher.TargetDirectory = outputPath;
            publisher.Normative = false;
            publisher.DotLocation = @"C:\Users\kevin\Downloads\graphviz-2.38\release\bin";
            publisher.Publish(cogsModel);
            // test with normative since 2.5 does not have a xsd schema yet
            Validate(Path.Combine(outputPath, "uml.xmi.xml"), Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\normativeXMI.xsd"));

            publisher = new UmlSchemaPublisher();
            publisher.TargetDirectory = outputPath;
            publisher.Normative = true;
            publisher.Publish(cogsModel);
            // not working yet
        //    Validate(Path.Combine(outputPath, "uml.xmi.xml"), Path.Combine(Directory.GetCurrentDirectory(), "..\\..\\..\\normativeXMI.xsd"));
        }


        // takes filename of created xml document and filename for schema and validates the schema
        private static void Validate(string filename, string schemaFile)
        {
            // used https://msdn.microsoft.com/en-us/library/system.xml.schema.validationeventargs.severity(v=vs.110).aspx
            Console.WriteLine();
            Console.WriteLine("\r\nValidating XML file {0}...", filename);

            XmlSchemaSet schemaSet = new XmlSchemaSet();
            schemaSet.Add(null, schemaFile);

            XmlSchema compiledSchema = null;

            foreach (XmlSchema schema in schemaSet.Schemas())
            {
                compiledSchema = schema;
            }

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.Schemas.Add(compiledSchema);
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
