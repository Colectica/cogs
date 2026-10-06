using Cogs.Model;
using Cogs.Publishers;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Cogs.Tests;

public sealed class DotPublisherTests
{
    [Fact]
    public void RawDotNeedsNoGraphvizAndIncludesIsolatedInheritedAndNestedRelationships()
    {
        using var temporary = new TemporaryDirectory();
        var publisher = new DotSchemaPublisher
        {
            TargetDirectory = temporary.Child("dot"),
            Format = "dot",
            Output = "all",
            Inheritance = true,
            ShowReusables = false
        };

        Assert.Equal(0, publisher.Publish(BuildModel()));
        string graph = File.ReadAllText(Path.Combine(temporary.Child("dot"), "output.dot"));

        Assert.Contains("\"Isolated\" [", graph, StringComparison.Ordinal);
        Assert.Contains("\"Derived\" -> \"Base\" [arrowhead=empty", graph, StringComparison.Ordinal);
        Assert.Contains("\"Derived\" -> \"Target\"", graph, StringComparison.Ordinal);
        Assert.Contains("Nested.Target [0..n]", graph, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Node\" [", graph, StringComparison.Ordinal);
        Assert.Empty(publisher.Errors);
    }

    [Fact]
    public void TopicGraphsRetainTheirBroaderInheritedAndNestedRelationshipScope()
    {
        using var temporary = new TemporaryDirectory();
        var publisher = new DotSchemaPublisher
        {
            TargetDirectory = temporary.Child("topics"), Format = "dot", Output = "topic"
        };
        Assert.Equal(0, publisher.Publish(BuildModel()));
        string graph = File.ReadAllText(Path.Combine(temporary.Child("topics"), "Related.dot"));
        Assert.Contains("\"Derived\" -> \"Target\"", graph);
        Assert.Contains("Nested.Target [0..n]", graph);
        Assert.DoesNotContain("\"Node\" [", graph);
        Assert.DoesNotContain("\"Isolated\" [", graph);
        Assert.Empty(publisher.Errors);
    }

    [Fact]
    public void SingleGraphsContainOnlyDeclaredDirectIncomingAndOutgoingLinks()
    {
        using var temporary = new TemporaryDirectory();
        string graph = PublishLocal(temporary, BuildLocalModel(), "Focus");

        Assert.Contains("\"Focus\" -> \"Out\" [arrowhead=none, label=\"OutOne [0..1]\"]", graph);
        Assert.Contains("\"Focus\" -> \"Out\" [arrowhead=none, label=\"OutTwo [0..1]\"]", graph);
        Assert.Contains("Repeated [2..3] {ordered}", graph);
        Assert.Contains("\"Incoming\" -> \"Focus\"", graph);
        Assert.Contains("\"ExternalComposite\" -> \"Focus\"", graph);
        Assert.Single(Regex.Matches(graph, "\"Focus\" -> \"Focus\"").Cast<Match>());
        Assert.Equal(7, CountEdges(graph));
        Assert.DoesNotContain("\"Out\" ->", graph);
        Assert.DoesNotContain("\"Incoming\" -> \"Out\"", graph);
        Assert.DoesNotContain("\"Far\" [", graph);
        Assert.DoesNotContain("\"Box\" [", graph);
        Assert.DoesNotContain("\"Parent\" [", graph);
        Assert.DoesNotContain("InheritedLink", graph);
        Assert.DoesNotContain("AncestorLink", graph);
        Assert.DoesNotContain("Next :", graph);
        Assert.DoesNotContain("Nested.Link", graph);
        Assert.Contains("\"Out\" [shape=ellipse", graph);
        Assert.Contains("\"Focus\" [shape=record", graph);
    }

    [Fact]
    public void SingleGraphInheritanceIncludesOnlyImmediateParentAndChildren()
    {
        using var temporary = new TemporaryDirectory();
        string graph = PublishLocal(temporary, BuildLocalModel(), "Focus", inheritance: true);

        Assert.Contains("\"Focus\" -> \"Parent\" [arrowhead=empty", graph);
        Assert.Contains("\"Child\" -> \"Focus\" [arrowhead=empty", graph);
        Assert.DoesNotContain("\"Grandparent\" [", graph);
        Assert.DoesNotContain("\"Grandchild\" [", graph);
        Assert.DoesNotContain("InheritedLink", graph);
        Assert.DoesNotContain("AncestorLink", graph);
        Assert.Equal(9, CountEdges(graph));
    }

    [Fact]
    public void SingleGraphsExposeContainedCompositesWithoutFlatteningOrExpandingItemNeighbors()
    {
        using var temporary = new TemporaryDirectory();
        string graph = PublishLocal(temporary, BuildLocalModel(), "Focus", composites: true);

        Assert.Contains("\"Focus\" -> \"Box\" [arrowhead=none, label=\"Nested [0..n]\"]", graph);
        Assert.Contains("\"Box\" [shape=record", graph);
        Assert.Contains("\"Inner\" [shape=record", graph);
        Assert.Contains("\"Box\" -> \"Box\" [arrowhead=none, label=\"Self [0..1]\"]", graph);
        Assert.Contains("\"Inner\" -> \"Box\" [arrowhead=none, label=\"Back [0..1]\"]", graph);
        Assert.Contains("\"Inner\" -> \"Far\" [arrowhead=none, label=\"Link [1..1]\"]", graph);
        Assert.DoesNotContain("\"Out\" ->", graph);
        Assert.DoesNotContain("Next :", graph);
        Assert.DoesNotContain("Nested.Link", graph);
        Assert.DoesNotContain("Nested.Inner", graph);
        Assert.Equal(13, CountEdges(graph));
    }

    [Fact]
    public void SingleCompositeAndIsolatedGraphsRetainLocalScope()
    {
        using var temporary = new TemporaryDirectory();
        CogsModel model = BuildLocalModel();
        string graph = PublishLocal(temporary, model, "Box");
        Assert.Contains("\"Focus\" -> \"Box\"", graph);
        Assert.Contains("\"Inner\" -> \"Box\"", graph);
        Assert.Contains("\"Box\" -> \"Out\"", graph);
        Assert.DoesNotContain("\"Inner\" -> \"Far\"", graph);
        Assert.DoesNotContain("\"Focus\" -> \"Out\"", graph);
        Assert.Equal(4, CountEdges(graph));

        string isolated = File.ReadAllText(Path.Combine(temporary.Child("local"), "Isolated.dot"));
        Assert.Contains("\"Isolated\" [shape=record", isolated);
        Assert.Equal(0, CountEdges(isolated));
        Assert.DoesNotContain("\"Focus\"", isolated);
        Assert.Equal(model.AllDataTypes.Count(), Directory.GetFiles(temporary.Child("local"), "*.dot").Length);
    }

    [Fact]
    public void SingleGraphsAreDeterministicAndRebuildTheirIndexesOnEachPublication()
    {
        using var temporary = new TemporaryDirectory();
        CogsModel model = BuildLocalModel();
        string snapshot = string.Join(";", model.AllDataTypes.Select(type => type.Name + ":" +
            string.Join(",", type.Properties.Select(property => property.Name))));
        var publisher = new DotSchemaPublisher { TargetDirectory = temporary.Child("first"), Format = "dot", Output = "single" };
        Assert.Equal(0, publisher.Publish(model));
        publisher.TargetDirectory = temporary.Child("second");
        Assert.Equal(0, publisher.Publish(model));
        foreach (string first in Directory.GetFiles(temporary.Child("first"), "*.dot"))
            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(Path.Combine(temporary.Child("second"), Path.GetFileName(first))));
        Assert.Equal(snapshot, string.Join(";", model.AllDataTypes.Select(type => type.Name + ":" +
            string.Join(",", type.Properties.Select(property => property.Name)))));

        publisher.TargetDirectory = temporary.Child("other-model");
        Assert.Equal(0, publisher.Publish(BuildModel()));
        string graph = File.ReadAllText(Path.Combine(temporary.Child("other-model"), "Target.dot"));
        Assert.DoesNotContain("\"Focus\"", graph);
        Assert.Contains("\"Node\" -> \"Target\"", graph);
        Assert.Empty(publisher.Errors);
    }

    [Fact]
    public void RenderFailureIsAnErrorAndRollsBackTheTarget()
    {
        using var temporary = new TemporaryDirectory();
        string target = temporary.Child("rendered");
        var publisher = new DotSchemaPublisher
        {
            TargetDirectory = target,
            DotLocation = temporary.Child("missing dot executable"),
            Format = "png",
            Output = "all"
        };

        Assert.Throws<CogsPublicationException>(() => publisher.Publish(BuildModel()));
        Assert.Contains(publisher.Errors, error => error.Code == "PROJ2705");
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void PdfMetadataNormalizationIsFixedWidthLosslessAndIdempotent()
    {
        using var temporary = new TemporaryDirectory();
        string path = temporary.Child("graph.pdf");
        byte[] prefix = [0x25, 0x50, 0x44, 0x46, 0x2d, 0xff, 0x00];
        byte[] metadata = Encoding.Latin1.GetBytes(
            "/CreationDate (D:20260717071124Z) /ModDate (D:20260717071259-05'00')");
        byte[] original = prefix.Concat(metadata).ToArray();
        File.WriteAllBytes(path, original);

        DotSchemaPublisher.NormalizePdfMetadata(path);
        byte[] normalized = File.ReadAllBytes(path);
        Assert.Equal(original.Length, normalized.Length);
        Assert.Equal(prefix, normalized[..prefix.Length]);
        string text = Encoding.Latin1.GetString(normalized);
        Assert.Contains("/CreationDate (D:19700101000000Z)", text, StringComparison.Ordinal);
        Assert.Contains("/ModDate (D:19700101000000-05'00')", text, StringComparison.Ordinal);

        DotSchemaPublisher.NormalizePdfMetadata(path);
        Assert.Equal(normalized, File.ReadAllBytes(path));
    }

    [Fact]
    public void PdfRenderingUsesAReproducibleSourceDateEpoch()
    {
        var pdfStartInfo = new ProcessStartInfo();
        pdfStartInfo.Environment["SOURCE_DATE_EPOCH"] = "987654321";

        DotSchemaPublisher.ConfigureGraphvizEnvironment(pdfStartInfo, "pdf");

        Assert.Equal("0", pdfStartInfo.Environment["SOURCE_DATE_EPOCH"]);

        var svgStartInfo = new ProcessStartInfo();
        DotSchemaPublisher.ConfigureGraphvizEnvironment(svgStartInfo, "svg");
        Assert.False(svgStartInfo.Environment.ContainsKey("SOURCE_DATE_EPOCH"));
    }

    private static int CountEdges(string graph) => Regex.Matches(graph, "(?m)^  \"[^\"]+\" -> \"[^\"]+\"").Count;

    private static string PublishLocal(TemporaryDirectory temporary, CogsModel model, string focus,
        bool inheritance = false, bool composites = false)
    {
        var publisher = new DotSchemaPublisher
        {
            TargetDirectory = temporary.Child("local"), Format = "dot", Output = "single",
            Inheritance = inheritance, ShowReusables = composites
        };
        Assert.Equal(0, publisher.Publish(model));
        Assert.Empty(publisher.Errors);
        return File.ReadAllText(Path.Combine(temporary.Child("local"), focus + ".dot"));
    }

    private static CogsModel BuildLocalModel()
    {
        var dto = new Cogs.Dto.CogsDtoModel();
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespaceUrl", Value = "https://example.org/local-dot" });
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespacePrefix", Value = "d" });
        dto.Identification.Add(Property("ID", "string", "1", "1"));
        var grandparent = new Cogs.Dto.ItemType { Name = "Grandparent" };
        grandparent.Properties.Add(Property("AncestorLink", "Far"));
        var parent = new Cogs.Dto.ItemType { Name = "Parent", Extends = "Grandparent" };
        parent.Properties.Add(Property("InheritedLink", "Far"));
        var focus = new Cogs.Dto.ItemType { Name = "Focus", Extends = "Parent" };
        focus.Properties.Add(Property("OutOne", "Out"));
        focus.Properties.Add(Property("OutTwo", "Out"));
        focus.Properties.Add(Property("Self", "Focus"));
        focus.Properties.Add(Property("Nested", "Box", "0", "n"));
        var repeated = Property("Repeated", "Out", "2", "3");
        repeated.Ordered = "true";
        focus.Properties.Add(repeated);
        var outgoing = new Cogs.Dto.ItemType { Name = "Out" };
        outgoing.Properties.Add(Property("Next", "Far"));
        var incoming = new Cogs.Dto.ItemType { Name = "Incoming" };
        incoming.Properties.Add(Property("Link", "Focus"));
        incoming.Properties.Add(Property("OtherLink", "Focus"));
        incoming.Properties.Add(Property("Unrelated", "Out"));
        foreach (var type in new[] { grandparent, parent, focus, outgoing, incoming,
            new Cogs.Dto.ItemType { Name = "Child", Extends = "Focus" },
            new Cogs.Dto.ItemType { Name = "Grandchild", Extends = "Child" },
            new Cogs.Dto.ItemType { Name = "Far" }, new Cogs.Dto.ItemType { Name = "Isolated" } }) dto.ItemTypes.Add(type);
        var box = new Cogs.Dto.DataType { Name = "Box" };
        box.Properties.Add(Property("Self", "Box"));
        box.Properties.Add(Property("Inner", "Inner"));
        box.Properties.Add(Property("Link", "Out"));
        var inner = new Cogs.Dto.DataType { Name = "Inner" };
        inner.Properties.Add(Property("Back", "Box"));
        inner.Properties.Add(Property("Link", "Far", "1", "1"));
        var external = new Cogs.Dto.DataType { Name = "ExternalComposite" };
        external.Properties.Add(Property("FocusLink", "Focus"));
        foreach (var type in new[] { box, inner, external }) dto.ReusableDataTypes.Add(type);
        CogsBuildResult result = new CogsModelBuilder().BuildResult(dto);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return result.Model!;
    }

    private static CogsModel BuildModel()
    {
        var dto = new Cogs.Dto.CogsDtoModel();
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespaceUrl", Value = "https://example.org/dot" });
        dto.Settings.Add(new Cogs.Dto.Setting { Key = "NamespacePrefix", Value = "d" });
        dto.Identification.Add(Property("ID", "string", "1", "1"));

        var node = new Cogs.Dto.DataType { Name = "Node" };
        node.Properties.Add(Property("Self", "Node"));
        node.Properties.Add(Property("Target", "Target", "1", "1"));
        dto.ReusableDataTypes.Add(node);

        var root = new Cogs.Dto.ItemType { Name = "Base", IsAbstract = true };
        root.Properties.Add(Property("Nested", "Node", "0", "n"));
        dto.ItemTypes.Add(root);
        dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "Derived", Extends = "Base" });
        dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "Target" });
        dto.ItemTypes.Add(new Cogs.Dto.ItemType { Name = "Isolated" });
        dto.TopicIndices.Add(new Cogs.Dto.TopicIndex { Name = "Related", ItemTypes = ["Derived"] });

        CogsBuildResult result = new CogsModelBuilder().BuildResult(dto);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return result.Model!;
    }

    private static Cogs.Dto.Property Property(string name, string type, string minimum = "0", string maximum = "1") => new()
    {
        Name = name,
        DataType = type,
        MinCardinality = minimum,
        MaxCardinality = maximum
    };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cogs-dot-" + Guid.NewGuid().ToString("N"));
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
