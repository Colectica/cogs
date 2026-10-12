// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using VDS.RDF.Parsing;

namespace Cogs.Publishers;

/// <summary>Publisher-level guards for directly constructed RDF-capable models.</summary>
internal static class RdfPublisherValidation
{
    internal static readonly IReadOnlyList<string> ShapeCategories = Array.AsReadOnly(
        new[] { "type", "target", "signature", "property", "value", "primitive", "constraint", "declaration", "identity" });
    private static readonly IReadOnlyList<string> ReservedPrefixes = Array.AsReadOnly(
        new[] { "rdf", "rdfs", "owl", "xsd", "xml", "schema", "cogsmeta", "sh", "shapes", "constraintParameters" }
            .Concat(ShapeCategories.Select(category => category + "Shapes")).ToArray());

    internal static string? ResolveModelPrefix(CogsModel model, string? requested, IList<CogsError> diagnostics)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(requested)) throw new XmlException("The prefix must not be blank.");
            XmlConvert.VerifyNCName(requested);
        }
        catch (XmlException exception)
        {
            diagnostics.Add(new CogsError(ErrorLevel.Error, "OWL1005",
                "The RDF namespace prefix must be a nonblank XML NCName: " + exception.Message,
                sourcePath: model.SourceDirectory, modelPath: "/Settings/NamespacePrefix"));
            return null;
        }

        var terms = model.AllDataTypes.Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        terms.Add("CogsDate");
        foreach (PropertyUse use in GetPropertyUses(model))
        {
            if (!CogsRdfNaming.TryToPropertyLocalName(use.Property.Name, out string term)) continue;
            terms.Add(term);
            if (use.Property.Ordered) terms.Add(CogsRdfNaming.OrderedCompanionLocalName(use.Property.Name));
        }
        var reserved = ReservedPrefixes.ToHashSet(StringComparer.Ordinal);
        string? reason = reserved.Contains(requested) ? "it is reserved by the RDF output bundle"
            : terms.Contains(requested) ? "it would shadow a model term in JSON-LD"
            : !TurtleSpecsHelper.IsPNPrefix(requested, TurtleSyntax.W3C) ? "it is not a valid W3C Turtle prefix"
            : null;
        if (reason is null) return requested;
        string replacement = "model";
        int suffix = 2;
        while (reserved.Contains(replacement) || terms.Contains(replacement)) replacement = "model" + suffix++;
        diagnostics.Add(new CogsError(ErrorLevel.Warning, "OWL2005",
            $"RDF prefix '{requested}' cannot be used because {reason}; '{replacement}' was used for the model namespace throughout the RDF bundle.",
            sourcePath: model.SourceDirectory, modelPath: "/Settings/NamespacePrefix"));
        return replacement;
    }

    internal static IReadOnlyList<CogsError> ValidateShapeNamespace(CogsModel model, string namespaceUrl)
    {
        var diagnostics = new List<CogsError>();
        // Invalid namespace configuration retains the existing publication
        // diagnostic path; this guard is only about canonical term collisions.
        if (!Uri.TryCreate(namespaceUrl, UriKind.Absolute, out _)) return diagnostics;
        string termBase = CogsRdfNaming.GetTermBase(namespaceUrl);
        string shapes = new Uri(termBase + "shapes/", UriKind.Absolute).AbsoluteUri;
        void Check(string iri, string? sourcePath, string modelPath)
        {
            if (Uri.TryCreate(iri, UriKind.Absolute, out Uri? term) && term.AbsoluteUri.StartsWith(shapes, StringComparison.Ordinal))
                diagnostics.Add(new CogsError(ErrorLevel.Error, "OWL1004",
                    $"Model term '{term.AbsoluteUri}' conflicts with the generator-owned shape namespace '{shapes}'.",
                    sourcePath: sourcePath, modelPath: modelPath));
        }
        foreach (DataType type in model.AllDataTypes.OrderBy(type => type.Name, StringComparer.Ordinal))
            if (!string.IsNullOrWhiteSpace(type.Name))
                Check(CogsRdfNaming.ClassIri(termBase, type.Name), type.Path ?? model.SourceDirectory,
                    $"/{(type is ItemType ? "ItemTypes" : "CompositeTypes")}/{type.Name}");
        foreach (PropertyUse use in GetPropertyUses(model))
        {
            if (!CogsRdfNaming.TryToPropertyLocalName(use.Property.Name, out _)) continue;
            Check(CogsRdfNaming.PropertyIri(termBase, use.Property.Name), use.SourcePath, use.ModelPath);
            if (use.Property.Ordered)
                Check(CogsRdfNaming.OrderedCompanionIri(termBase, use.Property.Name), use.SourcePath, use.ModelPath);
        }
        return diagnostics;
    }

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
