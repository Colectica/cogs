using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Cogs.Model.Comparison;

/// <summary>Definition comparison, independent of file IO and report presentation.</summary>
public static class CogsModelComparer
{
    public static ModelComparisonResult Compare(ComparisonInput before, ComparisonInput after, IEnumerable<RenameMapping> renames = null)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var old = Definitions(before);
        var current = Definitions(after);
        var matches = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rename in renames ?? [])
        {
            if (rename == null || rename.From == null || rename.To == null ||
                !old.TryGetValue(rename.From, out var a) || !current.TryGetValue(rename.To, out var b) ||
                a.Kind != b.Kind || a.Kind == ComparisonEntityKind.Setting ||
                !matches.TryAdd(rename.From, rename.To) || !used.Add(rename.To))
                throw new ArgumentException("Rename mappings require existing, one-to-one endpoints of the same entity kind.", nameof(renames));
        }
        void Match(string from, string to)
        {
            if (!matches.ContainsKey(from) && current.ContainsKey(to) && used.Add(to)) matches.Add(from, to);
        }
        foreach (var entity in old.Values.Where(d => d.Kind != ComparisonEntityKind.Property)) Match(entity.Path, entity.Path);
        foreach (var entity in old.Values.Where(d => d.Kind == ComparisonEntityKind.Property))
        {
            string owner = Owner(entity.Path);
            if (matches.TryGetValue(owner, out var targetOwner)) Match(entity.Path, targetOwner + "/" + Name(entity.Path));
        }
        var typeNames = matches.Where(m => IsType(old[m.Key])).ToDictionary(m => Name(m.Key), m => Name(m.Value), StringComparer.Ordinal);
        string MapType(string value) => typeNames.GetValueOrDefault(value ?? "", value);
        var changes = new List<ModelChange>();
        foreach (var a in old.Values)
        {
            if (!matches.TryGetValue(a.Path, out var target))
            {
                changes.Add(new ModelChange(a, null, ModelChangeKind.Removed, [], Affected(before, a)));
                continue;
            }
            var b = current[target];
            ModelChangeKind kind = 0;
            if (Name(a.Path) != Name(b.Path)) kind |= ModelChangeKind.Renamed;
            if (a.Kind == ComparisonEntityKind.Property && matches.GetValueOrDefault(Owner(a.Path), Owner(a.Path)) != Owner(b.Path))
                kind |= ModelChangeKind.Moved;
            var fields = new List<FieldChange>();
            foreach (string key in a.Fields.Keys.Union(b.Fields.Keys, StringComparer.Ordinal))
            {
                string x = a.Fields.GetValueOrDefault(key), y = b.Fields.GetValueOrDefault(key);
                if (key == "Name") continue;
                if (key is "Property order" or "Identification order")
                {
                    // Compare only surviving, matched declarations: insertions are not reorderings.
                    var left = Split(x).Where(matches.ContainsKey).Select(p => matches[p]).Where(p => Split(y).Contains(p, StringComparer.Ordinal)).ToArray();
                    var right = Split(y).Where(p => left.Contains(p, StringComparer.Ordinal)).ToArray();
                    if (!left.SequenceEqual(right, StringComparer.Ordinal))
                    {
                        kind |= ModelChangeKind.Reordered;
                        fields.Add(new FieldChange(key, x, y));
                    }
                }
                else if (key == "Enumeration")
                {
                    var removed = Split(x).Except(Split(y), StringComparer.Ordinal).ToArray();
                    var added = Split(y).Except(Split(x), StringComparer.Ordinal).ToArray();
                    if (removed.Length > 0) fields.Add(new FieldChange("Enumeration removed", string.Join("\n", removed), ""));
                    if (added.Length > 0) fields.Add(new FieldChange("Enumeration added", "", string.Join("\n", added)));
                    var left = Split(x).Intersect(Split(y), StringComparer.Ordinal);
                    var right = Split(y).Intersect(Split(x), StringComparer.Ordinal);
                    if (!left.SequenceEqual(right, StringComparer.Ordinal)) fields.Add(new FieldChange("Enumeration order", x, y));
                }
                else if ((key is "Datatype" or "Parent" ? MapType(x) : x) != y)
                    fields.Add(new FieldChange(key, x, y));
            }
            if (fields.Any(f => f.Field is not ("Property order" or "Identification order"))) kind |= ModelChangeKind.Modified;
            if (kind != 0) changes.Add(new ModelChange(a, b, kind, fields, Affected(before, a).Concat(Affected(after, b))));
        }
        foreach (var b in current.Values.Where(d => !used.Contains(d.Path)))
            changes.Add(new ModelChange(null, b, ModelChangeKind.Added, [], Affected(after, b)));

        // Conservative hints, never matches: ambiguity leaves explicit additions/removals intact.
        var removedGroups = old.Values.Where(d => !matches.ContainsKey(d.Path) && d.Kind != ComparisonEntityKind.Setting)
            .GroupBy(d => Fingerprint(d, old, MapType), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var addedGroups = current.Values.Where(d => !used.Contains(d.Path) && d.Kind != ComparisonEntityKind.Setting)
            .GroupBy(d => Fingerprint(d, current, x => x), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var suggestions = removedGroups.Where(g => g.Value.Length == 1 && addedGroups.TryGetValue(g.Key, out var candidates) && candidates.Length == 1)
            .Select(g => new RenameSuggestion(g.Value[0].Path, addedGroups[g.Key][0].Path));
        return new ModelComparisonResult(before, after, changes, suggestions);
    }

    private static string Fingerprint(ModelDefinition definition, Dictionary<string, ModelDefinition> all, Func<string, string> mapType)
    {
        object Fields(ModelDefinition d) => d.Fields.Where(f =>
                f.Key is not ("Name" or "Description" or "Property order"))
            .OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => new[] { f.Key,
                f.Key is "Datatype" or "Parent" ? (f.Value == Name(definition.Path) && IsType(definition) ? "$self" : mapType(f.Value)) : f.Value }).ToArray();
        return JsonSerializer.Serialize(new { definition.Kind, Fields = Fields(definition),
            Properties = IsType(definition) ? Split(definition.Fields["Property order"]).Select(p => Fields(all[p])).ToArray() : [] });
    }

    private static Dictionary<string, ModelDefinition> Definitions(ComparisonInput input)
    {
        var result = new Dictionary<string, ModelDefinition>(StringComparer.Ordinal);
        void Add(string path, ComparisonEntityKind kind, Dictionary<string, string> fields)
        {
            foreach (var key in fields.Keys.ToArray()) fields[key] = Normalize(fields[key]);
            result.Add(path, new ModelDefinition(path, kind, fields, input.Locations.GetValueOrDefault(path)));
        }
        var model = input.Model;
        foreach (var property in typeof(Settings).GetProperties().Where(p => p.DeclaringType == typeof(Settings) && p.Name != nameof(Settings.ExtraSettings)))
            Add("Settings/" + property.Name, ComparisonEntityKind.Setting, new() { ["Value"] = property.GetValue(model.Settings)?.ToString() ?? "" });
        foreach (var setting in model.Settings.ExtraSettings)
            Add("Settings/" + setting.Key, ComparisonEntityKind.Setting, new() { ["Value"] = setting.Value });
        Add("Identification", ComparisonEntityKind.Setting, new() { ["Identification order"] = string.Join("\n", model.Identification.Select(p => "Identification/" + p.Name)) });
        foreach (var property in model.Identification)
        {
            var fields = PropertyFields(property);
            fields.Add("Declaration", property.FromMixin ? "Identification.Mixin.csv" : "Identification.csv");
            Add("Identification/" + property.Name, ComparisonEntityKind.Identification, fields);
        }
        foreach (var type in model.AllDataTypes)
        {
            bool item = model.ItemTypes.Contains(type);
            string path = (item ? "ItemTypes/" : "CompositeTypes/") + type.Name;
            var properties = type.Properties.Where(p => !item || !model.Identification.Any(id => id.Name == p.Name)).ToArray();
            Add(path, item ? ComparisonEntityKind.ItemType : ComparisonEntityKind.CompositeType, new()
            {
                ["Name"] = type.Name, ["Description"] = type.Description, ["Parent"] = type.ExtendsTypeName,
                ["Abstract"] = Flag(type.IsAbstract), ["Primitive"] = Flag(type.IsPrimitive),
                ["Deprecated"] = Flag(type.IsDeprecated), ["Deprecated namespace"] = type.DeprecatedNamespace,
                ["Property order"] = string.Join("\n", properties.Select(p => path + "/" + p.Name))
            });
            foreach (var property in properties) Add(path + "/" + property.Name, ComparisonEntityKind.Property, PropertyFields(property));
        }
        return result;
    }

    private static Dictionary<string, string> PropertyFields(Property p) => new()
    {
        ["Name"] = p.Name, ["Datatype"] = p.DataTypeName, ["Description"] = p.Description,
        ["Minimum"] = p.MinCardinality, ["Maximum"] = p.MaxCardinality,
        ["Ordered"] = Flag(p.Ordered), ["Allow subtypes"] = Flag(CogsTypeSystem.AllowsSubtypes(p)),
        ["Minimum length"] = p.MinLength?.ToString(CultureInfo.InvariantCulture), ["Maximum length"] = p.MaxLength?.ToString(CultureInfo.InvariantCulture),
        ["Enumeration"] = string.Join("\n", p.Enumeration), ["Pattern"] = p.Pattern,
        ["Minimum inclusive"] = p.MinInclusive, ["Minimum exclusive"] = p.MinExclusive,
        ["Maximum inclusive"] = p.MaxInclusive, ["Maximum exclusive"] = p.MaxExclusive
    };

    private static IEnumerable<string> Affected(ComparisonInput input, ModelDefinition definition)
    {
        if (definition.Kind == ComparisonEntityKind.Identification || definition.Path == "Identification")
            return input.Model.ItemTypes.Select(t => "ItemTypes/" + t.Name);
        if (definition.Kind == ComparisonEntityKind.Setting) return [];
        string owner = definition.Kind == ComparisonEntityKind.Property ? Owner(definition.Path) : definition.Path;
        var type = input.Model.AllDataTypes.FirstOrDefault(t => t.Name == Name(owner));
        return type == null ? [] : input.Model.AllDataTypes.Where(t => t != type && CogsTypeSystem.IsAssignableFrom(type, t))
            .Select(t => (input.Model.ItemTypes.Contains(t) ? "ItemTypes/" : "CompositeTypes/") + t.Name);
    }

    private static bool IsType(ModelDefinition d) => d.Kind is ComparisonEntityKind.ItemType or ComparisonEntityKind.CompositeType;
    private static string Owner(string path) => path[..path.LastIndexOf('/')];
    private static string Name(string path) => path[(path.LastIndexOf('/') + 1)..];
    private static string Flag(bool value) => value ? "true" : "false";
    private static string Normalize(string value) => (value ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    private static string[] Split(string value) => string.IsNullOrEmpty(value) ? [] : value.Split('\n');
}
