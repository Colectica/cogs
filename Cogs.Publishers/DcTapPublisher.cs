using Cogs.Common;
using Cogs.Model;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Cogs.Publishers;

/// <summary>Publishes the deliberately lossy, but syntactically conformant, DCTAP projection.</summary>
public sealed class DcTapPublisher
{
    public required string TargetDirectory { get; set; }
    public bool Overwrite { get; set; }
    public required CogsModel CogsModel { get; set; }
    public PublicationResult? LastResult { get; private set; }

    private readonly List<CogsError> _diagnostics = new();
    private string _namespacePrefix = string.Empty;

    public void Publish()
    {
        PublicationResult result = PublishResult();
        if (!result.Success)
        {
            CogsError error = result.Diagnostics.First(diagnostic => diagnostic.Level >= ErrorLevel.Error);
            throw new CogsPublicationException(error.Message, error.Exception ?? new InvalidOperationException(error.Message));
        }
    }

    public PublicationResult PublishResult()
    {
        _diagnostics.Clear();
        _diagnostics.AddRange(RdfPublisherValidation.ValidatePropertyTermCollisions(
            CogsModel,
            "DCT1001",
            "DCTAP"));
        if (_diagnostics.Any(diagnostic => diagnostic.Level >= ErrorLevel.Error))
        {
            LastResult = new PublicationResult(Array.Empty<string>(), _diagnostics);
            return LastResult;
        }

        PublicationResult transaction = DirectoryPublication.PublishResult(
            TargetDirectory,
            Overwrite,
            PublishCore,
            CogsModel.SourceDirectory);
        LastResult = new PublicationResult(transaction.Artifacts, transaction.Diagnostics.Concat(_diagnostics));
        return LastResult;
    }

    private void PublishCore(string targetDirectory)
    {
        string prefix = CogsModel.Settings.NamespacePrefix;
        if (prefix is "dcterms" or "rdf" or "xsd")
        {
            string original = prefix;
            prefix = "cogs";
            _diagnostics.Add(new CogsError(
                ErrorLevel.Warning,
                "DCT2008",
                $"DCTAP prefix '{original}' conflicts with a vocabulary prefix; '{prefix}' was used for model terms.",
                sourcePath: CogsModel.SourceDirectory,
                modelPath: "/Settings/NamespacePrefix"));
        }
        _namespacePrefix = string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix + ":";

        var entries = new List<DcTapEntry>();
        foreach (DataType type in CogsModel.AllDataTypes.OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            entries.Add(new DcTapEntry
            {
                ShapeId = ShapeId(type),
                ShapeLabel = GetLabel(type.Name)
            });

            if (!string.IsNullOrWhiteSpace(type.Description))
            {
                Warn("DCT2009",
                    $"DCTAP note applies to statement templates and cannot carry the description of type '{type.Name}'; the type description was omitted.",
                    type,
                    $"/{Kind(type)}/{type.Name}");
            }

            foreach (Property property in CogsTypeSystem.EffectiveProperties(type))
            {
                entries.Add(CreatePropertyEntry(type, property));
            }

            if (type.IsAbstract)
            {
                Warn("DCT2004",
                    $"DCTAP cannot enforce that abstract COGS type '{type.Name}' has no direct instances.",
                    type,
                    $"/{Kind(type)}/{type.Name}");
            }

            entries.Add(new DcTapEntry());
        }

        if (entries.Count > 0) entries.RemoveAt(entries.Count - 1);
        using var writer = new StreamWriter(Path.Combine(targetDirectory, "dctap.csv"));
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(entries);
    }

    /// <summary>Returns entries for properties declared by <paramref name="dataType"/> without modifying the model.</summary>
    public List<DcTapEntry> GetPropertyEntries(DataType dataType) =>
        dataType.Properties.Select(property => CreatePropertyEntry(dataType, property)).ToList();

    private DcTapEntry CreatePropertyEntry(DataType owner, Property property)
    {
        var entry = new DcTapEntry
        {
            PropertyId = PropertyId(property),
            PropertyLabel = GetLabel(property.Name),
            Mandatory = property.MinCardinality != "0",
            Repeatable = property.MaxCardinality != "1",
            Note = property.Description
        };

        if (IsPrimitive(property))
        {
            entry.ValueNodeType = "literal";
            entry.ValueDataType = GetValueDataType(owner, property);
        }
        else
        {
            bool isItem = property.DataType is ItemType;
            entry.ValueNodeType = isItem ? "IRI" : "bnode";
            entry.ValueShape = ShapeId(property.DataType);

            if (!CogsTypeSystem.AllowsSubtypes(property) && property.DataType.ChildTypes.Any())
            {
                Warn("DCT2002",
                    $"DCTAP cannot enforce the property-local subtype exclusion on '{owner.Name}.{property.Name}'.",
                    owner,
                    PropertyPath(owner, property));
            }
        }

        AddConstraint(owner, property, entry);

        if (property.Ordered)
        {
            Warn("DCT2006",
                $"DCTAP cannot preserve ordering for '{owner.Name}.{property.Name}'.",
                owner,
                PropertyPath(owner, property));
        }

        return entry;
    }

    private string? GetValueDataType(DataType owner, Property property)
    {
        if (property.DataTypeName == "cogsDate")
        {
            Warn("DCT2001",
                $"DCTAP has only one valueDataType cell and cannot represent the cogsDate union on '{owner.Name}.{property.Name}'; the datatype was omitted.",
                owner,
                PropertyPath(owner, property));
            return null;
        }

        return property.DataTypeName == "langString"
            ? "rdf:langString"
            : "xsd:" + property.DataTypeName;
    }

    private void AddConstraint(DataType owner, Property property, DcTapEntry entry)
    {
        var constraints = new List<(string Type, string Value)>();
        if (property.Enumeration.Count > 0)
        {
            if (property.Enumeration.Any(value => value.Contains(',')))
            {
                Warn("DCT2005",
                    $"DCTAP picklists cannot losslessly encode comma-containing enumeration values on '{owner.Name}.{property.Name}'; the enumeration was omitted.",
                    owner,
                    PropertyPath(owner, property));
            }
            else
            {
                constraints.Add(("picklist", string.Join(",", property.Enumeration)));
            }
        }

        if (!string.IsNullOrWhiteSpace(property.Pattern)) constraints.Add(("pattern", property.Pattern));
        if (property.MinLength.HasValue) constraints.Add(("minLength", property.MinLength.Value.ToString(CultureInfo.InvariantCulture)));
        if (property.MaxLength.HasValue) constraints.Add(("maxLength", property.MaxLength.Value.ToString(CultureInfo.InvariantCulture)));
        if (!string.IsNullOrEmpty(property.MinInclusive)) constraints.Add(("minInclusive", property.MinInclusive));
        if (!string.IsNullOrEmpty(property.MaxInclusive)) constraints.Add(("maxInclusive", property.MaxInclusive));

        if (!string.IsNullOrEmpty(property.MinExclusive) || !string.IsNullOrEmpty(property.MaxExclusive))
        {
            Warn("DCT2003",
                $"DCTAP has no standard exclusive-bound constraint for '{owner.Name}.{property.Name}'; the exclusive bound was omitted.",
                owner,
                PropertyPath(owner, property));
        }

        if (constraints.Count == 0) return;
        (string Type, string Value) selected = constraints
            .OrderBy(constraint => ConstraintPriority(constraint.Type))
            .First();
        entry.ValueConstraintType = selected.Type;
        entry.ValueConstraint = selected.Value;

        if (constraints.Count > 1)
        {
            string omitted = string.Join(", ", constraints
                .Where(constraint => constraint != selected)
                .Select(constraint => constraint.Type));
            Warn("DCT2003",
                $"DCTAP permits one constraint per statement template; '{selected.Type}' was kept for '{owner.Name}.{property.Name}' and {omitted} was omitted.",
                owner,
                PropertyPath(owner, property));
        }
    }

    private static int ConstraintPriority(string constraintType) => constraintType switch
    {
        "picklist" => 0,
        "pattern" => 1,
        _ => 2
    };

    private static bool IsPrimitive(Property property) =>
        CogsTypes.SimpleTypeNames.Contains(property.DataTypeName, StringComparer.Ordinal);

    private string ShapeId(DataType type) => _namespacePrefix + type.Name;

    private string PropertyId(Property property)
    {
        if (property.Name.StartsWith("DublinCore", StringComparison.Ordinal))
        {
            string term = property.Name["DublinCore".Length..];
            if (term.Length > 0)
            {
                return "dcterms:" + char.ToLowerInvariant(term[0]) + term[1..];
            }
        }
        return _namespacePrefix + CogsRdfNaming.ToPropertyLocalName(property.Name);
    }

    private static string GetLabel(string name) => string.Join(" ", SplitCamelCase(name));

    public static IEnumerable<string> SplitCamelCase(string source)
    {
        if (source is "ID" or "URN")
        {
            yield return source;
            yield break;
        }

        foreach (Match match in Regex.Matches(source, @"[A-Z][a-z]*|[a-z]+|\d+"))
        {
            yield return match.Value;
        }
    }

    private void Warn(string code, string message, DataType type, string modelPath) =>
        _diagnostics.Add(new CogsError(
            ErrorLevel.Warning,
            code,
            message,
            sourcePath: type.Path,
            modelPath: modelPath));

    private static string PropertyPath(DataType owner, Property property) =>
        $"/{Kind(owner)}/{owner.Name}/Properties/{property.Name}";
    private static string Kind(DataType type) => type is ItemType ? "ItemTypes" : "CompositeTypes";
}

public sealed class DcTapEntry
{
    [Name("shapeID")]
    public string? ShapeId { get; set; }
    [Name("shapeLabel")]
    public string? ShapeLabel { get; set; }
    [Name("propertyID")]
    public string? PropertyId { get; set; }
    [Name("propertyLabel")]
    public string? PropertyLabel { get; set; }
    [BooleanTrueValues("TRUE")]
    [BooleanFalseValues("FALSE")]
    [Name("mandatory")]
    public bool? Mandatory { get; set; }
    [BooleanTrueValues("TRUE")]
    [BooleanFalseValues("FALSE")]
    [Name("repeatable")]
    public bool? Repeatable { get; set; }
    [Name("valueNodeType")]
    public string? ValueNodeType { get; set; }
    [Name("valueDataType")]
    public string? ValueDataType { get; set; }
    [Name("valueShape")]
    public string? ValueShape { get; set; }
    [Name("valueConstraint")]
    public string? ValueConstraint { get; set; }
    [Name("valueConstraintType")]
    public string? ValueConstraintType { get; set; }
    [Name("note")]
    public string? Note { get; set; }
}
