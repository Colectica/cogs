using Cogs.Common;
using Cogs.Dto;
using Cogs.Model;
using Cogs.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Cogs.Tests
{
    public sealed class Cogs2ContractTests
    {
        private const string PropertyHeader = "Name,DataType,MinCardinality,MaxCardinality,Description,Ordered,AllowSubtypes,MinLength,MaxLength,Enumeration,Pattern,MinInclusive,MinExclusive,MaxInclusive,MaxExclusive,DeprecatedNamespace,DeprecatedElementOrAttribute,DeprecatedChoiceGroup";

        [Fact]
        public void ReaderReportsMissingDirectoryWithoutThrowing()
        {
            var reader = new CogsDirectoryReader();
            var result = reader.LoadResult(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, x => x.Code == "COGS-READ-003");
        }

        [Fact]
        public void ReaderEnforcesExactSettingsDirectoryBeforeOtherHeaders()
        {
            using var fixture = new ModelDirectory();
            Directory.Move(Path.Combine(fixture.Path, "Settings"), Path.Combine(fixture.Path, "settings"));
            File.WriteAllText(Path.Combine(fixture.Path, "ItemTypes", "Thing", "Thing.csv"), PropertyHeader.Replace("DataType", "Datatype") + Environment.NewLine);

            var result = new CogsDirectoryReader().LoadResult(fixture.Path);

            Assert.Contains(result.Diagnostics, x => x.Code == "COGS-READ-070");
            Assert.DoesNotContain(result.Diagnostics, x => x.Code is "COGS-READ-062" or "COGS-READ-063");
        }

        [Fact]
        public void ReaderEnforcesExactPropertyHeaderCasingAfterVersionSelection()
        {
            using var fixture = new ModelDirectory();
            File.WriteAllText(Path.Combine(fixture.Path, "ItemTypes", "Thing", "Thing.csv"),
                PropertyHeader.Replace("DataType", "Datatype") + Environment.NewLine);

            CogsLoadResult result = new CogsDirectoryReader().LoadResult(fixture.Path);

            Assert.Contains(result.Diagnostics, x => x.Code == "COGS-READ-062");
            Assert.Contains(result.Diagnostics, x => x.Code == "COGS-READ-063");
        }

        [Fact]
        public void ReaderSelectsCogsVersionBeforeInterpretingOtherFiles()
        {
            using var fixture = new ModelDirectory();
            File.WriteAllText(Path.Combine(fixture.Path, "Settings", "Settings.csv"),
                "Key,Value" + Environment.NewLine + "Title,No Version" + Environment.NewLine);
            File.WriteAllText(Path.Combine(fixture.Path, "Settings", "Identification.csv"),
                "Wrong,Header" + Environment.NewLine + "x,y" + Environment.NewLine);

            CogsLoadResult result = new CogsDirectoryReader().LoadResult(fixture.Path);

            Assert.Contains(result.Diagnostics, error => error.Code == "COGS-READ-090");
            Assert.DoesNotContain(result.Diagnostics, error => error.SourcePath?.EndsWith("Identification.csv", StringComparison.Ordinal) == true);
            Assert.Null(result.Model);
        }

        [Fact]
        public void ReaderRejectsMisspelledMarkerFiles()
        {
            using var fixture = new ModelDirectory();
            File.WriteAllText(Path.Combine(fixture.Path, "ItemTypes", "Thing", "Abstact"), string.Empty);

            CogsLoadResult result = new CogsDirectoryReader().LoadResult(fixture.Path);

            Assert.Contains(result.Diagnostics, error => error.Code == "COGS-READ-044" &&
                error.SourcePath?.EndsWith("Abstact", StringComparison.Ordinal) == true);
        }

        [Theory]
        [InlineData("Abstract", false)]
        [InlineData("abstract", true)]
        [InlineData("ABSTRACT", true)]
        public void ReaderAcceptsAbstractMarkerKeywordCasing(string markerName, bool expectsWarning)
        {
            using var fixture = new ModelDirectory();
            File.WriteAllText(Path.Combine(fixture.Path, "ItemTypes", "Thing", markerName), string.Empty);

            CogsLoadResult loaded = new CogsDirectoryReader().LoadResult(fixture.Path);

            List<CogsError> warnings = loaded.Diagnostics.Where(error => error.Code == "COGS-READ-040").ToList();
            Assert.Equal(expectsWarning ? 1 : 0, warnings.Count);
            if (expectsWarning)
            {
                Assert.Equal(ErrorLevel.Warning, warnings[0].Level);
                Assert.EndsWith(markerName, warnings[0].SourcePath, StringComparison.Ordinal);
            }
            Assert.True(loaded.Success, string.Join(Environment.NewLine, loaded.Diagnostics));
            Assert.True(Assert.Single(loaded.Model.ItemTypes).IsAbstract);
            Assert.DoesNotContain(DtoValidation.Validate(loaded.Model), error => error.Level == ErrorLevel.Error);
        }

        [Theory]
        [InlineData("Primitive", false)]
        [InlineData("primitive", true)]
        [InlineData("PRIMITIVE", true)]
        public void ReaderAcceptsPrimitiveMarkerKeywordCasing(string markerName, bool expectsWarning)
        {
            using var fixture = new ModelDirectory();
            string valueDirectory = Path.Combine(fixture.Path, "CompositeTypes", "Value");
            Directory.CreateDirectory(valueDirectory);
            File.WriteAllText(Path.Combine(valueDirectory, "Value.csv"), PropertyHeader + Environment.NewLine);
            File.WriteAllText(Path.Combine(valueDirectory, markerName), string.Empty);

            CogsLoadResult loaded = new CogsDirectoryReader().LoadResult(fixture.Path);

            List<CogsError> warnings = loaded.Diagnostics.Where(error => error.Code == "COGS-READ-040").ToList();
            Assert.Equal(expectsWarning ? 1 : 0, warnings.Count);
            if (expectsWarning)
            {
                Assert.Equal(ErrorLevel.Warning, warnings[0].Level);
                Assert.EndsWith(markerName, warnings[0].SourcePath, StringComparison.Ordinal);
            }
            Assert.True(loaded.Success, string.Join(Environment.NewLine, loaded.Diagnostics));
            Assert.True(Assert.Single(loaded.Model.ReusableDataTypes).IsPrimitive);
            Assert.DoesNotContain(DtoValidation.Validate(loaded.Model), error => error.Level == ErrorLevel.Error);
        }

        [Theory]
        [InlineData("Extends.Versionable", false)]
        [InlineData("extends.Versionable", true)]
        [InlineData("EXTENDS.Versionable", true)]
        public void ReaderAcceptsExtendsMarkerKeywordCasingAndRetainsInheritance(string markerName, bool expectsWarning)
        {
            using var fixture = new ModelDirectory();
            string parentDirectory = Path.Combine(fixture.Path, "ItemTypes", "Versionable");
            Directory.CreateDirectory(parentDirectory);
            File.WriteAllText(Path.Combine(parentDirectory, "Versionable.csv"), PropertyHeader + Environment.NewLine);
            File.WriteAllText(Path.Combine(fixture.Path, "ItemTypes", "Thing", markerName), string.Empty);

            CogsLoadResult loaded = new CogsDirectoryReader().LoadResult(fixture.Path);

            List<CogsError> warnings = loaded.Diagnostics.Where(error => error.Code == "COGS-READ-041").ToList();
            Assert.Equal(expectsWarning ? 1 : 0, warnings.Count);
            if (expectsWarning)
            {
                Assert.Equal(ErrorLevel.Warning, warnings[0].Level);
                Assert.EndsWith(markerName, warnings[0].SourcePath, StringComparison.Ordinal);
            }
            Assert.True(loaded.Success, string.Join(Environment.NewLine, loaded.Diagnostics));
            Assert.Equal("Versionable", loaded.Model.ItemTypes.Single(type => type.Name == "Thing").Extends);
            Assert.DoesNotContain(DtoValidation.Validate(loaded.Model), error => error.Level == ErrorLevel.Error);

            CogsBuildResult built = new CogsModelBuilder().BuildResult(loaded.Model);
            Assert.True(built.Success, string.Join(Environment.NewLine, built.Diagnostics));
            Assert.Equal("Versionable", Assert.Single(
                built.Model.ItemTypes.Single(type => type.Name == "Thing").ParentTypes).Name);
        }

        [Fact]
        public void ReaderKeepsParentTypeCasingStrict()
        {
            using var fixture = new ModelDirectory();
            string parentDirectory = Path.Combine(fixture.Path, "ItemTypes", "Versionable");
            Directory.CreateDirectory(parentDirectory);
            File.WriteAllText(Path.Combine(parentDirectory, "Versionable.csv"), PropertyHeader + Environment.NewLine);
            File.WriteAllText(Path.Combine(fixture.Path, "ItemTypes", "Thing", "Extends.versionable"), string.Empty);

            CogsLoadResult loaded = new CogsDirectoryReader().LoadResult(fixture.Path);

            Assert.True(loaded.Success, string.Join(Environment.NewLine, loaded.Diagnostics));
            Assert.DoesNotContain(loaded.Diagnostics, error => error.Code == "COGS-READ-041");
            Assert.Contains(DtoValidation.Validate(loaded.Model), error => error.Code == "COGS-VAL-INH-003");

            using var missingFixture = new ModelDirectory();
            File.WriteAllText(Path.Combine(missingFixture.Path, "ItemTypes", "Thing", "Extends.MissingParent"), string.Empty);
            CogsLoadResult missing = new CogsDirectoryReader().LoadResult(missingFixture.Path);
            Assert.True(missing.Success, string.Join(Environment.NewLine, missing.Diagnostics));
            Assert.Contains(DtoValidation.Validate(missing.Model), error => error.Code == "COGS-VAL-INH-002");
        }

        [Fact]
        public void ReaderRejectsEmptyAndCompetingExtendsMarkers()
        {
            using var emptyFixture = new ModelDirectory();
            File.WriteAllText(Path.Combine(emptyFixture.Path, "ItemTypes", "Thing", "Extends.\u00a0"), string.Empty);
            CogsLoadResult empty = new CogsDirectoryReader().LoadResult(emptyFixture.Path);
            Assert.Contains(empty.Diagnostics, error => error.Code == "COGS-READ-043");

            using var competingFixture = new ModelDirectory();
            string thing = Path.Combine(competingFixture.Path, "ItemTypes", "Thing");
            File.WriteAllText(Path.Combine(thing, "Extends.First"), string.Empty);
            File.WriteAllText(Path.Combine(thing, "extends.Second"), string.Empty);
            CogsLoadResult competing = new CogsDirectoryReader().LoadResult(competingFixture.Path);
            Assert.Contains(competing.Diagnostics, error => error.Code == "COGS-READ-042");
        }

        [Fact]
        public void ReaderRejectsMultipleCaseEquivalentNamedMarkersWhenFilesystemSupportsThem()
        {
            using var fixture = new ModelDirectory();
            string thing = Path.Combine(fixture.Path, "ItemTypes", "Thing");
            File.WriteAllText(Path.Combine(thing, "abstract"), string.Empty);
            File.WriteAllText(Path.Combine(thing, "ABSTRACT"), string.Empty);
            if (Directory.EnumerateFiles(thing)
                .Select(Path.GetFileName)
                .Count(name => string.Equals(name, "Abstract", StringComparison.OrdinalIgnoreCase)) < 2)
            {
                return;
            }

            CogsLoadResult loaded = new CogsDirectoryReader().LoadResult(fixture.Path);

            Assert.Contains(loaded.Diagnostics, error => error.Code == "COGS-READ-045" && error.Level == ErrorLevel.Error);
        }

        [Fact]
        public void ReaderRejectsMalformedDcTermsMarkerAtItsSourceRow()
        {
            using var fixture = new ModelDirectory();
            File.WriteAllText(Path.Combine(fixture.Path, "ItemTypes", "Thing", "Thing.csv"),
                PropertyHeader + Environment.NewLine + "DcTerms,dcTerms,0,1,not blank,,,,,,,,,,,,," + Environment.NewLine);

            var result = new CogsDirectoryReader().LoadResult(fixture.Path);

            var error = Assert.Single(result.Diagnostics, x => x.Code == "COGS-READ-022");
            Assert.Equal(2, error.Line);
            Assert.EndsWith("Thing.csv", error.SourcePath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ReaderLoadsValidCogs2DirectoryAndCarriesSourceDirectory()
        {
            using var fixture = new ModelDirectory();

            var loaded = new CogsDirectoryReader().LoadResult(fixture.Path);
            var validation = DtoValidation.Validate(loaded.Model);
            var built = new CogsModelBuilder().BuildResult(loaded.Model);

            Assert.True(loaded.Success, string.Join(Environment.NewLine, loaded.Diagnostics));
            Assert.DoesNotContain(validation, x => x.Level == ErrorLevel.Error);
            Assert.True(built.Success, string.Join(Environment.NewLine, built.Diagnostics));
            Assert.Equal(Path.GetFullPath(fixture.Path), built.Model.SourceDirectory);
        }

        [Fact]
        public void ReaderLoadsNormalizedRootAndTopicArticlePaths()
        {
            using var fixture = new ModelDirectory();
            string articles = Path.Combine(fixture.Path, "Articles");
            Directory.CreateDirectory(Path.Combine(articles, "about"));
            File.WriteAllText(Path.Combine(articles, "about", "index.rst"), "About\n=====\n");
            File.WriteAllText(Path.Combine(articles, "toc.txt"), "about/index" + Environment.NewLine + Environment.NewLine);

            string topic = Path.Combine(fixture.Path, "Topics", "All Content Items");
            Directory.CreateDirectory(Path.Combine(topic, "Articles"));
            File.WriteAllText(Path.Combine(fixture.Path, "Topics", "index.txt"), "All Content Items" + Environment.NewLine);
            File.WriteAllText(Path.Combine(topic, "items.txt"), "Thing" + Environment.NewLine);
            File.WriteAllText(Path.Combine(topic, "toc.txt"), "guide" + Environment.NewLine);
            File.WriteAllText(Path.Combine(topic, "Articles", "guide.md"), "# Guide\n");

            CogsLoadResult result = new CogsDirectoryReader().LoadResult(fixture.Path);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.Equal(new[] { "about/index" }, result.Model.ArticleTocEntries);
            Cogs.Dto.TopicIndex loadedTopic = Assert.Single(result.Model.TopicIndices);
            Assert.Equal(new[] { "guide" }, loadedTopic.ArticleTocEntries);
            Assert.DoesNotContain(DtoValidation.Validate(result.Model), error => error.Level == ErrorLevel.Error);
        }

        [Fact]
        public void ReaderRejectsUnsafeMissingMiscasedAndDuplicateArticleTocEntriesAtTheirRows()
        {
            using var fixture = new ModelDirectory();
            string articles = Path.Combine(fixture.Path, "Articles");
            Directory.CreateDirectory(articles);
            File.WriteAllText(Path.Combine(articles, "guide.md"), "# Guide\n");
            File.WriteAllLines(Path.Combine(articles, "toc.txt"), new[]
            {
                "guide",
                "../outside",
                ":hidden:",
                ".. include:: injected",
                "missing",
                "Guide",
                "guide",
                "nested//article",
                "guide#anchor",
                "guide[1]",
                "guide.md"
            });

            CogsLoadResult result = new CogsDirectoryReader().LoadResult(fixture.Path);

            Assert.False(result.Success);
            Assert.Null(result.Model);
            AssertDiagnostic(result.Diagnostics, "COGS-READ-087", "toc.txt", 2);
            AssertDiagnostic(result.Diagnostics, "COGS-READ-085", "toc.txt", 3);
            AssertDiagnostic(result.Diagnostics, "COGS-READ-085", "toc.txt", 4);
            AssertDiagnostic(result.Diagnostics, "COGS-READ-088", "toc.txt", 5);
            AssertDiagnostic(result.Diagnostics, "COGS-READ-089", "toc.txt", 6);
            AssertDiagnostic(result.Diagnostics, "COGS-READ-086", "toc.txt", 7);
            AssertDiagnostic(result.Diagnostics, "COGS-READ-084", "toc.txt", 8);
            AssertDiagnostic(result.Diagnostics, "COGS-READ-085", "toc.txt", 9);
            AssertDiagnostic(result.Diagnostics, "COGS-READ-085", "toc.txt", 10);
            AssertDiagnostic(result.Diagnostics, "COGS-READ-086", "toc.txt", 11);
        }

        [Fact]
        public void TopicIndexAndItemSyntaxDiagnosticsRetainOriginRows()
        {
            using var fixture = new ModelDirectory();
            string topics = Path.Combine(fixture.Path, "Topics");
            string topic = Path.Combine(topics, "All Content Items");
            Directory.CreateDirectory(topic);
            File.WriteAllText(Path.Combine(topic, "items.txt"), "Thing" + Environment.NewLine);
            File.WriteAllLines(Path.Combine(topics, "index.txt"), new[]
            {
                "All Content Items", "", "Bad/Path", "All Content Items", "Missing Topic", "Bad:Topic", "Bad[Topic]"
            });

            CogsLoadResult topicResult = new CogsDirectoryReader().LoadResult(fixture.Path);

            AssertDiagnostic(topicResult.Diagnostics, "COGS-READ-030", "index.txt", 2);
            AssertDiagnostic(topicResult.Diagnostics, "COGS-READ-031", "index.txt", 3);
            AssertDiagnostic(topicResult.Diagnostics, "COGS-READ-032", "index.txt", 4);
            AssertDiagnostic(topicResult.Diagnostics, "COGS-READ-035", "index.txt", 5);
            AssertDiagnostic(topicResult.Diagnostics, "COGS-READ-031", "index.txt", 6);
            AssertDiagnostic(topicResult.Diagnostics, "COGS-READ-031", "index.txt", 7);

            File.WriteAllText(Path.Combine(topics, "index.txt"), "All Content Items" + Environment.NewLine);
            File.WriteAllLines(Path.Combine(topic, "items.txt"), new[] { "Thing", "", "Bad/Path", "Thing" });
            CogsLoadResult itemResult = new CogsDirectoryReader().LoadResult(fixture.Path);

            AssertDiagnostic(itemResult.Diagnostics, "COGS-READ-036", "items.txt", 2);
            AssertDiagnostic(itemResult.Diagnostics, "COGS-READ-038", "items.txt", 3);
            AssertDiagnostic(itemResult.Diagnostics, "COGS-READ-039", "items.txt", 4);
        }

        [Fact]
        public void TopicMembershipDiagnosticsRetainItemsFileRows()
        {
            using var fixture = new ModelDirectory();
            string topics = Path.Combine(fixture.Path, "Topics");
            string topic = Path.Combine(topics, "All Content Items");
            Directory.CreateDirectory(topic);
            File.WriteAllText(Path.Combine(topics, "index.txt"), "All Content Items" + Environment.NewLine);
            File.WriteAllLines(Path.Combine(topic, "items.txt"), new[] { "Thing", "thing", "Missing" });

            CogsLoadResult loaded = new CogsDirectoryReader().LoadResult(fixture.Path);
            Assert.True(loaded.Success, string.Join(Environment.NewLine, loaded.Diagnostics));
            List<CogsError> diagnostics = DtoValidation.Validate(loaded.Model);

            AssertDiagnostic(diagnostics, "COGS-VAL-TOPIC-003", "items.txt", 2);
            AssertDiagnostic(diagnostics, "COGS-VAL-TOPIC-002", "items.txt", 3);
        }

        [Fact]
        public void SemanticValidationRejectsDirectiveSyntaxWithTocSourceLocation()
        {
            using var temporary = new ModelDirectory();
            string articles = Path.Combine(temporary.Path, "manual-articles");
            Directory.CreateDirectory(articles);
            var dto = ValidDto();
            dto.ArticlesPath = articles;
            dto.ArticleTocEntries.Add(":hidden:");
            dto.ArticleTocEntrySources.Add(new SourceTextEntry
            {
                Value = ":hidden:", SourcePath = Path.Combine(articles, "toc.txt"), SourceLine = 7, SourceColumn = 1
            });

            List<CogsError> diagnostics = DtoValidation.Validate(dto);

            AssertDiagnostic(diagnostics, "COGS-VAL-ARTICLE-002", "toc.txt", 7);
        }

        [Fact]
        public void SettingsAreRequiredAndStrictlyValidated()
        {
            var dto = ValidDto();
            dto.Settings.RemoveAll(x => x.Key == "Author");
            dto.Settings.First(x => x.Key == "Version").Value = "1.2";
            dto.Settings.First(x => x.Key == "Slug").Value = "Bad Slug";
            dto.Settings.First(x => x.Key == "NamespaceUrl").Value = "relative";
            dto.Settings.First(x => x.Key == "NamespacePrefix").Value = "xml";

            var codes = DtoValidation.Validate(dto).Select(x => x.Code).ToHashSet();

            Assert.Contains("COGS-VAL-SET-004", codes);
            Assert.Contains("COGS-VAL-SET-008", codes);
            Assert.Contains("COGS-VAL-SET-007", codes);
            Assert.Contains("COGS-VAL-SET-009", codes);
            Assert.Contains("COGS-VAL-SET-010", codes);
        }

        [Fact]
        public void IdentificationMustBeNonemptyScalarAndStringOrUri()
        {
            var dto = ValidDto();
            dto.Identification.Clear();
            dto.Identification.Add(Property("BadId", "int", "0", "n"));

            var codes = DtoValidation.Validate(dto).Select(x => x.Code).ToHashSet();

            Assert.Contains("COGS-VAL-ID-003", codes);
            Assert.Contains("COGS-VAL-ID-004", codes);
        }

        [Fact]
        public void IdentificationAcceptsExplicitFalseFlagsButRejectsTrueOrMalformedFlags()
        {
            var dto = ValidDto();
            dto.Identification[0].Ordered = "FALSE";
            dto.Identification[0].AllowSubtypes = "false";
            Assert.DoesNotContain(DtoValidation.Validate(dto), error => error.Code == "COGS-VAL-ID-005");

            dto.Identification[0].Ordered = "true";
            Assert.Contains(DtoValidation.Validate(dto), error => error.Code == "COGS-VAL-ID-005");
            dto.Identification[0].Ordered = "sometimes";
            Assert.Contains(DtoValidation.Validate(dto), error => error.Code == "COGS-VAL-ID-005");
        }

        [Fact]
        public void CardinalityAndFlagsUseOneCanonicalParser()
        {
            var dto = ValidDto();
            dto.ItemTypes[0].Properties.Add(new Cogs.Dto.Property
            {
                Name = "BadFlags",
                DataType = "string",
                MinCardinality = "01",
                MaxCardinality = "0",
                Ordered = "yes",
                AllowSubtypes = "1"
            });

            var codes = DtoValidation.Validate(dto).Select(x => x.Code).ToHashSet();

            Assert.Contains("COGS-VAL-CARD-001", codes);
            Assert.Contains("COGS-VAL-FLAG-001", codes);
            Assert.Contains("COGS-VAL-FLAG-002", codes);
        }

        [Fact]
        public void AllowSubtypesAppliesToItemsAndCompositesButNotPrimitives()
        {
            var dto = ValidDto();
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "RelatedItem" });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "RelatedValue" });
            var itemValue = Property("ItemValue", "RelatedItem");
            itemValue.AllowSubtypes = "true";
            var compositeValue = Property("CompositeValue", "RelatedValue");
            compositeValue.AllowSubtypes = "true";
            var primitiveValue = Property("PrimitiveValue", "string");
            primitiveValue.AllowSubtypes = "true";
            dto.ItemTypes[0].Properties.Add(itemValue);
            dto.ItemTypes[0].Properties.Add(compositeValue);
            dto.ItemTypes[0].Properties.Add(primitiveValue);

            var diagnostics = DtoValidation.Validate(dto);

            Assert.DoesNotContain(diagnostics, error => error.Code == "COGS-VAL-SUB-001" &&
                (error.ModelPath is "Thing.ItemValue" or "Thing.CompositeValue"));
            Assert.Contains(diagnostics, error => error.Code == "COGS-VAL-SUB-001" &&
                error.ModelPath == "Thing.PrimitiveValue");
        }

        [Fact]
        public void AllowSubtypesTrueWarnsWhenDeclaredTypesHaveNoDescendants()
        {
            var dto = ValidDto();
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "RelatedItem" });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "RelatedValue" });

            var itemValue = Property("ItemValue", "RelatedItem");
            itemValue.AllowSubtypes = "true";
            itemValue.SourcePath = "ItemTypes/Thing/Thing.csv";
            itemValue.SourceLine = 4;
            dto.ItemTypes[0].Properties.Add(itemValue);

            var compositeValue = Property("CompositeValue", "RelatedValue");
            compositeValue.AllowSubtypes = "TRUE";
            compositeValue.SourcePath = "ItemTypes/Thing/Thing.csv";
            compositeValue.SourceLine = 5;
            dto.ItemTypes[0].Properties.Add(compositeValue);

            CogsError[] warnings = DtoValidation.Validate(dto)
                .Where(error => error.Code == "COGS-VAL-SUB-003")
                .ToArray();

            Assert.Equal(2, warnings.Length);
            Assert.All(warnings, warning => Assert.Equal(ErrorLevel.Warning, warning.Level));
            Assert.Contains(warnings, warning => warning.ModelPath == "Thing.ItemValue" &&
                warning.SourcePath == "ItemTypes/Thing/Thing.csv" && warning.Line == 4 &&
                warning.Message.Contains("no other item type extends 'RelatedItem'", StringComparison.Ordinal));
            Assert.Contains(warnings, warning => warning.ModelPath == "Thing.CompositeValue" &&
                warning.SourcePath == "ItemTypes/Thing/Thing.csv" && warning.Line == 5 &&
                warning.Message.Contains("no other composite type extends 'RelatedValue'", StringComparison.Ordinal));
        }

        [Fact]
        public void AllowSubtypesTrueDoesNotWarnWhenDeclaredTypesHaveDescendants()
        {
            var dto = ValidDto();
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "RelatedItem" });
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "SpecialItem", Extends = "RelatedItem" });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "RelatedValue" });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "SpecialValue", Extends = "RelatedValue" });

            var itemValue = Property("ItemValue", "RelatedItem");
            itemValue.AllowSubtypes = "true";
            dto.ItemTypes[0].Properties.Add(itemValue);

            var compositeValue = Property("CompositeValue", "RelatedValue");
            compositeValue.AllowSubtypes = "true";
            dto.ItemTypes[0].Properties.Add(compositeValue);

            IReadOnlyList<CogsError> diagnostics = DtoValidation.Validate(dto);

            Assert.DoesNotContain(diagnostics, error => error.Code == "COGS-VAL-SUB-003");
        }

        [Fact]
        public void AbstractDeclaredTypesWarnAndAreBuiltAsSubtypeEnabled()
        {
            var dto = ValidDto();
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "AbstractItem", IsAbstract = true });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "AbstractValue", IsAbstract = true });
            dto.ItemTypes[0].Properties.Add(Property("ItemValue", "AbstractItem"));
            var compositeValue = Property("CompositeValue", "AbstractValue");
            compositeValue.AllowSubtypes = "false";
            dto.ItemTypes[0].Properties.Add(compositeValue);

            var diagnostics = DtoValidation.Validate(dto);
            var warnings = diagnostics.Where(error => error.Code == "COGS-VAL-SUB-002").ToArray();

            Assert.Equal(2, warnings.Length);
            Assert.All(warnings, warning => Assert.Equal(ErrorLevel.Warning, warning.Level));
            Assert.DoesNotContain(diagnostics, error => error.Level == ErrorLevel.Error);

            CogsBuildResult built = new CogsModelBuilder().BuildResult(dto);
            Assert.True(built.Success, string.Join(Environment.NewLine, built.Diagnostics));
            var properties = built.Model.ItemTypes.Single(type => type.Name == "Thing").Properties;
            Assert.True(properties.Single(property => property.Name == "ItemValue").AllowSubtypes);
            Assert.True(properties.Single(property => property.Name == "CompositeValue").AllowSubtypes);
        }

        [Fact]
        public void DcTermsIsOnlyASourceMacroAndNeverARuntimeBuiltin()
        {
            Assert.DoesNotContain("dcTerms", CogsTypes.SimpleTypeNames);
            var dto = ValidDto();
            dto.ItemTypes[0].Properties.Add(Property("NotTheMacro", "dcTerms"));

            Assert.Contains(DtoValidation.Validate(dto), error =>
                error.Code is "COGS-VAL-DCTERMS-001" or "COGS-VAL-PROP-004");
            Assert.False(new CogsModelBuilder().BuildResult(dto).Success);
        }

        [Theory]
        [InlineData("This")]
        [InlineData("Any")]
        [InlineData("DCterms")]
        [InlineData("dcterms")]
        [InlineData("DcTerms")]
        public void RetiredAndNearMatchPseudoTypesAreRejected(string dataType)
        {
            var dto = ValidDto();
            dto.ItemTypes[0].Properties.Add(Property("PseudoValue", dataType));

            IReadOnlyList<CogsError> diagnostics = DtoValidation.Validate(dto);

            Assert.Contains(diagnostics, error =>
                error.Code is "COGS-VAL-PROP-004" or "COGS-VAL-DCTERMS-001");
            Assert.False(new CogsModelBuilder().BuildResult(dto).Success);
        }

        public static IEnumerable<object[]> BuiltinCaseVariants => CogsTypes.SimpleTypeNames
            .Select(name => new object[] { name, char.ToUpperInvariant(name[0]) + name[1..] });

        [Theory]
        [MemberData(nameof(BuiltinCaseVariants))]
        public void EveryBuiltinRejectsCaseInsensitiveShadowingAndNearMatchReferences(
            string builtin,
            string nearMatch)
        {
            var shadowModel = ValidDto();
            shadowModel.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = nearMatch });
            IReadOnlyList<CogsError> shadowDiagnostics = DtoValidation.Validate(shadowModel);

            Assert.Contains(shadowDiagnostics, error =>
                error.Code == "COGS-VAL-NAME-002" && error.Message.Contains(builtin, StringComparison.Ordinal));

            var referenceModel = ValidDto();
            referenceModel.ItemTypes[0].Properties.Add(Property("NearMatch", nearMatch));
            IReadOnlyList<CogsError> referenceDiagnostics = DtoValidation.Validate(referenceModel);

            Assert.Contains(referenceDiagnostics, error =>
                error.Code == "COGS-VAL-PROP-005" && error.Message.Contains(builtin, StringComparison.Ordinal));
        }

        [Fact]
        public void ExactDcTermsMarkerExpandsToDeclaredPropertiesWithoutRuntimePseudoType()
        {
            using var fixture = new ModelDirectory();
            string marker = string.Join(",", new[]
            {
                "DcTerms", "dcTerms", "0", "1", "", "", "", "", "", "", "", "", "", "", "",
                "urn:historical:namespace", "legacy-element", "legacy-choice"
            });
            File.WriteAllText(Path.Combine(fixture.Path, "ItemTypes", "Thing", "Thing.csv"),
                PropertyHeader + Environment.NewLine + marker + Environment.NewLine);

            CogsLoadResult loaded = new CogsDirectoryReader().LoadResult(fixture.Path);

            Assert.True(loaded.Success, string.Join(Environment.NewLine, loaded.Diagnostics));
            Cogs.Dto.ItemType thing = Assert.Single(loaded.Model.ItemTypes);
            Assert.Contains(thing.Properties, property => property.Name == "DublinCoreTitle" && property.DataType == "langString");
            Assert.DoesNotContain(thing.Properties, property => property.Name == "DcTerms" || property.DataType == "dcTerms");
            Assert.DoesNotContain(DtoValidation.Validate(loaded.Model), error => error.Level >= ErrorLevel.Error);
        }

        [Fact]
        public void HistoricalPropertyColumnsAreInertButRemainAvailableOnTheModel()
        {
            var dto = ValidDto();
            var property = Property("LegacyValue", "string");
            property.DeprecatedNamespace = "not a namespace";
            property.DeprecatedElementOrAttribute = "neither";
            property.DeprecatedChoiceGroup = "historical group";
            dto.ItemTypes[0].Properties.Add(property);

            IReadOnlyList<CogsError> diagnostics = DtoValidation.Validate(dto);

            Assert.DoesNotContain(diagnostics, error => error.Level >= ErrorLevel.Error);
            CogsBuildResult built = new CogsModelBuilder().BuildResult(dto);
            Assert.True(built.Success, string.Join(Environment.NewLine, built.Diagnostics));
            Cogs.Model.Property modelProperty = built.Model!.ItemTypes.Single().Properties
                .Single(candidate => candidate.Name == "LegacyValue");
            Assert.Equal("not a namespace", modelProperty.DeprecatedNamespace);
            Assert.Equal("neither", modelProperty.DeprecatedElementOrAttribute);
            Assert.Equal("historical group", modelProperty.DeprecatedChoiceGroup);
        }

        [Fact]
        public void ArbitrarilyLargeCanonicalCardinalityIsAccepted()
        {
            Assert.True(CogsConventions.TryParseCardinality("0", "999999999999999999999999999", out _, out var maximum, out _));
            Assert.NotNull(maximum);
        }

        [Fact]
        public void EnumerationParserUsesExactWhitespaceTokenization()
        {
            Assert.Empty(CogsConventions.ParseEnumeration(null));
            Assert.Empty(CogsConventions.ParseEnumeration(" \t\r\n "));
            Assert.Equal(new[] { "OnlyValue" }, CogsConventions.ParseEnumeration("OnlyValue"));
            Assert.Equal(new[] { "Red", "green", "BLUE" },
                CogsConventions.ParseEnumeration("  Red   green\tBLUE\r\n"));
            Assert.Equal(new[] { "[\"draft\",\"final\"]" },
                CogsConventions.ParseEnumeration("[\"draft\",\"final\"]"));
        }

        [Fact]
        public void InheritanceCyclesAndCrossKindParentsAreErrors()
        {
            var cycle = ValidDto();
            cycle.ItemTypes[0].Extends = "SecondItem";
            cycle.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "SecondItem", Extends = "Thing" });

            Assert.Contains(DtoValidation.Validate(cycle), x => x.Code == "COGS-VAL-INH-005");

            var crossKind = ValidDto();
            crossKind.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "Value" });
            crossKind.ItemTypes[0].Extends = "Value";

            Assert.Contains(DtoValidation.Validate(crossKind), x => x.Code == "COGS-VAL-INH-004");
        }

        [Fact]
        public void AbstractTypesWithoutConcreteDescendantsProduceSourceLocatedWarnings()
        {
            var dto = ValidDto();
            dto.ItemTypes.Add(new Cogs.Dto.ItemType
            {
                Name = "LonelyItem",
                IsAbstract = true,
                SourcePath = "ItemTypes/LonelyItem"
            });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType
            {
                Name = "LonelyComposite",
                IsAbstract = true,
                SourcePath = "CompositeTypes/LonelyComposite"
            });

            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "ItemBase", IsAbstract = true });
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "ItemMiddle", IsAbstract = true, Extends = "ItemBase" });
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "ItemLeaf", Extends = "ItemMiddle" });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "CompositeBase", IsAbstract = true });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "CompositeMiddle", IsAbstract = true, Extends = "CompositeBase" });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "CompositeLeaf", Extends = "CompositeMiddle" });

            CogsError[] warnings = DtoValidation.Validate(dto)
                .Where(error => error.Code == "COGS-VAL-INH-007")
                .ToArray();

            Assert.Equal(2, warnings.Length);
            Assert.All(warnings, warning => Assert.Equal(ErrorLevel.Warning, warning.Level));
            Assert.Contains(warnings, warning => warning.ModelPath == "LonelyItem" &&
                warning.SourcePath == "ItemTypes/LonelyItem" && warning.Message.Contains("abstract item", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(warnings, warning => warning.ModelPath == "LonelyComposite" &&
                warning.SourcePath == "CompositeTypes/LonelyComposite" && warning.Message.Contains("abstract composite", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(warnings, warning => warning.ModelPath is "ItemBase" or "ItemMiddle" or "CompositeBase" or "CompositeMiddle");
        }

        [Fact]
        public void PrimitiveMarkerIsCompositeOnlyAndDoesNotChangeCompositeShape()
        {
            var invalidItem = ValidDto();
            invalidItem.ItemTypes[0].IsPrimitive = true;
            Assert.Contains(DtoValidation.Validate(invalidItem), error => error.Code == "COGS-VAL-TYPE-001");

            var validComposite = ValidDto();
            var value = new Cogs.Dto.DataType { Name = "ValueObject", IsPrimitive = true };
            value.Properties.Add(Property("Content", "string", "1", "1"));
            validComposite.ReusableDataTypes.Add(value);
            Assert.DoesNotContain(DtoValidation.Validate(validComposite), error => error.Level >= ErrorLevel.Error);

            CogsBuildResult built = new CogsModelBuilder().BuildResult(validComposite);
            Assert.True(built.Success, string.Join(Environment.NewLine, built.Diagnostics));
            Cogs.Model.DataType generated = Assert.Single(built.Model.ReusableDataTypes);
            Assert.True(generated.IsPrimitive);
            Assert.Equal("Content", Assert.Single(generated.Properties).Name);
        }

        [Fact]
        public void InheritedAndTargetNormalizedNamesCannotCollide()
        {
            var dto = ValidDto();
            dto.ItemTypes[0].IsAbstract = true;
            dto.ItemTypes[0].Properties.Add(Property("DisplayName", "string"));
            var child = new Cogs.Dto.ItemType { Name = "ChildThing", Extends = "Thing" };
            child.Properties.Add(Property("Display_Name", "string"));
            dto.ItemTypes.Add(child);
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "FooBar" });
            dto.ReusableDataTypes.Add(new Cogs.Dto.DataType { Name = "Foo_Bar" });

            var codes = DtoValidation.Validate(dto).Select(x => x.Code).ToHashSet();

            Assert.Contains("COGS-VAL-INH-006", codes);
            Assert.Contains("COGS-VAL-NAME-007", codes);
        }

        [Fact]
        public void RuntimeMemberNamesCannotBeProperties()
        {
            var dto = ValidDto();
            dto.ItemTypes[0].Properties.Add(Property("ToJson", "string"));

            Assert.Contains(DtoValidation.Validate(dto), x => x.Code == "COGS-VAL-PROP-006");
        }

        [Fact]
        public void ReusedPropertyNamesRequireTheSameDatatypeAcrossTypes()
        {
            var dto = ValidDto();
            var alpha = new Cogs.Dto.ItemType { Name = "AlphaType" };
            alpha.Properties.Add(Property("SharedValue", "string"));
            var zeta = new Cogs.Dto.ItemType { Name = "ZetaType" };
            Cogs.Dto.Property conflicting = Property("SharedValue", "int");
            conflicting.SourcePath = "ItemTypes/ZetaType/ZetaType.csv";
            conflicting.SourceLine = 7;
            zeta.Properties.Add(conflicting);
            dto.ItemTypes.Add(alpha);
            dto.ItemTypes.Add(zeta);

            CogsError diagnostic = Assert.Single(
                DtoValidation.Validate(dto), error => error.Code == "COGS-VAL-PROP-007");

            Assert.Equal(ErrorLevel.Error, diagnostic.Level);
            Assert.Equal("ItemTypes/ZetaType/ZetaType.csv", diagnostic.SourcePath);
            Assert.Equal(7, diagnostic.Line);
            Assert.Equal("ZetaType.SharedValue", diagnostic.ModelPath);
        }

        [Fact]
        public void ReusedPropertyNamesRequireTheSameDatatypeAcrossIdentificationAndTypes()
        {
            var dto = ValidDto();
            Cogs.Dto.Property conflicting = Property("ID", "int");
            conflicting.SourcePath = "ItemTypes/Thing/Thing.csv";
            conflicting.SourceLine = 3;
            dto.ItemTypes[0].Properties.Add(conflicting);

            CogsError diagnostic = Assert.Single(
                DtoValidation.Validate(dto), error => error.Code == "COGS-VAL-PROP-007");

            Assert.Equal(ErrorLevel.Error, diagnostic.Level);
            Assert.Equal("ItemTypes/Thing/Thing.csv", diagnostic.SourcePath);
            Assert.Equal(3, diagnostic.Line);
            Assert.Equal("Thing.ID", diagnostic.ModelPath);
            Assert.Contains("Identification.ID", diagnostic.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ReusedPropertyNamesMayHaveDifferentLocalDescriptionsCardinalitiesAndFacets()
        {
            var dto = ValidDto();
            var alpha = new Cogs.Dto.ItemType { Name = "AlphaType" };
            Cogs.Dto.Property alphaValue = Property("SharedValue", "string", "0", "1");
            alphaValue.Description = "Alpha description";
            alphaValue.MinLength = 1;
            alpha.Properties.Add(alphaValue);

            var zeta = new Cogs.Dto.ItemType { Name = "ZetaType" };
            Cogs.Dto.Property zetaValue = Property("SharedValue", "string", "1", "n");
            zetaValue.Description = "Zeta description";
            zetaValue.MaxLength = 20;
            zeta.Properties.Add(zetaValue);

            dto.ItemTypes.Add(alpha);
            dto.ItemTypes.Add(zeta);

            IReadOnlyList<CogsError> diagnostics = DtoValidation.Validate(dto);

            Assert.DoesNotContain(diagnostics, error => error.Code == "COGS-VAL-PROP-007");
            Assert.DoesNotContain(diagnostics, error => error.Level == ErrorLevel.Error);
        }

        [Fact]
        public void WhitespaceEnumerationsAreCanonicalAndNonportablePatternsAreRejected()
        {
            var dto = ValidDto();
            var property = Property("Code", "string");
            property.Enumeration = "red blue";
            property.Pattern = "^[A-Z]+$";
            dto.ItemTypes[0].Properties.Add(property);

            var codes = DtoValidation.Validate(dto).Select(x => x.Code).ToHashSet();

            Assert.DoesNotContain("COGS-VAL-FACET-005", codes);
            Assert.DoesNotContain("COGS-VAL-FACET-012", codes);
            Assert.Contains("COGS-VAL-FACET-004", codes);

            CogsBuildResult built = new CogsModelBuilder().BuildResult(dto);
            Assert.True(built.Success, string.Join(Environment.NewLine, built.Diagnostics));
            Assert.Equal(new[] { "red", "blue" },
                built.Model.ItemTypes[0].Properties.Single(x => x.Name == "Code").Enumeration);
        }

        [Fact]
        public void EnumerationValidationRetainsDuplicateLexicalAndFacetChecks()
        {
            var dto = ValidDto();

            var duplicate = Property("Duplicate", "string");
            duplicate.Enumeration = "red red";
            dto.ItemTypes[0].Properties.Add(duplicate);

            var malformed = Property("Malformed", "int");
            malformed.Enumeration = "1 nope";
            dto.ItemTypes[0].Properties.Add(malformed);

            var duplicateValue = Property("DuplicateValue", "decimal");
            duplicateValue.Enumeration = "1.0 1.00";
            dto.ItemTypes[0].Properties.Add(duplicateValue);

            var contradictory = Property("Contradictory", "string");
            contradictory.Enumeration = "red blue";
            contradictory.MinLength = 4;
            dto.ItemTypes[0].Properties.Add(contradictory);

            var codes = DtoValidation.Validate(dto).Select(x => x.Code).ToHashSet();

            Assert.Contains("COGS-VAL-FACET-007", codes);
            Assert.Contains("COGS-VAL-FACET-013", codes);
            Assert.Contains("COGS-VAL-FACET-014", codes);
            Assert.Contains("COGS-VAL-FACET-015", codes);
            Assert.DoesNotContain("COGS-VAL-FACET-005", codes);
            Assert.DoesNotContain("COGS-VAL-FACET-012", codes);
        }

        [Theory]
        [InlineData("date", "2023-02-29")]
        [InlineData("dateTime", "2024-01-01T25:00:00Z")]
        [InlineData("time", "12:00:00+14:01")]
        [InlineData("gMonthDay", "--02-30")]
        [InlineData("decimal", "+1.0")]
        [InlineData("decimal", "01.0")]
        public void MalformedFacetLexicalValuesAreRejected(string dataType, string lexical)
        {
            var dto = ValidDto();
            var property = Property("Constrained", dataType);
            property.MinInclusive = lexical;
            dto.ItemTypes[0].Properties.Add(property);

            Assert.Contains(DtoValidation.Validate(dto), x => x.Code == "COGS-VAL-FACET-010");
        }

        [Fact]
        public void BuilderDoesNotFabricateUnknownTypes()
        {
            var dto = ValidDto();
            dto.ItemTypes[0].Properties.Add(Property("Mystery", "UnknownType"));

            var result = new CogsModelBuilder().BuildResult(dto);

            Assert.False(result.Success);
            Assert.Null(result.Model);
            Assert.Contains(result.Diagnostics, x => x.Code == "COGS-BUILD-020");
        }

        [Fact]
        public void RelationshipsRetainDistinctPathsAndGuardCompositeCycles()
        {
            var dto = ValidDto();
            var node = new Cogs.Dto.DataType { Name = "Node" };
            node.Properties.Add(Property("Self", "Node"));
            node.Properties.Add(Property("Target", "Thing"));
            dto.ReusableDataTypes.Add(node);
            dto.ItemTypes[0].Properties.Add(Property("First", "Node"));
            dto.ItemTypes[0].Properties.Add(Property("Second", "Node"));

            var result = new CogsModelBuilder().BuildResult(dto);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.Contains(result.Model.ItemTypes[0].Relationships, x => x.PropertyName == "First/Target");
            Assert.Contains(result.Model.ItemTypes[0].Relationships, x => x.PropertyName == "Second/Target");
        }

        private static CogsDtoModel ValidDto()
        {
            var dto = new CogsDtoModel();
            foreach (var (key, value) in new[]
            {
                ("CogsVersion", "2.0"), ("Title", "Test Model"), ("ShortTitle", "Test"),
                ("Slug", "test_model"), ("Description", ""), ("Version", "1.0.0"),
                ("Author", ""), ("Copyright", ""), ("NamespaceUrl", "https://example.org/model"),
                ("NamespacePrefix", "model")
            }) dto.Settings.Add(new Setting { Key = key, Value = value });
            dto.Identification.Add(Property("ID", "string", "1", "1"));
            dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "Thing" });
            return dto;
        }

        private static Cogs.Dto.Property Property(string name, string dataType, string min = "0", string max = "1") => new Cogs.Dto.Property
        {
            Name = name,
            DataType = dataType,
            MinCardinality = min,
            MaxCardinality = max
        };

        private static void AssertDiagnostic(IEnumerable<CogsError> diagnostics, string code, string sourceFile, int line)
        {
            Assert.Contains(diagnostics, error => error.Code == code && error.Line == line && error.Column == 1 &&
                error.SourcePath?.EndsWith(sourceFile, StringComparison.Ordinal) == true);
        }

        private sealed class ModelDirectory : IDisposable
        {
            public ModelDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cogs-contract-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(System.IO.Path.Combine(Path, "Settings"));
                Directory.CreateDirectory(System.IO.Path.Combine(Path, "ItemTypes", "Thing"));
                Directory.CreateDirectory(System.IO.Path.Combine(Path, "CompositeTypes"));
                File.WriteAllText(System.IO.Path.Combine(Path, "Settings", "Identification.csv"),
                    PropertyHeader + Environment.NewLine + "ID,string,1,1,,,,,,,,,,,,,," + Environment.NewLine);
                File.WriteAllText(System.IO.Path.Combine(Path, "Settings", "Settings.csv"),
                    "Key,Value" + Environment.NewLine +
                    "CogsVersion,2.0" + Environment.NewLine + "Title,Test Model" + Environment.NewLine +
                    "ShortTitle,Test" + Environment.NewLine + "Slug,test_model" + Environment.NewLine +
                    "Description," + Environment.NewLine + "Version,1.0.0" + Environment.NewLine +
                    "Author," + Environment.NewLine + "Copyright," + Environment.NewLine +
                    "NamespaceUrl,https://example.org/model" + Environment.NewLine + "NamespacePrefix,model" + Environment.NewLine);
                File.WriteAllText(System.IO.Path.Combine(Path, "ItemTypes", "Thing", "Thing.csv"), PropertyHeader + Environment.NewLine);
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
        }
    }
}
