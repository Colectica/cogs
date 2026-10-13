using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Cogs.Publishers.Dot;

/// <summary>Produces two self-contained appearances without changing Graphviz geometry.</summary>
internal static class SvgAppearance
{
    internal static void WriteVariants(string lightPath, string title)
    {
        XDocument light;
        using (var reader = XmlReader.Create(lightPath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null }))
            light = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        XElement root = light.Root ?? throw new InvalidDataException("Graphviz produced an empty SVG document.");
        XNamespace svg = "http://www.w3.org/2000/svg";
        if (root.Name != svg + "svg") throw new InvalidDataException("Graphviz did not produce an SVG document.");
        root.SetAttributeValue("role", "img");
        root.SetAttributeValue("data-appearance", "light");
        root.SetAttributeValue("aria-labelledby", "diagram-title diagram-description");
        foreach (XElement text in root.Descendants(svg + "text"))
            text.SetAttributeValue("font-family", "Arial, Helvetica, sans-serif");
        root.AddFirst(new XElement(svg + "title", new XAttribute("id", "diagram-title"), title),
            new XElement(svg + "desc", new XAttribute("id", "diagram-description"),
                "Model types and their relationships. Detailed cards show properties and cardinalities."));

        var dark = new XDocument(light);
        dark.Root!.SetAttributeValue("data-appearance", "dark");
        var source = DiagramPalette.Light.Colors;
        var target = DiagramPalette.Dark.Colors;
        var colors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < source.Count; i++) colors.Add(source[i], target[i]);
        // Graphviz may shorten the controlled white canvas color to its name.
        colors.Add("white", DiagramPalette.Dark.Canvas);
        foreach (XAttribute attribute in dark.Descendants().Attributes().Where(a => a.Name.LocalName is "fill" or "stroke"))
            if (colors.TryGetValue(attribute.Value, out string? replacement)) attribute.Value = replacement;
        Write(light, lightPath);
        Write(dark, Path.Combine(Path.GetDirectoryName(lightPath)!, Path.GetFileNameWithoutExtension(lightPath) + ".dark.svg"));
    }

    private static void Write(XDocument document, string path)
    {
        using var writer = XmlWriter.Create(path, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false), NewLineChars = "\n", NewLineHandling = NewLineHandling.Entitize,
            Indent = false
        });
        document.Save(writer);
    }
}
