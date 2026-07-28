using Cogs.Publishers;
using System;
using System.IO;
using Xunit;

namespace Cogs.Tests;

public sealed class DirectoryPublicationTests
{
    [Fact]
    public void PublishDoesNotExposePartialOutputWhenWriterFails()
    {
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "output");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "original.txt"), "original");

        Assert.Throws<CogsPublicationException>(() => DirectoryPublication.Publish(
            target,
            overwrite: true,
            staging =>
            {
                File.WriteAllText(Path.Combine(staging, "partial.txt"), "partial");
                throw new IOException("simulated failure");
            }));

        Assert.Equal("original", File.ReadAllText(Path.Combine(target, "original.txt")));
        Assert.False(File.Exists(Path.Combine(target, "partial.txt")));
        Assert.Empty(Directory.GetDirectories(temporary.Path, ".output.cogs-*"));
    }

    [Fact]
    public void PublishReplacesExistingDirectoryOnlyWithOverwrite()
    {
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "output");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "old.txt"), "old");

        Assert.Throws<InvalidOperationException>(() => DirectoryPublication.Publish(
            target,
            overwrite: false,
            staging => File.WriteAllText(Path.Combine(staging, "new.txt"), "new")));

        DirectoryPublication.Publish(
            target,
            overwrite: true,
            staging => File.WriteAllText(Path.Combine(staging, "new.txt"), "new"));

        Assert.False(File.Exists(Path.Combine(target, "old.txt")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(target, "new.txt")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("child")]
    [InlineData("child/grandchild")]
    public void PublishRejectsSourceTargetOverlap(string targetSuffix)
    {
        using var temporary = new TemporaryDirectory();
        string source = Path.Combine(temporary.Path, "model");
        Directory.CreateDirectory(source);
        string target = string.IsNullOrEmpty(targetSuffix)
            ? source
            : Path.Combine(source, targetSuffix.Replace('/', Path.DirectorySeparatorChar));

        Assert.Throws<CogsPublicationException>(() => DirectoryPublication.Publish(
            target,
            overwrite: true,
            _ => { },
            source));
    }

    [Fact]
    public void PublishRejectsTargetThatContainsSource()
    {
        using var temporary = new TemporaryDirectory();
        string target = Path.Combine(temporary.Path, "parent");
        string source = Path.Combine(target, "model");
        Directory.CreateDirectory(source);

        Assert.Throws<CogsPublicationException>(() => DirectoryPublication.Publish(
            target,
            overwrite: true,
            _ => { },
            source));
    }

    [Fact]
    public void PrefixLookalikesDoNotOverlap()
    {
        using var temporary = new TemporaryDirectory();
        string source = Path.Combine(temporary.Path, "model");
        string target = Path.Combine(temporary.Path, "model-output");
        Directory.CreateDirectory(source);

        DirectoryPublication.Publish(
            target,
            overwrite: false,
            staging => File.WriteAllText(Path.Combine(staging, "result.txt"), "ok"),
            source);

        Assert.True(File.Exists(Path.Combine(target, "result.txt")));
    }

    [Fact]
    public void DotDotSegmentsCannotBypassOverlapCheck()
    {
        using var temporary = new TemporaryDirectory();
        string source = Path.Combine(temporary.Path, "model");
        Directory.CreateDirectory(source);
        string target = Path.Combine(source, "child", "..", "output");

        Assert.Throws<CogsPublicationException>(() => DirectoryPublication.Publish(
            target,
            overwrite: false,
            _ => { },
            source));
    }

    [Fact]
    public void SymbolicLinkAliasesCannotBypassOverlapCheck()
    {
        using var temporary = new TemporaryDirectory();
        string source = Path.Combine(temporary.Path, "model");
        string alias = Path.Combine(temporary.Path, "alias");
        Directory.CreateDirectory(source);
        try
        {
            Directory.CreateSymbolicLink(alias, source);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return;
        }

        Assert.Throws<CogsPublicationException>(() => DirectoryPublication.Publish(
            Path.Combine(alias, "generated"),
            overwrite: false,
            _ => { },
            source));
    }

    [Fact]
    public void FilesystemRootIsNeverAValidTarget()
    {
        string root = Path.GetPathRoot(Path.GetTempPath())!;
        Assert.Throws<CogsPublicationException>(() => DirectoryPublication.Publish(
            root,
            overwrite: true,
            _ => { }));
    }

    [Fact]
    public void InvalidFilesystemPathsAreModeledPublicationErrors()
    {
        using var temporary = new TemporaryDirectory();
        string invalid = Path.Combine(temporary.Path, "bad\0name");

        Assert.Throws<CogsPublicationException>(() => DirectoryPublication.Publish(
            invalid,
            overwrite: false,
            _ => { }));

        PublicationResult result = DirectoryPublication.PublishResult(
            invalid,
            overwrite: false,
            _ => { });
        Cogs.Common.CogsError diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PUB1001", diagnostic.Code);
        Assert.False(result.Success);
        Assert.Empty(result.Artifacts);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cogs-publication-tests", Guid.NewGuid().ToString("N"));
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
