using Cogs.Model;
using Cogs.Publishers;
using Cogs.Publishers.Dot;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Collections.Generic;
using Xunit;

namespace Cogs.Tests;

public sealed class DotPublisherTests
{
    [Theory]
    [InlineData("dot - graphviz version 16.1.0 (20260903)", "", "16.1.0")]
    [InlineData("", "dot - graphviz version 16.1 (build)\r\n", "16.1.0")]
    [InlineData("output", "dot - graphviz version 17.2.3 (build)", "17.2.3")]
    [InlineData("dot - graphviz version 2.38.0 (build)", "", "2.38.0")]
    public void GraphvizVersionParsingReadsEitherStream(string stdout, string stderr, string expected)
    {
        Assert.True(GraphvizTool.TryParseVersion(stdout, stderr, out Version version));
        Assert.Equal(new Version(expected), version);
    }

    [Theory]
    [InlineData(0, "16.1.0", null)]
    [InlineData(0, "16.2.0", null)]
    [InlineData(0, "18.0.0", null)]
    [InlineData(0, "16.0.9", "PROJ2707")]
    [InlineData(0, "2.42.2", "PROJ2707")]
    [InlineData(1, "16.1.0", "PROJ2708")]
    [InlineData(0, "not-a-version", "PROJ2708")]
    [InlineData(0, "99999999999999999999.1.0", "PROJ2708")]
    public void GraphvizVersionPolicyIsAMinimumNotAnExactPin(int exit, string version, string code)
    {
        var error = GraphvizTool.CheckVersion(new(exit, "", "dot - graphviz version " + version), "/tools/dot");
        Assert.Equal(code, error?.Code);
        if (error is not null)
        {
            Assert.Equal(Cogs.Common.ErrorLevel.Error, error.Level);
            Assert.Equal("/tools/dot", error.SourcePath);
        }
    }

    [Fact]
    public void RenderingProbesOnceAndNewPublicationsRecheckTheExecutable()
    {
        using var temporary = new TemporaryDirectory();
        var errors = new List<Cogs.Common.CogsError>();
        int probes = 0, renders = 0;
        string version = "16.1.0";
        GraphvizProcessResult Run(ProcessStartInfo start)
        {
            if (start.ArgumentList.Count == 1 && start.ArgumentList[0] == "-V")
            {
                probes++;
                return new(0, "", "dot - graphviz version " + version);
            }
            renders++;
            Assert.Equal("-Tsvg", start.ArgumentList[0]);
            string input = File.ReadAllText(start.ArgumentList[3]);
            Match id = Regex.Match(input, "svgid=\"([^\"]+)\"");
            File.WriteAllText(start.ArgumentList[2], "<svg xmlns='http://www.w3.org/2000/svg' id='" + id.Groups[1].Value +
                "'><text xml:space='preserve'> padded  value </text></svg>");
            return new(0, "", "");
        }
        var renderer = new GraphvizRenderer(temporary.Path, "test-dot", errors, Run);
        foreach (string name in new[] { "first", "second" })
        {
            Assert.True(renderer.Render(new(name, name, Array.Empty<DiagramNode>(), Array.Empty<DiagramEdge>(), null), DiagramFormat.Svg));
            XDocument light = XDocument.Load(temporary.Child(name + ".svg"));
            XDocument dark = XDocument.Load(temporary.Child(name + ".dark.svg"));
            Assert.Equal(DotWriter.SvgId(name), light.Root!.Attribute("id")!.Value);
            Assert.Equal(light.Root.Attribute("id")!.Value, dark.Root!.Attribute("id")!.Value);
            Assert.Equal("preserve", light.Root.Element(XName.Get("text", "http://www.w3.org/2000/svg"))!.Attribute(XNamespace.Xml + "space")!.Value);
            Assert.Equal(" padded  value ", dark.Root.Element(XName.Get("text", "http://www.w3.org/2000/svg"))!.Value);
        }
        Assert.Equal(1, probes);
        Assert.Equal(2, renders);
        Assert.Empty(errors);
        version = "15.0.0";
        var next = new GraphvizRenderer(temporary.Path, "test-dot", errors, Run);
        Assert.False(next.Render(new("third", "third", Array.Empty<DiagramNode>(), Array.Empty<DiagramEdge>(), null), DiagramFormat.Svg));
        Assert.Equal(2, probes);
        Assert.Equal(2, renders);
        Assert.Contains(errors, e => e.Code == "PROJ2707");
        Assert.False(File.Exists(temporary.Child("third.svg")));
        Assert.Empty(Directory.GetFiles(temporary.Path, "*.dot-input"));
    }

    [Fact]
    public void RawDotDoesNotExecuteOrProbeAnOutdatedTool()
    {
        using var temporary = new TemporaryDirectory();
        var renderer = new GraphvizRenderer(temporary.Path, "old-dot", new(), _ => throw new Exception("Must not execute."));
        Assert.True(renderer.Render(new("raw", "raw", Array.Empty<DiagramNode>(), Array.Empty<DiagramEdge>(), null), DiagramFormat.Dot));
        Assert.True(File.Exists(temporary.Child("raw.dot")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedDiscoveryHonorsOptionEnvironmentThenPath(bool windows)
    {
        using var temporary = new TemporaryDirectory();
        string directory = temporary.Child("path with spaces");
        Directory.CreateDirectory(directory);
        string executable = Path.Combine(directory, windows ? "dot.exe" : "dot");
        File.WriteAllText(executable, "fixture");
        Assert.Equal("explicit", GraphvizTool.Discover("explicit", "configured", directory, windows));
        Assert.Equal("configured", GraphvizTool.Discover(null, "configured", directory, windows));
        Assert.Equal(executable, GraphvizTool.Discover(" ", "", temporary.Child("missing") + Path.PathSeparator + directory, windows));
        Assert.Null(GraphvizTool.Discover(null, null, temporary.Child("missing"), windows));
    }

    [Fact]
    public void VersionFailureRollsBackAnExistingPublication()
    {
        using var temporary = new TemporaryDirectory();
        string target = temporary.Child("existing");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.txt"), "unchanged");
        var errors = new List<Cogs.Common.CogsError>();
        Assert.Throws<CogsPublicationException>(() => DirectoryPublication.Publish(target, true, staging =>
        {
            var renderer = new GraphvizRenderer(staging, "old-dot", errors, _ => new(0, "", "dot - graphviz version 2.38.0"));
            if (!renderer.Render(LocalDiagram(BuildLocalModel(), "Focus"), DiagramFormat.Svg)) throw new CogsPublicationException("Failed.");
        }));
        Assert.Contains(errors, e => e.Code == "PROJ2707");
        Assert.Equal(new[] { "keep.txt" }, Directory.GetFiles(target).Select(Path.GetFileName));
        Assert.Equal("unchanged", File.ReadAllText(Path.Combine(target, "keep.txt")));
    }

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
        Assert.False(LocalDiagram(BuildLocalModel(), "Focus").Nodes.Single(n => n.Name == "Out").Detailed);
        Assert.True(LocalDiagram(BuildLocalModel(), "Focus").Nodes.Single(n => n.Name == "Focus").Detailed);
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
        Assert.True(LocalDiagram(BuildLocalModel(), "Focus", composites: true).Nodes.Single(n => n.Name == "Box").Detailed);
        Assert.True(LocalDiagram(BuildLocalModel(), "Focus", composites: true).Nodes.Single(n => n.Name == "Inner").Detailed);
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
        Assert.True(LocalDiagram(model, "Isolated").Nodes.Single().Detailed);
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

        var failure = Assert.Throws<CogsPublicationException>(() => publisher.Publish(BuildModel()));
        Assert.Contains("PROJ2705", failure.Message);
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

        GraphvizRenderer.NormalizePdfMetadata(path);
        byte[] normalized = File.ReadAllBytes(path);
        Assert.Equal(original.Length, normalized.Length);
        Assert.Equal(prefix, normalized[..prefix.Length]);
        string text = Encoding.Latin1.GetString(normalized);
        Assert.Contains("/CreationDate (D:19700101000000Z)", text, StringComparison.Ordinal);
        Assert.Contains("/ModDate (D:19700101000000-05'00')", text, StringComparison.Ordinal);

        GraphvizRenderer.NormalizePdfMetadata(path);
        Assert.Equal(normalized, File.ReadAllBytes(path));
    }

    [Fact]
    public void PdfRenderingUsesAReproducibleSourceDateEpoch()
    {
        var pdfStartInfo = new ProcessStartInfo();
        pdfStartInfo.Environment["SOURCE_DATE_EPOCH"] = "987654321";

        GraphvizRenderer.ConfigureGraphvizEnvironment(pdfStartInfo, "pdf");

        Assert.Equal("0", pdfStartInfo.Environment["SOURCE_DATE_EPOCH"]);

        var svgStartInfo = new ProcessStartInfo();
        GraphvizRenderer.ConfigureGraphvizEnvironment(svgStartInfo, "svg");
        Assert.False(svgStartInfo.Environment.ContainsKey("SOURCE_DATE_EPOCH"));
    }

    [Fact]
    public void StructuredProjectionRetainsScopeAndPropertyOrder()
    {
        Diagram diagram = LocalDiagram(BuildLocalModel(), "Focus");
        DiagramNode focus = diagram.Nodes.Single(n => n.Name == "Focus");
        Assert.Equal(new[] { "OutOne", "OutTwo", "Self", "Nested", "Repeated" }, focus.Properties.Select(p => p.Name));
        Assert.Empty(diagram.Nodes.Single(n => n.Name == "Out").Properties);
        Assert.Equal(7, diagram.Edges.Count);
        Assert.Contains(diagram.Edges, e => e.Source == "Focus" && e.Target == "Out" && e.PropertyPath == "Repeated"
            && e.Minimum == "2" && e.Maximum == "3" && e.Ordered);
        Assert.Single(diagram.Edges, e => e.Source == "Focus" && e.Target == "Focus");
    }

    [Theory]
    [InlineData("0", "single", "PROJ2701")]
    [InlineData("svg", "1", "PROJ2702")]
    public void NumericEnumSpellingsRemainInvalid(string format, string mode, string code)
    {
        using var temporary = new TemporaryDirectory();
        var publisher = new DotSchemaPublisher { TargetDirectory = temporary.Child("out"), Format = format, Output = mode };
        Assert.Throws<CogsPublicationException>(() => publisher.Publish(BuildModel()));
        Assert.Contains(publisher.Errors, e => e.Code == code);
        Assert.False(Directory.Exists(publisher.TargetDirectory));
    }

    [Fact]
    public void CompanionFilenameCollisionsPreserveExistingOutputBeforeRendering()
    {
        using var temporary = new TemporaryDirectory();
        var model = new CogsModel();
        model.ItemTypes.Add(new ItemType { Name = "Foo" });
        model.ItemTypes.Add(new ItemType { Name = "Foo.dark" });
        string target = temporary.Child("existing");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.txt"), "unchanged");
        var publisher = new DotSchemaPublisher { TargetDirectory = target, Overwrite = true, DotLocation = "not-an-executable" };
        Assert.Throws<CogsPublicationException>(() => publisher.Publish(model));
        Assert.Contains(publisher.Errors, e => e.Code == "PROJ2706" && e.Message.Contains("Foo.dark.svg", StringComparison.Ordinal));
        Assert.Equal(new[] { "keep.txt" }, Directory.GetFiles(target).Select(Path.GetFileName));
        Assert.Equal(target, publisher.TargetDirectory);
    }

    [Fact]
    public void ModernWriterUsesCardsAndEscapesBothDotAndHtmlContent()
    {
        var node = new DiagramNode("Quoted\"<&> {record} α", true, true, true,
            new[] { new DiagramProperty("Value<&>", "string", "0", "n", true) });
        string text = DotWriter.Write(new Diagram("test", "Test", new[] { node }, Array.Empty<DiagramEdge>(), null));
        Assert.Contains("rankdir=TB", text);
        Assert.Contains("rounded,filled", text);
        Assert.Contains("fontname=\"Arial\"", text);
        Assert.Contains("Quoted\\\"<&> {record} α", text);
        Assert.Contains("Quoted&quot;&lt;&amp;&gt; {record}", text);
        Assert.Contains("Value&lt;&amp;&gt;", text);
        Assert.Contains("«abstract»", text);
        Assert.Contains("{ordered}", text);
        Assert.DoesNotContain("shape=record", text);
        Assert.DoesNotContain("#f7b733", text);
        Assert.DoesNotContain("#fc4a1a", text);
    }

    [Fact]
    public void SvgVariantsPreserveGeometryLiteralDataAndHaveNoShadows()
    {
        using var temporary = new TemporaryDirectory();
        string path = temporary.Child("test.svg");
        File.WriteAllText(path, "<svg xmlns='http://www.w3.org/2000/svg' width='120pt' height='80pt' viewBox='0 0 120 80'>" +
            "<g id='graph0'><polygon fill='white' stroke='none' points='0,0 120,0 120,80 0,80'/>" +
            "<g id='node1'><path fill='#f8fafc' stroke='#94a3b8' d='M0 0L10 10'/><text fill='#1e293b' x='4' y='8'>α &amp; value</text></g></g></svg>");
        SvgAppearance.WriteVariants(path, "A quoted \"title\" α\rwith newline");
        XDocument light = XDocument.Load(path);
        XDocument dark = XDocument.Load(temporary.Child("test.dark.svg"));
        XNamespace svg = "http://www.w3.org/2000/svg";
        Assert.Equal("img", light.Root!.Attribute("role")!.Value);
        Assert.Equal("A quoted \"title\" α\rwith newline", light.Root.Element(svg + "title")!.Value);
        Assert.Equal(DiagramPalette.Dark.Canvas, dark.Descendants(svg + "polygon").Single().Attribute("fill")!.Value);
        Assert.Equal("α & value", dark.Descendants(svg + "text").Single().Value);
        foreach (XDocument document in new[] { light, dark })
            foreach (XAttribute attribute in document.Descendants().Attributes().Where(a => a.Name.LocalName is "fill" or "stroke" or "data-appearance").ToArray()) attribute.Remove();
        Assert.True(XNode.DeepEquals(light, dark));
        Assert.Empty(light.Descendants(svg + "filter"));
        byte[] first = File.ReadAllBytes(path);
        Assert.False(first.Take(3).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.DoesNotContain('\r', File.ReadAllText(path));
    }

    [Fact]
    public void PaletteTextAndEdgesMeetContrastTargets()
    {
        foreach (DiagramPalette p in new[] { DiagramPalette.Light, DiagramPalette.Dark })
        {
            Assert.True(Contrast(p.Text, p.Surface) >= 4.5);
            Assert.True(Contrast(p.Muted, p.Surface) >= 4.5);
            Assert.True(Contrast(p.Item, p.Header) >= 4.5);
            Assert.True(Contrast(p.Composite, p.Header) >= 4.5);
            Assert.True(Contrast(p.Edge, p.Canvas) >= 3);
        }
        static double Contrast(string a, string b)
        {
            static double Luminance(string color)
            {
                double[] rgb = Enumerable.Range(0, 3).Select(i => Convert.ToInt32(color.Substring(1 + i * 2, 2), 16) / 255.0)
                    .Select(v => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4)).ToArray();
                return rgb[0] * 0.2126 + rgb[1] * 0.7152 + rgb[2] * 0.0722;
            }
            double x = Luminance(a), y = Luminance(b);
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }
    }

    private static Diagram LocalDiagram(CogsModel model, string name, bool inheritance = false, bool composites = false) =>
        new DiagramProjection(model, inheritance, composites).Build(DiagramMode.Single).Single(d => d.BaseName == name);

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
