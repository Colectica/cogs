// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Cogs.Publishers;

/// <summary>Publishes deterministic Graphviz graphs without mutating the COGS model.</summary>
public sealed class DotSchemaPublisher
{
    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "dot", "svg", "png", "jpeg", "jpg", "pdf"
    };

    public string? TargetDirectory { get; set; }
    public string? DotLocation { get; set; }
    public bool Overwrite { get; set; }
    public string Format { get; set; } = "svg";
    public string Output { get; set; } = "single";
    public bool Inheritance { get; set; }
    public bool ShowReusables { get; set; }
    public List<CogsError> Errors { get; } = new();

    public int Publish(CogsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        string target = TargetDirectory ?? throw new InvalidOperationException("Target directory must be specified.");
        int result = 0;
        Errors.Clear();
        DirectoryPublication.Publish(target, Overwrite, stagingDirectory =>
        {
            string? original = TargetDirectory;
            TargetDirectory = stagingDirectory;
            try
            {
                result = PublishCore(model);
                if (result != 0 || Errors.Any(x => x.Level == ErrorLevel.Error))
                {
                    throw new CogsPublicationException("DOT generation failed.");
                }
            }
            finally
            {
                TargetDirectory = original;
            }
        }, model.SourceDirectory);
        return result;
    }

    internal int PublishIntoExistingDirectory(CogsModel model)
    {
        Errors.Clear();
        return PublishCore(model);
    }

    private int PublishCore(CogsModel model)
    {
        string target = TargetDirectory ?? throw new InvalidOperationException("Target directory must be specified.");
        Directory.CreateDirectory(target);

        string format = (Format ?? string.Empty).Trim().ToLowerInvariant();
        if (!SupportedFormats.Contains(format))
        {
            Errors.Add(new CogsError(ErrorLevel.Error, "PROJ2701",
                $"Unsupported DOT output format '{Format}'. Use dot, svg, png, jpeg, jpg, or pdf."));
            return -1;
        }
        string outputMode = (Output ?? string.Empty).Trim().ToLowerInvariant();
        if (outputMode is not ("all" or "topic" or "single"))
        {
            Errors.Add(new CogsError(ErrorLevel.Error, "PROJ2702",
                $"Unsupported DOT output mode '{Output}'. Use all, topic, or single."));
            return -1;
        }

        var artifacts = outputMode switch
        {
            "all" => new[] { ("output", BuildGraph(model,
                ShowReusables ? model.AllDataTypes : model.ItemTypes)) },
            "topic" => model.TopicIndices
                .OrderBy(topic => topic.Name, StringComparer.Ordinal)
                .Select(topic => (SafeFileName(topic.Name), BuildGraph(model, Closure(model, topic.ItemTypes))))
                .ToArray(),
            _ => BuildLocalGraphs(model)
        };

        foreach ((string name, string graph) in artifacts)
        {
            if (!WriteArtifact(name, graph, format)) return -1;
        }
        return 0;
    }

    private readonly record struct DeclaredReference(DataType Owner, Property Property);

    private (string Name, string Graph)[] BuildLocalGraphs(CogsModel model)
    {
        DataType[] types = model.AllDataTypes.OrderBy(type => type.Name, StringComparer.Ordinal).ToArray();
        var byName = types.ToDictionary(type => type.Name, StringComparer.Ordinal);
        var composites = model.ReusableDataTypes.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        var incoming = new Dictionary<string, List<DeclaredReference>>(StringComparer.Ordinal);
        var children = new Dictionary<string, List<DataType>>(StringComparer.Ordinal);
        foreach (DataType owner in types)
        {
            foreach (Property property in owner.Properties)
            {
                if (property.DataType is null || !byName.ContainsKey(property.DataType.Name)) continue;
                if (!incoming.TryGetValue(property.DataType.Name, out var references))
                    incoming.Add(property.DataType.Name, references = new List<DeclaredReference>());
                references.Add(new DeclaredReference(owner, property));
            }
            if (!string.IsNullOrWhiteSpace(owner.ExtendsTypeName))
            {
                if (!children.TryGetValue(owner.ExtendsTypeName, out var descendants))
                    children.Add(owner.ExtendsTypeName, descendants = new List<DataType>());
                descendants.Add(owner);
            }
        }
        return types.Select(type => (SafeFileName(type.Name), BuildLocalGraph(type, byName, composites, incoming, children))).ToArray();
    }

    private string BuildLocalGraph(
        DataType focus,
        IReadOnlyDictionary<string, DataType> byName,
        ISet<string> composites,
        IReadOnlyDictionary<string, List<DeclaredReference>> incoming,
        IReadOnlyDictionary<string, List<DataType>> children)
    {
        var nodes = new Dictionary<string, DataType>(StringComparer.Ordinal) { [focus.Name] = focus };
        var detailed = new HashSet<string>(StringComparer.Ordinal) { focus.Name };
        var edges = new HashSet<string>(StringComparer.Ordinal);
        AddOutgoing(focus);
        if (incoming.TryGetValue(focus.Name, out var references))
        {
            foreach (DeclaredReference reference in references) AddReference(reference.Owner, reference.Property, focus);
        }
        if (Inheritance)
        {
            if (!string.IsNullOrWhiteSpace(focus.ExtendsTypeName) && byName.TryGetValue(focus.ExtendsTypeName, out DataType? parent))
                AddInheritance(focus, parent);
            if (children.TryGetValue(focus.Name, out var descendants))
                foreach (DataType child in descendants) AddInheritance(child, focus);
        }

        var builder = new StringBuilder();
        builder.AppendLine("digraph COGS {");
        builder.AppendLine("  graph [rankdir=LR, compound=true, fontsize=9];");
        builder.AppendLine("  node [style=filled, fontsize=9];");
        builder.AppendLine("  edge [fontsize=8];");
        foreach (DataType type in nodes.Values.OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            string fill = type is ItemType ? "#f7b733" : "#fc4a1a";
            string name = type.Name + (type.IsAbstract ? " «abstract»" : string.Empty);
            bool showProperties = detailed.Contains(type.Name);
            string label = Escape(name);
            if (showProperties)
            {
                var properties = type.Properties.Select(property =>
                    $"{property.Name} : {property.DataTypeName} [{property.MinCardinality}..{property.MaxCardinality}]" +
                    (property.Ordered ? " {ordered}" : string.Empty)).ToArray();
                label = "{" + EscapeRecord(name) + "|" + string.Join("\\l", properties.Select(EscapeRecord)) +
                    (properties.Length > 0 ? "\\l" : string.Empty) + "}";
            }
            builder.Append("  ").Append(QuoteId(type.Name)).Append(" [shape=")
                .Append(showProperties ? "record" : "ellipse").Append(", fillcolor=\"").Append(fill)
                .Append("\", label=\"").Append(label).AppendLine("\"];");
        }
        foreach (string edge in edges.OrderBy(edge => edge, StringComparer.Ordinal)) builder.AppendLine(edge);
        builder.AppendLine("}");
        return builder.ToString();

        void AddOutgoing(DataType owner)
        {
            foreach (Property property in owner.Properties)
            {
                if (property.DataType is null || !byName.TryGetValue(property.DataType.Name, out DataType? target)) continue;
                if (target is ItemType) AddReference(owner, property, target);
                else if (ShowReusables && composites.Contains(target.Name))
                {
                    AddReference(owner, property, target);
                    // A type is expanded only once, including in recursive composite structures.
                    if (detailed.Add(target.Name)) AddOutgoing(target);
                }
            }
        }

        void AddReference(DataType owner, Property property, DataType target)
        {
            nodes.TryAdd(owner.Name, owner);
            nodes.TryAdd(target.Name, target);
            string label = $"{property.Name} [{property.MinCardinality}..{property.MaxCardinality}]" +
                (property.Ordered ? " {ordered}" : string.Empty);
            edges.Add($"  {QuoteId(owner.Name)} -> {QuoteId(target.Name)} [arrowhead=none, label=\"{Escape(label)}\"];");
        }

        void AddInheritance(DataType child, DataType parent)
        {
            nodes.TryAdd(child.Name, child);
            nodes.TryAdd(parent.Name, parent);
            edges.Add($"  {QuoteId(child.Name)} -> {QuoteId(parent.Name)} [arrowhead=empty, label=\"extends\"];");
        }
    }

    private IEnumerable<DataType> Closure(CogsModel model, IEnumerable<DataType> roots)
    {
        var result = new Dictionary<string, DataType>(StringComparer.Ordinal);
        var stack = new Stack<DataType>(roots.Where(type => type is not null).Reverse());
        while (stack.Count > 0)
        {
            DataType current = stack.Pop();
            if (!result.TryAdd(current.Name, current)) continue;
            if (Inheritance)
            {
                foreach (DataType parent in current.ParentTypes) stack.Push(parent);
                foreach (DataType child in current.ChildTypes) stack.Push(child);
            }
            foreach (Property property in CogsTypeSystem.EffectiveProperties(current))
            {
                if (property.DataType is ItemType || ShowReusables && model.ReusableDataTypes.Contains(property.DataType))
                {
                    stack.Push(property.DataType);
                }
                else if (model.ReusableDataTypes.Contains(property.DataType))
                {
                    AddNestedTargets(property.DataType, stack, new HashSet<string>(StringComparer.Ordinal));
                }
            }
        }
        return result.Values.OrderBy(type => type.Name, StringComparer.Ordinal);

        void AddNestedTargets(DataType composite, Stack<DataType> targets, HashSet<string> recursionStack)
        {
            if (!recursionStack.Add(composite.Name)) return;
            foreach (Property nested in CogsTypeSystem.EffectiveProperties(composite))
            {
                if (nested.DataType is ItemType) targets.Push(nested.DataType);
                else if (model.ReusableDataTypes.Contains(nested.DataType))
                {
                    AddNestedTargets(nested.DataType, targets, recursionStack);
                }
            }
            recursionStack.Remove(composite.Name);
        }
    }

    private string BuildGraph(CogsModel model, IEnumerable<DataType> selected)
    {
        DataType[] types = selected.DistinctBy(type => type.Name)
            .OrderBy(type => type.Name, StringComparer.Ordinal).ToArray();
        var included = types.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        var builder = new StringBuilder();
        builder.AppendLine("digraph COGS {");
        builder.AppendLine("  graph [rankdir=LR, compound=true, fontsize=9];");
        builder.AppendLine("  node [shape=record, style=filled, fontsize=9];");
        builder.AppendLine("  edge [fontsize=8];");

        foreach (DataType type in types)
        {
            string fill = type is ItemType ? "#f7b733" : "#fc4a1a";
            string abstractLabel = type.IsAbstract ? " «abstract»" : string.Empty;
            var properties = CogsTypeSystem.EffectiveProperties(type)
                .Select(property => $"{property.Name} : {property.DataTypeName} [{property.MinCardinality}..{property.MaxCardinality}]" +
                    (property.Ordered ? " {ordered}" : string.Empty));
            string label = "{" + type.Name + abstractLabel + "|" +
                string.Join("\\l", properties.Select(EscapeRecord)) +
                (properties.Any() ? "\\l" : string.Empty) + "}";
            builder.Append("  ").Append(QuoteId(type.Name))
                .Append(" [fillcolor=\"").Append(fill).Append("\", label=\"")
                .Append(label).AppendLine("\"];");
        }

        var edges = new HashSet<string>(StringComparer.Ordinal);
        foreach (DataType owner in types)
        {
            if (Inheritance && !string.IsNullOrWhiteSpace(owner.ExtendsTypeName) && included.Contains(owner.ExtendsTypeName))
            {
                edges.Add($"  {QuoteId(owner.Name)} -> {QuoteId(owner.ExtendsTypeName)} [arrowhead=empty, label=\"extends\"];");
            }
            foreach (Property property in CogsTypeSystem.EffectiveProperties(owner))
            {
                AddPropertyEdges(owner, property, property.Name, property.MinCardinality, property.MaxCardinality,
                    property.Ordered, new HashSet<string>(StringComparer.Ordinal));
            }
        }
        foreach (string edge in edges.OrderBy(edge => edge, StringComparer.Ordinal)) builder.AppendLine(edge);
        builder.AppendLine("}");
        return builder.ToString();

        void AddPropertyEdges(
            DataType owner,
            Property property,
            string path,
            string minimum,
            string maximum,
            bool ordered,
            HashSet<string> recursionStack)
        {
            DataType? target = property.DataType;
            if (target is null || target.IsXmlPrimitive) return;
            if (target is ItemType || ShowReusables)
            {
                if (!included.Contains(target.Name)) return;
                string label = $"{path} [{minimum}..{maximum}]" + (ordered ? " {ordered}" : string.Empty);
                edges.Add($"  {QuoteId(owner.Name)} -> {QuoteId(target.Name)} [arrowhead=none, label=\"{Escape(label)}\"];");
                return;
            }
            if (!model.ReusableDataTypes.Contains(target) || !recursionStack.Add(target.Name)) return;
            foreach (Property nested in CogsTypeSystem.EffectiveProperties(target))
            {
                AddPropertyEdges(owner, nested, path + "." + nested.Name,
                    MultiplyMinimum(minimum, nested.MinCardinality),
                    MultiplyMaximum(maximum, nested.MaxCardinality),
                    ordered || nested.Ordered, recursionStack);
            }
            recursionStack.Remove(target.Name);
        }
    }

    private bool WriteArtifact(string baseName, string graph, string format)
    {
        string target = TargetDirectory!;
        string extension = format == "jpg" ? "jpeg" : format;
        string outputPath = Path.Combine(target, baseName + "." + extension);
        if (format == "dot")
        {
            File.WriteAllText(outputPath, graph, new UTF8Encoding(false));
            return true;
        }

        string? executable = DiscoverDot();
        if (executable is null)
        {
            Errors.Add(new CogsError(ErrorLevel.Error, "PROJ2703",
                "Graphviz dot was not found. Set --dot, COGS_DOT, or add dot to PATH."));
            return false;
        }

        string inputPath = Path.Combine(target, "." + baseName + ".dot-input");
        File.WriteAllText(inputPath, graph, new UTF8Encoding(false));
        try
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            ConfigureGraphvizEnvironment(startInfo, format);
            startInfo.ArgumentList.Add("-T" + (format == "jpg" ? "jpeg" : format));
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputPath);
            startInfo.ArgumentList.Add(inputPath);
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Graphviz dot did not start.");
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "PROJ2704",
                    $"Graphviz dot failed with exit code {process.ExitCode}: {stderr.Trim()} {stdout.Trim()}".Trim()));
                return false;
            }
            if (format == "svg") AddSvgShadow(outputPath);
            if (format == "pdf") NormalizePdfMetadata(outputPath);
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            Errors.Add(new CogsError(ErrorLevel.Error, "PROJ2705",
                $"Graphviz dot could not be executed: {exception.Message}", exception: exception));
            return false;
        }
        finally
        {
            if (File.Exists(inputPath)) File.Delete(inputPath);
        }
    }

    internal static void NormalizePdfMetadata(string path)
    {
        // Older Cairo-backed Graphviz builds may ignore SOURCE_DATE_EPOCH and
        // write an uncompressed wall-clock time into PDF metadata. Replace
        // only the fixed-width timestamp digits so PDF byte offsets remain
        // valid and identical graphs remain reproducible.
        byte[] bytes = File.ReadAllBytes(path);
        string content = Encoding.Latin1.GetString(bytes);
        string normalized = Regex.Replace(content,
            @"(?<=/CreationDate \(D:)\d{14}", "19700101000000",
            RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized,
            @"(?<=/ModDate \(D:)\d{14}", "19700101000000",
            RegexOptions.CultureInvariant);
        if (!string.Equals(content, normalized, StringComparison.Ordinal))
        {
            File.WriteAllBytes(path, Encoding.Latin1.GetBytes(normalized));
        }
    }

    internal static void ConfigureGraphvizEnvironment(ProcessStartInfo startInfo, string format)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            // Graphviz passes SOURCE_DATE_EPOCH to Cairo as the PDF creation
            // date, including when the metadata dictionary is compressed.
            startInfo.Environment["SOURCE_DATE_EPOCH"] = "0";
        }
    }

    private string? DiscoverDot()
    {
        string? configured = string.IsNullOrWhiteSpace(DotLocation)
            ? Environment.GetEnvironmentVariable("COGS_DOT")
            : DotLocation;
        if (!string.IsNullOrWhiteSpace(configured)) return configured;

        string executableName = OperatingSystem.IsWindows() ? "dot.exe" : "dot";
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static void AddSvgShadow(string path)
    {
        XDocument document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        XElement root = document.Root ?? throw new InvalidDataException("Graphviz produced an empty SVG document.");
        XNamespace svg = root.Name.Namespace;
        var filter = new XElement(svg + "filter",
            new XAttribute("id", "cogs-dropshadow"),
            new XAttribute("height", "130%"),
            new XElement(svg + "feGaussianBlur", new XAttribute("in", "SourceAlpha"), new XAttribute("stdDeviation", "3")),
            new XElement(svg + "feOffset", new XAttribute("dx", "2"), new XAttribute("dy", "2"), new XAttribute("result", "offsetblur")),
            new XElement(svg + "feMerge",
                new XElement(svg + "feMergeNode"),
                new XElement(svg + "feMergeNode", new XAttribute("in", "SourceGraphic"))));
        XElement? definitions = root.Element(svg + "defs");
        if (definitions is null)
        {
            definitions = new XElement(svg + "defs");
            root.AddFirst(definitions);
        }
        definitions.Add(filter);
        foreach (XElement shape in root.Descendants().Where(element => element.Name == svg + "polygon" || element.Name == svg + "ellipse"))
        {
            string? fill = (string?)shape.Attribute("fill");
            if (fill is null || fill is "none" or "black") continue;
            string style = (string?)shape.Attribute("style") ?? string.Empty;
            if (style.Length > 0 && !style.EndsWith(";", StringComparison.Ordinal)) style += ";";
            shape.SetAttributeValue("style", style + "filter:url(#cogs-dropshadow)");
        }
        document.Save(path, SaveOptions.DisableFormatting);
    }

    private static string MultiplyMinimum(string left, string right) =>
        BigInteger.TryParse(left, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out BigInteger a) &&
        BigInteger.TryParse(right, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out BigInteger b)
            ? (a * b).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "0";

    private static string MultiplyMaximum(string left, string right) =>
        left == "n" || right == "n" ? "n" :
        BigInteger.TryParse(left, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out BigInteger a) &&
        BigInteger.TryParse(right, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out BigInteger b)
            ? (a * b).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "n";

    private static string QuoteId(string value) => "\"" + Escape(value) + "\"";
    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    private static string EscapeRecord(string value) => Escape(value).Replace("{", "\\{", StringComparison.Ordinal).Replace("}", "\\}", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);
    private static string SafeFileName(string value) => string.Concat(value.Where(character => !Path.GetInvalidFileNameChars().Contains(character))).Replace(" ", string.Empty, StringComparison.Ordinal);
}
