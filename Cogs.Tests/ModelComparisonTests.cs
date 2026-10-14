using Cogs.Common;
using Cogs.Console;
using Cogs.Dto;
using Cogs.Model;
using Cogs.Model.Comparison;
using Cogs.Publishers.Comparison;
using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;
using DtoProperty = Cogs.Dto.Property;
using DtoType = Cogs.Dto.DataType;

namespace Cogs.Tests;

public sealed class ModelComparisonTests
{
    [Fact]
    public void IdenticalModelsIgnoreSourceOnlyAndFormattingChanges()
    {
        var before = Fixture(); var after = Fixture();
        before.ItemTypes[0].Description = "First\r\nSecond";
        after.ItemTypes[0].Description = "First\nSecond";
        after.ItemTypes[0].Properties[0].MinCardinality = "";
        after.ItemTypes[0].Properties[0].Ordered = "FALSE";
        after.ItemTypes[0].Properties[0].DeprecatedNamespace = "ignored sentinel";
        after.ItemTypes[0].Properties[0].DeprecatedChoiceGroup = "ignored";
        after.ItemTypes[0].Properties[0].DeprecatedElementOrAttribute = "ignored";
        after.ItemTypes[0].AdditionalText.Add(new AdditionalText { Name = "Ignored", Content = "notes" });
        after.HeaderInclude = "ignored";
        Assert.Empty(Compare(before, after).Changes);
    }

    [Theory]
    [InlineData("Description", "A new description", "Description")]
    [InlineData("DataType", "anyURI", "Datatype")]
    [InlineData("MinCardinality", "1", "Minimum")]
    [InlineData("MaxCardinality", "9999999999999999999999999999", "Maximum")]
    [InlineData("Ordered", "true", "Ordered")]
    [InlineData("MinInclusive", "1", "Minimum inclusive")]
    [InlineData("MinExclusive", "2", "Minimum exclusive")]
    [InlineData("MaxInclusive", "99", "Maximum inclusive")]
    [InlineData("MaxExclusive", "98", "Maximum exclusive")]
    [InlineData("Pattern", "[A-Z]+", "Pattern")]
    public void EveryScalarPropertyFieldIsReportedOnceAtItsDeclaration(string field, string value, string label)
    {
        var before = Fixture(); var after = Fixture();
        typeof(DtoProperty).GetProperty(field).SetValue(after.ItemTypes[0].Properties[0], value);
        var change = Assert.Single(Compare(before, after).Changes);
        Assert.Equal("ItemTypes/Root/Label", change.Path);
        Assert.Contains(change.Fields, f => f.Field == label && f.After == value);
        Assert.Equal(new[] { "ItemTypes/Child", "ItemTypes/Leaf" }, change.AffectedTypes);
    }

    [Fact]
    public void FlagsLengthsEnumerationsAndIdentityAreComparedWithoutInheritedCopies()
    {
        var before = Fixture(); var after = Fixture();
        before.ItemTypes[0].Properties[0].Enumeration = "red green blue";
        after.ItemTypes[0].Properties[0].Enumeration = "blue\tred yellow";
        after.ItemTypes[0].Properties[0].MinLength = 2;
        after.ItemTypes[0].Properties[0].MaxLength = 10;
        after.ItemTypes[0].Properties[1].AllowSubtypes = "true";
        after.Identification[0].Description = "New global ID text";
        var result = Compare(before, after);
        Assert.Equal(3, result.Changes.Count);
        var label = result.Changes.Single(c => c.Path.EndsWith("/Label"));
        Assert.Contains(label.Fields, f => f.Field == "Enumeration removed" && f.Before == "green");
        Assert.Contains(label.Fields, f => f.Field == "Enumeration added" && f.After == "yellow");
        Assert.Contains(label.Fields, f => f.Field == "Enumeration order");
        Assert.Contains(label.Fields, f => f.Field == "Minimum length" && f.After == "2");
        Assert.Contains(label.Fields, f => f.Field == "Maximum length" && f.After == "10");
        Assert.Contains(result.Changes, c => c.Fields.Any(f => f.Field == "Allow subtypes"));
        Assert.Equal(3, result.Changes.Single(c => c.Path == "Identification/ID").AffectedTypes.Count);
    }

    [Fact]
    public void InsertionDoesNotReorderSurvivorsButSwappingThemDoes()
    {
        var before = Fixture(); var after = Fixture();
        after.ItemTypes[0].Properties.Insert(0, Property("Inserted"));
        Assert.Equal(ModelChangeKind.Added, Assert.Single(Compare(before, after).Changes).Kind);
        after.ItemTypes[0].Properties.Reverse();
        Assert.Contains(Compare(before, after).Changes, c => c.Kind.HasFlag(ModelChangeKind.Reordered));
    }

    [Fact]
    public void ConfirmedTypeAndPropertyRenamesDoNotProduceReferenceNoise()
    {
        var before = Fixture(); var after = Fixture();
        after.ItemTypes[0].Name = "Renamed";
        after.ItemTypes[1].Extends = "Renamed";
        after.ItemTypes[0].Properties[0].Name = "Caption";
        var result = Compare(before, after,
            new("ItemTypes/Root", "ItemTypes/Renamed"), new("ItemTypes/Root/Label", "ItemTypes/Renamed/Caption"));
        Assert.Equal(2, result.Changes.Count);
        Assert.All(result.Changes, c => Assert.Equal(ModelChangeKind.Renamed, c.Kind));
        after.ReusableDataTypes[0].Name = "NewCell";
        after.ItemTypes[0].Properties[1].DataType = "NewCell";
        var another = Compare(before, after, new("ItemTypes/Root", "ItemTypes/Renamed"),
            new("ItemTypes/Root/Label", "ItemTypes/Renamed/Caption"), new("CompositeTypes/Cell", "CompositeTypes/NewCell"));
        Assert.Equal(3, another.Changes.Count);
        Assert.DoesNotContain(another.Changes, c => c.Fields.Any(f => f.Field == "Datatype"));
    }

    [Fact]
    public void PropertyMovesCanAlsoRenameAndModifyFields()
    {
        var before = Fixture(); var after = Fixture();
        var property = after.ItemTypes[0].Properties[0];
        after.ItemTypes[0].Properties.Remove(property);
        property.Name = "Caption"; property.Description = "Changed";
        after.ItemTypes[1].Properties.Add(property);
        var change = Assert.Single(Compare(before, after, new RenameMapping("ItemTypes/Root/Label", "ItemTypes/Child/Caption")).Changes);
        Assert.Equal(ModelChangeKind.Moved | ModelChangeKind.Renamed | ModelChangeKind.Modified, change.Kind);
    }

    [Fact]
    public void SuggestionsAreUniqueAndNeverSilentlyConfirmRenames()
    {
        var before = Fixture(); var after = Fixture();
        after.ReusableDataTypes[0].Properties[0].Name = "RenamedValue";
        var result = Compare(before, after);
        Assert.Contains(result.Suggestions, s => s.From == "CompositeTypes/Cell/Value" && s.To.EndsWith("RenamedValue"));
        Assert.Contains(result.Changes, c => c.Kind == ModelChangeKind.Added);
        Assert.Contains(result.Changes, c => c.Kind == ModelChangeKind.Removed);
        after.ReusableDataTypes[0].Properties.Add(Property("AnotherValue"));
        Assert.DoesNotContain(Compare(before, after).Suggestions, s => s.From == "CompositeTypes/Cell/Value");
    }

    [Fact]
    public void TypeSuggestionsIgnoreTheirPropertyNamesAndDescriptions()
    {
        var before = Fixture(); var after = Fixture();
        after.ReusableDataTypes[0].Name = "Replacement";
        after.ReusableDataTypes[0].Description = "Changed type description";
        after.ReusableDataTypes[0].Properties[0].Name = "RenamedValue";
        after.ReusableDataTypes[0].Properties[0].Description = "Changed field description";
        after.ItemTypes[0].Properties[1].DataType = "Replacement";
        var result = Compare(before, after);
        Assert.Contains(result.Suggestions, s => s.From == "CompositeTypes/Cell" && s.To == "CompositeTypes/Replacement");
        Assert.Contains(result.Changes, c => c.Path == "CompositeTypes/Replacement" && c.Kind == ModelChangeKind.Added);
    }

    [Fact]
    public void RecursiveUnusedTypesAndTypeFlagsDoNotCauseRecursionOrPruning()
    {
        var before = Fixture(); var after = Fixture();
        after.ReusableDataTypes.Add(new DtoType { Name = "Unused", IsPrimitive = true, Properties = [Property("Self", "Unused")] });
        after.ItemTypes[0].IsAbstract = true;
        after.Settings.Add(new Setting { Key = "Extra", Value = "value" });
        var result = Compare(before, after);
        Assert.Contains(result.Changes, c => c.Path == "CompositeTypes/Unused" && c.Kind == ModelChangeKind.Added);
        Assert.Contains(result.Changes, c => c.Path == "CompositeTypes/Unused/Self");
        Assert.Contains(result.Changes, c => c.Fields.Any(f => f.Field == "Abstract"));
        Assert.Contains(result.Changes, c => c.Path == "Settings/Extra");
    }

    [Fact]
    public void RejectsMissingDuplicateAndCrossKindMappings()
    {
        var before = Fixture(); var after = Fixture();
        foreach (var mappings in new RenameMapping[][] {
            [new("ItemTypes/Missing", "ItemTypes/Root")],
            [new("ItemTypes/Root", "CompositeTypes/Cell")],
            [new("ItemTypes/Root", "ItemTypes/Root"), new("ItemTypes/Child", "ItemTypes/Root")],
            [new("Settings/Title", "Settings/Title")] })
            Assert.Throws<ArgumentException>(() => Compare(before, after, mappings));
    }

    [Fact]
    public void SharedDiffSpansReconstructExactInputsAndReportEscapesUntrustedText()
    {
        foreach (var (before, after) in new[] { ("A café, old value.", "A café, new value."), ("First\nSecond\n", "First\nThird\n") })
        {
            var spans = ComparisonReportDocument.TextDiff(before, after);
            Assert.Equal(before, string.Concat(spans.Where(s => s.Change <= 0).Select(s => s.Text)));
            Assert.Equal(after, string.Concat(spans.Where(s => s.Change >= 0).Select(s => s.Text)));
        }
        var old = Fixture(); var current = Fixture();
        current.ItemTypes[0].Properties[0].Description = "Quoted \"text\", café.\n ```\n#read(\"/secret\")\n<script>x</script>";
        var blocks = ComparisonReportDocument.Build(Compare(old, current));
        string md = ComparisonReportDocument.Markdown(blocks), typ = ComparisonReportDocument.Typst(blocks);
        Assert.Contains("````text", md);
        Assert.Contains("\\\"/secret\\\"", typ);
        Assert.DoesNotContain("#read(\"/secret\")", typ);
        Assert.Contains("#removed(", typ); Assert.Contains("#added(", typ);
        Assert.Equal(md, ComparisonReportDocument.Markdown(blocks));
        Assert.Equal(typ, ComparisonReportDocument.Typst(blocks));
    }

    [Fact]
    public void ReportCountsChangedOwnersOnceAndGroupsTypesBeforeSettings()
    {
        var before = Fixture(); var after = Fixture();
        before.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "RemovedItem", Properties = [Property("Old")] });
        before.ReusableDataTypes.Add(new DtoType { Name = "RemovedComposite", Properties = [Property("Old")] });
        after.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "Zebra", Properties = [Property("New")] });
        after.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "Alpha", Properties = [Property("New")] });
        after.ReusableDataTypes.Add(new DtoType { Name = "AddedComposite", Properties = [Property("New")] });
        after.ItemTypes[0].Properties[0].Description = "Property-only change";
        after.ReusableDataTypes[0].Properties[0].Description = "Composite property change";
        after.Identification[0].Description = "Identification change";
        after.Settings.Single(s => s.Key == "Title").Value = "New title";
        var result = Compare(before, after);
        var organization = new ComparisonReportOrganization(result);
        Assert.Equal(new[] { new ReportSummaryRow("ItemTypes", 2, 1, 1), new("CompositeTypes", 1, 1, 1), new("Properties", 3, 2, 3) }, organization.Summary);
        Assert.DoesNotContain(organization.Types, t => t.Path is "ItemTypes/Child" or "ItemTypes/Leaf");
        var blocks = ComparisonReportDocument.Build(result);
        Assert.Equal(new[] { "Summary", "Item types", "Composite types", "Possible renames - review required", "Settings" }, blocks.Where(b => b.Kind == ReportBlockKind.Heading && b.Level == 2).Select(b => b.Text));
        Assert.Equal(new[] { "Added", "Removed", "Modified", "Added", "Removed", "Modified" }, blocks.Where(b => b.InContents && b.Level == 3).Select(b => b.Text));
        Assert.Equal(new[] { "Alpha", "Zebra", "RemovedItem", "Root", "AddedComposite", "RemovedComposite", "Cell" }, blocks.Where(b => b.Level == 4).Select(b => b.Text));
        foreach (var change in result.Changes)
        {
            if (change.Before != null) Assert.Contains(blocks, b => b.Text.StartsWith("Before: " + change.Before.Path));
            if (change.After != null) Assert.Contains(blocks, b => b.Text.StartsWith("After: " + change.After.Path));
            if (change.Before == null || change.After == null)
                foreach (var field in (change.Before ?? change.After).Fields.Where(f => f.Value.Length > 0))
                    Assert.Contains(blocks, b => b.Kind == ReportBlockKind.Value && b.Label == field.Key && b.Text == field.Value);
        }
    }

    [Fact]
    public void ReportMovesAffectBothOwnersWithoutDoubleCountingOrLosingDefinitions()
    {
        var before = Fixture(); var after = Fixture();
        var property = after.ItemTypes[0].Properties[0];
        after.ItemTypes[0].Properties.Remove(property);
        property.Name = "Caption"; property.Description = "Changed";
        after.ItemTypes[1].Properties.Add(property);
        var comparison = Compare(before, after, new RenameMapping("ItemTypes/Root/Label", "ItemTypes/Child/Caption"));
        var organization = new ComparisonReportOrganization(comparison);
        Assert.Equal(new ReportSummaryRow("ItemTypes", 0, 0, 2), organization.Summary[0]);
        Assert.Equal(new ReportSummaryRow("Properties", 0, 0, 1), organization.Summary[2]);
        var blocks = ComparisonReportDocument.Build(comparison);
        var link = Assert.Single(blocks, b => b.Kind == ReportBlockKind.Link);
        Assert.Contains(blocks, b => b.Kind == ReportBlockKind.Heading && b.Anchor == link.Anchor && b.Text == "Caption");
        Assert.Single(blocks, b => b.Text == "Before: ItemTypes/Root/Label");

        // A move between removed/added owners retains the complete definition on both sides.
        before = Fixture(); after = Fixture();
        before.ReusableDataTypes.Add(new DtoType { Name = "OldOwner", Properties = [Property("Moved")] });
        after.ReusableDataTypes.Add(new DtoType { Name = "NewOwner", Properties = [Property("Moved")] });
        comparison = Compare(before, after, new RenameMapping("CompositeTypes/OldOwner/Moved", "CompositeTypes/NewOwner/Moved"));
        organization = new(comparison);
        Assert.Equal(new ReportSummaryRow("CompositeTypes", 1, 1, 0), organization.Summary[1]);
        Assert.Equal(new ReportSummaryRow("Properties", 0, 0, 1), organization.Summary[2]);
        blocks = ComparisonReportDocument.Build(comparison);
        Assert.Equal(2, blocks.Count(b => b.Kind == ReportBlockKind.Value && b.Label == "Datatype" && b.Text == "string"));
    }

    [Fact]
    public void ReportCountsRenamedAndReorderedPropertiesWithoutInsertionOrInheritanceNoise()
    {
        var before = Fixture(); var after = Fixture();
        after.ItemTypes[0].Name = "Renamed"; after.ItemTypes[1].Extends = "Renamed";
        after.ItemTypes[0].Properties[0].Name = "Caption";
        after.ItemTypes[0].Properties.Reverse();
        after.ItemTypes[0].Properties.Insert(0, Property("Inserted"));
        var organization = new ComparisonReportOrganization(Compare(before, after,
            new("ItemTypes/Root", "ItemTypes/Renamed"), new("ItemTypes/Root/Label", "ItemTypes/Renamed/Caption")));
        Assert.Equal(new ReportSummaryRow("ItemTypes", 0, 0, 1), organization.Summary[0]);
        Assert.Equal(new ReportSummaryRow("Properties", 1, 0, 2), organization.Summary[2]);
        Assert.Equal(new[] { "ItemTypes/Renamed/Caption", "ItemTypes/Renamed/Parts" }, Assert.Single(organization.Types).ReorderedProperties);
        after = Fixture(); after.ItemTypes[0].Properties.Insert(0, Property("Inserted"));
        Assert.Equal(new ReportSummaryRow("Properties", 1, 0, 0), new ComparisonReportOrganization(Compare(before, after)).Summary[2]);
        after = Fixture(); after.Identification.Reverse();
        after.Identification.Insert(0, after.IdentificationMixin[0]); after.IdentificationMixin.Clear();
        organization = new(Compare(before, after));
        Assert.Equal(new ReportSummaryRow("ItemTypes", 0, 0, 0), organization.Summary[0]);
        Assert.Equal(new ReportSummaryRow("Properties", 0, 0, 2), organization.Summary[2]);
    }

    [Fact]
    public void ReportNavigationStopsAtTypesAndOmitsAllPreparationMetadata()
    {
        var before = Fixture(); var after = Fixture();
        foreach (string name in new[] { "Alpha.Beta", "Alpha_Beta", "Café" })
            after.ReusableDataTypes.Add(new DtoType { Name = name, Properties = [Property("AddedValue")] });
        after.Settings.Single(s => s.Key == "Title").Value = "Changed";
        var comparison = CogsModelComparer.Compare(new ComparisonInput(Build(before), "before", ["PREPARATION_SENTINEL"]),
            new ComparisonInput(Build(after), "after", ["AFTER_SETUP_SENTINEL"]));
        var blocks = ComparisonReportDocument.Build(comparison);
        var headings = blocks.Where(b => b.Kind == ReportBlockKind.Heading && b.Anchor != null).ToArray();
        Assert.Equal(headings.Length, headings.Select(b => b.Anchor).Distinct(StringComparer.Ordinal).Count());
        Assert.All(blocks.Where(b => b.InContents), b => Assert.InRange(b.Level, 2, 4));
        string md = ComparisonReportDocument.Markdown(blocks), typ = ComparisonReportDocument.Typst(blocks);
        foreach (var heading in headings.Where(b => b.InContents)) Assert.Contains("](#" + heading.Anchor + ")", md);
        foreach (var heading in headings.Where(b => !b.InContents)) Assert.DoesNotContain("](#" + heading.Anchor + ")", md);
        Assert.Contains("#outline(title: [Table of contents], depth: 4", typ);
        foreach (string text in new[] { md, typ })
        {
            Assert.DoesNotContain("PREPARATION_SENTINEL", text); Assert.DoesNotContain("AFTER_SETUP_SENTINEL", text);
            Assert.DoesNotContain("preparation:", text);
        }
        var unchanged = ComparisonReportDocument.Build(Compare(before, before));
        Assert.Contains(unchanged, b => b.Text == "No definition changes.");
        Assert.DoesNotContain(unchanged, b => b.Kind == ReportBlockKind.Contents);
        Assert.All(Assert.Single(unchanged, b => b.Kind == ReportBlockKind.Summary).Rows, r => Assert.Equal((0, 0, 0), (r.Added, r.Removed, r.Modified)));
    }

    [Fact]
    public void MarkdownPublicationIsTransactionalAndChecksBothSources()
    {
        using var temp = new Temporary();
        string sourceA = temp.Directory("before"), sourceB = temp.Directory("after");
        var result = CogsModelComparer.Compare(new ComparisonInput(Build(Fixture()), "before", originalDirectory: sourceA), new ComparisonInput(Build(Fixture()), "after", originalDirectory: sourceB));
        var publisher = new ComparisonReportPublisher { TargetDirectory = temp.PathFor("output"), Format = ComparisonReportFormat.Markdown, TypstExecutable = "does-not-exist" };
        var success = publisher.PublishResult(result);
        Assert.True(success.Success, string.Join("\n", success.Diagnostics));
        Assert.Equal(new[] { "comparison.changelog.md" }, success.Artifacts);
        string content = File.ReadAllText(temp.PathFor("output/comparison.changelog.md"));
        Assert.Contains("No definition changes", content);
        Assert.False(publisher.PublishResult(result).Success);
        publisher.Overwrite = true;
        publisher.Format = ComparisonReportFormat.Both;
        Assert.False(publisher.PublishResult(result).Success);
        Assert.Equal(content, File.ReadAllText(temp.PathFor("output/comparison.changelog.md")));
        publisher.Format = ComparisonReportFormat.Markdown;
        foreach (var target in new[] { sourceA, sourceB, temp.PathFor("after/nested"), temp.Root })
        {
            publisher.TargetDirectory = target;
            Assert.False(publisher.PublishResult(result).Success);
        }
    }

    [Fact]
    public void DirectorySnapshotsUpgradeLegacyWithoutChangingSourceBytes()
    {
        using var temp = new Temporary();
        string before = WriteFixture(temp.PathFor("legacy model"));
        string after = WriteFixture(temp.PathFor("modern model"));
        string settings = Path.Combine(before, "Settings/Settings.csv");
        File.WriteAllText(settings, File.ReadAllText(settings).Replace("CogsVersion,2.0\n", "").Replace("Version,0.1.0", "Version,0.1"));
        var hashes = HashTree(before);
        using var sources = new ComparisonSources(CheckDiagnostics);
        var inputs = sources.Load(before, after);
        Assert.Single(inputs.Before.Preparation);
        Assert.Empty(CogsModelComparer.Compare(inputs.Before, inputs.After).Changes);
        Assert.Equal(hashes, HashTree(before));
        Assert.StartsWith(before, inputs.Before.Source);
        Assert.DoesNotContain(inputs.Before.Locations.Values, p => p.Contains("cogs-compare-"));
    }

    [Fact]
    public void InvalidSnapshotsStopWithoutPublishingAndCliUsageFailsBeforeLoading()
    {
        using var temp = new Temporary();
        string model = WriteFixture(temp.PathFor("model"));
        File.WriteAllText(Path.Combine(model, "ItemTypes/Root/Root.csv"), "invalid\n");
        Assert.Equal(100, Cli("compare", model, model, temp.PathFor("output"), "--format", "markdown"));
        Assert.False(System.IO.Directory.Exists(temp.PathFor("output")));
        Assert.Equal(2, Cli("compare", "missing", "missing", "output", "--format", "invalid"));
        Assert.Equal(2, Cli("compare"));
    }

    [Fact]
    public void GitSnapshotsReadBlobsIgnoreDirtyWorktreeAndPreserveStatus()
    {
        using var temp = new Temporary();
        string model = WriteFixture(temp.PathFor("repository/model with spaces"));
        string repository = temp.PathFor("repository");
        Git(repository, "init", "-q"); Git(repository, "config", "core.autocrlf", "false"); Git(repository, "config", "commit.gpgsign", "false");
        File.WriteAllText(Path.Combine(repository, ".gitattributes"), "* export-subst\nmodel* export-ignore\n");
        Git(repository, "add", "."); Git(repository, "-c", "user.name=Test", "-c", "user.email=test@example.org", "commit", "-qm", "before");
        string a = Git(repository, "rev-parse", "HEAD").Trim();
        string csv = Path.Combine(model, "ItemTypes/Root/Root.csv");
        File.WriteAllText(csv, File.ReadAllText(csv).Replace("Original", "Changed"));
        Git(repository, "add", "."); Git(repository, "-c", "user.name=Test", "-c", "user.email=test@example.org", "commit", "-qm", "after");
        string b = Git(repository, "rev-parse", "HEAD").Trim();
        File.AppendAllText(csv, "dirty invalid data");
        string status = Git(repository, "status", "--porcelain=v1");
        var hashes = HashTree(model);
        using var sources = new ComparisonSources(CheckDiagnostics);
        var inputs = sources.Load(a, b, model);
        var change = Assert.Single(CogsModelComparer.Compare(inputs.Before, inputs.After).Changes);
        Assert.Contains(change.Fields, f => f.Before == "Original" && f.After == "Changed");
        Assert.Equal("model with spaces @ " + a, inputs.Before.Source);
        Assert.Equal("model with spaces @ " + b, inputs.After.Source);
        string report = ComparisonReportDocument.Markdown(ComparisonReportDocument.Build(CogsModelComparer.Compare(inputs.Before, inputs.After)));
        Assert.DoesNotContain(repository, report);
        Assert.Equal(model, inputs.Before.OriginalDirectory);
        Assert.Equal(status, Git(repository, "status", "--porcelain=v1"));
        Assert.Equal(hashes, HashTree(model));
        using var unavailable = new ComparisonSources(CheckDiagnostics);
        Assert.Throws<InvalidOperationException>(() => unavailable.Load("not-a-revision", b, model));
    }

    [Fact]
    public void RealTypstCompilesWhenExplicitlyProvisioned()
    {
        string typst = Environment.GetEnvironmentVariable("COGS_TYPST");
        if (string.IsNullOrWhiteSpace(typst)) return; // CI provisions the pinned compiler and additionally runs the PDF corpus.
        using var temp = new Temporary();
        var before = Fixture(); var after = Fixture();
        after.ItemTypes[0].Description = "New description with café and \"quotes\".\nSecond paragraph.";
        after.ItemTypes[0].Properties[0].Description = "A revised text with new words.";
        var result = new ComparisonReportPublisher { TargetDirectory = temp.PathFor("report"), TypstExecutable = typst }.PublishResult(Compare(before, after));
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        Assert.Equal(3, result.Artifacts.Count);
        Assert.True(File.ReadAllBytes(temp.PathFor("report/comparison.changelog.pdf")).AsSpan(0, 5).SequenceEqual("%PDF-"u8));
    }

    [Fact]
    public void CompilerFailureAfterStagingRollsBackAndPublisherReuseRechecksVersion()
    {
        using var temp = new Temporary();
        string target = temp.Directory("output");
        File.WriteAllText(Path.Combine(target, "existing"), "preserve");
        int probes = 0, compiles = 0;
        var publisher = new ComparisonReportPublisher { TargetDirectory = target, Overwrite = true,
            ExecuteTool = (exe, cwd, timeout, args) =>
            {
                if (args.SequenceEqual(new[] { "--version" })) { probes++; return "typst 0.15.1\n"; }
                compiles++;
                Assert.Equal(300_000, timeout);
                Assert.Contains("--ignore-system-fonts", args);
                Assert.True(File.Exists(Path.Combine(cwd, "comparison.changelog.md")));
                Assert.Equal("--root", args[1]); Assert.Equal(cwd, args[2]);
                throw new InvalidOperationException("injected compiler failure");
            } };
        var comparison = Compare(Fixture(), Fixture());
        Assert.False(publisher.PublishResult(comparison).Success);
        Assert.False(publisher.PublishResult(comparison).Success);
        Assert.Equal(2, probes); Assert.Equal(2, compiles);
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(target, "existing")));
        Assert.Single(System.IO.Directory.GetDirectories(temp.Root));
        publisher.ExecuteTool = (exe, cwd, timeout, args) => "typst 0.14.0";
        Assert.Contains(publisher.PublishResult(comparison).Diagnostics, d => d.Message.Contains("0.15.1"));
    }

    [Fact]
    public void SuccessfulCompilerWarningsAreRetainedInPublicationDiagnostics()
    {
        using var temp = new Temporary();
        var publisher = new ComparisonReportPublisher { TargetDirectory = temp.PathFor("report"),
            ExecuteTool = (exe, cwd, timeout, args) =>
            {
                if (args[0] == "--version") return "typst 0.16.0";
                File.WriteAllText(args[^1], "%PDF-fake-test-output");
                return "warning: font does not contain the requested character";
            } };
        var result = publisher.PublishResult(Compare(Fixture(), Fixture()));
        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "CMP2002" && d.Level == ErrorLevel.Warning);
    }

    [Fact]
    public void TypstProcessDeadlineKillsTheCompiler()
    {
        // PowerShell is provisioned on both workflow runners. No shell interpretation of arguments occurs in the publisher.
        var exception = Assert.Throws<InvalidOperationException>(() => ComparisonReportPublisher.Run(
            "pwsh", null, 500, "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30"));
        Assert.Contains("timed out", exception.Message);
    }

    [Theory]
    [InlineData("{\"renames\":[],\"renames\":[]}")]
    [InlineData("{\"renames\":[{\"from\":\"a\",\"from\":\"b\"}]}")]
    [InlineData("{\"renames\":[{\"from\":\"a\",\"to\":42}]}")]
    public void MappingFileRejectsDuplicateAndMalformedMembers(string text)
    {
        using var temp = new Temporary();
        temp.Directory("input"); string file = temp.PathFor("input/renames.json"); File.WriteAllText(file, text);
        Assert.Throws<InvalidOperationException>(() => ComparisonSources.ReadMappings(file));
    }

    [Fact]
    public void WorktreeAndSubmoduleCheckoutsResolveTheirOwnModelLocations()
    {
        using var temp = new Temporary();
        string repository = WriteFixture(temp.PathFor("repository"));
        Git(repository, "init", "-q"); Git(repository, "config", "commit.gpgsign", "false");
        Git(repository, "add", "."); Git(repository, "-c", "user.name=Test", "-c", "user.email=test@example.org", "commit", "-qm", "model");
        string worktree = temp.PathFor("separate worktree");
        Git(repository, "worktree", "add", "--detach", worktree, "HEAD");
        string parent = temp.Directory("parent repository");
        Git(parent, "init", "-q");
        Git(parent, "-c", "protocol.file.allow=always", "submodule", "add", repository, "nested model");
        foreach (string model in new[] { worktree, Path.Combine(parent, "nested model") })
        {
            using var sources = new ComparisonSources(CheckDiagnostics);
            var input = sources.Load("HEAD", "HEAD", model);
            Assert.False(CogsModelComparer.Compare(input.Before, input.After).HasChanges);
            Assert.Equal(new DirectoryInfo(model).Name + " @ " + Git(model, "rev-parse", "HEAD").Trim(), input.Before.Source);
            Assert.DoesNotContain(temp.Root, input.Before.Source);
        }
    }

    [Fact]
    public void IdentityRenamesAndMixinPlacementAreVisibleOnce()
    {
        var before = Fixture(); var after = Fixture();
        after.Identification[0].Name = "Identifier";
        after.Identification.Add(after.IdentificationMixin[0]); after.IdentificationMixin.Clear();
        var result = Compare(before, after, new RenameMapping("Identification/ID", "Identification/Identifier"));
        Assert.Equal(2, result.Changes.Count);
        Assert.Contains(result.Changes, c => c.Path == "Identification/Identifier" && c.Kind == ModelChangeKind.Renamed);
        Assert.Contains(result.Changes, c => c.Path == "Identification/Agency" && c.Fields.Any(f => f.Field == "Declaration"));
        Assert.DoesNotContain(result.Changes, c => c.Path.StartsWith("ItemTypes/"));
    }

    internal static int Cli(params string[] args) => (int)typeof(Cogs.Console.Program).GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, [args]);
    private static void CheckDiagnostics(IEnumerable<CogsError> errors) { Assert.DoesNotContain(errors, e => e.Level == ErrorLevel.Error); }
    private static ModelComparisonResult Compare(CogsDtoModel a, CogsDtoModel b, params RenameMapping[] mappings) =>
        CogsModelComparer.Compare(new ComparisonInput(Build(a), "before"), new ComparisonInput(Build(b), "after"), mappings);
    private static CogsModel Build(CogsDtoModel dto)
    {
        var result = new CogsModelBuilder().BuildResult(dto);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return result.Model;
    }
    private static DtoProperty Property(string name, string type = "string") => new() { Name = name, DataType = type, MinCardinality = "0", MaxCardinality = "1", Description = "Original" };
    private static CogsDtoModel Fixture()
    {
        var dto = new CogsDtoModel();
        foreach (string row in SettingsText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            string[] cells = row.Split(',', 2); dto.Settings.Add(new Setting { Key = cells[0], Value = cells[1] });
        }
        dto.Identification.Add(new DtoProperty { Name = "ID", DataType = "string", MinCardinality = "1", MaxCardinality = "1" });
        dto.IdentificationMixin.Add(new DtoProperty { Name = "Agency", DataType = "string", MinCardinality = "1", MaxCardinality = "1" });
        dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "Root", Properties = [Property("Label"), Property("Parts", "Cell")] });
        dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "Child", Extends = "Root" });
        dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "Leaf", Extends = "Child" });
        dto.ReusableDataTypes.Add(new DtoType { Name = "Cell", Properties = [Property("Value")] });
        return dto;
    }
    private const string SettingsText = "Key,Value\nCogsVersion,2.0\nTitle,Comparison model\nShortTitle,Comparison\nSlug,comparison\nDescription,\nVersion,0.1.0\nAuthor,\nCopyright,\nNamespaceUrl,https://example.org/comparison\nNamespacePrefix,comparison\n";
    internal static string WriteFixture(string path)
    {
        System.IO.Directory.CreateDirectory(Path.Combine(path, "Settings"));
        System.IO.Directory.CreateDirectory(Path.Combine(path, "ItemTypes/Root"));
        System.IO.Directory.CreateDirectory(Path.Combine(path, "CompositeTypes"));
        File.WriteAllText(Path.Combine(path, "CompositeTypes/.gitkeep"), "");
        File.WriteAllText(Path.Combine(path, "Settings/Settings.csv"), SettingsText);
        WriteCsv(Path.Combine(path, "Settings/Identification.csv"), [new DtoProperty { Name = "ID", DataType = "string", MinCardinality = "1", MaxCardinality = "1" }]);
        WriteCsv(Path.Combine(path, "ItemTypes/Root/Root.csv"), [Property("Label")]);
        return path;
    }
    private static void WriteCsv(string path, IEnumerable<DtoProperty> properties)
    {
        using var writer = new StreamWriter(path);
        using var csv = new CsvHelper.CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(properties);
    }
    private static string[] HashTree(string path) => System.IO.Directory.GetFiles(path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
        .Select(file => Path.GetRelativePath(path, file) + " " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))).ToArray();
    private static string Git(string cwd, params string[] args)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("COGS_GIT") ?? "git") { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start);
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(30_000)); Assert.True(process.ExitCode == 0, error.GetAwaiter().GetResult());
        Assert.True(error.IsCompleted || error.Wait(1000)); return output.GetAwaiter().GetResult();
    }
    private sealed class Temporary : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "cogs-comparison-tests-" + Guid.NewGuid().ToString("N"));
        internal string PathFor(string path) => Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));
        internal string Directory(string path) { string value = PathFor(path); System.IO.Directory.CreateDirectory(value); return value; }
        public void Dispose()
        {
            if (!System.IO.Directory.Exists(Root)) return;
            foreach (string file in System.IO.Directory.GetFiles(Root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            System.IO.Directory.Delete(Root, true);
        }
    }
}
