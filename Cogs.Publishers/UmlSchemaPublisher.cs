// Copyright (c) 2024 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Cogs.Publishers;

/// <summary>Publishes UML 2.4.2 or Enterprise Architect compatible XMI 2.5.1.</summary>
public sealed class UmlSchemaPublisher
{
    public string? TargetDirectory { get; set; }
    public string? TargetFilename { get; set; }
    public bool Overwrite { get; set; }

    /// <summary>True for normative UML/XMI 2.4.2; false for EA XMI 2.5.1.</summary>
    public bool Normative { get; set; }

    // Retained for CLI and library compatibility. UML generation is deterministic and does not require Graphviz.
    public string? DotLocation { get; set; }

    /// <summary>Stable projection warnings produced by the last publication.</summary>
    public List<CogsError> Errors { get; } = new();

    public void Publish(CogsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Errors.Clear();
        string target = TargetDirectory ?? throw new InvalidOperationException("Target directory must be specified.");
        DirectoryPublication.Publish(target, Overwrite, staging => PublishCore(model, staging), model.SourceDirectory);
    }

    private void PublishCore(CogsModel model, string targetDirectory)
    {
        XNamespace xmi = Normative
            ? "http://www.omg.org/spec/XMI/20110701"
            : "http://www.omg.org/spec/XMI/20131001";
        XNamespace uml = Normative
            ? "http://www.omg.org/spec/UML/20110701"
            : "http://www.omg.org/spec/UML/20131001";

        string modelId = Id("model", model.Settings.Slug);
        string packageId = Id("package", model.Settings.Slug);
        var modelElement = new XElement(uml + "Model",
            new XAttribute(xmi + "id", modelId),
            new XAttribute("name", model.Settings.Slug));
        AddComment(modelElement, xmi, modelId, model.Settings.Description);

        var package = new XElement("packagedElement",
            new XAttribute(xmi + "type", "uml:Package"),
            new XAttribute(xmi + "id", packageId),
            new XAttribute("name", model.Settings.Title ?? model.Settings.Slug));
        modelElement.Add(package);

        foreach (string primitive in CogsTypes.SimpleTypeNames
                     .Where(name => !string.Equals(name, "dcTerms", StringComparison.Ordinal))
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            package.Add(new XElement("packagedElement",
                new XAttribute(xmi + "type", "uml:PrimitiveType"),
                new XAttribute(xmi + "id", PrimitiveId(primitive)),
                new XAttribute("name", primitive)));
        }

        var associations = new List<XElement>();
        foreach (DataType type in model.AllDataTypes.OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            package.Add(CreateType(model, type, xmi, associations));
        }
        foreach (XElement association in associations)
        {
            package.Add(association);
        }

        var root = new XElement(xmi + "XMI",
            new XAttribute(XNamespace.Xmlns + "xmi", xmi.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "uml", uml.NamespaceName),
            new XAttribute(xmi + "version", Normative ? "2.4.2" : "2.5.1"));
        if (!Normative)
        {
            root.Add(new XElement(xmi + "Documentation",
                new XAttribute("exporter", "COGS"),
                new XAttribute("exporterVersion", "2.0.0")));
        }
        root.Add(modelElement);
        if (!Normative)
        {
            root.Add(CreateEaExtension(model, xmi, modelId, packageId));
        }

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null));
        if (!string.IsNullOrWhiteSpace(model.HeaderInclude)) document.Add(new XComment(model.HeaderInclude));
        document.Add(root);

        string filename = string.IsNullOrWhiteSpace(TargetFilename) ? model.Settings.Slug + ".xmi" : TargetFilename;
        if (!string.Equals(filename, Path.GetFileName(filename), StringComparison.Ordinal) ||
            filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new CogsPublicationException("PROJ2602: TargetFilename must be a plain, valid file name.");
        }

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            NewLineChars = "\n",
            OmitXmlDeclaration = false
        };
        using XmlWriter writer = XmlWriter.Create(Path.Combine(targetDirectory, filename), settings);
        document.Save(writer);
    }

    private XElement CreateType(CogsModel model, DataType type, XNamespace xmi, List<XElement> associations)
    {
        string typeId = TypeId(type);
        var result = new XElement("packagedElement",
            new XAttribute(xmi + "type", "uml:Class"),
            new XAttribute(xmi + "id", typeId),
            new XAttribute("name", type.Name),
            new XAttribute("isAbstract", type.IsAbstract ? "true" : "false"));
        AddComment(result, xmi, typeId, type.Description);
        if (type.IsPrimitive)
        {
            AddComment(result, xmi, typeId, "COGS:primitive=true", "primitive");
        }

        if (!string.IsNullOrWhiteSpace(type.ExtendsTypeName))
        {
            DataType? parent = type.ParentTypes.LastOrDefault(candidate =>
                string.Equals(candidate.Name, type.ExtendsTypeName, StringComparison.Ordinal));
            result.Add(new XElement("generalization",
                new XAttribute(xmi + "type", "uml:Generalization"),
                new XAttribute(xmi + "id", Id("generalization", type.Name, type.ExtendsTypeName)),
                new XAttribute("general", parent is null ? Id("type", type.ExtendsTypeName) : TypeId(parent))));
        }

        foreach (Property property in type.Properties)
        {
            CreateProperty(model, type, property, result, xmi, associations);
        }
        return result;
    }

    private void CreateProperty(
        CogsModel model,
        DataType owner,
        Property property,
        XElement ownerElement,
        XNamespace xmi,
        List<XElement> associations)
    {
        string propertyId = Id("property", owner.Name, property.Name);
        bool repeated = !string.Equals(property.MaxCardinality, "1", StringComparison.Ordinal);
        bool primitive = property.DataType is null || property.DataType.IsXmlPrimitive;
        string targetId = primitive ? PrimitiveId(property.DataTypeName) : TypeId(property.DataType!);
        string? associationId = primitive ? null : Id("association", owner.Name, property.Name, property.DataTypeName);

        var attribute = new XElement("ownedAttribute",
            new XAttribute(xmi + "type", "uml:Property"),
            new XAttribute(xmi + "id", propertyId),
            new XAttribute("name", property.Name),
            new XAttribute("type", targetId),
            new XAttribute("visibility", "public"),
            new XAttribute("isOrdered", property.Ordered ? "true" : "false"),
            new XAttribute("isUnique", repeated ? "false" : "true"));
        if (associationId is not null) attribute.Add(new XAttribute("association", associationId));
        attribute.Add(LowerValue(xmi, propertyId, property.MinCardinality));
        attribute.Add(UpperValue(xmi, propertyId, property.MaxCardinality));
        AddComment(attribute, xmi, propertyId, property.Description);

        int identityPosition = owner is ItemType
            ? model.Identification
                .Select((id, index) => (id, index))
                .Where(entry => string.Equals(entry.id.Name, property.Name, StringComparison.Ordinal))
                .Select(entry => entry.index)
                .DefaultIfEmpty(-1)
                .First()
            : -1;
        if (identityPosition >= 0)
        {
            AddComment(attribute, xmi, propertyId,
                "COGS:identification=true;position=" + identityPosition.ToString(CultureInfo.InvariantCulture), "identification");
        }
        ownerElement.Add(attribute);

        string? facetExpression = BuildFacetExpression(property);
        if (facetExpression is not null)
        {
            ownerElement.Add(CreateConstraint(xmi, owner.Name, property.Name, propertyId, "facets", facetExpression));
        }
        if (identityPosition >= 0)
        {
            ownerElement.Add(CreateConstraint(xmi, owner.Name, property.Name, propertyId, "identification",
                "identification(position=" + identityPosition.ToString(CultureInfo.InvariantCulture) + ")"));
        }

        if (associationId is not null)
        {
            string oppositeId = Id("associationEnd", owner.Name, property.Name, property.DataTypeName);
            associations.Add(new XElement("packagedElement",
                new XAttribute(xmi + "type", "uml:Association"),
                new XAttribute(xmi + "id", associationId),
                new XAttribute("name", owner.Name + "_" + property.Name),
                new XAttribute("memberEnd", propertyId + " " + oppositeId),
                new XElement("ownedEnd",
                    new XAttribute(xmi + "type", "uml:Property"),
                    new XAttribute(xmi + "id", oppositeId),
                    new XAttribute("type", TypeId(owner)),
                    new XAttribute("association", associationId),
                    new XAttribute("isOrdered", "false"),
                    new XAttribute("isUnique", "false"),
                    LowerValue(xmi, oppositeId, "0"),
                    UpperValue(xmi, oppositeId, "n"))));

            if (!CogsTypeSystem.AllowsSubtypes(property) &&
                model.AllDataTypes.Any(candidate => !candidate.IsAbstract &&
                    !ReferenceEquals(candidate, property.DataType) &&
                    CogsTypeSystem.IsAssignableFrom(property.DataType, candidate)))
            {
                Errors.Add(new CogsError(ErrorLevel.Warning, "PROJ2601",
                    $"UML cannot enforce the property-local subtype exclusion on '{owner.Name}.{property.Name}'; the association targets the declared base type.",
                    sourcePath: owner.Path, modelPath: owner.Name + "." + property.Name));
            }
        }
    }

    private static XElement LowerValue(XNamespace xmi, string propertyId, string value) =>
        new("lowerValue",
            new XAttribute(xmi + "type", "uml:LiteralInteger"),
            new XAttribute(xmi + "id", propertyId + ".lower"),
            new XAttribute("value", value));

    private static XElement UpperValue(XNamespace xmi, string propertyId, string value) =>
        new("upperValue",
            new XAttribute(xmi + "type", "uml:LiteralUnlimitedNatural"),
            new XAttribute(xmi + "id", propertyId + ".upper"),
            new XAttribute("value", string.Equals(value, "n", StringComparison.Ordinal) ? "*" : value));

    private static XElement CreateConstraint(
        XNamespace xmi,
        string owner,
        string property,
        string propertyId,
        string kind,
        string expression)
    {
        string id = Id("constraint", owner, property, kind);
        return new XElement("ownedRule",
            new XAttribute(xmi + "type", "uml:Constraint"),
            new XAttribute(xmi + "id", id),
            new XAttribute("name", "COGS " + kind),
            new XAttribute("constrainedElement", propertyId),
            new XElement("specification",
                new XAttribute(xmi + "type", "uml:OpaqueExpression"),
                new XAttribute(xmi + "id", id + ".specification"),
                new XElement("language", "COGS"),
                new XElement("body", expression)));
    }

    private static string? BuildFacetExpression(Property property)
    {
        var facets = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        if (property.MinLength.HasValue) facets["minLength"] = property.MinLength.Value;
        if (property.MaxLength.HasValue) facets["maxLength"] = property.MaxLength.Value;
        if (!string.IsNullOrWhiteSpace(property.Pattern)) facets["pattern"] = property.Pattern;
        if (property.Enumeration.Count != 0) facets["enumeration"] = property.Enumeration;
        AddFacet(facets, "minInclusive", property.MinInclusive);
        AddFacet(facets, "minExclusive", property.MinExclusive);
        AddFacet(facets, "maxInclusive", property.MaxInclusive);
        AddFacet(facets, "maxExclusive", property.MaxExclusive);
        return facets.Count == 0 ? null : JsonSerializer.Serialize(facets);
    }

    private static void AddFacet(IDictionary<string, object?> facets, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) facets[name] = value;
    }

    private static void AddComment(XElement owner, XNamespace xmi, string ownerId, string? body, string? key = null)
    {
        if (string.IsNullOrWhiteSpace(body)) return;
        owner.Add(new XElement("ownedComment",
            new XAttribute(xmi + "type", "uml:Comment"),
            new XAttribute(xmi + "id", Id("comment", ownerId, key ?? "description")),
            new XAttribute("annotatedElement", ownerId),
            new XElement("body", body)));
    }

    private static XElement CreateEaExtension(CogsModel model, XNamespace xmi, string modelId, string packageId)
    {
        var elements = new XElement("elements");
        int index = 0;
        foreach (DataType type in model.AllDataTypes.OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            int column = index % 4;
            int row = index / 4;
            int left = 40 + column * 260;
            int top = 40 + row * 180;
            elements.Add(new XElement("element",
                new XAttribute("subject", TypeId(type)),
                new XAttribute("geometry", $"Left={left};Top={top};Right={left + 220};Bottom={top + 140};"),
                new XAttribute("seqno", (++index).ToString(CultureInfo.InvariantCulture))));
        }

        return new XElement(xmi + "Extension",
            new XAttribute("extender", "Enterprise Architect"),
            new XAttribute("extenderID", "6.5"),
            new XElement("diagrams",
                new XElement("diagram",
                    new XAttribute(xmi + "id", Id("diagram", model.Settings.Slug)),
                    new XElement("model",
                        new XAttribute("package", packageId),
                        new XAttribute("owner", modelId)),
                    new XElement("properties",
                        new XAttribute("name", model.Settings.Title ?? model.Settings.Slug),
                        new XAttribute("type", "Logical")),
                    elements)));
    }

    private static string PrimitiveId(string name) => Id("primitive", name);
    private static string TypeId(DataType type) => Id("type", type.Name);

    private static string Id(params string[] parts)
    {
        var builder = new StringBuilder("cogs");
        foreach (string part in parts)
        {
            builder.Append('.');
            foreach (Rune rune in part.EnumerateRunes())
            {
                int value = rune.Value;
                if ((value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z') ||
                    (value >= '0' && value <= '9') || value is '_' or '-' or '.')
                {
                    builder.Append((char)value);
                }
                else
                {
                    builder.Append("_u").Append(value.ToString("X", CultureInfo.InvariantCulture)).Append('_');
                }
            }
        }
        return builder.ToString();
    }
}
