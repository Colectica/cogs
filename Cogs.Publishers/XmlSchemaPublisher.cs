// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Model;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Threading;

namespace Cogs.Publishers
{
    /// <summary>
    /// Generate an xml schema using the Garden of Eden approach, all elements and type definitions are declared globally
    /// </summary>
    public class XmlSchemaPublisher
    {
        public string CogsLocation { get; set; }
        public string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }

        public string TargetNamespace { get; set; }
        public string TargetNamespacePrefix { get; set; }

        Dictionary<string, string> createdElements = new Dictionary<string, string>();

        private CogsModel CogsModel { get; set; }
        private XmlSchema CogsSchema { get; set; }

        public void Publish(CogsModel model2)
        {
            if (CogsLocation == null)
            {
                throw new InvalidOperationException("Cogs location must be specified");
            }
            if (TargetDirectory == null)
            {
                throw new InvalidOperationException("Target directory must be specified");
            }

            if (Overwrite && Directory.Exists(TargetDirectory))
            {
                Directory.Delete(TargetDirectory, true);
                Thread.Sleep(50);
            }

            Directory.CreateDirectory(TargetDirectory);


            CogsModel = model2;
            CogsSchema = new XmlSchema();

            CogsSchema.TargetNamespace = TargetNamespace;
            CogsSchema.Namespaces.Add(TargetNamespacePrefix, TargetNamespace);
            CogsSchema.ElementFormDefault = XmlSchemaForm.Qualified;
            CogsSchema.AttributeFormDefault = XmlSchemaForm.Unqualified;

            // create built in types
            XmlSchemaComplexType referenceType = CreateReferenceType();
            

            // Create the container
            /*
            XmlSchemaComplexType containerType = new XmlSchemaComplexType();
            containerType.Name = "FragmentInstance";
            containerType.AddSchemaDocumentation("A Fragment Instance is used to transfer items plus any associated notes and other material. TopLevelReference provides a record of the main item of the FragmentInstance.");
            cogsSchema.Items.Add(containerType);

            XmlSchemaSequence containerSequence = new XmlSchemaSequence();
            containerType.Particle = containerSequence;
            */

            XmlSchemaChoice itemChoices = null;
            XmlSchemaComplexType containerType = CreateItemContainerType(out itemChoices);
            CogsSchema.Items.Add(containerType);

            XmlSchemaElement container = new XmlSchemaElement();
            container.Name = "ItemContainer";
            container.AddSchemaDocumentation("A Item Container is used to transfer items plus any associated notes and other material. TopLevelReference provides a record of the main item of the Item Container.");
            container.SchemaTypeName = new XmlQualifiedName("ItemContainerType", TargetNamespace);
            CogsSchema.Items.Add(container);

            foreach (var item in CogsModel.ItemTypes)
            {

                CreateDataType(item);

                // create a usage for the container
                XmlSchemaElement element = new XmlSchemaElement();
                element.Name = item.Name;
                element.SchemaTypeName = new XmlQualifiedName(item.Name, TargetNamespace);
                CogsSchema.Items.Add(element);

                // include item in container via element reference
                XmlSchemaElement elementRef = new XmlSchemaElement();
                elementRef.RefName = new XmlQualifiedName(item.Name, TargetNamespace);
                itemChoices.Items.Add(elementRef);
            }


            foreach (var dataType in CogsModel.ReusableDataTypes)
            {
                CreateDataType(dataType);
            }

            XmlSchemaSet schemaSet = new XmlSchemaSet();
            schemaSet.ValidationEventHandler += new ValidationEventHandler(ValidationCallback);
            schemaSet.Add(CogsSchema);
            schemaSet.Compile();

            foreach (XmlSchema schema in schemaSet.Schemas())
            {
                CogsSchema = schema;
            }

            // Write the complete schema
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;
            settings.Indent = true;
            settings.IndentChars = "    ";
            using (XmlWriter writer = XmlWriter.Create(Path.Combine(TargetDirectory, "schema.xsd"), settings))
            {

                CogsSchema.Write(writer);
            }
                


            
        }


        public XmlSchemaComplexType CreateDataType(DataType dataType)
        {
            // create types

            XmlSchemaComplexType complexType = new XmlSchemaComplexType();
            complexType.Name = dataType.Name;
            complexType.AddSchemaDocumentation(dataType.Description);
            CogsSchema.Items.Add(complexType);

            XmlSchemaSequence itemElements = new XmlSchemaSequence();

            if (dataType.IsAbstract)
            {
                complexType.IsAbstract = true;
            }
            if (!string.IsNullOrWhiteSpace(dataType.ExtendsTypeName))
            {
                XmlSchemaComplexContent complexContent = new XmlSchemaComplexContent();
                complexType.ContentModel = complexContent;

                XmlSchemaComplexContentExtension contentExtension = new XmlSchemaComplexContentExtension();
                complexContent.Content = contentExtension;
                contentExtension.BaseTypeName = new XmlQualifiedName(dataType.ExtendsTypeName, TargetNamespace);

                contentExtension.Particle = itemElements;
            }
            else
            {
                complexType.Particle = itemElements;
            }

            foreach (var property in dataType.Properties)
            {

                if (property.Name == "space")
                {
                    continue;//TODO
                }


                CreateElementReference(itemElements, property);
            }

            return complexType;
        }

        private void CreateElementReference(XmlSchemaSequence itemElements, Property property)
        {
            // TODO refactor the model so base classes are used here
            var dataTypes = property.DataTypeName.Split(' ');

            if (!createdElements.ContainsKey(property.Name))
            {
                XmlSchemaElement element = new XmlSchemaElement();
                element.Name = property.Name;

                // if a reference to an item
                if (CogsModel.ItemTypes.Exists(x => dataTypes.Contains(x.Name)))
                {
                    element.SchemaTypeName = new XmlQualifiedName("ReferenceType", TargetNamespace);
                }
                else
                {
                    element.SchemaTypeName = new XmlQualifiedName(property.DataTypeName, TargetNamespace);
                }
                CogsSchema.Items.Add(element);
                createdElements[property.Name] = property.DataTypeName;
            }

            // include reference to datatype property
            XmlSchemaElement datatypeRef = new XmlSchemaElement();
            datatypeRef.RefName = new XmlQualifiedName(property.Name, TargetNamespace);
            datatypeRef.MinOccursString = property.MinCardinality == "" ? "0" : property.MinCardinality ?? "0";
            datatypeRef.MaxOccursString = property.MaxCardinality == "n" ? "unbounded" : property.MaxCardinality ?? "unbounded";
            datatypeRef.AddSchemaDocumentation(property.Description);
            itemElements.Items.Add(datatypeRef);
        }

        XmlSchemaComplexType CreateItemContainerType(out XmlSchemaChoice itemChoices)
        {
            // Item Container type
            XmlSchemaComplexType containerType = new XmlSchemaComplexType();
            containerType.Name = "ItemContainerType";            
            containerType.AddSchemaDocumentation("Used for serializing a set of items.");

            var sequence = new XmlSchemaSequence();
            containerType.Particle = sequence;

            // Top level reference element
            XmlSchemaElement element = new XmlSchemaElement();
            element.Name = "TopLevelReference";
            element.SchemaTypeName = new XmlQualifiedName("ReferenceType", TargetNamespace);
            element.AddSchemaDocumentation("Denote which items in the Fragment Instance are the main items of interest.");
            CogsSchema.Items.Add(element);

            // include top level reference
            XmlSchemaElement elementRef = new XmlSchemaElement();
            elementRef.RefName = new XmlQualifiedName("TopLevelReference", TargetNamespace);
            elementRef.MinOccurs = 0;
            elementRef.MaxOccursString = "unbounded";
            sequence.Items.Add(elementRef);

            itemChoices = new XmlSchemaChoice();
            itemChoices.MinOccurs = 0;
            itemChoices.MaxOccursString = "unbounded";
            sequence.Items.Add(itemChoices);



            return containerType;
        }

        XmlSchemaComplexType CreateReferenceType()
        {

            DataType reference = new DataType();
            reference.Name = "ReferenceType";
            reference.Description = "Used for referencing an identified item, by lookup of the URN and/or an IRDI identification sequence.";

            reference.Properties.AddRange(CogsModel.Identification);

            Property p = new Property()
            {
                DataType = reference,
                DataTypeName = reference.Name,
                Description = "Strongly typed name of the item which is being referenced.",
                MinCardinality = "1",
                MaxCardinality = "1",
                Name = "TypeOfObject"
            };

            return CreateDataType(reference);
        }

        static void ValidationCallback(object sender, ValidationEventArgs args)
        {
            if (args.Severity == XmlSeverityType.Warning)
                System.Console.Write("WARNING: ");
            else if (args.Severity == XmlSeverityType.Error)
                System.Console.Write("ERROR: ");

            System.Console.WriteLine(args.Message);
        }

        public static XmlNode[] TextToNodeArray(string text)
        {
            XmlDocument doc = new XmlDocument();
            return new XmlNode[1] { doc.CreateTextNode(text) };
        }

    }

    public static class Extensions
    {
        public static void AddSchemaDocumentation(this XmlSchemaAnnotated item, params string[] texts)
        {
            var annotation = new XmlSchemaAnnotation();
            foreach(var text in texts)
            {
                var documentation = new XmlSchemaDocumentation();
                documentation.Language = "en";

                XmlDocument doc = new XmlDocument();
                documentation.Markup = new XmlNode[1] { doc.CreateTextNode(text) };

                annotation.Items.Add(documentation);
            }

            item.Annotation = annotation;
        }
    }
}
