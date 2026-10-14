using Cogs.Model.Comparison;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cogs.Publishers.Comparison;

internal enum ReportChangeCategory { Added, Removed, Modified }
internal sealed record ReportSummaryRow(string Name, int Added, int Removed, int Modified);
internal sealed record ReportTypeGroup(string Path, ReportChangeCategory Category, ModelChange? Definition,
    IReadOnlyList<ModelChange> Properties, IReadOnlyList<ModelChange> MovedAway, IReadOnlyList<string> ReorderedProperties);

/// <summary>Publication grouping and counts, without changing the comparison's declaration-level facts.</summary>
internal sealed class ComparisonReportOrganization
{
    internal IReadOnlyList<ReportTypeGroup> Types { get; }
    internal IReadOnlyList<ReportSummaryRow> Summary { get; }
    internal IReadOnlyList<string> ReorderedIdentification { get; }

    internal ComparisonReportOrganization(ModelComparisonResult comparison)
    {
        var groups = new Dictionary<string, GroupBuilder>(StringComparer.Ordinal);
        var types = comparison.Changes.Where(c => Entity(c) is ComparisonEntityKind.ItemType or ComparisonEntityKind.CompositeType).ToArray();
        var typeMatches = types.Where(c => c.Before != null && c.After != null)
            .ToDictionary(c => c.Before.Path, c => c.After.Path, StringComparer.Ordinal);
        string CurrentOwner(string path) => typeMatches.GetValueOrDefault(path, path);
        GroupBuilder Group(string path)
        {
            if (!groups.TryGetValue(path, out var group)) groups.Add(path, group = new(path));
            return group;
        }
        foreach (var type in types)
        {
            var group = Group(type.Path);
            group.Definition = type;
            group.Category = Category(type);
        }
        var properties = comparison.Changes.Where(c => Entity(c) is ComparisonEntityKind.Property or ComparisonEntityKind.Identification).ToArray();
        foreach (var property in properties.Where(c => Entity(c) == ComparisonEntityKind.Property))
        {
            string destination = property.After != null ? Owner(property.After.Path) : CurrentOwner(Owner(property.Before.Path));
            Group(destination).Properties.Add(property);
            if (property.Kind.HasFlag(ModelChangeKind.Moved))
                Group(CurrentOwner(Owner(property.Before.Path))).MovedAway.Add(property);
        }

        // The comparer records declaration order once on the owner. Count each surviving
        // property whose relative order changes, not every index shifted by an insertion.
        var propertyMatches = properties.Where(c => c.Before != null && c.After != null)
            .ToDictionary(c => c.Before.Path, c => c.After.Path, StringComparer.Ordinal);
        var removed = properties.Where(c => c.After == null).Select(c => c.Before.Path).ToHashSet(StringComparer.Ordinal);
        string? CurrentProperty(string path) => removed.Contains(path) ? null : propertyMatches.GetValueOrDefault(path,
            path.StartsWith("Identification/", StringComparison.Ordinal) ? path : CurrentOwner(Owner(path)) + "/" + Name(path));
        var reordered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var change in comparison.Changes)
        foreach (var field in change.Fields.Where(f => f.Field is "Property order" or "Identification order"))
        {
            string[] right = Split(field.After);
            var rightPositions = right.Select((p, i) => (p, i)).ToDictionary(p => p.p, p => p.i, StringComparer.Ordinal);
            string[] left = Split(field.Before).Select(CurrentProperty).Where(p => p != null && rightPositions.ContainsKey(p)).Cast<string>().ToArray();
            for (int i = 0; i < left.Length; i++)
            for (int j = i + 1; j < left.Length; j++)
                if (rightPositions[left[i]] > rightPositions[left[j]]) { reordered.Add(left[i]); reordered.Add(left[j]); }
        }
        foreach (string path in reordered.Where(p => !p.StartsWith("Identification/", StringComparison.Ordinal)))
            Group(Owner(path)).ReorderedProperties.Add(path);
        ReorderedIdentification = Array.AsReadOnly(reordered.Where(p => p.StartsWith("Identification/", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray());
        Types = Array.AsReadOnly(groups.Values.OrderBy(g => g.Path, StringComparer.Ordinal).Select(g => new ReportTypeGroup(g.Path, g.Category, g.Definition,
            Array.AsReadOnly(g.Properties.OrderBy(Category).ThenBy(c => c.Path, StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(g.MovedAway.OrderBy(c => c.Before.Path, StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(g.ReorderedProperties.Order(StringComparer.Ordinal).ToArray()))).ToArray());
        ReportSummaryRow TypeCounts(string name) => new(name,
            Types.Count(g => g.Path.StartsWith(name + "/", StringComparison.Ordinal) && g.Category == ReportChangeCategory.Added),
            Types.Count(g => g.Path.StartsWith(name + "/", StringComparison.Ordinal) && g.Category == ReportChangeCategory.Removed),
            Types.Count(g => g.Path.StartsWith(name + "/", StringComparison.Ordinal) && g.Category == ReportChangeCategory.Modified));
        var modifiedProperties = properties.Where(c => Category(c) == ReportChangeCategory.Modified).Select(c => c.Path).ToHashSet(StringComparer.Ordinal);
        modifiedProperties.UnionWith(reordered);
        Summary = Array.AsReadOnly(new[] { TypeCounts("ItemTypes"), TypeCounts("CompositeTypes"),
            new ReportSummaryRow("Properties", properties.Count(c => Category(c) == ReportChangeCategory.Added),
                properties.Count(c => Category(c) == ReportChangeCategory.Removed), modifiedProperties.Count) });
    }

    internal static ReportChangeCategory Category(ModelChange change) => change.Before == null ? ReportChangeCategory.Added :
        change.After == null ? ReportChangeCategory.Removed : ReportChangeCategory.Modified;
    internal static ComparisonEntityKind Entity(ModelChange change) => (change.After ?? change.Before).Kind;
    internal static string Name(string path) => path[(path.LastIndexOf('/') + 1)..];
    private static string Owner(string path) => path[..path.LastIndexOf('/')];
    private static string[] Split(string? value) => string.IsNullOrEmpty(value) ? [] : value.Split('\n');
    private sealed class GroupBuilder(string path)
    {
        internal string Path { get; } = path;
        internal ReportChangeCategory Category { get; set; } = ReportChangeCategory.Modified;
        internal ModelChange? Definition { get; set; }
        internal List<ModelChange> Properties { get; } = [];
        internal List<ModelChange> MovedAway { get; } = [];
        internal List<string> ReorderedProperties { get; } = [];
    }
}
