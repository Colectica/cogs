using Cogs.Common;
using Cogs.Model;
using Cogs.Publishers.FluentJson;
using Json.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Cogs.Publishers;

/// <summary>
/// Authoritative COGS 2.0 instance validation. Standard schema validation is
/// combined with the lossless lexical and cross-field checks that JSON Schema
/// cannot express by itself.
/// </summary>
public static class CogsInstanceValidator
{
    private static readonly Regex DecimalLexical = new(
        @"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?$",
        RegexOptions.CultureInvariant);

    public static IReadOnlyList<CogsError> ValidateJson(CogsModel model, string json, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        var errors = new List<CogsError>();
        byte[] utf8 = Encoding.UTF8.GetBytes(json ?? string.Empty);
        CheckDuplicateJsonNames(utf8, sourcePath, errors);
        if (errors.Any(error => error.Level == ErrorLevel.Error)) return Sort(errors);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
        }
        catch (JsonException exception)
        {
            errors.Add(new CogsError(ErrorLevel.Error, "INS1001", exception.Message,
                sourcePath,
                exception.LineNumber is long line ? checked((int)line + 1) : null,
                exception.BytePositionInLine is long column ? checked((int)column + 1) : null,
                exception: exception));
            return Sort(errors);
        }

        using (document)
        {
            var schema = new FluentJsonSchemaPublisher().BuildSchema(model);
            var result = schema.Evaluate(document.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
                RequireFormatValidation = false
            });
            if (!result.IsValid)
            {
                foreach (var detail in result.Details?.Where(detail => detail.Errors?.Count > 0)
                    ?? Enumerable.Empty<EvaluationResults>())
                {
                    string message = string.Join("; ", detail.Errors!.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => $"{pair.Key}: {pair.Value}"));
                    errors.Add(new CogsError(ErrorLevel.Error, "INS1002", message,
                        sourcePath, modelPath: detail.InstanceLocation.ToString()));
                }
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                CheckJsonDefinitions(model, document.RootElement, sourcePath, errors);
                WalkJsonModel(model, document.RootElement, sourcePath, errors);
            }
        }
        return Sort(errors);
    }

    public static IReadOnlyList<CogsError> ValidateXml(CogsModel model, string xml, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        var errors = new List<CogsError>();
        var publisher = new XmlSchemaPublisher
        {
            CogsLocation = model.SourceDirectory ?? string.Empty,
            TargetDirectory = ".",
            TargetNamespace = model.Settings.NamespaceUrl,
            TargetNamespacePrefix = model.Settings.NamespacePrefix,
            CogsModel = model
        };
        XmlSchemaSet schemas;
        try
        {
            schemas = publisher.BuildSchemaSet();
        }
        catch (Exception exception)
        {
            errors.Add(new CogsError(ErrorLevel.Error, "INS2001",
                $"The model's XML Schema could not be compiled: {exception.Message}", sourcePath, exception: exception));
            return Sort(errors);
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            ValidationType = ValidationType.None
        };

        XDocument? document = null;
        try
        {
            using var text = new StringReader(xml ?? string.Empty);
            using var reader = XmlReader.Create(text, settings);
            document = XDocument.Load(reader, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            errors.Add(new CogsError(ErrorLevel.Error, "INS2003", exception.Message,
                sourcePath, exception.LineNumber, exception.LinePosition, exception: exception));
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(new CogsError(ErrorLevel.Error, "INS2004", exception.Message,
                sourcePath, exception: exception));
        }

        if (document?.Root is not null)
        {
            var properties = IndexXmlProperties(model, document.Root, sourcePath, errors);
            document.Validate(schemas, (sender, eventArgs) =>
            {
                var exception = eventArgs.Exception;
                XElement? element = sender as XElement;
                if ((element is null || !properties.ContainsKey(element)) && exception?.LineNumber > 0)
                {
                    element = properties.Keys.FirstOrDefault(candidate =>
                    {
                        var line = (IXmlLineInfo)candidate;
                        return line.HasLineInfo() && line.LineNumber == exception.LineNumber;
                    });
                }
                if (element is not null && properties.TryGetValue(element, out var property) &&
                    IsKnownDotNetXsdLexicalLimitation(property, element.Value, eventArgs.Message))
                {
                    return;
                }
                errors.Add(new CogsError(ErrorLevel.Error, "INS2002", eventArgs.Message,
                    sourcePath,
                    exception?.LineNumber > 0 ? exception.LineNumber : null,
                    exception?.LinePosition > 0 ? exception.LinePosition : null,
                    exception: exception));
            }, addSchemaInfo: true);
            CheckXmlDefinitions(model, document.Root, sourcePath, errors);
        }
        return Sort(errors);
    }

    private static void CheckDuplicateJsonNames(byte[] utf8, string? sourcePath, List<CogsError> errors)
    {
        try
        {
            var reader = new Utf8JsonReader(utf8, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            var objects = new Stack<HashSet<string>>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    objects.Push(new HashSet<string>(StringComparer.Ordinal));
                }
                else if (reader.TokenType == JsonTokenType.EndObject)
                {
                    objects.Pop();
                }
                else if (reader.TokenType == JsonTokenType.PropertyName &&
                         !objects.Peek().Add(reader.GetString()!))
                {
                    (int line, int column) = GetJsonLocation(utf8, reader.TokenStartIndex);
                    errors.Add(new CogsError(ErrorLevel.Error, "INS1003",
                        $"Duplicate JSON field '{reader.GetString()}'.", sourcePath,
                        line, column));
                }
            }
        }
        catch (JsonException exception)
        {
            errors.Add(new CogsError(ErrorLevel.Error, "INS1001", exception.Message,
                sourcePath,
                exception.LineNumber is long line ? checked((int)line + 1) : null,
                exception.BytePositionInLine is long column ? checked((int)column + 1) : null,
                exception: exception));
        }
    }

    private static (int Line, int Column) GetJsonLocation(byte[] utf8, long tokenStartIndex)
    {
        int length = checked((int)Math.Clamp(tokenStartIndex, 0, utf8.LongLength));
        string prefix = Encoding.UTF8.GetString(utf8, 0, length);
        int line = 1;
        int column = 1;
        for (int index = 0; index < prefix.Length; index++)
        {
            if (prefix[index] == '\r')
            {
                line++;
                column = 1;
                if (index + 1 < prefix.Length && prefix[index + 1] == '\n') index++;
            }
            else if (prefix[index] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }
        return (line, column);
    }

    private static void CheckJsonDefinitions(CogsModel model, JsonElement root, string? sourcePath, List<CogsError> errors)
    {
        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return;
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("$type", out var discriminator) ||
                discriminator.ValueKind != JsonValueKind.String) continue;
            var tuple = new List<string?> { discriminator.GetString() };
            bool complete = true;
            foreach (var identity in model.Identification)
            {
                if (!item.TryGetProperty(identity.Name, out var value) || value.ValueKind != JsonValueKind.String)
                {
                    complete = false;
                    break;
                }
                tuple.Add(value.GetString());
            }
            if (complete && !identities.Add(JsonSerializer.Serialize(tuple)))
            {
                errors.Add(new CogsError(ErrorLevel.Error, "INS1004",
                    $"Duplicate full item definition for '{discriminator.GetString()}'.",
                    sourcePath, modelPath: "/items"));
            }
        }
    }

    private static void WalkJsonModel(CogsModel model, JsonElement root, string? sourcePath, List<CogsError> errors)
    {
        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return;
        var itemTypes = model.ItemTypes.ToDictionary(type => type.Name, StringComparer.Ordinal);
        int index = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("$type", out var discriminator) &&
                discriminator.ValueKind == JsonValueKind.String &&
                itemTypes.TryGetValue(discriminator.GetString()!, out var type))
            {
                WalkJsonObject(model, type, item, $"/items/{index}", sourcePath, errors);
            }
            index++;
        }
    }

    private static void WalkJsonObject(
        CogsModel model,
        DataType type,
        JsonElement value,
        string path,
        string? sourcePath,
        List<CogsError> errors)
    {
        foreach (var property in CogsTypeSystem.EffectiveProperties(type))
        {
            if (!value.TryGetProperty(property.Name, out var propertyValue)) continue;
            if (property.MaxCardinality != "1" && propertyValue.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var element in propertyValue.EnumerateArray())
                {
                    WalkJsonValue(model, property, element, $"{path}/{property.Name}/{index++}", sourcePath, errors);
                }
            }
            else
            {
                WalkJsonValue(model, property, propertyValue, $"{path}/{property.Name}", sourcePath, errors);
            }
        }
    }

    private static void WalkJsonValue(
        CogsModel model,
        Property property,
        JsonElement value,
        string path,
        string? sourcePath,
        List<CogsError> errors)
    {
        if (property.DataType?.IsXmlPrimitive == true)
        {
            if (property.DataTypeName == "decimal" &&
                (value.ValueKind != JsonValueKind.Number || !DecimalLexical.IsMatch(value.GetRawText())))
            {
                errors.Add(new CogsError(ErrorLevel.Error, "INS1005",
                    "decimal requires a JSON-compatible XSD decimal number without an exponent.",
                    sourcePath, modelPath: path));
            }
            if (property.DataTypeName == "cogsDate")
            {
                ValidateJsonCogsDate(value, path, sourcePath, errors);
            }
            else if (IsBoundedTemporal(property.DataTypeName) &&
                TryGetJsonTemporalLexical(property.DataTypeName, value, out string lexical))
            {
                ValidatePrimitiveBounds(property, lexical, path, sourcePath, errors, "INS1006", "INS1007");
            }
            return;
        }

        if (property.DataType is ItemType || value.ValueKind != JsonValueKind.Object || property.DataType is null) return;
        var actual = property.DataType;
        if (value.TryGetProperty("$type", out var discriminator) && discriminator.ValueKind == JsonValueKind.String)
        {
            actual = model.ReusableDataTypes.FirstOrDefault(type => type.Name == discriminator.GetString()) ?? actual;
        }
        WalkJsonObject(model, actual, value, path, sourcePath, errors);
    }

    private static bool TryGetJsonTemporalLexical(string dataType, JsonElement value, out string lexical)
    {
        lexical = string.Empty;
        if (!CogsGregorianLexical.IsGregorianType(dataType))
        {
            if (value.ValueKind != JsonValueKind.String) return false;
            lexical = value.GetString()!;
            return true;
        }
        if (value.ValueKind != JsonValueKind.Object) return false;

        int? year = TryGetInt32(value, "Year");
        int? month = TryGetInt32(value, "Month");
        int? day = TryGetInt32(value, "Day");
        string? timezone = null;
        if (value.TryGetProperty("Timezone", out JsonElement timezoneValue))
        {
            if (timezoneValue.ValueKind != JsonValueKind.String) return false;
            timezone = timezoneValue.GetString();
        }
        return CogsGregorianLexical.TryFormat(
            dataType,
            new CogsGregorianValue(year, month, day, timezone),
            out lexical);
    }

    private static int? TryGetInt32(JsonElement value, string name)
    {
        return value.TryGetProperty(name, out JsonElement component) &&
            component.ValueKind == JsonValueKind.Number &&
            component.TryGetInt32(out int result)
                ? result
                : null;
    }

    private static void ValidateJsonCogsDate(
        JsonElement value,
        string path,
        string? sourcePath,
        List<CogsError> errors)
    {
        if (value.ValueKind != JsonValueKind.Object) return;
        JsonProperty[] arms = value.EnumerateObject().ToArray();
        if (arms.Length != 1) return;

        JsonProperty arm = arms[0];
        string dataType = arm.Name switch
        {
            "DateTime" => "dateTime",
            "Date" => "date",
            "GYearMonth" => "gYearMonth",
            "GYear" => "gYear",
            "Duration" => "duration",
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(dataType)) return;

        bool valid = CogsGregorianLexical.IsGregorianType(dataType)
            ? TryGetJsonTemporalLexical(dataType, arm.Value, out _)
            : arm.Value.ValueKind == JsonValueKind.String &&
                CogsPrimitiveLexical.IsValid(dataType, arm.Value.GetString()!);
        if (!valid)
        {
            errors.Add(new CogsError(ErrorLevel.Error, "INS1006",
                $"The '{arm.Name}' arm is not a valid {dataType} value.",
                sourcePath, modelPath: $"{path}/{arm.Name}"));
        }
    }

    private static void ValidatePrimitiveBounds(
        Property property,
        string lexical,
        string path,
        string? sourcePath,
        List<CogsError> errors,
        string lexicalCode,
        string boundCode)
    {
        if (!CogsPrimitiveLexical.IsValid(property.DataTypeName, lexical))
        {
            errors.Add(new CogsError(ErrorLevel.Error, lexicalCode,
                $"'{lexical}' is not a valid {property.DataTypeName} lexical value.",
                sourcePath, modelPath: path));
            return;
        }

        CheckBound(property.MinInclusive, "minInclusive", order => order is CogsPrimitiveOrder.Equal or CogsPrimitiveOrder.Greater);
        CheckBound(property.MinExclusive, "minExclusive", order => order == CogsPrimitiveOrder.Greater);
        CheckBound(property.MaxInclusive, "maxInclusive", order => order is CogsPrimitiveOrder.Equal or CogsPrimitiveOrder.Less);
        CheckBound(property.MaxExclusive, "maxExclusive", order => order == CogsPrimitiveOrder.Less);

        void CheckBound(string boundLexical, string name, Func<CogsPrimitiveOrder, bool> accepts)
        {
            if (string.IsNullOrWhiteSpace(boundLexical)) return;
            var comparison = CogsPrimitiveLexical.Compare(property.DataTypeName, lexical, boundLexical);
            if (!accepts(comparison))
            {
                errors.Add(new CogsError(ErrorLevel.Error, boundCode,
                    $"Value does not satisfy {name} '{boundLexical}' for {property.DataTypeName}; indeterminate XSD comparisons do not satisfy a bound.",
                    sourcePath, modelPath: path));
            }
        }
    }

    private static void CheckXmlDefinitions(CogsModel model, XElement root, string? sourcePath, List<CogsError> errors)
    {
        XNamespace ns = model.Settings.NamespaceUrl;
        var itemNames = model.ItemTypes.Where(type => !type.IsAbstract).Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in root.Elements().Where(element => itemNames.Contains(element.Name.LocalName)))
        {
            var tuple = new List<string?> { item.Name.LocalName };
            bool complete = true;
            foreach (var identity in model.Identification)
            {
                var element = item.Element(ns + identity.Name);
                if (element is null)
                {
                    complete = false;
                    break;
                }
                tuple.Add(element.Value);
            }
            if (complete && !identities.Add(JsonSerializer.Serialize(tuple)))
            {
                var line = (IXmlLineInfo)item;
                errors.Add(new CogsError(ErrorLevel.Error, "INS2005",
                    $"Duplicate full item definition for '{item.Name.LocalName}'.", sourcePath,
                    line.HasLineInfo() ? line.LineNumber : null,
                    line.HasLineInfo() ? line.LinePosition : null));
            }
        }
    }

    private static Dictionary<XElement, Property> IndexXmlProperties(
        CogsModel model,
        XElement root,
        string? sourcePath,
        List<CogsError> errors)
    {
        XNamespace ns = model.Settings.NamespaceUrl;
        var itemTypes = model.ItemTypes.ToDictionary(type => type.Name, StringComparer.Ordinal);
        var reusableTypes = model.ReusableDataTypes.ToDictionary(type => type.Name, StringComparer.Ordinal);
        var result = new Dictionary<XElement, Property>();
        int itemIndex = 0;
        foreach (XElement item in root.Elements())
        {
            if (item.Name.Namespace != ns || !itemTypes.TryGetValue(item.Name.LocalName, out ItemType? type)) continue;
            Walk(type, item, $"/items/{itemIndex++}");
        }
        return result;

        void Walk(DataType type, XElement element, string path)
        {
            foreach (Property property in CogsTypeSystem.EffectiveProperties(type))
            {
                int index = 0;
                foreach (XElement child in element.Elements(ns + property.Name))
                {
                    result[child] = property;
                    string childPath = property.MaxCardinality == "1"
                        ? $"{path}/{property.Name}"
                        : $"{path}/{property.Name}/{index++}";
                    if (property.DataType?.IsXmlPrimitive == true)
                    {
                        if (property.DataTypeName == "cogsDate")
                        {
                            if (!CogsPrimitiveLexical.TryGetCogsDateDataType(child.Value, out _))
                            {
                                errors.Add(new CogsError(ErrorLevel.Error, "INS2006",
                                    $"'{child.Value}' is not a valid cogsDate lexical value.",
                                    sourcePath, modelPath: childPath));
                            }
                        }
                        else if (IsBoundedTemporal(property.DataTypeName) || IsNumeric(property.DataTypeName))
                        {
                            ValidatePrimitiveBounds(property, child.Value, childPath, sourcePath, errors,
                                "INS2006", "INS2007");
                        }
                        continue;
                    }
                    if (property.DataType is ItemType || property.DataType is null) continue;
                    DataType actual = property.DataType;
                    XAttribute? xsiType = child.Attribute(XNamespace.Get(XmlSchema.InstanceNamespace) + "type");
                    if (xsiType is not null)
                    {
                        string localName = xsiType.Value.Contains(':', StringComparison.Ordinal)
                            ? xsiType.Value[(xsiType.Value.IndexOf(':') + 1)..]
                            : xsiType.Value;
                        if (reusableTypes.TryGetValue(localName, out DataType? candidate)) actual = candidate;
                    }
                    Walk(actual, child, childPath);
                }
            }
        }
    }

    private static bool IsKnownDotNetXsdLexicalLimitation(Property property, string lexical, string message)
    {
        if (property.DataTypeName == "cogsDate")
        {
            return CogsPrimitiveLexical.TryGetCogsDateDataType(lexical, out _) &&
                message.Contains("not valid", StringComparison.OrdinalIgnoreCase);
        }
        if (!CogsPrimitiveLexical.IsValid(property.DataTypeName, lexical)) return false;
        if (property.DataTypeName is "nonPositiveInteger" or "negativeInteger" or "nonNegativeInteger" or "positiveInteger")
        {
            return message.EndsWith($"The string '{lexical}' is not a valid Integer value.", StringComparison.Ordinal);
        }
        if (property.DataTypeName == "time")
        {
            return message.EndsWith($"The string '{lexical}' is not a valid Time value.", StringComparison.Ordinal);
        }
        if (property.DataTypeName == "dateTime")
        {
            return message.EndsWith($"The string '{lexical}' is not a valid DateTime value.", StringComparison.Ordinal);
        }
        if (property.DataTypeName is "date" or "gYearMonth" or "gYear" &&
            message.Contains("not a valid", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    private static bool IsBoundedTemporal(string name) => name is
        "duration" or "dateTime" or "time" or "date" or "gYearMonth" or "gYear" or "gMonthDay" or "gDay" or "gMonth";

    private static bool IsNumeric(string name) => name is
        "decimal" or "float" or "double" or "nonPositiveInteger" or "negativeInteger" or "long" or "int" or
        "nonNegativeInteger" or "unsignedLong" or "positiveInteger";

    private static IReadOnlyList<CogsError> Sort(IEnumerable<CogsError> errors) => errors
        .OrderBy(error => error.SourcePath ?? string.Empty, StringComparer.Ordinal)
        .ThenBy(error => error.Line ?? 0)
        .ThenBy(error => error.Column ?? 0)
        .ThenBy(error => error.Code ?? string.Empty, StringComparer.Ordinal)
        .ThenBy(error => error.ModelPath ?? string.Empty, StringComparer.Ordinal)
        .ToArray();
}
