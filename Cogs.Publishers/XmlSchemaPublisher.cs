// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace Cogs.Publishers
{
    /// <summary>Generates the closed, qualified COGS 2.0 XML Schema contract.</summary>
    public class XmlSchemaPublisher
    {
        private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";
        private const string XmlSchemaLocation = "xml.xsd";
        private const string XmlSchemaResourceName = "Cogs.Publishers.xml.xsd";

        public List<CogsError> Errors { get; } = new List<CogsError>();
        public required string CogsLocation { get; set; }
        public required string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }
        public required string TargetNamespace { get; set; }
        public required string TargetNamespacePrefix { get; set; }
        public required CogsModel CogsModel { get; set; }

        private XmlSchema CogsSchema { get; set; } = new XmlSchema();

        public void Publish()
        {
            var originalTarget = TargetDirectory;
            Errors.Clear();
            DirectoryPublication.Publish(originalTarget, Overwrite, stagingDirectory =>
            {
                TargetDirectory = stagingDirectory;
                try
                {
                    PublishCore();
                    if (Errors.Any(x => x.Level == ErrorLevel.Error))
                    {
                        throw new CogsPublicationException("The generated XML Schema contains errors.");
                    }
                }
                finally { TargetDirectory = originalTarget; }
            }, string.IsNullOrWhiteSpace(CogsModel.SourceDirectory) ? CogsLocation : CogsModel.SourceDirectory);
        }

        private void PublishCore()
        {
            if (string.IsNullOrWhiteSpace(CogsLocation)) throw new InvalidOperationException("Cogs location must be specified.");
            if (string.IsNullOrWhiteSpace(TargetDirectory)) throw new InvalidOperationException("Target directory must be specified.");
            if (string.IsNullOrWhiteSpace(TargetNamespace)) throw new InvalidOperationException("Target namespace must be specified.");

            BuildSchemaSet();

            WriteXmlNamespaceSchema(Path.Combine(TargetDirectory, XmlSchemaLocation));
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                IndentChars = "    "
            };
            using var writer = XmlWriter.Create(Path.Combine(TargetDirectory, "schema.xsd"), settings);
            if (!string.IsNullOrWhiteSpace(CogsModel.HeaderInclude)) writer.WriteComment(CogsModel.HeaderInclude);
            CogsSchema.Write(writer);
        }

        /// <summary>
        /// Builds an uncompiled in-memory schema for the current model. Publisher
        /// state is reset on every call so instances are safely reusable.
        /// </summary>
        public XmlSchema BuildSchema()
        {
            if (CogsModel == null) throw new InvalidOperationException("A COGS model must be specified.");
            if (string.IsNullOrWhiteSpace(TargetNamespace)) throw new InvalidOperationException("Target namespace must be specified.");

            // Publisher instances are reusable. Never retain declarations from a previous model.
            CogsSchema = new XmlSchema
            {
                TargetNamespace = TargetNamespace,
                ElementFormDefault = XmlSchemaForm.Qualified,
                AttributeFormDefault = XmlSchemaForm.Unqualified
            };
            CogsSchema.Namespaces.Add(TargetNamespacePrefix, TargetNamespace);
            CogsSchema.Namespaces.Add("xml", XmlNamespace);
            CogsSchema.Includes.Add(new XmlSchemaImport { Namespace = XmlNamespace, SchemaLocation = XmlSchemaLocation });
            AddModelDocumentation();

            CreateNativePrimitiveTypes();
            CreateCogsDateType();
            CreateCogsLangStringType();
            CreateLanguageType();
            CreateAnyUriType();
            CreateIdentificationGroup();
            foreach (var item in CogsModel.ItemTypes) CreateDataType(item);
            foreach (var composite in CogsModel.ReusableDataTypes) CreateDataType(composite);
            CreateReferenceType();
            CreateContainer();

            return CogsSchema;
        }

        private void AddModelDocumentation()
        {
            Settings settings = CogsModel.Settings;
            if (settings is null)
            {
                return;
            }

            List<string> sections = new List<string>();
            bool hasTitle = !string.IsNullOrWhiteSpace(settings.Title);
            bool hasVersion = !string.IsNullOrWhiteSpace(settings.Version);
            bool hasAuthor = !string.IsNullOrWhiteSpace(settings.Author);
            if (hasTitle || hasVersion || hasAuthor)
            {
                string introduction = hasTitle ? $"This schema describes {settings.Title}" : "This schema";
                if (hasVersion)
                {
                    introduction += hasTitle ? $", version {settings.Version}" : $" is version {settings.Version}";
                }
                if (hasAuthor)
                {
                    introduction += hasTitle || hasVersion ? $", authored by {settings.Author}" : $" was authored by {settings.Author}";
                }
                sections.Add(introduction.EndsWith('.') ? introduction : introduction + ".");
            }
            if (!string.IsNullOrWhiteSpace(settings.Description))
            {
                sections.Add(settings.Description);
            }
            if (!string.IsNullOrWhiteSpace(settings.Copyright))
            {
                string copyright = settings.Copyright;
                if (!copyright.StartsWith("Copyright", StringComparison.OrdinalIgnoreCase) && !copyright.StartsWith('©'))
                {
                    copyright = "Copyright " + copyright;
                }
                sections.Add(copyright.EndsWith('.') ? copyright : copyright + ".");
            }
            if (sections.Count == 0)
            {
                return;
            }

            XmlSchemaAnnotation annotation = new XmlSchemaAnnotation();
            XmlSchemaDocumentation documentation = new XmlSchemaDocumentation();
            XmlDocument document = new XmlDocument();
            documentation.Markup =
            [
                document.CreateTextNode(string.Join("\n\n", sections))
            ];
            annotation.Items.Add(documentation);
            CogsSchema.Items.Add(annotation);
        }

        /// <summary>
        /// Builds and compiles the complete in-memory schema set, including the
        /// XML namespace schema needed by <c>xml:lang</c>.
        /// </summary>
        public XmlSchemaSet BuildSchemaSet()
        {
            Errors.Clear();
            var schema = BuildSchema();

            var schemaSet = new XmlSchemaSet();
            schemaSet.ValidationEventHandler += ValidationCallback;
            schemaSet.Add(LoadXmlNamespaceSchema());
            schemaSet.Add(schema);
            schemaSet.Compile();
            if (Errors.Any(error => error.Level == ErrorLevel.Error))
            {
                throw new CogsPublicationException(string.Join(Environment.NewLine, Errors));
            }
            return schemaSet;
        }

        private void CreateNativePrimitiveTypes()
        {
            foreach (string name in new[] { "long", "unsignedLong", "positiveInteger", "negativeInteger", "nonPositiveInteger", "nonNegativeInteger", "decimal", "float", "double", "dateTime", "date", "time", "duration" })
            {
                XmlSchemaSimpleTypeRestriction restriction = new XmlSchemaSimpleTypeRestriction
                {
                    BaseTypeName = new XmlQualifiedName(name, XmlSchema.Namespace)
                };
                string? minimum = null;
                string? maximum = null;
                string? pattern = null;
                switch (name)
                {
                    case "long":
                        minimum = "-9007199254740991";
                        maximum = "9007199254740991";
                        break;
                    case "unsignedLong":
                    case "nonNegativeInteger":
                        minimum = "0";
                        maximum = "9007199254740991";
                        break;
                    case "positiveInteger":
                        minimum = "1";
                        maximum = "9007199254740991";
                        break;
                    case "negativeInteger":
                        minimum = "-9007199254740991";
                        maximum = "-1";
                        break;
                    case "nonPositiveInteger":
                        minimum = "-9007199254740991";
                        maximum = "0";
                        break;
                    case "decimal":
                        minimum = "-79228162514264337593543950335";
                        maximum = "79228162514264337593543950335";
                        restriction.Facets.Add(new XmlSchemaTotalDigitsFacet { Value = "29" });
                        restriction.Facets.Add(new XmlSchemaFractionDigitsFacet { Value = "28" });
                        break;
                    case "float":
                        minimum = "-3.4028234663852886E38";
                        maximum = "3.4028234663852886E38";
                        break;
                    case "double":
                        minimum = "-1.7976931348623157E308";
                        maximum = "1.7976931348623157E308";
                        break;
                    case "dateTime":
                        minimum = "0001-01-01T00:00:00Z";
                        maximum = "9999-12-31T23:59:59.999Z";
                        pattern = @"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,3}0*)?(Z|[+-][0-9]{2}:[0-9]{2})";
                        break;
                    case "date":
                        pattern = @"[0-9]{4}-[0-9]{2}-[0-9]{2}";
                        break;
                    case "time":
                        pattern = @"[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,6}0*)?";
                        break;
                    case "duration":
                        minimum = "-P10675199DT2H48M5.477S";
                        maximum = "P10675199DT2H48M5.477S";
                        pattern = @"-?P([0-9]+D)?(T([0-9]+H)?([0-9]+M)?(([0-9]+(\.[0-9]{0,3}0*)?|\.[0-9]{1,3}0*)S)?)?";
                        break;
                }
                if (minimum is not null)
                {
                    restriction.Facets.Add(new XmlSchemaMinInclusiveFacet { Value = minimum });
                }
                if (maximum is not null)
                {
                    restriction.Facets.Add(new XmlSchemaMaxInclusiveFacet { Value = maximum });
                }
                if (pattern is not null)
                {
                    restriction.Facets.Add(new XmlSchemaPatternFacet { Value = pattern });
                }
                if (name is "float" or "double")
                {
                    restriction.Facets.Add(new XmlSchemaPatternFacet { Value = @"[+-]?([0-9]+(\.[0-9]*)?|\.[0-9]+)([eE][+-]?[0-9]+)?" });
                }
                XmlSchemaSimpleType type = new XmlSchemaSimpleType { Name = name, Content = restriction };
                type.AddSchemaDocumentation(name switch
                {
                    "long" => "An xs:long restricted to integers from -9007199254740991 through 9007199254740991, inclusive.",
                    "unsignedLong" => "An xs:unsignedLong restricted to integers from 0 through 9007199254740991, inclusive.",
                    "positiveInteger" => "An xs:positiveInteger restricted to integers from 1 through 9007199254740991, inclusive.",
                    "negativeInteger" => "An xs:negativeInteger restricted to integers from -9007199254740991 through -1, inclusive.",
                    "nonPositiveInteger" => "An xs:nonPositiveInteger restricted to integers from -9007199254740991 through 0, inclusive.",
                    "nonNegativeInteger" => "An xs:nonNegativeInteger restricted to integers from 0 through 9007199254740991, inclusive.",
                    "decimal" => "An xs:decimal with at most 29 total digits and 28 fractional digits, between -79228162514264337593543950335 and 79228162514264337593543950335, inclusive. " +
                        "Exact interchange additionally requires an unsigned 96-bit coefficient with scale 0 through 28 and an unchanged decimal value after JavaScript JSON parsing and writing; application validation must enforce these additional conditions.",
                    "float" => "A finite IEEE 754 binary32 value, between -3.4028234663852886E38 and 3.4028234663852886E38, inclusive. " +
                        "Accepts xs:float decimal and scientific notation, including subnormal values; excludes NaN and infinities.",
                    "double" => "A finite IEEE 754 binary64 value, between -1.7976931348623157E308 and 1.7976931348623157E308, inclusive. " +
                        "Accepts xs:double decimal and scientific notation, including subnormal values; excludes NaN and infinities.",
                    "dateTime" => "An xs:dateTime with a required timezone and whole-millisecond precision, from 0001-01-01T00:00:00Z through 9999-12-31T23:59:59.999Z, inclusive. " +
                        "Timezone offsets are permitted; the bounds apply to the equivalent UTC instant.",
                    "date" => "An xs:date representing a local calendar date in years 0001 through 9999, without a timezone.",
                    "time" => "An xs:time representing a local time without a timezone, at microsecond precision. The end-of-day value 24:00:00 is equivalent to 00:00:00.",
                    "duration" => "An xs:duration representing elapsed whole milliseconds from -922337203685477 through 922337203685477, inclusive. " +
                        "Accepts positive and negative durations expressed in days, hours, minutes and seconds; year and month components are excluded.",
                    _ => throw new InvalidOperationException($"No XML Schema description is defined for primitive '{name}'.")
                });
                CogsSchema.Items.Add(type);
            }
        }

        public XmlSchemaSimpleType CreateCogsDateType()
        {
            var type = new XmlSchemaSimpleType { Name = "cogsDate" };
            type.AddSchemaDocumentation("A union of dateTime, date, gYearMonth, gYear, and duration.");
            type.Content = new XmlSchemaSimpleTypeUnion
            {
                MemberTypes = new[]
                {
                    PrimitiveTypeName("dateTime"),
                    PrimitiveTypeName("date"),
                    XmlSchemaSimpleType.GetBuiltInSimpleType(XmlTypeCode.GYearMonth).QualifiedName,
                    XmlSchemaSimpleType.GetBuiltInSimpleType(XmlTypeCode.GYear).QualifiedName,
                    PrimitiveTypeName("duration")
                }
            };
            CogsSchema.Items.Add(type);
            return type;
        }

        public XmlSchemaComplexType CreateCogsLangStringType()
        {
            var type = new XmlSchemaComplexType { Name = "LangString" };
            type.AddSchemaDocumentation("A string paired with a required BCP 47 xml:lang value.");
            var extension = new XmlSchemaSimpleContentExtension
            {
                BaseTypeName = new XmlQualifiedName("string", XmlSchema.Namespace)
            };
            extension.Attributes.Add(new XmlSchemaAttribute
            {
                RefName = new XmlQualifiedName("lang", XmlNamespace),
                Use = XmlSchemaUse.Required
            });
            type.ContentModel = new XmlSchemaSimpleContent { Content = extension };
            CogsSchema.Items.Add(type);
            return type;
        }

        private XmlSchemaSimpleType CreateLanguageType()
        {
            var restriction = new XmlSchemaSimpleTypeRestriction
            {
                BaseTypeName = new XmlQualifiedName("language", XmlSchema.Namespace)
            };
            restriction.Facets.Add(new XmlSchemaPatternFacet { Value = ToXsdPattern(CogsPrimitiveLexical.Bcp47Pattern) });
            var type = new XmlSchemaSimpleType { Name = "language", Content = restriction };
            type.AddSchemaDocumentation("A BCP 47 language tag. Instance validation applies the complete grammar, including grandfathered tags.");
            CogsSchema.Items.Add(type);
            return type;
        }

        private XmlSchemaSimpleType CreateAnyUriType()
        {
            var restriction = new XmlSchemaSimpleTypeRestriction
            {
                BaseTypeName = new XmlQualifiedName("anyURI", XmlSchema.Namespace)
            };
            restriction.Facets.Add(new XmlSchemaPatternFacet { Value = ToXsdPattern(CogsPrimitiveLexical.UriReferenceCharacterPattern) });
            var type = new XmlSchemaSimpleType { Name = "anyURI", Content = restriction };
            type.AddSchemaDocumentation("An RFC 3986 URI reference. Instance validation additionally checks scheme, fragment, and bracket structure.");
            CogsSchema.Items.Add(type);
            return type;
        }

        public XmlSchemaComplexType CreateDataType(DataType dataType)
        {
            var type = new XmlSchemaComplexType
            {
                Name = dataType.Name,
                IsAbstract = dataType.IsAbstract
            };
            type.AddSchemaDocumentation(dataType.Description ?? string.Empty);
            var sequence = new XmlSchemaSequence();
            if (!string.IsNullOrWhiteSpace(dataType.ExtendsTypeName))
            {
                type.ContentModel = new XmlSchemaComplexContent
                {
                    Content = new XmlSchemaComplexContentExtension
                    {
                        BaseTypeName = new XmlQualifiedName(dataType.ExtendsTypeName, TargetNamespace),
                        Particle = sequence
                    }
                };
            }
            else
            {
                type.Particle = sequence;
            }

            foreach (var property in dataType.Properties)
            {
                sequence.Items.Add(CreateLocalPropertyElement(property));
            }
            CogsSchema.Items.Add(type);
            return type;
        }

        private XmlSchemaElement CreateLocalPropertyElement(Property property)
        {
            var element = new XmlSchemaElement
            {
                Name = property.Name,
                MinOccursString = string.IsNullOrWhiteSpace(property.MinCardinality) ? "0" : property.MinCardinality,
                MaxOccursString = property.MaxCardinality == "n" || string.IsNullOrWhiteSpace(property.MaxCardinality) ? "unbounded" : property.MaxCardinality
            };
            element.AddSchemaDocumentation(property.Description ?? string.Empty);

            if (property.DataType is ItemType itemType)
            {
                element.SchemaType = BuildReferenceType(CogsTypeSystem.ConcreteTypesForProperty(CogsModel, property));
                return element;
            }

            if (property.DataType != null && !property.DataType.IsXmlPrimitive)
            {
                element.SchemaTypeName = new XmlQualifiedName(property.DataType.Name, TargetNamespace);
                if (!CogsTypeSystem.AllowsSubtypes(property))
                {
                    element.Block = XmlSchemaDerivationMethod.Extension | XmlSchemaDerivationMethod.Restriction;
                }
                return element;
            }

            if (property.DataTypeName == "langString")
            {
                if (HasStringFacets(property)) element.SchemaType = BuildRestrictedLangString(property);
                else element.SchemaTypeName = new XmlQualifiedName("LangString", TargetNamespace);
                return element;
            }
            if (property.DataTypeName == "cogsDate")
            {
                element.SchemaTypeName = new XmlQualifiedName("cogsDate", TargetNamespace);
                return element;
            }

            if (HasFacets(property) || property.DataTypeName is "decimal" or "float" or "double")
            {
                element.SchemaType = BuildPrimitiveRestriction(property);
            }
            else
            {
                element.SchemaTypeName = PrimitiveTypeName(property.DataTypeName);
            }
            return element;
        }

        private XmlSchemaSimpleType BuildPrimitiveRestriction(Property property)
        {
            var restriction = new XmlSchemaSimpleTypeRestriction
            {
                BaseTypeName = PrimitiveTypeName(property.DataTypeName)
            };
            if (property.DataTypeName is "float" or "double")
            {
                // XSD permits INF and NaN; the COGS primitive domain is finite.
                restriction.Facets.Add(new XmlSchemaPatternFacet { Value = @"[+-]?(([0-9]+(\.[0-9]*)?)|(\.[0-9]+))([eE][+-]?[0-9]+)?" });
            }
            AddFacets(restriction, property);
            return new XmlSchemaSimpleType { Content = restriction };
        }

        private XmlSchemaComplexType BuildRestrictedLangString(Property property)
        {
            var restriction = new XmlSchemaSimpleContentRestriction
            {
                BaseTypeName = new XmlQualifiedName("LangString", TargetNamespace)
            };
            AddFacets(restriction, property);
            restriction.Attributes.Add(new XmlSchemaAttribute
            {
                RefName = new XmlQualifiedName("lang", XmlNamespace),
                Use = XmlSchemaUse.Required
            });
            return new XmlSchemaComplexType
            {
                ContentModel = new XmlSchemaSimpleContent { Content = restriction }
            };
        }

        private static void AddFacets(XmlSchemaObjectCollection facets, Property property)
        {
            if (!string.IsNullOrWhiteSpace(property.Pattern)) facets.Add(new XmlSchemaPatternFacet { Value = ToXsdSubstringPattern(property.Pattern) });
            if (property.MinLength.HasValue) facets.Add(new XmlSchemaMinLengthFacet { Value = property.MinLength.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            if (property.MaxLength.HasValue) facets.Add(new XmlSchemaMaxLengthFacet { Value = property.MaxLength.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            foreach (var value in property.Enumeration) facets.Add(new XmlSchemaEnumerationFacet { Value = value });
            if (!string.IsNullOrWhiteSpace(property.MinInclusive)) facets.Add(new XmlSchemaMinInclusiveFacet { Value = property.MinInclusive });
            if (!string.IsNullOrWhiteSpace(property.MinExclusive)) facets.Add(new XmlSchemaMinExclusiveFacet { Value = property.MinExclusive });
            if (!string.IsNullOrWhiteSpace(property.MaxInclusive)) facets.Add(new XmlSchemaMaxInclusiveFacet { Value = property.MaxInclusive });
            if (!string.IsNullOrWhiteSpace(property.MaxExclusive)) facets.Add(new XmlSchemaMaxExclusiveFacet { Value = property.MaxExclusive });
        }

        private static void AddFacets(XmlSchemaSimpleTypeRestriction restriction, Property property) => AddFacets(restriction.Facets, property);
        private static void AddFacets(XmlSchemaSimpleContentRestriction restriction, Property property) => AddFacets(restriction.Facets, property);

        private void CreateIdentificationGroup()
        {
            var sequence = new XmlSchemaSequence();
            foreach (var identification in CogsModel.Identification)
            {
                var id = CreateLocalPropertyElement(identification);
                id.MinOccurs = 1;
                id.MaxOccurs = 1;
                sequence.Items.Add(id);
            }

            var group = new XmlSchemaGroup
            {
                Name = "IdentificationGroup",
                Particle = sequence
            };
            group.AddSchemaDocumentation(
                "The ordered identification fields shared by all item references. " +
                "Full item definitions declare their identification fields as normal properties.");
            CogsSchema.Items.Add(group);
        }

        private void CreateReferenceType()
        {
            var type = BuildReferenceType(CogsModel.ItemTypes.Where(x => !x.IsAbstract));
            type.Name = "ReferenceType";
            type.AddSchemaDocumentation("An identification-only reference to any concrete item.");
            CogsSchema.Items.Add(type);
        }

        private XmlSchemaComplexType BuildReferenceType(IEnumerable<DataType> concreteTypes)
        {
            var sequence = new XmlSchemaSequence();
            sequence.Items.Add(new XmlSchemaGroupRef
            {
                RefName = new XmlQualifiedName("IdentificationGroup", TargetNamespace)
            });

            var typeRestriction = new XmlSchemaSimpleTypeRestriction
            {
                BaseTypeName = new XmlQualifiedName("string", XmlSchema.Namespace)
            };
            foreach (var typeName in concreteTypes.Select(x => x.Name).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
            {
                typeRestriction.Facets.Add(new XmlSchemaEnumerationFacet { Value = typeName });
            }
            sequence.Items.Add(new XmlSchemaElement
            {
                Name = "TypeOfObject",
                MinOccurs = 1,
                MaxOccurs = 1,
                SchemaType = new XmlSchemaSimpleType { Content = typeRestriction }
            });

            var type = new XmlSchemaComplexType { Particle = sequence };
            type.Attributes.Add(new XmlSchemaAttribute
            {
                Name = "isReference",
                SchemaTypeName = new XmlQualifiedName("boolean", XmlSchema.Namespace),
                FixedValue = "true"
            });
            return type;
        }

        private void CreateContainer()
        {
            var sequence = new XmlSchemaSequence();
            sequence.Items.Add(new XmlSchemaElement
            {
                Name = "TopLevelReference",
                SchemaTypeName = new XmlQualifiedName("ReferenceType", TargetNamespace),
                MinOccurs = 0,
                MaxOccursString = "unbounded"
            });

            var choices = new XmlSchemaChoice { MinOccurs = 0, MaxOccursString = "unbounded" };
            foreach (var item in CogsModel.ItemTypes.Where(x => !x.IsAbstract).OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                choices.Items.Add(new XmlSchemaElement
                {
                    Name = item.Name,
                    SchemaTypeName = new XmlQualifiedName(item.Name, TargetNamespace)
                });
            }
            sequence.Items.Add(choices);

            var containerType = new XmlSchemaComplexType { Name = "ItemContainerType", Particle = sequence };
            containerType.AddSchemaDocumentation("A sequence of top-level references followed by concrete item definitions.");
            CogsSchema.Items.Add(containerType);
            var root = new XmlSchemaElement
            {
                Name = "ItemContainer",
                SchemaTypeName = new XmlQualifiedName("ItemContainerType", TargetNamespace)
            };
            root.AddSchemaDocumentation("The root element for XML instances, containing the item type instances.");
            CogsSchema.Items.Add(root);
        }

        private static bool HasStringFacets(Property property) =>
            property.MinLength.HasValue || property.MaxLength.HasValue || !string.IsNullOrWhiteSpace(property.Pattern) || property.Enumeration.Count > 0;

        private static bool HasFacets(Property property) => HasStringFacets(property) ||
            !string.IsNullOrWhiteSpace(property.MinInclusive) || !string.IsNullOrWhiteSpace(property.MinExclusive) ||
            !string.IsNullOrWhiteSpace(property.MaxInclusive) || !string.IsNullOrWhiteSpace(property.MaxExclusive);

        private XmlQualifiedName PrimitiveTypeName(string name) => name is "language" or "anyURI" or "decimal" or "float" or "double" or "long" or "unsignedLong" or "positiveInteger" or "negativeInteger" or "nonPositiveInteger" or "nonNegativeInteger" or "dateTime" or "date" or "time" or "duration"
            ? new XmlQualifiedName(name, TargetNamespace)
            : new XmlQualifiedName(name, XmlSchema.Namespace);

        private static string ToXsdPattern(string ecmaPattern)
        {
            var pattern = ecmaPattern;
            if (pattern.StartsWith("^", StringComparison.Ordinal)) pattern = pattern.Substring(1);
            if (pattern.EndsWith("$", StringComparison.Ordinal)) pattern = pattern.Substring(0, pattern.Length - 1);
            return pattern.Replace("(?:", "(", StringComparison.Ordinal);
        }

        private static string ToXsdSubstringPattern(string portablePattern) =>
            $"(.|\\r|\\n)*({CogsConventions.TranslatePortablePattern(portablePattern, xsd: true)})(.|\\r|\\n)*";

        private static XmlSchema LoadXmlNamespaceSchema()
        {
            using var stream = OpenXmlNamespaceSchemaStream();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse });
            return XmlSchema.Read(reader, (_, e) => throw e.Exception ?? new XmlSchemaException(e.Message))
                ?? throw new InvalidOperationException("Unable to load xml.xsd.");
        }

        private static Stream OpenXmlNamespaceSchemaStream() =>
            typeof(XmlSchemaPublisher).GetTypeInfo().Assembly.GetManifestResourceStream(XmlSchemaResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{XmlSchemaResourceName}' was not found.");

        private static void WriteXmlNamespaceSchema(string path)
        {
            using var source = OpenXmlNamespaceSchemaStream();
            using var destination = File.Create(path);
            source.CopyTo(destination);
        }

        private void ValidationCallback(object? sender, ValidationEventArgs e)
        {
            var level = e.Severity == XmlSeverityType.Warning ? ErrorLevel.Warning : ErrorLevel.Error;
            Errors.Add(new CogsError(level, "COGS-XSD-001", e.Message, exception: e.Exception));
        }

        public static XmlNode[] TextToNodeArray(string text)
        {
            var document = new XmlDocument();
            return new[] { document.CreateTextNode(text) };
        }
    }

    public static class Extensions
    {
        public static void AddSchemaDocumentation(this XmlSchemaAnnotated item, params string[] texts)
        {
            var annotation = new XmlSchemaAnnotation();
            foreach (var text in texts)
            {
                var documentation = new XmlSchemaDocumentation { Language = "en" };
                var document = new XmlDocument();
                documentation.Markup = new[] { document.CreateTextNode(text ?? string.Empty) };
                annotation.Items.Add(documentation);
            }
            item.Annotation = annotation;
        }

        public static string ToLowerFirstLetter(this string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return string.Empty;
            if (str == "URN") return "urn";
            if (str == "ID") return "id";
            if (str.StartsWith("URL", StringComparison.Ordinal)) return str.Replace("URL", "url", StringComparison.Ordinal);
            return str.Length == 1 ? str.ToLowerInvariant() : char.ToLowerInvariant(str[0]) + str.Substring(1);
        }
    }
}
