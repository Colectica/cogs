using Cogs.Model.Comparison;
using DiffPlex;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cogs.Publishers.Comparison;

internal enum ReportBlockKind { Heading, Paragraph, Value, Diff, Summary, Contents, Link }
internal sealed record DiffSpan(string Text, int Change);
internal sealed record ReportBlock(ReportBlockKind Kind, string Text, int Level = 0, string? Label = null,
    IReadOnlyList<DiffSpan>? Spans = null, string? Anchor = null, bool InContents = false, IReadOnlyList<ReportSummaryRow>? Rows = null);

/// <summary>Shared content and highlighting; neither renderer decides what changed.</summary>
internal static class ComparisonReportDocument
{
    internal static IReadOnlyList<ReportBlock> Build(ModelComparisonResult comparison)
    {
        var blocks = new List<ReportBlock>();
        var organization = new ComparisonReportOrganization(comparison);
        void Heading(string text, int level, string? anchor = null, bool contents = false) =>
            blocks.Add(new(ReportBlockKind.Heading, text, level, Anchor: anchor, InContents: contents));
        void Paragraph(string text) => blocks.Add(new(ReportBlockKind.Paragraph, text));
        void Value(string label, string? text) => blocks.Add(new(ReportBlockKind.Value, text ?? "(absent)", Label: label));
        Heading(comparison.After.Model.Settings.Title + " - Model changelog", 1);
        Paragraph($"Before: {comparison.Before.Model.Settings.Title} ({comparison.Before.Model.Settings.Version})");
        Paragraph($"After: {comparison.After.Model.Settings.Title} ({comparison.After.Model.Settings.Version})");
        Value("Before source", comparison.Before.Source);
        Value("After source", comparison.After.Source);
        Heading("Summary", 2);
        blocks.Add(new(ReportBlockKind.Summary, "", Rows: organization.Summary));
        if (!comparison.HasChanges) Paragraph("No definition changes.");
        if (comparison.HasChanges) blocks.Add(new(ReportBlockKind.Contents, "Table of contents"));
        foreach (var (prefix, title) in new[] { ("ItemTypes/", "Item types"), ("CompositeTypes/", "Composite types") })
        {
            var types = organization.Types.Where(t => t.Path.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            if (types.Length == 0) continue;
            Heading(title, 2, Anchor("section", prefix), true);
            foreach (var category in Enum.GetValues<ReportChangeCategory>())
            {
                var group = types.Where(t => t.Category == category).ToArray();
                if (group.Length == 0) continue;
                Heading(category.ToString(), 3, Anchor("group", prefix + category), true);
                foreach (var type in group)
                {
                    Heading(ComparisonReportOrganization.Name(type.Path), 4, Anchor("type", type.Path), true);
                    if (type.Definition != null) Change(type.Definition);
                    else Paragraph("Modified: locally declared properties changed. Model path: " + type.Path);
                    foreach (var propertyCategory in Enum.GetValues<ReportChangeCategory>())
                    {
                        var properties = type.Properties.Where(c => ComparisonReportOrganization.Category(c) == propertyCategory).ToArray();
                        bool modifications = propertyCategory == ReportChangeCategory.Modified;
                        if (properties.Length == 0 && !(modifications && (type.MovedAway.Count > 0 || type.ReorderedProperties.Count > 0))) continue;
                        Heading("Properties - " + propertyCategory, 5);
                        foreach (var property in properties)
                        {
                            Heading(ComparisonReportOrganization.Name(property.Path), 6, Anchor("change", property.Path));
                            Change(property);
                            // A confirmed move into an added owner still needs the complete new definition.
                            if (type.Category == ReportChangeCategory.Added && property.Before != null) Definition(property.After!);
                        }
                        if (modifications)
                        {
                            foreach (var moved in type.MovedAway)
                            {
                                blocks.Add(new(ReportBlockKind.Link, "Moved from " + moved.Before.Path + " to " + moved.After.Path + " (full change)",
                                    Anchor: Anchor("change", moved.Path)));
                                if (type.Category == ReportChangeCategory.Removed) Definition(moved.Before);
                            }
                            if (type.ReorderedProperties.Count > 0)
                                Paragraph("Changed relative declaration order: " + string.Join(", ", type.ReorderedProperties) + ". See the type-level before/after property order.");
                        }
                    }
                }
            }
        }
        if (comparison.Suggestions.Count > 0)
        {
            Heading("Possible renames - review required", 2, "possible-renames", true);
            Paragraph("These unique structural matches are suggestions, not confirmed changes. Their additions/removals remain in the report. Confirm only intended mappings with --renames and rerun.");
            foreach (var suggestion in comparison.Suggestions) Paragraph(suggestion.From + " -> " + suggestion.To);
        }
        var settings = comparison.Changes.Where(c => ComparisonReportOrganization.Entity(c) is ComparisonEntityKind.Setting or ComparisonEntityKind.Identification).ToArray();
        if (settings.Length > 0)
        {
            Heading("Settings", 2, "settings", true);
            foreach (var change in settings)
            {
                Heading(change.Path, 3, Anchor("change", change.Path));
                Change(change);
            }
            if (organization.ReorderedIdentification.Count > 0)
                Paragraph("Changed relative identification order: " + string.Join(", ", organization.ReorderedIdentification) + ".");
        }
        return blocks.AsReadOnly();

        void Definition(ModelDefinition definition)
        {
            foreach (var field in definition.Fields.Where(f => f.Value.Length > 0)) Value(field.Key, field.Value);
            string[] empty = definition.Fields.Where(f => f.Value.Length == 0).Select(f => f.Key).ToArray();
            if (empty.Length > 0) Value("Empty fields", string.Join(", ", empty));
        }
        void Change(ModelChange change)
        {
            Paragraph(change.Kind.ToString());
            if (change.Before != null) Paragraph("Before: " + change.Before.Path + Location(change.Before));
            if (change.After != null) Paragraph("After: " + change.After.Path + Location(change.After));
            if (change.AffectedTypes.Count > 0) Paragraph("Affected descendants/items: " + string.Join(", ", change.AffectedTypes));
            if (change.Before == null || change.After == null)
                Definition(change.Before ?? change.After!);
            else foreach (var field in change.Fields)
            {
                Value(field.Field + " - before", field.Before);
                Value(field.Field + " - after", field.After);
                if (field.Field == "Description" || (field.Field == "Value" && change.Path == "Settings/Description"))
                    blocks.Add(new(ReportBlockKind.Diff, "Text changes", Spans: TextDiff(field.Before ?? "", field.After ?? "")));
            }
        }
    }

    internal static IReadOnlyList<DiffSpan> TextDiff(string before, string after)
    {
        char[] separators = before.Contains('\n') || after.Contains('\n') ? ['\n'] : [' ', '\t', '.', ',', ';', ':', '?', '!', '(', ')'];
        var diff = new Differ().CreateWordDiffs(before, after, false, separators);
        var spans = new List<DiffSpan>();
        int position = 0;
        void Add(IReadOnlyList<string> pieces, int start, int count, int change)
        {
            if (count > 0) spans.Add(new(string.Concat(pieces.Skip(start).Take(count)), change));
        }
        foreach (var block in diff.DiffBlocks)
        {
            Add(diff.PiecesNew, position, block.InsertStartB - position, 0);
            Add(diff.PiecesOld, block.DeleteStartA, block.DeleteCountA, -1);
            Add(diff.PiecesNew, block.InsertStartB, block.InsertCountB, 1);
            position = block.InsertStartB + block.InsertCountB;
        }
        Add(diff.PiecesNew, position, diff.PiecesNew.Count - position, 0);
        return spans.AsReadOnly();
    }

    internal static string Markdown(IReadOnlyList<ReportBlock> blocks)
    {
        var output = new StringBuilder();
        foreach (var block in blocks)
        {
            switch (block.Kind)
            {
                case ReportBlockKind.Heading:
                    if (block.Anchor != null) output.Append("<a id=\"").Append(block.Anchor).Append("\"></a>\n\n");
                    output.Append(new string('#', block.Level)).Append(' ').Append(Escape(block.Text)).Append("\n\n"); break;
                case ReportBlockKind.Summary:
                    output.Append("| Definition | Added | Removed | Modified |\n| --- | ---: | ---: | ---: |\n");
                    foreach (var row in block.Rows!) output.Append($"| {row.Name} | {row.Added} | {row.Removed} | {row.Modified} |\n");
                    output.Append('\n'); break;
                case ReportBlockKind.Contents:
                    output.Append("## Table of contents\n\n");
                    foreach (var entry in blocks.Where(b => b.InContents))
                        output.Append(new string(' ', (entry.Level - 2) * 2)).Append("- [").Append(Escape(entry.Text)).Append("](#").Append(entry.Anchor).Append(")\n");
                    output.Append('\n'); break;
                case ReportBlockKind.Link: output.Append('[').Append(Escape(block.Text)).Append("](#").Append(block.Anchor).Append(")\n\n"); break;
                case ReportBlockKind.Paragraph: output.Append(Escape(block.Text)).Append("\n\n"); break;
                case ReportBlockKind.Value:
                    output.Append("**").Append(Escape(block.Label!)).Append("**\n\n");
                    Fence(block.Text.Length == 0 ? "(empty)" : block.Text); break;
                case ReportBlockKind.Diff:
                    output.Append("Text changes (`[-removed-]`, `{+added+}`):\n\n");
                    Fence(string.Concat(block.Spans!.Select(s => s.Change switch { -1 => "[-" + s.Text + "-]", 1 => "{+" + s.Text + "+}", _ => s.Text }))); break;
            }
        }
        return output.ToString();
        void Fence(string value)
        {
            string fence = new('`', Math.Max(3, System.Text.RegularExpressions.Regex.Matches(value, "`+").Select(m => m.Length).DefaultIfEmpty().Max() + 1));
            output.Append(fence).Append("text\n").Append(value).Append('\n').Append(fence).Append("\n\n");
        }
    }

    internal static string Typst(IReadOnlyList<ReportBlock> blocks)
    {
        using var resource = typeof(ComparisonReportDocument).Assembly.GetManifestResourceStream("Cogs.Publishers.Comparison.Report.typ")!;
        using var reader = new System.IO.StreamReader(resource);
        var output = new StringBuilder(reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal));
        output.Append("\n#set document(title: ").Append(Literal(blocks[0].Text)).Append(")\n");
        bool introductionOpen = false;
        foreach (var block in blocks)
        {
            if (introductionOpen && block.Kind != ReportBlockKind.Paragraph)
            {
                output.Append("\n]\n"); introductionOpen = false;
            }
            if (block.Kind == ReportBlockKind.Heading && block.Level >= 3)
            {
                output.Append("\n#block(sticky: true, above: 10pt, below: 4pt)[\n"); introductionOpen = true;
            }
            switch (block.Kind)
            {
                case ReportBlockKind.Heading:
                    output.Append("\n#heading(level: ").Append(block.Level).Append(", outlined: ").Append(block.InContents ? "true" : "false")
                        .Append(", bookmarked: ").Append(block.InContents ? "true" : "false")
                        .Append(", text(").Append(Literal(block.Text.Replace("/", "/\u200b", StringComparison.Ordinal))).Append("))");
                    if (block.Anchor != null) output.Append(" <").Append(block.Anchor).Append('>');
                    output.Append('\n'); break;
                case ReportBlockKind.Summary:
                    output.Append("\n#summary((\n");
                    foreach (var row in block.Rows!) output.Append('(').Append(Literal(row.Name)).Append($", {row.Added}, {row.Removed}, {row.Modified}),\n");
                    output.Append("))\n"); break;
                case ReportBlockKind.Contents: output.Append("\n#pagebreak(weak: true)\n#outline(title: [Table of contents], depth: 4, indent: auto)\n#pagebreak(weak: true)\n"); break;
                case ReportBlockKind.Link: output.Append("\n#link(<").Append(block.Anchor).Append(">, text(").Append(Literal(block.Text)).Append("))\n"); break;
                case ReportBlockKind.Paragraph: output.Append("\n#prose(").Append(Literal(block.Text)).Append(")\n"); break;
                case ReportBlockKind.Value: output.Append("\n#value(").Append(Literal(block.Label!)).Append(", ").Append(Literal(block.Text.Length == 0 ? "(empty)" : block.Text)).Append(")\n"); break;
                case ReportBlockKind.Diff:
                    output.Append("\n#strong(\"Text changes (removed: struck; added: underlined)\")\n\n");
                    int previous = 0;
                    foreach (var span in block.Spans!)
                    {
                        if (previous == -1 && span.Change == 1) output.Append("#h(0.25em)");
                        output.Append('#').Append(span.Change switch { -1 => "removed", 1 => "added", _ => "prose" }).Append('(').Append(Literal(span.Text)).Append(')');
                        previous = span.Change;
                    }
                    output.Append('\n'); break;
            }
        }
        if (introductionOpen) output.Append("\n]\n");
        return output.ToString();
    }

    internal static string Literal(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal) + "\"";
    private static string Escape(string value) => string.Concat(value.Select(c => "\\`*_{}[]<>#|!".Contains(c) ? "\\" + c : c.ToString()));
    // Injective UTF-8 encoding keeps anchors stable across renderers, Unicode and punctuation.
    private static string Anchor(string kind, string path) => kind + "-" + Convert.ToHexString(Encoding.UTF8.GetBytes(path)).ToLowerInvariant();
    private static string Location(ModelDefinition definition) => definition.Location == null ? "" : " (" + definition.Location + ")";
}
