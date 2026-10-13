using System.Collections.Generic;

namespace Cogs.Publishers.Dot;

internal enum DiagramFormat { Dot, Svg, Png, Jpeg, Pdf }
internal enum DiagramMode { All, Topic, Single }
internal enum DiagramEdgeKind { Property, Inheritance }

internal sealed record DiagramProperty(string Name, string TypeName, string Minimum, string Maximum, bool Ordered);
internal sealed record DiagramNode(string Name, bool IsItem, bool IsAbstract, bool Detailed,
    IReadOnlyList<DiagramProperty> Properties);
internal sealed record DiagramEdge(string Source, string Target, DiagramEdgeKind Kind,
    string PropertyPath, string Minimum, string Maximum, bool Ordered)
{
    internal string Label => Kind == DiagramEdgeKind.Inheritance ? "extends" :
        $"{PropertyPath} [{Minimum}..{Maximum}]" + (Ordered ? " {ordered}" : string.Empty);
}
internal sealed record Diagram(string BaseName, string Title, IReadOnlyList<DiagramNode> Nodes,
    IReadOnlyList<DiagramEdge> Edges, string? SourcePath);
