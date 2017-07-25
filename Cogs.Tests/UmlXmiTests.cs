using System;
using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System.IO;
using Xunit;
using System.Xml;
using System.Xml.Schema;
using System.Reflection;

namespace Cogs.Tests
{
    public class UmlXmiTests
    {
        [Fact]
        public void UmlForHamburgersTest()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "cogsburger");

            string subdir = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            string outputPath = Path.Combine(Path.GetTempPath(), subdir);

            var directoryReader = new CogsDirectoryReader();
            var cogsDtoModel = directoryReader.Load(path);

            var modelBuilder = new CogsModelBuilder();
            var cogsModel = modelBuilder.Build(cogsDtoModel);

            // test both normative and not normative outputs
            var publisher = new UmlSchemaPublisher
            {
                TargetDirectory = outputPath,
                Normative = false
            };
            publisher.Publish(cogsModel);
            // test with normative since 2.5 does not have a xsd schema yet
            Validate(Path.Combine(outputPath, "uml.xmi.xml"));

            publisher = new UmlSchemaPublisher
            {
                TargetDirectory = outputPath,
                Normative = true
            };
            publisher.Publish(cogsModel);
            // not working yet
            Validate(Path.Combine(outputPath, "uml.xmi.xml"));
        }


        // takes filename of created xml document and filename for schema and validates the schema
        private static void Validate(string filename)
        {
            Console.WriteLine();
            Console.WriteLine("\r\nValidating XML file {0}...", filename);

            XmlSchemaSet schemaSet = new XmlSchemaSet();
            //get schema
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Cogs.Tests.normativeXMI.xsd"))
            {
                schemaSet.Add(null, XmlReader.Create(stream));
            }
            

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
            else if (args.Message.ToLower().Contains("uml"))
                // ignore uml issues
                return;
            else
            {
                var x = sender.ToString();
                Console.WriteLine("\tValidation error: " + args.Message);
                Assert.False(true);
            }
        }
    }
}
