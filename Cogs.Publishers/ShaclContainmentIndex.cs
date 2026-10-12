using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cogs.Publishers;

/// <summary>
/// Publication-local containment topology. Shared type-system services decide
/// which concrete values a property permits; this index only projects edges.
/// </summary>
internal sealed class ShaclContainmentIndex
{
    internal sealed record Profile(IReadOnlyList<string> ItemTypes,
        IReadOnlyList<string> RootProperties, IReadOnlyList<string> NestedProperties,
        IReadOnlyList<string> CycleProperties);

    private sealed record Edge(string Owner, string Property, string Target);
    private readonly Edge[] roots;
    private readonly Edge[] nested;
    private readonly Dictionary<string, int> components = new(StringComparer.Ordinal);
    private readonly Dictionary<int, HashSet<int>> predecessors = new();
    private readonly Dictionary<int, Profile> profiles = new();

    internal ShaclContainmentIndex(CogsModel model)
    {
        var composites = model.ReusableDataTypes.Where(t => !t.IsAbstract)
            .Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var rootEdges = new HashSet<Edge>();
        var nestedEdges = new HashSet<Edge>();
        foreach (DataType owner in model.AllDataTypes.Where(t => !t.IsAbstract))
        {
            foreach (Property property in CogsTypeSystem.EffectiveProperties(owner))
            {
                if (CogsTypes.SimpleTypeNames.Contains(property.DataTypeName, StringComparer.Ordinal)
                    || property.DataType is ItemType || property.MaxCardinality == "0") continue;
                foreach (DataType target in CogsTypeSystem.ConcreteTypesForProperty(model, property))
                {
                    if (target.IsAbstract || !composites.Contains(target.Name)) continue;
                    (owner is ItemType ? rootEdges : nestedEdges).Add(new(owner.Name, property.Name, target.Name));
                }
            }
        }
        roots = Sort(rootEdges);
        nested = Sort(nestedEdges);
        var forward = composites.ToDictionary(n => n, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var reverse = composites.ToDictionary(n => n, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (Edge edge in nested)
        {
            forward[edge.Owner].Add(edge.Target);
            reverse[edge.Target].Add(edge.Owner);
        }

        // Iterative Kosaraju avoids recursion depth limits on large models.
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var finished = new List<string>();
        foreach (string root in composites.Order(StringComparer.Ordinal))
        {
            var stack = new Stack<(string Name, bool Finish)>();
            stack.Push((root, false));
            while (stack.TryPop(out var entry))
            {
                if (entry.Finish) { finished.Add(entry.Name); continue; }
                if (!visited.Add(entry.Name)) continue;
                stack.Push((entry.Name, true));
                foreach (string child in forward[entry.Name].OrderDescending(StringComparer.Ordinal))
                    if (!visited.Contains(child)) stack.Push((child, false));
            }
        }
        foreach (string root in finished.AsEnumerable().Reverse())
        {
            if (components.ContainsKey(root)) continue;
            int component = predecessors.Count;
            predecessors.Add(component, new());
            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.TryPop(out string? name))
            {
                if (!components.TryAdd(name, component)) continue;
                foreach (string parent in reverse[name].OrderDescending(StringComparer.Ordinal)) stack.Push(parent);
            }
        }
        foreach (Edge edge in nested)
            if (components[edge.Owner] != components[edge.Target])
                predecessors[components[edge.Target]].Add(components[edge.Owner]);
    }

    internal Profile For(DataType type)
    {
        int component = components[type.Name];
        if (profiles.TryGetValue(component, out Profile? profile)) return profile;
        var ancestors = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(component);
        while (stack.TryPop(out int current))
            if (ancestors.Add(current))
                foreach (int parent in predecessors[current]) stack.Push(parent);
        Edge[] relevantRoots = roots.Where(e => ancestors.Contains(components[e.Target])).ToArray();
        Edge[] relevantNested = nested.Where(e => ancestors.Contains(components[e.Owner])
            && ancestors.Contains(components[e.Target])).ToArray();
        Edge[] cycles = nested.Where(e => components[e.Owner] == component && components[e.Target] == component).ToArray();
        profile = new(Names(relevantRoots.Select(e => e.Owner)), Names(relevantRoots.Select(e => e.Property)),
            Names(relevantNested.Select(e => e.Property)), Names(cycles.Select(e => e.Property)));
        profiles.Add(component, profile);
        return profile;
    }

    private static string[] Names(IEnumerable<string> names) => names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    private static Edge[] Sort(IEnumerable<Edge> edges) => edges.OrderBy(e => e.Owner, StringComparer.Ordinal)
        .ThenBy(e => e.Property, StringComparer.Ordinal).ThenBy(e => e.Target, StringComparer.Ordinal).ToArray();
}
