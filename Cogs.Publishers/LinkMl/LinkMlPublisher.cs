using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using YamlDotNet.Serialization;

namespace Cogs.Publishers.LinkMl;

/// <summary>Publishes a valid LinkML projection and reports projection losses.</summary>
public sealed class LinkMlPublisher
{
    public required string Name { get; set; }
    public required string NamespaceUriPrefix { get; set; }
    public required string NamespaceUri { get; set; }
    public required string TargetDirectory { get; set; }
    public bool Overwrite { get; set; }
    public PublicationResult? LastResult { get; private set; }

    private readonly List<CogsError> _diagnostics = new();

    private static readonly IReadOnlyDictionary<string, string> RangeMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["string"] = "string",
            ["boolean"] = "boolean",
            ["decimal"] = "decimal",
            ["float"] = "float",
            ["double"] = "double",
            ["duration"] = "cogs_duration",
            ["dateTime"] = "cogs_date_time",
            ["time"] = "time",
            ["date"] = "cogs_date_only",
            ["gYearMonth"] = "cogs_g_year_month",
            ["gYear"] = "cogs_g_year",
            ["gMonthDay"] = "cogs_g_month_day",
            ["gDay"] = "cogs_g_day",
            ["gMonth"] = "cogs_g_month",
            ["anyURI"] = "cogs_any_uri",
            ["language"] = "cogs_language",
            ["nonPositiveInteger"] = "cogs_non_positive_integer",
            ["negativeInteger"] = "cogs_negative_integer",
            ["long"] = "cogs_long",
            ["int"] = "cogs_int",
            ["nonNegativeInteger"] = "cogs_non_negative_integer",
            ["unsignedLong"] = "cogs_unsigned_long",
            ["positiveInteger"] = "cogs_positive_integer",
            ["cogsDate"] = "cogs_date",
            ["langString"] = "cogs_lang_string"
        };

    public void Publish(CogsModel model)
    {
        PublicationResult result = PublishResult(model);
        if (!result.Success)
        {
            CogsError error = result.Diagnostics.First(diagnostic => diagnostic.Level >= ErrorLevel.Error);
            throw new CogsPublicationException(error.Message, error.Exception ?? new InvalidOperationException(error.Message));
        }
    }

    public PublicationResult PublishResult(CogsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _diagnostics.Clear();
        _diagnostics.AddRange(RdfPublisherValidation.ValidatePropertyTermCollisions(
            model,
            "LNK1001",
            "LinkML"));
        if (_diagnostics.Any(diagnostic => diagnostic.Level >= ErrorLevel.Error))
        {
            LastResult = new PublicationResult(Array.Empty<string>(), _diagnostics);
            return LastResult;
        }

        PublicationResult transaction = DirectoryPublication.PublishResult(
            TargetDirectory,
            Overwrite,
            stagingDirectory => PublishCore(model, stagingDirectory),
            model.SourceDirectory);

        LastResult = new PublicationResult(transaction.Artifacts, transaction.Diagnostics.Concat(_diagnostics));
        return LastResult;
    }

    private void PublishCore(CogsModel model, string targetDirectory)
    {
        string modelPrefix = NamespaceUriPrefix;
        if (modelPrefix is "linkml" or "rdf" or "xsd")
        {
            modelPrefix = "cogs";
            _diagnostics.Add(new CogsError(
                ErrorLevel.Warning,
                "LNK2006",
                $"LinkML prefix '{NamespaceUriPrefix}' conflicts with a required vocabulary prefix; '{modelPrefix}' was used for the model namespace.",
                sourcePath: model.SourceDirectory,
                modelPath: "/Settings/NamespacePrefix"));
        }

        var slots = model.Identification
            .Concat(model.AllDataTypes.SelectMany(type => type.Properties))
            .Select(property => SlotName(property.Name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToDictionary(
                name => name,
                name => new LinkMLSlot { slot_uri = modelPrefix + ":" + name },
                StringComparer.Ordinal);

        var classes = new Dictionary<string, LinkMLClass>(StringComparer.Ordinal);
        foreach (DataType type in model.AllDataTypes.OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            var slotUsage = new Dictionary<string, LinkMLSlot>(StringComparer.Ordinal);
            var declaredSlots = new List<string>();
            foreach (Property property in type.Properties)
            {
                string slotName = SlotName(property.Name);
                declaredSlots.Add(slotName);
                slotUsage.Add(slotName, PropertyToSlot(model, type, property));
                AddProjectionWarnings(model, type, property);
            }

            var keys = new Dictionary<string, LinkMLUniqueKeySlots>(StringComparer.Ordinal);
            if (type is ItemType && string.IsNullOrEmpty(type.ExtendsTypeName))
            {
                keys.Add("identification", new LinkMLUniqueKeySlots
                {
                    unique_key_slots = model.Identification
                        .Select(identity => SlotName(identity.Name))
                        .ToList()
                });
            }

            classes.Add(type.Name, new LinkMLClass
            {
                description = type.Description,
                is_a = string.IsNullOrEmpty(type.ExtendsTypeName) ? null : type.ExtendsTypeName,
                IsAbstract = type.IsAbstract,
                deprecated = type.IsDeprecated ? "Deprecated in the COGS model." : null,
                slots = declaredSlots,
                slot_usage = slotUsage,
                unique_keys = keys
            });
        }

        string schemaName = NormalizeSchemaName(Name);
        if (!string.Equals(schemaName, Name, StringComparison.Ordinal))
        {
            _diagnostics.Add(new CogsError(
                ErrorLevel.Warning,
                "LNK2003",
                $"LinkML schema name '{Name}' was normalized to '{schemaName}'.",
                sourcePath: model.SourceDirectory,
                modelPath: "/Settings/ShortTitle"));
        }

        var linkml = new LinkMLModel
        {
            id = NamespaceUri,
            name = schemaName,
            default_prefix = modelPrefix,
            classes = classes,
            slots = slots,
            types = CreateTypes(),
            prefixes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["linkml"] = "https://w3id.org/linkml/",
                [modelPrefix] = CogsRdfNaming.GetTermBase(NamespaceUri),
                ["rdf"] = "http://www.w3.org/1999/02/22-rdf-syntax-ns#",
                ["xsd"] = "http://www.w3.org/2001/XMLSchema#"
            }
        };

        var serializer = new SerializerBuilder()
            .WithTypeConverter(new BigIntegerYamlTypeConverter())
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitEmptyCollections)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
        File.WriteAllText(Path.Combine(targetDirectory, "linkml.yml"), serializer.Serialize(linkml));
    }

    private LinkMLSlot PropertyToSlot(CogsModel model, DataType owner, Property property)
    {
        BigInteger minimum = BigInteger.Parse(property.MinCardinality, CultureInfo.InvariantCulture);
        BigInteger? maximum = property.MaxCardinality == "n"
            ? null
            : BigInteger.Parse(property.MaxCardinality, CultureInfo.InvariantCulture);
        bool isComposite = property.DataType is not null && property.DataType is not ItemType && !property.DataType.IsXmlPrimitive;
        string range = MapRange(property.DataTypeName);

        List<string>? enumeration = null;
        if (property.Enumeration.Count > 0 && IsStringDerived(property.DataTypeName))
        {
            enumeration = property.Enumeration.ToList();
        }

        return new LinkMLSlot
        {
            description = property.Description,
            range = range,
            required = minimum > 0,
            multivalued = maximum != BigInteger.One,
            minimum_cardinality = maximum != BigInteger.One ? minimum : null,
            maximum_cardinality = maximum != BigInteger.One ? maximum : null,
            inlined = isComposite ? true : property.DataType is ItemType ? false : null,
            inlined_as_list = isComposite && maximum != BigInteger.One ? true : null,
            list_elements_ordered = maximum != BigInteger.One ? property.Ordered : null,
            pattern = string.IsNullOrEmpty(property.Pattern) ? null : property.Pattern,
            minimum_value = string.IsNullOrEmpty(property.MinInclusive) ? null : property.MinInclusive,
            maximum_value = string.IsNullOrEmpty(property.MaxInclusive) ? null : property.MaxInclusive,
            equals_string_in = enumeration
        };
    }

    private void AddProjectionWarnings(CogsModel model, DataType owner, Property property)
    {
        string modelPath = $"/{Kind(owner)}/{owner.Name}/Properties/{property.Name}";
        if (property.DataType is not null
            && !property.DataType.IsXmlPrimitive
            && !CogsTypeSystem.AllowsSubtypes(property)
            && property.DataType.ChildTypes.Any())
        {
            Warn("LNK2001",
                $"LinkML cannot enforce the property-local subtype exclusion on '{owner.Name}.{property.Name}'.",
                owner,
                modelPath);
        }

        if (!string.IsNullOrEmpty(property.MinExclusive) || !string.IsNullOrEmpty(property.MaxExclusive))
        {
            Warn("LNK2002",
                $"LinkML has no exact exclusive-bound slot for '{owner.Name}.{property.Name}'; the bound was omitted.",
                owner,
                modelPath);
        }

        if ((property.MinLength.HasValue || property.MaxLength.HasValue))
        {
            Warn("LNK2004",
                $"LinkML has no portable direct length facet for '{owner.Name}.{property.Name}'; the facet was omitted.",
                owner,
                modelPath);
        }

        if (property.Enumeration.Count > 0 && !IsStringDerived(property.DataTypeName))
        {
            Warn("LNK2005",
                $"LinkML's lexical enumeration expression is string-only; the enumeration on '{owner.Name}.{property.Name}' was omitted.",
                owner,
                modelPath);
        }
    }

    private static Dictionary<string, LinkMLType> CreateTypes() => new(StringComparer.Ordinal)
    {
        ["cogs_duration"] = Alias("string", "xsd:duration", "An exact XSD duration lexical value."),
        ["cogs_date_time"] = Alias("string", "xsd:dateTime", "An XSD dateTime lexical value whose nonzero calendar year is a signed 32-bit integer."),
        ["cogs_date_only"] = Alias("string", "xsd:date", "An XSD date lexical value whose nonzero calendar year is a signed 32-bit integer."),
        ["cogs_g_year_month"] = Alias("string", "xsd:gYearMonth", "An XSD gYearMonth lexical value whose nonzero year is a signed 32-bit integer."),
        ["cogs_g_year"] = Alias("string", "xsd:gYear", "An XSD gYear lexical value whose nonzero year is a signed 32-bit integer."),
        ["cogs_g_month_day"] = Alias("string", "xsd:gMonthDay", "An XSD gMonthDay lexical value."),
        ["cogs_g_day"] = Alias("string", "xsd:gDay", "An XSD gDay lexical value."),
        ["cogs_g_month"] = Alias("string", "xsd:gMonth", "An XSD gMonth lexical value."),
        ["cogs_any_uri"] = Alias("uri", "xsd:anyURI", "An RFC 3986 URI reference."),
        ["cogs_language"] = new LinkMLType
        {
            TypeOf = "string",
            uri = "xsd:language",
            description = "A BCP 47 language tag.",
            pattern = "[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*"
        },
        ["cogs_non_positive_integer"] = Alias("integer", "xsd:nonPositiveInteger", "An integer no greater than zero.", maximum: BigInteger.Zero),
        ["cogs_negative_integer"] = Alias("integer", "xsd:negativeInteger", "An integer less than zero.", maximum: BigInteger.MinusOne),
        ["cogs_long"] = Alias("integer", "xsd:long", "A signed 64-bit integer.", long.MinValue, long.MaxValue),
        // LinkML deliberately rejects xsd:int as a type URI. Preserve the XSD int
        // value space with integer plus exact bounds while using xsd:integer.
        ["cogs_int"] = Alias("integer", "xsd:integer", "A signed 32-bit XSD int.", int.MinValue, int.MaxValue),
        ["cogs_non_negative_integer"] = Alias("integer", "xsd:nonNegativeInteger", "An integer no less than zero.", BigInteger.Zero),
        ["cogs_unsigned_long"] = Alias("integer", "xsd:unsignedLong", "An unsigned 64-bit integer.", BigInteger.Zero, BigInteger.Parse("18446744073709551615", CultureInfo.InvariantCulture)),
        ["cogs_positive_integer"] = Alias("integer", "xsd:positiveInteger", "An integer greater than zero.", BigInteger.One),
        ["cogs_date"] = new LinkMLType
        {
            description = "The COGS date union.",
            TypeOf = "string",
            uri = "https://cogsdata.org/types/cogsDate",
            union_of = ["cogs_date_only", "cogs_date_time", "cogs_duration", "cogs_g_year", "cogs_g_year_month"]
        },
        ["cogs_lang_string"] = Alias("string", "rdf:langString", "A language-tagged string.")
    };

    private static LinkMLType Alias(
        string parent,
        string uri,
        string description,
        object? minimum = null,
        object? maximum = null) => new()
    {
        TypeOf = parent,
        uri = uri,
        description = description,
        minimum_value = minimum,
        maximum_value = maximum
    };

    private static string MapRange(string cogsName) =>
        RangeMap.TryGetValue(cogsName, out string? range) ? range : cogsName;

    private static bool IsStringDerived(string cogsName) => cogsName is
        "string" or "language" or "anyURI" or "langString";

    private static string SlotName(string name) => CogsRdfNaming.ToPropertyLocalName(name);

    private static string NormalizeSchemaName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "model";
        var builder = new StringBuilder(name.Length);
        foreach (char character in name.Trim())
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }
        if (!char.IsLetter(builder[0]) && builder[0] != '_') builder.Insert(0, '_');
        return builder.ToString();
    }

    private void Warn(string code, string message, DataType type, string modelPath) =>
        _diagnostics.Add(new CogsError(
            ErrorLevel.Warning,
            code,
            message,
            sourcePath: type.Path,
            modelPath: modelPath));

    private static string Kind(DataType type) => type is ItemType ? "ItemTypes" : "CompositeTypes";
}
