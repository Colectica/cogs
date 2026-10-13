using System;
using System.Net;
using System.Text;

namespace Cogs.Publishers.Dot;

/// <summary>One presentation writer for every graph scope.</summary>
internal static class DotWriter
{
    internal static string Write(Diagram diagram)
    {
        var p = DiagramPalette.Light;
        var output = new StringBuilder("digraph COGS {\n");
        output.Append("  graph [rankdir=TB, compound=true, nodesep=0.35, ranksep=0.6, pad=0.25, bgcolor=").Append(Quote(p.Canvas)).Append(", fontname=\"Arial\"];\n");
        output.Append("  node [shape=box, style=\"rounded,filled\", margin=0.12, penwidth=1, fontname=\"Arial\", fontsize=11, color=")
            .Append(Quote(p.Border)).Append(", fillcolor=").Append(Quote(p.Surface)).Append(", fontcolor=").Append(Quote(p.Text)).Append("];\n");
        output.Append("  edge [fontname=\"Arial\", fontsize=10, color=").Append(Quote(p.Edge)).Append(", fontcolor=").Append(Quote(p.Muted)).Append("];\n");
        foreach (DiagramNode node in diagram.Nodes)
        {
            output.Append("  ").Append(Quote(node.Name)).Append(" [label=<");
            WriteCard(output, node, p);
            output.Append(">];\n");
        }
        foreach (DiagramEdge edge in diagram.Edges)
            output.Append("  ").Append(Quote(edge.Source)).Append(" -> ").Append(Quote(edge.Target))
                .Append(" [arrowhead=").Append(edge.Kind == DiagramEdgeKind.Inheritance ? "empty" : "none")
                .Append(", label=").Append(Quote(edge.Label)).Append("];\n");
        return output.Append("}\n").ToString();
    }

    private static void WriteCard(StringBuilder output, DiagramNode node, DiagramPalette palette)
    {
        output.Append("<TABLE BORDER=\"0\" CELLBORDER=\"0\" CELLSPACING=\"0\" CELLPADDING=\"5\">");
        output.Append("<TR><TD COLSPAN=\"3\" ALIGN=\"LEFT\" BGCOLOR=\"").Append(palette.Header).Append("\"><FONT POINT-SIZE=\"13\" COLOR=\"")
            .Append(node.IsItem ? palette.Item : palette.Composite).Append("\"><B>").Append(Html(node.Name))
            .Append(node.IsAbstract ? " «abstract»" : "").Append("</B></FONT></TD></TR>");
        output.Append("<TR><TD COLSPAN=\"3\" ALIGN=\"LEFT\"><FONT POINT-SIZE=\"9\" COLOR=\"").Append(palette.Muted).Append("\">")
            .Append(node.IsItem ? "Item" : "Composite").Append("</FONT></TD></TR>");
        foreach (DiagramProperty property in node.Properties)
            output.Append("<TR><TD ALIGN=\"LEFT\">").Append(Html(property.Name)).Append("</TD><TD ALIGN=\"LEFT\"><FONT COLOR=\"")
                .Append(palette.Muted).Append("\">").Append(Html(property.TypeName)).Append("</FONT></TD><TD ALIGN=\"RIGHT\"><FONT COLOR=\"")
                .Append(palette.Muted).Append("\">").Append(Html($"[{property.Minimum}..{property.Maximum}]" + (property.Ordered ? " {ordered}" : "")))
                .Append("</FONT></TD></TR>");
        output.Append("</TABLE>");
    }

    internal static string Quote(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
    private static string Html(string value) => WebUtility.HtmlEncode(value).Replace("\r", "&#13;", StringComparison.Ordinal)
        .Replace("\n", "<BR ALIGN=\"LEFT\"/>", StringComparison.Ordinal);
}
