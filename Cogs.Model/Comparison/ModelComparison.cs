using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cogs.Model.Comparison;

/// <summary>A validated input and its original, non-temporary provenance.</summary>
public sealed class ComparisonInput
{
    public ComparisonInput(CogsModel model, string source, IEnumerable<string> preparation = null,
        IReadOnlyDictionary<string, string> locations = null, string originalDirectory = null)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Preparation = Array.AsReadOnly((preparation ?? []).ToArray());
        Locations = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(locations ?? new Dictionary<string, string>(), StringComparer.Ordinal));
        OriginalDirectory = originalDirectory ?? model.SourceDirectory;
    }
    public CogsModel Model { get; }
    public string Source { get; }
    public string OriginalDirectory { get; }
    public IReadOnlyList<string> Preparation { get; }
    public IReadOnlyDictionary<string, string> Locations { get; }
}

[Flags]
public enum ModelChangeKind { Added = 1, Removed = 2, Modified = 4, Renamed = 8, Moved = 16, Reordered = 32 }
public enum ComparisonEntityKind { Setting, ItemType, CompositeType, Identification, Property }
public sealed record RenameMapping(string From, string To);
public sealed record RenameSuggestion(string From, string To);
public sealed record FieldChange(string Field, string Before, string After);

public sealed class ModelDefinition
{
    internal ModelDefinition(string path, ComparisonEntityKind kind, IDictionary<string, string> fields, string location)
    {
        Path = path; Kind = kind; Location = location;
        Fields = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(fields, StringComparer.Ordinal));
    }
    public string Path { get; }
    public ComparisonEntityKind Kind { get; }
    public string Location { get; }
    public IReadOnlyDictionary<string, string> Fields { get; }
}

public sealed class ModelChange
{
    internal ModelChange(ModelDefinition before, ModelDefinition after, ModelChangeKind kind,
        IEnumerable<FieldChange> fields, IEnumerable<string> affected)
    {
        Before = before; After = after; Kind = kind;
        Fields = Array.AsReadOnly(fields.ToArray());
        AffectedTypes = Array.AsReadOnly(affected.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }
    public ModelDefinition Before { get; }
    public ModelDefinition After { get; }
    public ModelChangeKind Kind { get; }
    public string Path => After?.Path ?? Before.Path;
    public IReadOnlyList<FieldChange> Fields { get; }
    public IReadOnlyList<string> AffectedTypes { get; }
}

public sealed class ModelComparisonResult
{
    internal ModelComparisonResult(ComparisonInput before, ComparisonInput after,
        IEnumerable<ModelChange> changes, IEnumerable<RenameSuggestion> suggestions)
    {
        Before = before; After = after;
        Changes = Array.AsReadOnly(changes.OrderBy(c => c.Path, StringComparer.Ordinal).ToArray());
        Suggestions = Array.AsReadOnly(suggestions.OrderBy(s => s.From, StringComparer.Ordinal).ThenBy(s => s.To, StringComparer.Ordinal).ToArray());
    }
    public ComparisonInput Before { get; }
    public ComparisonInput After { get; }
    public IReadOnlyList<ModelChange> Changes { get; }
    public IReadOnlyList<RenameSuggestion> Suggestions { get; }
    public bool HasChanges => Changes.Count != 0;
}
