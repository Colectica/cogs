using Cogs.Common;
using Cogs.Dto;
using Cogs.Model;
using Cogs.Publishers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Cogs.Tests;

public sealed class ModelImmutabilityTests
{
    [Fact]
    public void SuccessfulBuildFreezesTheCompleteReachableModelGraph()
    {
        AdditionalText sourceText;
        CogsDtoModel dto = CreateDto(out sourceText);

        CogsBuildResult result = new CogsModelBuilder().BuildResult(dto);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        CogsModel model = Assert.IsType<CogsModel>(result.Model);
        Cogs.Model.ItemType root = model.ItemTypes.Single(type => type.Name == "RootItem");
        Cogs.Model.ItemType child = model.ItemTypes.Single(type => type.Name == "ChildItem");
        Cogs.Model.Property rootName = root.Properties.Single(property => property.Name == "RootName");
        Cogs.Model.Property peer = child.Properties.Single(property => property.Name == "Peer");
        Cogs.Model.TopicIndex topic = Assert.Single(model.TopicIndices);

        Assert.True(model.IsReadOnly);
        Assert.True(model.Settings.IsReadOnly);
        Assert.All(model.AllDataTypes, type => Assert.True(type.IsReadOnly));
        Assert.All(model.AllDataTypes.SelectMany(type => type.Properties), property => Assert.True(property.IsReadOnly));
        Assert.True(rootName.DataType.IsReadOnly); // A reachable builtin type is frozen too.
        Assert.True(Assert.Single(child.Relationships).IsReadOnly);
        Assert.True(topic.IsReadOnly);
        Assert.True(Assert.Single(root.AdditionalText).IsReadOnly);

        Assert.Throws<InvalidOperationException>(() => model.SourceDirectory = "changed");
        Assert.Throws<NotSupportedException>(() => model.ItemTypes.Add(new Cogs.Model.ItemType()));
        Assert.Throws<NotSupportedException>(() => model.Identification.Clear());
        Assert.Throws<NotSupportedException>(() => model.ArticleTocEntries.Add("changed"));
        Assert.Throws<InvalidOperationException>(() => model.Settings.Title = "changed");
        Assert.Throws<NotSupportedException>(() => model.Settings.ExtraSettings.Add("changed", "value"));
        Assert.Throws<InvalidOperationException>(() => root.Name = "Changed");
        Assert.Throws<NotSupportedException>(() => root.Properties.Add(new Cogs.Model.Property()));
        Assert.Throws<NotSupportedException>(() => child.ParentTypes.Clear());
        Assert.Throws<NotSupportedException>(() => root.ChildTypes.Clear());
        Assert.Throws<NotSupportedException>(() => child.Relationships.Clear());
        Assert.Throws<InvalidOperationException>(() => rootName.Name = "Changed");
        Assert.Throws<NotSupportedException>(() => rootName.Enumeration.Add("other"));
        Assert.Throws<InvalidOperationException>(() => rootName.DataType.Name = "Changed");
        Assert.Throws<InvalidOperationException>(() => peer.DataType = null);
        Assert.Throws<InvalidOperationException>(() => topic.Name = "Changed");
        Assert.Throws<NotSupportedException>(() => topic.ItemTypes.Clear());
        Assert.Throws<InvalidOperationException>(() => root.AdditionalText[0].Content = "changed");

        // Documentation metadata is cloned before freezing; building never freezes or aliases the DTO.
        sourceText.Content = "source remains mutable";
        Assert.Equal("Documentation", root.AdditionalText[0].Content);
    }

    [Fact]
    public void ResultApisNeverExposePartialModelsOrMutableResultCollections()
    {
        var error = new CogsError(ErrorLevel.Error, "COGS-TEST-001", "failure", "z.csv", 4, 2);
        var warning = new CogsError(ErrorLevel.Warning, "COGS-TEST-002", "warning", "a.csv", 2, 1);

        var load = new CogsLoadResult(new CogsDtoModel(), new[] { error, warning });
        var build = new CogsBuildResult(new CogsModel(), new[] { error, warning });
        var publication = new PublicationResult(new[] { "partial.txt" }, new[] { error, warning });

        Assert.False(load.Success);
        Assert.Null(load.Model);
        Assert.False(build.Success);
        Assert.Null(build.Model);
        Assert.False(publication.Success);
        Assert.Empty(publication.Artifacts);
        Assert.Equal("a.csv", load.Diagnostics[0].SourcePath);
        Assert.Equal("a.csv", build.Diagnostics[0].SourcePath);
        Assert.Equal("a.csv", publication.Diagnostics[0].SourcePath);
        Assert.Throws<NotSupportedException>(() => ((IList<CogsError>)load.Diagnostics).Add(error));
        Assert.Throws<NotSupportedException>(() => ((IList<CogsError>)build.Diagnostics).Add(error));
        Assert.Throws<NotSupportedException>(() => ((IList<CogsError>)publication.Diagnostics).Add(error));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)publication.Artifacts).Add("other.txt"));
    }

    [Fact]
    public void CompatibilityAdaptersAreObsoleteAndReturnNullForInvalidInput()
    {
        Assert.NotNull(typeof(CogsDirectoryReader).GetMethod(nameof(CogsDirectoryReader.Load))!
            .GetCustomAttribute<ObsoleteAttribute>());
        Assert.NotNull(typeof(CogsModelBuilder).GetMethod(nameof(CogsModelBuilder.Build))!
            .GetCustomAttribute<ObsoleteAttribute>());

        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
#pragma warning disable CS0618 // The test verifies the one-major-release compatibility adapters.
        Assert.Null(new CogsDirectoryReader().Load(missing));

        CogsDtoModel invalid = CreateDto(out _);
        invalid.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "RootItem" });
        Assert.Null(new CogsModelBuilder().Build(invalid));
#pragma warning restore CS0618
    }

    private static CogsDtoModel CreateDto(out AdditionalText sourceText)
    {
        var dto = new CogsDtoModel
        {
            SourceDirectory = Path.GetFullPath("model-source"),
            ArticlesPath = "articles",
            HeaderInclude = "header"
        };
        dto.ArticleTocEntries.Add("intro");
        dto.Settings.Add(new Setting { Key = "Title", Value = "Immutable Model" });
        dto.Settings.Add(new Setting { Key = "Extra", Value = "value" });
        dto.Identification.Add(DtoProperty("ID", "string", "1", "1"));

        sourceText = new AdditionalText
        {
            FilePath = "details.markdown",
            Format = "markdown",
            Name = "Details",
            Content = "Documentation"
        };
        var root = new Cogs.Dto.ItemType { Name = "RootItem", IsAbstract = true };
        root.AdditionalText.Add(sourceText);
        Cogs.Dto.Property rootName = DtoProperty("RootName", "string");
        rootName.Enumeration = "alpha beta";
        root.Properties.Add(rootName);
        dto.ItemTypes.Add(root);

        var child = new Cogs.Dto.ItemType { Name = "ChildItem", Extends = "RootItem" };
        child.Properties.Add(DtoProperty("Peer", "RootItem"));
        dto.ItemTypes.Add(child);

        var topic = new Cogs.Dto.TopicIndex { Name = "All" };
        topic.ItemTypes.Add("ChildItem");
        topic.ArticleTocEntries.Add("intro");
        dto.TopicIndices.Add(topic);
        return dto;
    }

    private static Cogs.Dto.Property DtoProperty(
        string name,
        string dataType,
        string minimum = "0",
        string maximum = "1") => new()
        {
            Name = name,
            DataType = dataType,
            MinCardinality = minimum,
            MaxCardinality = maximum
        };
}
