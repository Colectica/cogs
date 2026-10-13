using System.Collections.Generic;

namespace Cogs.Publishers.Dot;

/// <summary>Controlled semantic colors, shared by DOT and SVG conversion.</summary>
internal sealed record DiagramPalette(string Canvas, string Surface, string Header, string Border,
    string Text, string Muted, string Edge, string Item, string Composite)
{
    internal static DiagramPalette Light { get; } = new("#ffffff", "#f8fafc", "#f1f5f9", "#94a3b8",
        "#1e293b", "#475569", "#64748b", "#346790", "#2b7065");
    internal static DiagramPalette Dark { get; } = new("#111827", "#1f2937", "#273449", "#52627a",
        "#e2e8f0", "#cbd5e1", "#94a3b8", "#93c5fd", "#82cdc2");
    internal IReadOnlyList<string> Colors => [Canvas, Surface, Header, Border, Text, Muted, Edge, Item, Composite];
}
