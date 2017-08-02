using cogsBurger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Xunit;
using Cogs.SimpleTypes;

namespace Cogs.Tests.Integration
{
    public class XmlIntegrationTests
    {
        [Fact]
        public async void SimpleTypeGYearMonth()
        {
            ItemContainer container = new ItemContainer();
            Bread bread = new Bread
            {
                ID = Guid.NewGuid().ToString(),
                Gyearmonth = new GYearMonth(2009, 10, "-06:00")
            };
            container.Items.Add(bread);
            
            // validate xml
            XDocument doc = container.MakeXml();
            XmlValidation(doc);
        }



        private void XmlValidation(XDocument doc)
        {
            XmlSchemaSet schemas = GetXmlSchema();

            List<ValidationEventArgs> errors = new List<ValidationEventArgs>();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ValidationType = ValidationType.Schema;
            settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
            settings.Schemas = schemas;
            settings.ValidationEventHandler += (o, e) => 
            {
                Console.WriteLine("{0}", e.Message);
                errors.Add(e);
            };

            using (XmlReader xr = XmlReader.Create(doc.CreateReader(), settings))
            {
                while (xr.Read()) { }
            }
            Assert.Empty(errors);
        }

        private static void ValidationCallback(object sender, ValidationEventArgs args)
        {
            if (args.Severity == XmlSeverityType.Error) { Assert.True(false); }
            else if (args.Severity == XmlSeverityType.Warning) { Assert.True(false); }
        }

        private XmlSchemaSet GetXmlSchema()
        {
            // TODO build the json schema into the generated assembly as a resource
            string schemaPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "generated", "schema.xsd");
            

            XmlSchemaSet xmlSchemaSet = new XmlSchemaSet();
            xmlSchemaSet.ValidationEventHandler += new ValidationEventHandler(ValidationCallback);
            
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Parse;

            using (XmlReader reader = XmlReader.Create(schemaPath, settings))
            {
                XmlSchema xmlSchema = XmlSchema.Read(reader, new ValidationEventHandler(ValidationCallback));
                xmlSchemaSet.Add(xmlSchema);
            }

            xmlSchemaSet.Compile();

            return xmlSchemaSet;
        }
    }
}
