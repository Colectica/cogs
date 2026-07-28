// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cogs.Publishers;

/// <summary>Publisher-level guards for directly constructed RDF projections.</summary>
internal static class RdfPublisherValidation
{
    internal static IReadOnlyList<CogsError> ValidatePropertyTermCollisions(
        CogsModel model,
        string diagnosticCode,
        string publisherName)
    {
        ArgumentNullException.ThrowIfNull(model);

        var uses = new List<PropertyUse>();
        foreach (Property property in model.Identification)
        {
            uses.Add(new PropertyUse(
                "Identification",
                property,
                model.SourceDirectory,
                $"Identification.{property.Name}"));
        }

        foreach (DataType owner in model.AllDataTypes.OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            foreach (Property property in owner.Properties)
            {
                uses.Add(new PropertyUse(
                    owner.Name,
                    property,
                    owner.Path,
                    $"/{(owner is ItemType ? "ItemTypes" : "CompositeTypes")}/{owner.Name}/Properties/{property.Name}"));
            }
        }

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

    private sealed record PropertyUse(
        string OwnerName,
        Property Property,
        string? SourcePath,
        string ModelPath);
}
