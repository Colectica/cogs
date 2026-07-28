using Cogs.Dto;
using Cogs.Publishers;
using Cogs.Validation;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace Cogs.Tests;

public sealed class RewriteUpgradeTests
{
    [Fact]
    public void UpgradeCogs2MigratesOnlyMechanicalLegacySyntax()
    {
        using var fixture = new TemporaryModel();
        var settingsPath = Path.Combine(fixture.Path, "Settings", "Settings.csv");
        var settings = File.ReadAllLines(settingsPath)
            .Where(line => !line.Contains("CogsVersion", StringComparison.Ordinal) &&
                           !line.Contains("Author", StringComparison.Ordinal) &&
                           !line.Contains("Copyright", StringComparison.Ordinal))
            .Select(line => line.Replace("\"Version\",\"0.1.0\"", "\"Version\",\"0.1\"", StringComparison.Ordinal));
        File.WriteAllLines(settingsPath, settings);

        var propertyPath = Path.Combine(fixture.Path, "CompositeTypes", "CompositeOne", "CompositeOne.csv");
        var properties = ReadProperties(propertyPath);
        var property = Assert.Single(properties);
        property.DataType = "string";
        property.MinCardinality = "";
        property.MaxCardinality = "N";
        property.Ordered = "TRUE";
        property.AllowSubtypes = "FALSE";
        property.Enumeration = "red blue";
        property.DeprecatedNamespace = "urn:historical:changed";
        property.DeprecatedElementOrAttribute = "legacy-element";
        property.DeprecatedChoiceGroup = "legacy-choice";
        WriteProperties(propertyPath, properties);

        var unaffectedPath = Path.Combine(fixture.Path, "CompositeTypes", "CompositeTwo", "CompositeTwo.csv");
        var unaffectedProperties = ReadProperties(unaffectedPath);
        var unaffectedProperty = Assert.Single(unaffectedProperties);
        unaffectedProperty.DeprecatedNamespace = "urn:historical:unchanged";
        unaffectedProperty.DeprecatedElementOrAttribute = "legacy-attribute";
        unaffectedProperty.DeprecatedChoiceGroup = "unchanged-choice";
        WriteProperties(unaffectedPath, unaffectedProperties);
        File.WriteAllText(unaffectedPath, File.ReadAllText(unaffectedPath).Replace("\r\n", "\n", StringComparison.Ordinal));
        var unaffectedBefore = File.ReadAllBytes(unaffectedPath);
        var changedBefore = File.ReadAllBytes(propertyPath);

        var rewrite = new RewriteCsvFormat();
        rewrite.Rewrite(fixture.Path, upgradeCogs2: true);

        Assert.DoesNotContain(rewrite.Errors, error => error.Level == Cogs.Common.ErrorLevel.Error);

        var migratedSettings = File.ReadAllText(settingsPath);
        Assert.Contains("CogsVersion", migratedSettings);
        Assert.Contains("0.1.0", migratedSettings);
        Assert.Contains("Author", migratedSettings);
        Assert.Contains("Copyright", migratedSettings);

        property = Assert.Single(ReadProperties(propertyPath));
        Assert.Equal("0", property.MinCardinality);
        Assert.Equal("n", property.MaxCardinality);
        Assert.Equal("true", property.Ordered);
        Assert.Equal("false", property.AllowSubtypes);
        Assert.Equal("red blue", property.Enumeration);
        Assert.Equal("urn:historical:changed", property.DeprecatedNamespace);
        Assert.Equal("legacy-element", property.DeprecatedElementOrAttribute);
        Assert.Equal("legacy-choice", property.DeprecatedChoiceGroup);
        unaffectedProperty = Assert.Single(ReadProperties(unaffectedPath));
        Assert.Equal("urn:historical:unchanged", unaffectedProperty.DeprecatedNamespace);
        Assert.Equal("legacy-attribute", unaffectedProperty.DeprecatedElementOrAttribute);
        Assert.Equal("unchanged-choice", unaffectedProperty.DeprecatedChoiceGroup);
        Assert.False(changedBefore.SequenceEqual(File.ReadAllBytes(propertyPath)));
        Assert.True(unaffectedBefore.SequenceEqual(File.ReadAllBytes(unaffectedPath)),
            "A property file with no cardinality or flag normalization should remain byte-for-byte unchanged.");

        var load = new CogsDirectoryReader().LoadResult(fixture.Path);
        Assert.True(load.Success, string.Join(Environment.NewLine, load.Diagnostics));
        Assert.DoesNotContain(DtoValidation.Validate(load.Model), error => error.Level == Cogs.Common.ErrorLevel.Error);
    }

    [Fact]
    public void RewritePreservesHistoricalPropertyColumns()
    {
        using var fixture = new TemporaryModel();
        string propertyPath = Path.Combine(fixture.Path, "CompositeTypes", "CompositeOne", "CompositeOne.csv");
        List<Property> properties = ReadProperties(propertyPath);
        Property property = Assert.Single(properties);
        property.DeprecatedNamespace = "not a normalized URI";
        property.DeprecatedElementOrAttribute = "opaque e-or-a";
        property.DeprecatedChoiceGroup = "opaque choice group";
        WriteProperties(propertyPath, properties);

        var rewrite = new RewriteCsvFormat();
        rewrite.Rewrite(fixture.Path, upgradeCogs2: false);

        Assert.DoesNotContain(rewrite.Errors, error => error.Level == Cogs.Common.ErrorLevel.Error);
        property = Assert.Single(ReadProperties(propertyPath));
        Assert.Equal("not a normalized URI", property.DeprecatedNamespace);
        Assert.Equal("opaque e-or-a", property.DeprecatedElementOrAttribute);
        Assert.Equal("opaque choice group", property.DeprecatedChoiceGroup);
    }

    [Fact]
    public void UpgradeCogs2AbortsWithoutWritesWhenRequiredMeaningIsMissing()
    {
        using var fixture = new TemporaryModel();
        var settingsPath = Path.Combine(fixture.Path, "Settings", "Settings.csv");
        File.WriteAllLines(settingsPath,
            File.ReadAllLines(settingsPath).Where(line => !line.Contains("Slug", StringComparison.Ordinal)));
        var before = Snapshot(fixture.Path);

        var rewrite = new RewriteCsvFormat();
        rewrite.Rewrite(fixture.Path, upgradeCogs2: true);

        Assert.Contains(rewrite.Errors, error => error.Code == "MIG2002");
        Assert.Equal(before, Snapshot(fixture.Path));
    }

    [Fact]
    public void UpgradeCogs2RollsBackAllFilesWhenAPropertyCannotBeNormalized()
    {
        using var fixture = new TemporaryModel();
        var settingsPath = Path.Combine(fixture.Path, "Settings", "Settings.csv");
        var settings = File.ReadAllLines(settingsPath)
            .Where(line => !line.Contains("CogsVersion", StringComparison.Ordinal))
            .Select(line => line.Replace("\"Version\",\"0.1.0\"", "\"Version\",\"0.1\"", StringComparison.Ordinal));
        File.WriteAllLines(settingsPath, settings);

        var propertyPath = Path.Combine(fixture.Path, "CompositeTypes", "CompositeOne", "CompositeOne.csv");
        var properties = ReadProperties(propertyPath);
        var property = Assert.Single(properties);
        property.Ordered = "sometimes";
        property.Enumeration = "red blue";
        WriteProperties(propertyPath, properties);
        var before = Snapshot(fixture.Path);

        var rewrite = new RewriteCsvFormat();
        rewrite.Rewrite(fixture.Path, upgradeCogs2: true);

        Assert.Contains(rewrite.Errors, error => error.Code == "MIG2006");
        Assert.Equal(before, Snapshot(fixture.Path));
    }

    [Fact]
    public void UpgradeCogs2CanonicalizesLegacyMarkerCasing()
    {
        using var fixture = new TemporaryModel();
        string child = CreateLegacyMarkerFixture(fixture.Path);

        var rewrite = new RewriteCsvFormat();
        rewrite.Rewrite(fixture.Path, upgradeCogs2: true);

        Assert.DoesNotContain(rewrite.Errors, error => error.Level == Cogs.Common.ErrorLevel.Error);
        AssertExactFileNames(child, "Child.csv", "Extends.ItemOne");
        AssertExactFileNames(
            Path.Combine(fixture.Path, "ItemTypes", "ItemOne"),
            "Abstract", "ItemOne.csv", "readme.markdown");
        AssertExactFileNames(
            Path.Combine(fixture.Path, "CompositeTypes", "CompositeOne"),
            "CompositeOne.csv", "Primitive", "readme.markdown");
        Assert.Equal("legacy inheritance marker", File.ReadAllText(Path.Combine(child, "Extends.ItemOne")));

        CogsLoadResult loaded = new CogsDirectoryReader().LoadResult(fixture.Path);
        Assert.True(loaded.Success, string.Join(Environment.NewLine, loaded.Diagnostics));
        Assert.DoesNotContain(loaded.Diagnostics, error => error.Code is "COGS-READ-040" or "COGS-READ-041");
        Assert.DoesNotContain(DtoValidation.Validate(loaded.Model), error => error.Level == Cogs.Common.ErrorLevel.Error);
    }

    [Fact]
    public void UpgradeCogs2RollsBackEarlierMarkerRenamesWhenALaterRenameFails()
    {
        using var fixture = new TemporaryModel();
        CreateLegacyMarkerFixture(fixture.Path);
        string before = Snapshot(fixture.Path);
        string failingMarker = Path.Combine(fixture.Path, "ItemTypes", "ItemOne", "abstract");
        var rewrite = new RewriteCsvFormat
        {
            BeforeReplace = (path, _) =>
            {
                if (string.Equals(path, failingMarker, StringComparison.Ordinal))
                {
                    throw new IOException("Injected marker rename failure.");
                }
            }
        };

        rewrite.Rewrite(fixture.Path, upgradeCogs2: true);

        Assert.Contains(rewrite.Errors, error => error.Code == "COGS-RW-003");
        Assert.Equal(before, Snapshot(fixture.Path));
    }

    [Fact]
    public void UpgradeCogs2UsesGitMvForTrackedMarkersInNestedCheckout()
    {
        using var fixture = new TemporaryGitModel();
        string child = CreateLegacyMarkerFixture(fixture.ModelPath);
        fixture.CommitAll("legacy markers");

        RewriteCsvFormat rewrite = fixture.CreateRewrite();
        rewrite.Rewrite(fixture.ModelPath, upgradeCogs2: true);

        Assert.DoesNotContain(rewrite.Errors, error => error.Level == Cogs.Common.ErrorLevel.Error);
        AssertExactFileNames(child, "Child.csv", "Extends.ItemOne");
        Assert.Contains("legacy inheritance marker", File.ReadAllText(Path.Combine(child, "Extends.ItemOne")));

        string index = fixture.Git("ls-files");
        Assert.Contains(fixture.RepositoryPath("ItemTypes", "Child", "Extends.ItemOne"), index);
        Assert.Contains(fixture.RepositoryPath("ItemTypes", "ItemOne", "Abstract"), index);
        Assert.Contains(fixture.RepositoryPath("CompositeTypes", "CompositeOne", "Primitive"), index);
        Assert.DoesNotContain(fixture.RepositoryPath("ItemTypes", "Child", "extends.ItemOne"), index);

        string status = fixture.Status();
        Assert.Contains("Extends.ItemOne", status);
        Assert.Contains("Abstract", status);
        Assert.Contains("Primitive", status);
        string stagedPaths = fixture.Git("diff", "--cached", "--name-only");
        Assert.Contains(fixture.RepositoryPath("ItemTypes", "Child", "Extends.ItemOne"), stagedPaths);
        Assert.DoesNotContain(".csv", stagedPaths, StringComparison.OrdinalIgnoreCase);

        CogsLoadResult loaded = new CogsDirectoryReader().LoadResult(fixture.ModelPath);
        Assert.True(loaded.Success, string.Join(Environment.NewLine, loaded.Diagnostics));
        Assert.DoesNotContain(loaded.Diagnostics, error => error.Code == "COGS-READ-041");

        string statusBeforeSecondUpgrade = fixture.Status();
        rewrite.Rewrite(fixture.ModelPath, upgradeCogs2: true);
        Assert.DoesNotContain(rewrite.Errors, error => error.Level == Cogs.Common.ErrorLevel.Error);
        Assert.Equal(statusBeforeSecondUpgrade, fixture.Status());
    }

    [Fact]
    public void UpgradeCogs2UsesFilesystemRenameForUntrackedMarkersInsideGitCheckout()
    {
        using var fixture = new TemporaryGitModel();
        fixture.CommitAll("canonical model");
        string child = CreateLegacyMarkerFixture(fixture.ModelPath);
        fixture.Git("add", "--", fixture.RepositoryPath("ItemTypes", "Child", "Child.csv"));
        fixture.Git("commit", "--quiet", "-m", "child type");

        RewriteCsvFormat rewrite = fixture.CreateRewrite();
        rewrite.Rewrite(fixture.ModelPath, upgradeCogs2: true);

        Assert.DoesNotContain(rewrite.Errors, error => error.Level == Cogs.Common.ErrorLevel.Error);
        AssertExactFileNames(child, "Child.csv", "Extends.ItemOne");
        Assert.DoesNotContain(fixture.RepositoryPath("ItemTypes", "Child", "Extends.ItemOne"), fixture.Git("ls-files"));
        string untracked = fixture.Git("ls-files", "--others", "--exclude-standard");
        Assert.Contains(fixture.RepositoryPath("ItemTypes", "Child", "Extends.ItemOne"), untracked);
        string tracked = fixture.Git("ls-files");
        Assert.Contains(fixture.RepositoryPath("ItemTypes", "ItemOne", "Abstract"), tracked);
        Assert.Contains(fixture.RepositoryPath("CompositeTypes", "CompositeOne", "Primitive"), tracked);
    }

    [Fact]
    public void UpgradeCogs2UsesGitMvInsideLinkedWorktree()
    {
        using var fixture = new TemporaryGitModel(linkedWorktree: true);
        CreateLegacyMarkerFixture(fixture.ModelPath);
        fixture.CommitAll("legacy worktree markers");
        Assert.True(File.Exists(Path.Combine(fixture.RepositoryRoot, ".git")),
            "A linked worktree should expose Git metadata through a .git file.");

        RewriteCsvFormat rewrite = fixture.CreateRewrite();
        rewrite.Rewrite(fixture.ModelPath, upgradeCogs2: true);

        Assert.DoesNotContain(rewrite.Errors, error => error.Level == Cogs.Common.ErrorLevel.Error);
        Assert.Contains(fixture.RepositoryPath("ItemTypes", "Child", "Extends.ItemOne"), fixture.Git("ls-files"));
        Assert.Contains("Extends.ItemOne", fixture.Git("diff", "--cached", "--name-only"));
    }

    [Fact]
    public void UpgradeCogs2RestoresGitIndexAndWorkingTreeWhenGitMoveFails()
    {
        using var fixture = new TemporaryGitModel();
        CreateLegacyMarkerFixture(fixture.ModelPath);
        CreateAdditionalLegacyItemMarker(fixture.ModelPath, "ChildTwo");
        fixture.CommitAll("legacy markers");

        string firstMarker = Path.Combine(fixture.ModelPath, "ItemTypes", "Child", "extends.ItemOne");
        string secondMarker = Path.Combine(fixture.ModelPath, "ItemTypes", "ChildTwo", "extends.ItemOne");
        File.WriteAllText(firstMarker, "staged marker change");
        fixture.Git("add", "--", fixture.RepositoryPath("ItemTypes", "Child", "extends.ItemOne"));
        File.WriteAllText(secondMarker, "unstaged marker change");

        string modelBefore = Snapshot(fixture.ModelPath);
        string statusBefore = fixture.Status();
        string indexBefore = fixture.Git("ls-files", "--stage");
        RewriteCsvFormat rewrite = fixture.CreateRewrite();
        var actualRunner = rewrite.GitCommandRunner;
        rewrite.GitCommandRunner = (executable, workingDirectory, arguments) =>
        {
            if (arguments.Count == 5 && arguments[0] == "mv" &&
                arguments[3].EndsWith("/abstract", StringComparison.Ordinal))
            {
                RewriteCsvFormat.GitCommandResult applied = actualRunner(executable, workingDirectory, arguments);
                Assert.Equal(0, applied.ExitCode);
                return new RewriteCsvFormat.GitCommandResult(128, string.Empty, "injected git mv failure");
            }
            return actualRunner(executable, workingDirectory, arguments);
        };

        rewrite.Rewrite(fixture.ModelPath, upgradeCogs2: true);

        Cogs.Common.CogsError gitError = Assert.Single(rewrite.Errors, error => error.Code == "MIG2011");
        Assert.Contains("injected git mv failure", gitError.Message);
        Assert.Contains(rewrite.Errors, error => error.Code == "COGS-RW-003");
        Assert.Equal(modelBefore, Snapshot(fixture.ModelPath));
        Assert.Equal(statusBefore, fixture.Status());
        Assert.Equal(indexBefore, fixture.Git("ls-files", "--stage"));
    }

    [Fact]
    public void UpgradeCogs2AbortsBeforeWritesWhenCheckoutGitIsUnavailable()
    {
        using var fixture = new TemporaryGitModel();
        CreateLegacyMarkerFixture(fixture.ModelPath);
        fixture.CommitAll("legacy markers");
        string modelBefore = Snapshot(fixture.ModelPath);
        string statusBefore = fixture.Status();
        string indexBefore = fixture.Git("ls-files", "--stage");
        var rewrite = new RewriteCsvFormat
        {
            GitExecutableEnvironmentReader = () => "missing-cogs-git",
            GitCommandRunner = (_, _, _) => throw new FileNotFoundException("injected missing Git")
        };

        rewrite.Rewrite(fixture.ModelPath, upgradeCogs2: true);

        Cogs.Common.CogsError gitError = Assert.Single(rewrite.Errors, error => error.Code == "MIG2011");
        Assert.Contains("COGS_GIT", gitError.Message);
        Assert.Equal(modelBefore, Snapshot(fixture.ModelPath));
        Assert.Equal(statusBefore, fixture.Status());
        Assert.Equal(indexBefore, fixture.Git("ls-files", "--stage"));
    }

    [Fact]
    public void UpgradeCogs2PrefersCogsGitBeforePathDiscovery()
    {
        using var fixture = new TemporaryModel();
        CreateLegacyMarkerFixture(fixture.Path);
        var executables = new List<string>();
        var rewrite = new RewriteCsvFormat
        {
            GitExecutableEnvironmentReader = () => "configured git with spaces",
            GitCommandRunner = (executable, _, arguments) =>
            {
                executables.Add(executable);
                return arguments[0] == "rev-parse"
                    ? new RewriteCsvFormat.GitCommandResult(0, fixture.Path + Environment.NewLine, string.Empty)
                    : new RewriteCsvFormat.GitCommandResult(1, string.Empty, string.Empty);
            }
        };

        rewrite.Rewrite(fixture.Path, upgradeCogs2: true);

        Assert.DoesNotContain(rewrite.Errors, error => error.Level == Cogs.Common.ErrorLevel.Error);
        Assert.NotEmpty(executables);
        Assert.All(executables, executable => Assert.Equal("configured git with spaces", executable));
    }

    private static string CreateLegacyMarkerFixture(string model)
    {
        string itemOne = Path.Combine(model, "ItemTypes", "ItemOne", "ItemOne.csv");
        string child = Path.Combine(model, "ItemTypes", "Child");
        Directory.CreateDirectory(child);
        File.WriteAllText(Path.Combine(child, "Child.csv"), File.ReadLines(itemOne).First() + Environment.NewLine);
        File.WriteAllText(Path.Combine(child, "extends.ItemOne"), "legacy inheritance marker");
        string abstractDirectory = Path.Combine(model, "ItemTypes", "ItemOne");
        File.Delete(Path.Combine(abstractDirectory, "Abstract"));
        File.WriteAllText(Path.Combine(abstractDirectory, "abstract"), string.Empty);
        string primitiveDirectory = Path.Combine(model, "CompositeTypes", "CompositeOne");
        File.Delete(Path.Combine(primitiveDirectory, "Primitive"));
        File.WriteAllText(Path.Combine(primitiveDirectory, "PRIMITIVE"), string.Empty);
        return child;
    }

    private static void CreateAdditionalLegacyItemMarker(string model, string typeName)
    {
        string sourceCsv = Path.Combine(model, "ItemTypes", "ItemOne", "ItemOne.csv");
        string directory = Path.Combine(model, "ItemTypes", typeName);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, typeName + ".csv"), File.ReadLines(sourceCsv).First() + Environment.NewLine);
        File.WriteAllText(Path.Combine(directory, "extends.ItemOne"), "second legacy inheritance marker");
    }

    private static void AssertExactFileNames(string directory, params string[] expected)
    {
        Assert.Equal(
            expected.OrderBy(name => name, StringComparer.Ordinal),
            Directory.EnumerateFiles(directory).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
    }

    private static List<Property> ReadProperties(string path)
    {
        using var reader = File.OpenText(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return csv.GetRecords<Property>().ToList();
    }

    private static void WriteProperties(string path, IEnumerable<Property> properties)
    {
        using var writer = File.CreateText(path);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(properties);
    }

    private static string Snapshot(string root) => string.Join("\n",
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => $"{Path.GetRelativePath(root, path)}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))}"));

    private sealed class TemporaryModel : IDisposable
    {
        public TemporaryModel()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cogs-upgrade-" + Guid.NewGuid().ToString("N"));
            new ModelInitializer { Dir = Path }.Create();
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class TemporaryGitModel : IDisposable
    {
        public TemporaryGitModel(bool linkedWorktree = false)
        {
            GitExecutable = FindGitExecutable();
            Root = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "cogs git upgrade " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);

            if (linkedWorktree)
            {
                PrimaryRepository = System.IO.Path.Combine(Root, "primary repository");
                RepositoryRoot = System.IO.Path.Combine(Root, "linked worktree");
                Directory.CreateDirectory(PrimaryRepository);
                RunProcess(GitExecutable, PrimaryRepository, "init", "--quiet");
                ConfigureRepository(PrimaryRepository);
                File.WriteAllText(System.IO.Path.Combine(PrimaryRepository, "seed.txt"), "seed");
                RunProcess(GitExecutable, PrimaryRepository, "add", "--", "seed.txt");
                RunProcess(GitExecutable, PrimaryRepository, "commit", "--quiet", "-m", "initial");
                RunProcess(GitExecutable, PrimaryRepository, "worktree", "add", "--quiet", "-b", "cogs-rewrite-probe", RepositoryRoot);
            }
            else
            {
                RepositoryRoot = Root;
                RunProcess(GitExecutable, RepositoryRoot, "init", "--quiet");
                ConfigureRepository(RepositoryRoot);
            }

            ModelPath = System.IO.Path.Combine(RepositoryRoot, "models", "model with spaces");
            new ModelInitializer { Dir = ModelPath }.Create();
        }

        public string GitExecutable { get; }

        public string Root { get; }

        public string RepositoryRoot { get; }

        private string PrimaryRepository { get; }

        public string ModelPath { get; }

        public RewriteCsvFormat CreateRewrite() => new RewriteCsvFormat
        {
            GitExecutableEnvironmentReader = () => GitExecutable
        };

        public void CommitAll(string message)
        {
            Git("add", "--", ".");
            Git("commit", "--quiet", "-m", message);
        }

        public string RepositoryPath(params string[] modelParts)
        {
            string path = modelParts.Aggregate(ModelPath, System.IO.Path.Combine);
            return System.IO.Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');
        }

        public string Status() => Git("status", "--short", "--untracked-files=all");

        public string Git(params string[] arguments) => RunProcess(GitExecutable, RepositoryRoot, arguments);

        public void Dispose()
        {
            if (!string.IsNullOrWhiteSpace(PrimaryRepository) && Directory.Exists(PrimaryRepository))
            {
                try
                {
                    RunProcess(GitExecutable, PrimaryRepository, "worktree", "remove", "--force", RepositoryRoot);
                }
                catch
                {
                    // The verified TEMP-bound cleanup below remains authoritative.
                }
            }
            for (int attempt = 0; attempt < 3 && Directory.Exists(Root); attempt++)
            {
                foreach (string file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                }
                try
                {
                    Directory.Delete(Root, recursive: true);
                }
                catch when (attempt < 2)
                {
                    Thread.Sleep(50 * (attempt + 1));
                }
            }
        }

        private void ConfigureRepository(string repository)
        {
            RunProcess(GitExecutable, repository, "config", "user.email", "cogs-tests@example.invalid");
            RunProcess(GitExecutable, repository, "config", "user.name", "COGS Tests");
            RunProcess(GitExecutable, repository, "config", "commit.gpgsign", "false");
        }

        private static string FindGitExecutable()
        {
            string configured = Environment.GetEnvironmentVariable("COGS_GIT");
            foreach (string candidate in new[] { configured, "git" }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    RunProcess(candidate, Environment.CurrentDirectory, "--version");
                    return candidate;
                }
                catch
                {
                    // Try the portable command after an unusable override.
                }
            }
            throw new InvalidOperationException("Git is required for Git-aware rewrite tests. Set COGS_GIT or add git to PATH.");
        }

        private static string RunProcess(string executable, string workingDirectory, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Could not start '{executable}'.");
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Git failed with exit code {process.ExitCode}: {error}");
            }
            return output.TrimEnd('\r', '\n');
        }
    }
}
