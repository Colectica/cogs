using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Cogs.Tests;

public sealed class SphinxPublisherTests
{
    [Fact]
    public void DocumentationWithoutGraphvizPreservesMarkdownAndHasNoDiagramLinks()
    {
        using var temporary = new TemporaryDirectory();
        string articles = temporary.Child("articles");
        Directory.CreateDirectory(Path.Combine(articles, "nested"));
        File.WriteAllText(Path.Combine(articles, "guide.md"), "# Authored guide\n\nMarkdown stays Markdown.\n");
        File.WriteAllText(Path.Combine(articles, "nested", "legacy.rst"), "Legacy\n======\n");
        CogsModel model = BuildModel(articles);
        string output = temporary.Child("documentation");

        new BuildSphinxDocumentation().Build(model, output, includeDiagrams: false);

        string configuration = File.ReadAllText(Path.Combine(output, "source", "conf.py"));
        Assert.Contains("'myst_parser'", configuration, StringComparison.Ordinal);
        Assert.Contains("'.md': 'markdown'", configuration, StringComparison.Ordinal);
        Assert.Contains("language = 'en'", configuration, StringComparison.Ordinal);
        Assert.True(configuration.Contains("project = \"A \\u0022quoted\\u0022 title\\nwith newline\"", StringComparison.Ordinal), configuration);
        Assert.Equal("# Authored guide\n\nMarkdown stays Markdown.\n",
            File.ReadAllText(Path.Combine(output, "source", "guide.md")));
        Assert.True(File.Exists(Path.Combine(output, "source", "nested", "legacy.rst")));

        string typeDirectory = Path.Combine(output, "source", "item-types", "Thing");
        Assert.Contains("extra-details.md",
            Directory.EnumerateFiles(typeDirectory).Select(Path.GetFileName),
            StringComparer.Ordinal);
        Assert.Contains("untitled-notes.md",
            Directory.EnumerateFiles(typeDirectory).Select(Path.GetFileName),
            StringComparer.Ordinal);
        Assert.Equal("# Extra details\n\n*authored* content\n",
            File.ReadAllText(Path.Combine(typeDirectory, "extra-details.md")));
        Assert.Equal("# Untitled notes" + Environment.NewLine + Environment.NewLine + "Authored content without a heading.\n",
            File.ReadAllText(Path.Combine(typeDirectory, "untitled-notes.md")));
        string page = File.ReadAllText(Path.Combine(typeDirectory, "index.rst"));
        Assert.DoesNotContain("image::", page, StringComparison.Ordinal);
        Assert.Contains("minLength=2", page, StringComparison.Ordinal);
        Assert.Contains("pattern=[A-Z]+", page, StringComparison.Ordinal);
        Assert.Contains("Additional Documentation", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ItemReadmeMarkdownIsAReferencedMystDocumentAndCannotCollideWithAdditionalText()
    {
        using var temporary = new TemporaryDirectory();
        string articles = temporary.Child("articles");
        Directory.CreateDirectory(articles);
        File.WriteAllText(Path.Combine(articles, "guide.md"), "# Guide\n");
        const string readme = "# Item overview\n\n**item readme Markdown** remains authored content.\n";
        CogsModel model = BuildModel(
            articles,
            itemDescription: readme,
            addDescriptionAdditionalText: true);
        string output = temporary.Child("documentation");

        new BuildSphinxDocumentation().Build(model, output, includeDiagrams: false);

        string typeDirectory = Path.Combine(output, "source", "item-types", "Thing");
        Assert.Equal(readme, File.ReadAllText(Path.Combine(typeDirectory, "description.md")));
        Assert.Equal("# Additional description\n\nAdditional Markdown.\n",
            File.ReadAllText(Path.Combine(typeDirectory, "description-2.md")));
        string index = File.ReadAllText(Path.Combine(typeDirectory, "index.rst"));
        Assert.Contains("   description.md", index, StringComparison.Ordinal);
        Assert.Contains("Description <description-2.md>", index, StringComparison.Ordinal);
        Assert.DoesNotContain("**item readme Markdown**", index, StringComparison.Ordinal);
    }

    [Fact]
    public void TopicReadmeMarkdownIsAReferencedLowercaseMystDocument()
    {
        using var temporary = new TemporaryDirectory();
        string articles = temporary.Child("articles");
        Directory.CreateDirectory(articles);
        File.WriteAllText(Path.Combine(articles, "guide.md"), "# Guide\n");
        const string readme = "# Topic overview\n\n*topic readme Markdown* remains authored content.\n";
        CogsModel model = BuildModel(articles, topicDescription: readme);
        string output = temporary.Child("documentation");

        new BuildSphinxDocumentation().Build(model, output, includeDiagrams: false);

        string topicDirectory = Path.Combine(output, "source", "topics", "MarkdownTopic");
        Assert.Equal(readme, File.ReadAllText(Path.Combine(topicDirectory, "description.md")));
        string index = File.ReadAllText(Path.Combine(topicDirectory, "index.rst"));
        Assert.Contains("   description.md", index, StringComparison.Ordinal);
        Assert.DoesNotContain("*topic readme Markdown*", index, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentationRejectsUnsafeToctreeEntryBeforeWritingOutput()
    {
        using var temporary = new TemporaryDirectory();
        string articles = temporary.Child("articles");
        Directory.CreateDirectory(articles);
        File.WriteAllText(Path.Combine(articles, "guide.md"), "# Guide\n");
        CogsModel model = BuildModel(articles, ":hidden:");
        string output = temporary.Child("documentation");

        Assert.Throws<InvalidDataException>(() =>
            new BuildSphinxDocumentation().Build(model, output, includeDiagrams: false));
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void SphinxPublisherRejectsUnsafeToctreeWithoutChangingExistingTarget()
    {
        using var temporary = new TemporaryDirectory();
        string articles = temporary.Child("articles");
        Directory.CreateDirectory(articles);
        File.WriteAllText(Path.Combine(articles, "guide.md"), "# Guide\n");
        CogsModel model = BuildModel(articles, "guide#anchor");
        string output = temporary.Child("documentation");
        Directory.CreateDirectory(output);
        string marker = Path.Combine(output, "keep.txt");
        File.WriteAllText(marker, "unchanged");
        var publisher = new SphinxPublisher { TargetDirectory = output, Overwrite = true };

        Assert.Throws<InvalidDataException>(() => publisher.Publish(model));

        Assert.Equal("unchanged", File.ReadAllText(marker));
        Assert.Equal(new[] { "keep.txt" }, Directory.EnumerateFiles(output).Select(Path.GetFileName));
    }

    [Fact]
    public void ConfiguredGraphvizFailureIsAnErrorAndRollsBackSphinxOutput()
    {
        using var temporary = new TemporaryDirectory();
        string articles = temporary.Child("articles");
        Directory.CreateDirectory(articles);
        File.WriteAllText(Path.Combine(articles, "guide.md"), "# Guide\n");
        CogsModel model = BuildModel(articles);
        string output = temporary.Child("documentation");
        Directory.CreateDirectory(output);
        string marker = Path.Combine(output, "keep.txt");
        File.WriteAllText(marker, "unchanged");
        var publisher = new SphinxPublisher
        {
            TargetDirectory = output,
            Overwrite = true,
            DotLocation = temporary.Child("missing-graphviz-executable")
        };

        Assert.Throws<CogsPublicationException>(() => publisher.Publish(model));

        Assert.Contains(publisher.Errors, error => error.Code == "PROJ2705" && error.Level == Cogs.Common.ErrorLevel.Error);
        Assert.Equal("unchanged", File.ReadAllText(marker));
        Assert.Equal(new[] { "keep.txt" }, Directory.EnumerateFiles(output).Select(Path.GetFileName));
    }

    private static CogsModel BuildModel(
        string articles,
        string articleTocEntry = "guide",
        string itemDescription = null,
        string topicDescription = null,
        bool addDescriptionAdditionalText = false)
    {
        var dto = new CogsDtoModel
        {
            ArticlesPath = articles,
            SourceDirectory = Path.Combine(Path.GetDirectoryName(articles)!, "model-source")
        };
        dto.ArticleTocEntries.Add(articleTocEntry);
        dto.Settings.Add(new Setting { Key = "Title", Value = "A \"quoted\" title\nwith newline" });
        dto.Settings.Add(new Setting { Key = "ShortTitle", Value = "Docs" });
        dto.Settings.Add(new Setting { Key = "Slug", Value = "docs_test" });
        dto.Settings.Add(new Setting { Key = "Version", Value = "2.0.0" });
        dto.Settings.Add(new Setting { Key = "Author", Value = "An 'author'" });
        dto.Settings.Add(new Setting { Key = "Copyright", Value = "Copyright ©" });
        dto.Settings.Add(new Setting { Key = "NamespaceUrl", Value = "https://example.org/docs" });
        dto.Settings.Add(new Setting { Key = "NamespacePrefix", Value = "d" });
        dto.Identification.Add(new Cogs.Dto.Property
        {
            Name = "ID", DataType = "string", MinCardinality = "1", MaxCardinality = "1"
        });
        var item = new Cogs.Dto.ItemType { Name = "Thing", Description = itemDescription ?? string.Empty };
        item.Properties.Add(new Cogs.Dto.Property
        {
            Name = "Code", DataType = "string", MinCardinality = "0", MaxCardinality = "1",
            MinLength = 2, Pattern = "[A-Z]+"
        });
        item.AdditionalText.Add(new AdditionalText
        {
            Name = "Extra details", Format = "markdown", Content = "# Extra details\n\n*authored* content\n"
        });
        item.AdditionalText.Add(new AdditionalText
        {
            Name = "Untitled notes", Format = "markdown", Content = "Authored content without a heading.\n"
        });
        if (addDescriptionAdditionalText)
        {
            item.AdditionalText.Add(new AdditionalText
            {
                Name = "Description", Format = "markdown", Content = "# Additional description\n\nAdditional Markdown.\n"
            });
        }
        dto.ItemTypes.Add(item);
        if (topicDescription is not null)
        {
            var topic = new Cogs.Dto.TopicIndex
            {
                Name = "MarkdownTopic",
                Description = topicDescription,
                ArticlesPath = string.Empty
            };
            topic.ItemTypes.Add("Thing");
            dto.TopicIndices.Add(topic);
        }
        CogsBuildResult result = new CogsModelBuilder().BuildResult(dto);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return result.Model!;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cogs-sphinx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public string Path { get; }
        public string Child(string name) => System.IO.Path.Combine(Path, name);
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }
}
