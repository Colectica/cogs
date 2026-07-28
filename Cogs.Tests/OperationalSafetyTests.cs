using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using Cogs.Validation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Cogs.Tests;

public sealed class OperationalSafetyTests
{
    [Fact]
    public void ModelInitializerCreatesCanonicalCogs2Model()
    {
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "model");

        new ModelInitializer { Dir = target }.Create();

        string settings = File.ReadAllText(Path.Combine(target, "Settings", "Settings.csv"));
        Assert.Contains("CogsVersion", settings);
        Assert.Contains("2.0", settings);
        Assert.Contains("0.1.0", settings);
        Assert.True(File.Exists(Path.Combine(target, "Settings", "Identification.csv")));
        Assert.True(File.Exists(Path.Combine(target, "ItemTypes", "ItemOne", "ItemOne.csv")));
        Assert.True(File.Exists(Path.Combine(target, "ItemTypes", "ItemOne", "readme.markdown")));
        Assert.True(File.Exists(Path.Combine(target, "ItemTypes", "ItemOne", "Abstract")));
        Assert.True(File.Exists(Path.Combine(target, "ItemTypes", "ItemTwo", "Extends.ItemOne")));
        Assert.True(File.Exists(Path.Combine(target, "CompositeTypes", "CompositeOne", "CompositeOne.csv")));
        Assert.True(File.Exists(Path.Combine(target, "CompositeTypes", "CompositeOne", "Primitive")));
        Assert.Equal(
            new[] { "ItemOne", "ItemTwo" },
            File.ReadAllLines(Path.Combine(target, "Topics", "All", "items.txt")));

        CogsLoadResult result = new CogsDirectoryReader().LoadResult(target);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.DoesNotContain(result.Diagnostics, error => error.Code is "COGS-READ-040" or "COGS-READ-041");
        Assert.Single(result.Model.Identification);
        Assert.DoesNotContain(DtoValidation.Validate(result.Model), error => error.Level == Cogs.Common.ErrorLevel.Error);

        Cogs.Dto.ItemType itemOneDto = result.Model.ItemTypes.Single(type => type.Name == "ItemOne");
        Cogs.Dto.ItemType itemTwoDto = result.Model.ItemTypes.Single(type => type.Name == "ItemTwo");
        Assert.True(itemOneDto.IsAbstract);
        Assert.Equal("ItemOne", itemTwoDto.Extends);
        Assert.Equal("Details", Assert.Single(itemTwoDto.Properties).Name);
        Assert.True(result.Model.ReusableDataTypes.Single(type => type.Name == "CompositeOne").IsPrimitive);

        CogsBuildResult built = new CogsModelBuilder().BuildResult(result.Model);
        Assert.True(built.Success, string.Join(Environment.NewLine, built.Diagnostics));
        Cogs.Model.ItemType itemTwo = built.Model.ItemTypes.Single(type => type.Name == "ItemTwo");
        Assert.Equal("ItemOne", Assert.Single(itemTwo.ParentTypes).Name);
        string[] effectiveNames = CogsTypeSystem.EffectiveProperties(itemTwo).Select(property => property.Name).ToArray();
        Assert.Contains("Name", effectiveNames);
        Assert.Contains("Details", effectiveNames);
    }

    [Fact]
    public void ModelInitializerPreservesExistingTargetWhenCreationFails()
    {
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "model");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "owned.txt"), "keep");

        Assert.Throws<InvalidOperationException>(() =>
            new ModelInitializer { Dir = target, Overwrite = false }.Create());

        Assert.Equal("keep", File.ReadAllText(Path.Combine(target, "owned.txt")));
    }

    [Fact]
    public void RewriteLeavesEverySourceFileUnchangedWhenAnyCsvCannotBeRead()
    {
        using var temporary = new TemporaryDirectory();
        string model = Path.Combine(temporary.Path, "model");
        CreateRewriteFixture(model);
        string identification = Path.Combine(model, "Settings", "Identification.csv");
        File.WriteAllText(
            identification,
            "Name,DataType,MinCardinality,MaxCardinality,MinLength\r\nID,string,1,1,not-an-integer\r\n");
        Dictionary<string, byte[]> before = Snapshot(model);

        var rewrite = new RewriteCsvFormat();
        rewrite.Rewrite(model);

        Assert.Contains(rewrite.Errors, error => error.Level == Cogs.Common.ErrorLevel.Error);
        AssertSnapshotsEqual(before, Snapshot(model));
        Assert.Empty(Directory.GetDirectories(temporary.Path, ".model.cogs-rewrite-*"));
    }

    [Fact]
    public void RewriteCommitsAllCsvFilesAndPreservesOtherFiles()
    {
        using var temporary = new TemporaryDirectory();
        string model = Path.Combine(temporary.Path, "model");
        CreateRewriteFixture(model);
        string note = Path.Combine(model, "note.txt");
        File.WriteAllText(note, "preserve me");

        var rewrite = new RewriteCsvFormat();
        rewrite.Rewrite(model);

        Assert.DoesNotContain(rewrite.Errors, error => error.Level == Cogs.Common.ErrorLevel.Error);
        Assert.Equal("preserve me", File.ReadAllText(note));
        Assert.Contains("Name,DataType", File.ReadAllText(Path.Combine(model, "Settings", "Identification.csv")));
        Assert.Empty(Directory.GetDirectories(temporary.Path, ".model.cogs-rewrite-*"));
    }

    [Fact]
    public void RewriteDoesNotTouchLockedRepositoryMetadata()
    {
        using var temporary = new TemporaryDirectory();
        string model = Path.Combine(temporary.Path, "model");
        CreateRewriteFixture(model);
        string objectDirectory = Path.Combine(model, ".git", "objects");
        Directory.CreateDirectory(objectDirectory);
        string objectPath = Path.Combine(objectDirectory, "42287ddf83be7971ae0059982d6e1144554e62");
        File.WriteAllText(objectPath, "repository metadata");

        using (var locked = new FileStream(objectPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var rewrite = new RewriteCsvFormat();
            rewrite.Rewrite(model);

            Assert.DoesNotContain(rewrite.Errors, error => error.Level == Cogs.Common.ErrorLevel.Error);
            Assert.Equal("repository metadata", File.ReadAllText(objectPath));
            Assert.Equal('r', (char)locked.ReadByte());
            Assert.Empty(Directory.GetDirectories(temporary.Path, ".model.cogs-rewrite-*"));
        }
    }

    [Fact]
    public void RewriteRestoresEarlierCsvReplacementsWhenALaterCsvCannotBeReplaced()
    {
        using var temporary = new TemporaryDirectory();
        string model = Path.Combine(temporary.Path, "model");
        CreateRewriteFixture(model);
        Dictionary<string, byte[]> before = Snapshot(model);
        string laterProperty = Path.Combine(model, "Settings", "Identification.Mixin.csv");

        var rewrite = new RewriteCsvFormat
        {
            BeforeReplace = (path, _) =>
            {
                if (string.Equals(path, laterProperty, StringComparison.Ordinal))
                {
                    throw new IOException("Injected replacement failure.");
                }
            }
        };
        rewrite.Rewrite(model);

        Assert.Contains(rewrite.Errors, error => error.Code == "COGS-RW-003");
        AssertSnapshotsEqual(before, Snapshot(model));
        Assert.Empty(Directory.GetDirectories(temporary.Path, ".model.cogs-rewrite-*"));
    }

    private static void CreateRewriteFixture(string model)
    {
        Directory.CreateDirectory(Path.Combine(model, "Settings"));
        Directory.CreateDirectory(Path.Combine(model, "ItemTypes", "Thing"));
        Directory.CreateDirectory(Path.Combine(model, "CompositeTypes"));
        File.WriteAllText(
            Path.Combine(model, "Settings", "Identification.csv"),
            "Name,DataType,MinCardinality,MaxCardinality\r\nID,string,1,1\r\n");
        File.WriteAllText(
            Path.Combine(model, "Settings", "Identification.Mixin.csv"),
            "Name,DataType,MinCardinality,MaxCardinality\r\nAgency,string,1,1\r\n");
        File.WriteAllText(
            Path.Combine(model, "Settings", "Settings.csv"),
            "Key,Value\r\nCogsVersion,2.0\r\nTitle,Test\r\n");
        File.WriteAllText(
            Path.Combine(model, "ItemTypes", "Thing", "Thing.csv"),
            "Name,DataType,MinCardinality,MaxCardinality\r\nName,string,0,1\r\n");
    }

    private static Dictionary<string, byte[]> Snapshot(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                file => Path.GetRelativePath(root, file),
                File.ReadAllBytes,
                StringComparer.Ordinal);

    private static void AssertSnapshotsEqual(
        IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(x => x), actual.Keys.OrderBy(x => x));
        foreach ((string path, byte[] contents) in expected)
        {
            Assert.True(contents.SequenceEqual(actual[path]), $"'{path}' changed during a failed rewrite.");
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cogs-operational-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
