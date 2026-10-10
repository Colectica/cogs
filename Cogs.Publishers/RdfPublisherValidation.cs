// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cogs.Publishers;

/// <summary>Publisher-level guards for directly constructed RDF-capable models.</summary>
internal static class RdfPublisherValidation
{
    internal static IReadOnlyList<CogsError> ValidatePropertyTermCollisions(
        CogsModel model,
        string diagnosticCode,
        string publisherName)
    {
        ArgumentNullException.ThrowIfNull(model);
        List<PropertyUse> uses = GetPropertyUses(model);

        var diagnostics = new List<CogsError>();
        foreach (PropertyUse use in uses)
        {
            if (CogsRdfNaming.TryToPropertyLocalName(use.Property.Name, out _))
            {
                continue;
            }

            diagnostics.Add(new CogsError(
                ErrorLevel.Error,
                diagnosticCode,
                $"{publisherName} cannot construct an RDF property term from '{use.Property.Name}'.",
                sourcePath: use.SourcePath,
                modelPath: use.ModelPath));
        }

        foreach (IGrouping<string, PropertyUse> termGroup in uses
                     .Where(use => CogsRdfNaming.TryToPropertyLocalName(use.Property.Name, out _))
                     .GroupBy(
                         use => CogsRdfNaming.ToPropertyLocalName(use.Property.Name),
                         StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            PropertyUse[] distinctNames = termGroup
                .GroupBy(use => use.Property.Name, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(use => use.Property.Name, StringComparer.Ordinal)
                .ToArray();
            if (distinctNames.Length < 2)
            {
                continue;
            }

            PropertyUse first = distinctNames[0];
            foreach (PropertyUse current in distinctNames.Skip(1))
            {
                diagnostics.Add(new CogsError(
                    ErrorLevel.Error,
                    diagnosticCode,
                    $"{publisherName} properties '{first.OwnerName}.{first.Property.Name}' and " +
                    $"'{current.OwnerName}.{current.Property.Name}' both map to RDF term '{termGroup.Key}'.",
                    sourcePath: current.SourcePath,
                    modelPath: current.ModelPath));
            }
        }

        return diagnostics;
    }

    internal static IReadOnlyList<CogsError> ValidateOrderedCompanionCollisions(
        CogsModel model, string diagnosticCode, string publisherName)
    {
        List<PropertyUse> uses = GetPropertyUses(model)
            .Where(use => CogsRdfNaming.TryToPropertyLocalName(use.Property.Name, out _)).ToList();
        var diagnostics = new List<CogsError>();
        foreach (PropertyUse ordered in uses.Where(use => use.Property.Ordered)
                     .GroupBy(use => use.Property.Name, StringComparer.Ordinal)
                     .Select(group => group.First()))
        {
            string companion = CogsRdfNaming.OrderedCompanionLocalName(ordered.Property.Name);
            PropertyUse? conflict = uses.FirstOrDefault(use =>
                CogsRdfNaming.ToPropertyLocalName(use.Property.Name) == companion);
            if (conflict is null) continue;
            diagnostics.Add(new CogsError(ErrorLevel.Error, diagnosticCode,
                $"{publisherName} ordered property '{ordered.OwnerName}.{ordered.Property.Name}' requires RDF " +
                $"companion term '{companion}', which conflicts with '{conflict.OwnerName}.{conflict.Property.Name}'.",
                sourcePath: ordered.SourcePath, modelPath: ordered.ModelPath));
        }
        return diagnostics;
    }

    private static List<PropertyUse> GetPropertyUses(CogsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var uses = model.Identification.Select(property => new PropertyUse(
            "Identification", property, model.SourceDirectory, $"Identification.{property.Name}")).ToList();
        foreach (DataType owner in model.AllDataTypes.OrderBy(type => type.Name, StringComparer.Ordinal))
            uses.AddRange(owner.Properties.Select(property => new PropertyUse(owner.Name, property, owner.Path,
                $"/{(owner is ItemType ? "ItemTypes" : "CompositeTypes")}/{owner.Name}/Properties/{property.Name}")));
        return uses;
    }

    private sealed record PropertyUse(
        string OwnerName,
        Property Property,
        string? SourcePath,
        string ModelPath);
}
