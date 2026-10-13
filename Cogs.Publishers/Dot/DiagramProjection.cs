using Cogs.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Cogs.Publishers.Dot;

/// <summary>Publication-local model indexes. Local and broad scope rules stay separate.</summary>
internal sealed class DiagramProjection
{
    private readonly CogsModel model;
    private readonly bool inheritance;
    private readonly bool composites;
    private readonly Dictionary<string, DataType> byName;
    private readonly HashSet<string> compositeNames;
    private readonly Dictionary<string, Property[]> effective;
    private readonly Dictionary<string, List<(DataType Owner, Property Property)>> declaredIncoming = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<DataType>> children = new(StringComparer.Ordinal);

    internal DiagramProjection(CogsModel model, bool inheritance, bool composites)
    {
        this.model = model;
        this.inheritance = inheritance;
        this.composites = composites;
        byName = model.AllDataTypes.OrderBy(t => t.Name, StringComparer.Ordinal).ToDictionary(t => t.Name, StringComparer.Ordinal);
        compositeNames = model.ReusableDataTypes.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        effective = byName.Values.ToDictionary(t => t.Name, t => CogsTypeSystem.EffectiveProperties(t).ToArray(), StringComparer.Ordinal);
        foreach (DataType owner in byName.Values)
        {
            foreach (Property property in owner.Properties)
            {
                if (property.DataType is null || !byName.ContainsKey(property.DataType.Name)) continue;
                if (!declaredIncoming.TryGetValue(property.DataType.Name, out var references))
                    declaredIncoming.Add(property.DataType.Name, references = new());
                references.Add((owner, property));
            }
            if (string.IsNullOrWhiteSpace(owner.ExtendsTypeName)) continue;
            if (!children.TryGetValue(owner.ExtendsTypeName, out var descendants))
                children.Add(owner.ExtendsTypeName, descendants = new());
            descendants.Add(owner);
        }
    }

    internal IReadOnlyList<Diagram> Build(DiagramMode mode) => mode switch
    {
        DiagramMode.All => new[] { BuildBroad("output", model.Settings?.Title ?? "Model relationships",
            composites ? model.AllDataTypes : model.ItemTypes, model.SourceDirectory) },
        DiagramMode.Topic => model.TopicIndices.OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => BuildBroad(SafeFileName(t.Name), t.Name + " relationships", Closure(t.ItemTypes), model.SourceDirectory)).ToArray(),
        _ => byName.Values.Select(BuildLocal).ToArray()
    };

    private Diagram BuildLocal(DataType focus)
    {
        var nodes = new Dictionary<string, DataType>(StringComparer.Ordinal) { [focus.Name] = focus };
        var detailed = new HashSet<string>(StringComparer.Ordinal) { focus.Name };
        var edges = new HashSet<DiagramEdge>();
        AddOutgoing(focus);
        if (declaredIncoming.TryGetValue(focus.Name, out var incoming))
            foreach (var reference in incoming) AddReference(reference.Owner, reference.Property, focus);
        if (inheritance)
        {
            if (!string.IsNullOrWhiteSpace(focus.ExtendsTypeName) && byName.TryGetValue(focus.ExtendsTypeName, out var parent))
                AddInheritance(focus, parent);
            if (children.TryGetValue(focus.Name, out var descendants))
                foreach (DataType child in descendants) AddInheritance(child, focus);
        }
        return Create(SafeFileName(focus.Name), "Relationships for " + focus.Name,
            nodes.Values.Select(t => Node(t, detailed.Contains(t.Name), t.Properties)), edges, model.SourceDirectory);

        void AddOutgoing(DataType owner)
        {
            foreach (Property property in owner.Properties)
            {
                if (property.DataType is null || !byName.TryGetValue(property.DataType.Name, out var target)) continue;
                if (target is ItemType) AddReference(owner, property, target);
                else if (composites && compositeNames.Contains(target.Name))
                {
                    AddReference(owner, property, target);
                    if (detailed.Add(target.Name)) AddOutgoing(target);
                }
            }
        }
        void AddReference(DataType owner, Property property, DataType target)
        {
            nodes.TryAdd(owner.Name, owner);
            nodes.TryAdd(target.Name, target);
            edges.Add(PropertyEdge(owner.Name, target.Name, property.Name, property.MinCardinality, property.MaxCardinality, property.Ordered));
        }
        void AddInheritance(DataType child, DataType parent)
        {
            nodes.TryAdd(child.Name, child);
            nodes.TryAdd(parent.Name, parent);
            edges.Add(InheritanceEdge(child.Name, parent.Name));
        }
    }

    private IEnumerable<DataType> Closure(IEnumerable<DataType> roots)
    {
        var result = new Dictionary<string, DataType>(StringComparer.Ordinal);
        var stack = new Stack<DataType>(roots.Where(t => t is not null).Reverse());
        while (stack.Count > 0)
        {
            DataType current = stack.Pop();
            if (!result.TryAdd(current.Name, current)) continue;
            if (inheritance)
            {
                foreach (DataType parent in current.ParentTypes) stack.Push(parent);
                foreach (DataType child in current.ChildTypes) stack.Push(child);
            }
            foreach (Property property in effective[current.Name])
            {
                if (property.DataType is ItemType || composites && IsComposite(property.DataType)) stack.Push(property.DataType);
                else if (IsComposite(property.DataType)) AddNestedTargets(property.DataType, new(StringComparer.Ordinal));
            }
        }
        return result.Values.OrderBy(t => t.Name, StringComparer.Ordinal);

        void AddNestedTargets(DataType composite, HashSet<string> recursionStack)
        {
            if (!recursionStack.Add(composite.Name)) return;
            foreach (Property property in effective[composite.Name])
            {
                if (property.DataType is ItemType) stack.Push(property.DataType);
                else if (IsComposite(property.DataType)) AddNestedTargets(property.DataType, recursionStack);
            }
            recursionStack.Remove(composite.Name);
        }
    }

    private Diagram BuildBroad(string name, string title, IEnumerable<DataType> selected, string? sourcePath)
    {
        DataType[] types = selected.DistinctBy(t => t.Name).OrderBy(t => t.Name, StringComparer.Ordinal).ToArray();
        var included = types.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var edges = new HashSet<DiagramEdge>();
        foreach (DataType owner in types)
        {
            if (inheritance && !string.IsNullOrWhiteSpace(owner.ExtendsTypeName) && included.Contains(owner.ExtendsTypeName))
                edges.Add(InheritanceEdge(owner.Name, owner.ExtendsTypeName));
            foreach (Property property in effective[owner.Name])
                AddPropertyEdges(owner.Name, property, property.Name, property.MinCardinality, property.MaxCardinality,
                    property.Ordered, new(StringComparer.Ordinal));
        }
        return Create(name, title, types.Select(t => Node(t, true, effective[t.Name])), edges, sourcePath);

        void AddPropertyEdges(string owner, Property property, string path, string minimum, string maximum,
            bool ordered, HashSet<string> recursionStack)
        {
            DataType? target = property.DataType;
            if (target is null || target.IsXmlPrimitive) return;
            if (target is ItemType || composites)
            {
                if (included.Contains(target.Name)) edges.Add(PropertyEdge(owner, target.Name, path, minimum, maximum, ordered));
                return;
            }
            if (!IsComposite(target) || !recursionStack.Add(target.Name)) return;
            foreach (Property nested in effective[target.Name])
                AddPropertyEdges(owner, nested, path + "." + nested.Name, MultiplyMinimum(minimum, nested.MinCardinality),
                    MultiplyMaximum(maximum, nested.MaxCardinality), ordered || nested.Ordered, recursionStack);
            recursionStack.Remove(target.Name);
        }
    }

    private bool IsComposite(DataType? type) => type is not null && compositeNames.Contains(type.Name);
    private static DiagramNode Node(DataType type, bool detailed, IEnumerable<Property> properties) =>
        new(type.Name, type is ItemType, type.IsAbstract, detailed, Array.AsReadOnly(detailed
            ? properties.Select(p => new DiagramProperty(p.Name, p.DataTypeName, p.MinCardinality, p.MaxCardinality, p.Ordered)).ToArray()
            : Array.Empty<DiagramProperty>()));
    private static DiagramEdge PropertyEdge(string source, string target, string path, string minimum, string maximum, bool ordered) =>
        new(source, target, DiagramEdgeKind.Property, path, minimum, maximum, ordered);
    private static DiagramEdge InheritanceEdge(string source, string target) => new(source, target, DiagramEdgeKind.Inheritance, "", "", "", false);
    private static Diagram Create(string name, string title, IEnumerable<DiagramNode> nodes, IEnumerable<DiagramEdge> edges, string? sourcePath) =>
        new(name, title, Array.AsReadOnly(nodes.OrderBy(n => n.Name, StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(edges.OrderBy(e => e.Source, StringComparer.Ordinal).ThenBy(e => e.Target, StringComparer.Ordinal)
                .ThenBy(e => e.Kind).ThenBy(e => e.PropertyPath, StringComparer.Ordinal).ThenBy(e => e.Minimum, StringComparer.Ordinal)
                .ThenBy(e => e.Maximum, StringComparer.Ordinal).ThenBy(e => e.Ordered).ToArray()), sourcePath);
    private static string MultiplyMinimum(string a, string b) =>
        BigInteger.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out var x) &&
        BigInteger.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out var y) ? (x * y).ToString(CultureInfo.InvariantCulture) : "0";
    private static string MultiplyMaximum(string a, string b) => a == "n" || b == "n" ? "n" :
        BigInteger.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out var x) &&
        BigInteger.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out var y) ? (x * y).ToString(CultureInfo.InvariantCulture) : "n";
    private static string SafeFileName(string value) => string.Concat(value.Where(c => !Path.GetInvalidFileNameChars().Contains(c)))
        .Replace(" ", string.Empty, StringComparison.Ordinal);
}
