using Cogs.Model;
using Cogs.Publishers;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
