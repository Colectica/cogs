// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Cogs.Publishers.Python;

public sealed class PythonPublisher
{
    private const string RuntimeResourceName = "Cogs.Publishers.Python.Runtime.py";

    private static readonly HashSet<string> PythonKeywords = new(StringComparer.Ordinal)
    {
        "False", "None", "True", "and", "as", "assert", "async", "await", "break", "class",
        "continue", "def", "del", "elif", "else", "except", "finally", "for", "from", "global",
        "if", "import", "in", "is", "lambda", "nonlocal", "not", "or", "pass", "raise", "return",
        "try", "while", "with", "yield", "match", "case", "type",
    };

    private static readonly HashSet<string> RuntimeTypeNames = new(StringComparer.Ordinal)
    {
        "CogsDate", "CogsItem", "CogsValue", "GDay", "GMonth", "GMonthDay", "GYear",
        "GYearMonth", "ItemContainer", "LangString",
    };

    private static readonly HashSet<string> RuntimeAttributeNames = new(StringComparer.Ordinal)
    {
        "from_dict", "from_element", "from_json", "from_xml", "to_dict", "to_element",
        "to_json", "to_reference_dict", "to_xml", "_cogs_type", "_emit_type_field",
        "_is_abstract", "_is_item",
    };

    private static readonly HashSet<string> StringTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string", "language", "anyURI",
    };

    private static readonly HashSet<string> IntegerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "nonPositiveInteger", "negativeInteger", "long", "int", "nonNegativeInteger",
        "unsignedLong", "positiveInteger",
    };

    private static readonly Regex Pep440Pattern = new(
        @"^([1-9]\d*!)?(0|[1-9]\d*)(\.(0|[1-9]\d*))*(a|b|rc)?(0|[1-9]\d*)?(\.post(0|[1-9]\d*))?(\.dev(0|[1-9]\d*))?(\+[a-z0-9]+([._-][a-z0-9]+)*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly CogsModel model;

    public string TargetDirectory { get; }
    public string? TargetNamespace { get; set; }
    public bool Overwrite { get; set; }

    public PythonPublisher(CogsModel model, string targetDirectory)
    {
        this.model = model ?? throw new ArgumentNullException(nameof(model));
        TargetDirectory = targetDirectory;
    }

    public void Publish()
    {
        if (string.IsNullOrWhiteSpace(TargetDirectory))
        {
            throw new InvalidOperationException("Target directory must be specified.");
        }

        ValidateModelNames();
        string moduleName = NormalizeModuleName(model.Settings.Slug);
        string distributionName = NormalizeDistributionName(model.Settings.Slug);
        string version = model.Settings.Version;
        if (string.IsNullOrWhiteSpace(version) || !Pep440Pattern.IsMatch(version))
        {
            throw new InvalidOperationException($"Model version '{version}' is not a canonical PEP 440 version.");
        }

        if (Directory.Exists(TargetDirectory))
        {
            if (!Overwrite)
            {
                throw new InvalidOperationException("Target directory already exists.");
            }
            Directory.Delete(TargetDirectory, true);
        }

        Directory.CreateDirectory(TargetDirectory);
        string packageDirectory = Path.Combine(TargetDirectory, moduleName);
        Directory.CreateDirectory(packageDirectory);

        string targetNamespace = TargetNamespace ?? model.Settings.NamespaceUrl;
        if (string.IsNullOrWhiteSpace(targetNamespace))
        {
            throw new InvalidOperationException("An XML target namespace must be specified.");
        }

        string namespacePrefix = model.Settings.NamespacePrefix;
        if (string.IsNullOrWhiteSpace(namespacePrefix))
        {
            namespacePrefix = "model";
        }
        try
        {
            XmlConvert.VerifyNCName(namespacePrefix);
        }
        catch (XmlException exception)
        {
            throw new InvalidOperationException($"XML namespace prefix '{namespacePrefix}' is invalid.", exception);
        }
        if (namespacePrefix.Equals("xml", StringComparison.OrdinalIgnoreCase)
            || namespacePrefix.Equals("xmlns", StringComparison.OrdinalIgnoreCase)
            || namespacePrefix.Equals("xsi", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"XML namespace prefix '{namespacePrefix}' is reserved.");
        }

        File.WriteAllText(
            Path.Combine(TargetDirectory, "pyproject.toml"),
            GeneratePyProject(moduleName, distributionName, version),
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(packageDirectory, "__init__.py"),
            GenerateInit(),
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(packageDirectory, "model.py"),
            GenerateModel(targetNamespace, namespacePrefix),
            new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(packageDirectory, "py.typed"), string.Empty, new UTF8Encoding(false));
    }

    private string GeneratePyProject(string moduleName, string distributionName, string version)
    {
        string description = string.IsNullOrWhiteSpace(model.Settings.Description)
            ? model.Settings.ShortTitle
            : model.Settings.Description;
        return $$"""
            [build-system]
            requires = ["setuptools>=61"]
            build-backend = "setuptools.build_meta"

            [project]
            name = {{Quote(distributionName)}}
            version = {{Quote(version)}}
            description = {{Quote(description)}}
            requires-python = ">=3.11"

            [tool.setuptools]
            packages = [{{Quote(moduleName)}}]

            [tool.setuptools.package-data]
            {{moduleName}} = ["py.typed"]
            """;
    }

    private string GenerateInit()
    {
        var builder = new StringBuilder();
        AppendHeader(builder);
        builder.AppendLine("from .model import *");
        builder.AppendLine("from .model import __all__");
        return builder.ToString();
    }

    private string GenerateModel(string targetNamespace, string namespacePrefix)
    {
        using Stream stream = GetType().GetTypeInfo().Assembly.GetManifestResourceStream(RuntimeResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{RuntimeResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        string runtime = reader.ReadToEnd()
            .Replace("__TARGET_NAMESPACE__", Quote(targetNamespace), StringComparison.Ordinal)
            .Replace("__NAMESPACE_PREFIX__", Quote(namespacePrefix), StringComparison.Ordinal)
            .Replace("__IDENTIFICATION_FIELDS__", GetIdentificationTuple(), StringComparison.Ordinal);

        var builder = new StringBuilder();
        builder.AppendLine("# Generated by COGS. Do not edit by hand.");
        AppendHeader(builder);
        builder.Append(runtime.TrimStart());
        builder.AppendLine();

        foreach (DataType dataType in GetOrderedTypes())
        {
            AppendDataType(builder, dataType);
        }

        builder.AppendLine("ITEM_TYPE_REGISTRY: dict[str, type[CogsItem]] = {");
        foreach (ItemType item in model.ItemTypes.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            builder.AppendLine($"    {Quote(item.Name)}: {item.Name},");
        }
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("TYPE_REGISTRY: dict[str, type[CogsValue]] = {");
        foreach (DataType dataType in GetOrderedTypes().OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            builder.AppendLine($"    {Quote(dataType.Name)}: {dataType.Name},");
        }
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("__all__ = [");
        foreach (string name in new[]
        {
            "CogsDate", "CogsItem", "CogsValue", "GDay", "GMonth", "GMonthDay", "GYear",
            "GYearMonth", "ItemContainer", "LangString",
        }.Concat(GetOrderedTypes().Select(x => x.Name)).Distinct().OrderBy(x => x, StringComparer.Ordinal))
        {
            builder.AppendLine($"    {Quote(name)},");
        }
        builder.AppendLine("]");
        return builder.ToString();
    }

    private void AppendDataType(StringBuilder builder, DataType dataType)
    {
        string baseType = string.IsNullOrWhiteSpace(dataType.ExtendsTypeName)
            ? dataType is ItemType ? "CogsItem" : "CogsValue"
            : dataType.ExtendsTypeName;

        builder.AppendLine("@dataclass");
        builder.AppendLine($"class {dataType.Name}({baseType}):");
        builder.AppendLine($"    {Quote(dataType.Description ?? string.Empty)}");
        builder.AppendLine($"    _cogs_type: ClassVar[str] = {Quote(dataType.Name)}");
        builder.AppendLine($"    _is_abstract: ClassVar[bool] = {PythonBool(dataType.IsAbstract)}");
        if (dataType is not ItemType)
        {
            builder.AppendLine($"    _emit_type_field: ClassVar[bool] = {PythonBool(dataType.IsSubstitute)}");
        }
        foreach (Property property in dataType.Properties)
        {
            string attributeName = ToSnakeCase(property.Name);
            bool many = IsMany(property);
            string annotation = GetTypeAnnotation(property, many);
            string defaultValue = many ? "field(default_factory=list" : "field(default=None";
            builder.AppendLine(
                $"    {attributeName}: {annotation} = {defaultValue}, metadata={{" +
                $"{Quote("cogs_name")}: {Quote(property.Name)}, " +
                $"{Quote("description")}: {Quote(property.Description)}, " +
                $"{Quote("type_name")}: {Quote(property.DataType.Name)}, " +
                $"{Quote("kind")}: {Quote(GetKind(property))}, " +
                $"{Quote("many")}: {PythonBool(many)}, " +
                $"{Quote("ordered")}: {PythonBool(property.Ordered)}, " +
                $"{Quote("allow_subtypes")}: {PythonBool(property.AllowSubtypes)}" +
                "})");
        }
        builder.AppendLine();
    }

    private IEnumerable<DataType> GetOrderedTypes()
    {
        return model.ReusableDataTypes
            .Concat<DataType>(model.ItemTypes)
            .OrderBy(x => x.ParentTypes.Count)
            .ThenBy(x => x.Name, StringComparer.Ordinal);
    }

    private string GetIdentificationTuple()
    {
        string[] values = model.Identification
            .Select(x => $"({Quote(x.Name)}, {Quote(ToSnakeCase(x.Name))})")
            .ToArray();
        return values.Length switch
        {
            0 => "()",
            1 => $"({values[0]},)",
            _ => $"({string.Join(", ", values)})",
        };
    }

    private string GetTypeAnnotation(Property property, bool many)
    {
        string pythonType = GetPythonType(property.DataType);
        return many ? $"list[{pythonType}]" : $"{pythonType} | None";
    }

    private static string GetPythonType(DataType dataType)
    {
        if (StringTypes.Contains(dataType.Name)) return "str";
        if (IntegerTypes.Contains(dataType.Name)) return "int";
        return dataType.Name.ToLowerInvariant() switch
        {
            "boolean" => "bool",
            "decimal" => "Decimal",
            "float" or "double" => "float",
            "datetime" => "datetime",
            "date" => "date",
            "time" => "time",
            "duration" => "timedelta",
            "gyearmonth" => "GYearMonth",
            "gyear" => "GYear",
            "gmonthday" => "GMonthDay",
            "gmonth" => "GMonth",
            "gday" => "GDay",
            "langstring" => "LangString",
            "cogsdate" => "CogsDate",
            _ => dataType.Name,
        };
    }

    private string GetKind(Property property)
    {
        if (property.DataType is ItemType) return "item";
        if (CogsTypes.SimpleTypeNames.Contains(property.DataType.Name, StringComparer.OrdinalIgnoreCase)) return "simple";
        return "object";
    }

    private static bool IsMany(Property property) => property.MaxCardinality != "1";

    private void ValidateModelNames()
    {
        var typeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (DataType dataType in GetOrderedTypes())
        {
            ValidatePythonIdentifier(dataType.Name, "datatype");
            if (!typeNames.Add(dataType.Name))
            {
                throw new InvalidOperationException($"COGS datatype name '{dataType.Name}' is duplicated.");
            }
            if (RuntimeTypeNames.Contains(dataType.Name))
            {
                throw new InvalidOperationException(
                    $"COGS datatype name '{dataType.Name}' conflicts with the generated Python runtime.");
            }
            var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
            IEnumerable<Property> inherited = dataType.ParentTypes.SelectMany(x => x.Properties);
            foreach (Property property in inherited.Concat(dataType.Properties))
            {
                string normalized = ToSnakeCase(property.Name);
                if (RuntimeAttributeNames.Contains(normalized))
                {
                    throw new InvalidOperationException(
                        $"Property '{property.Name}' on '{dataType.Name}' conflicts with generated Python member '{normalized}'.");
                }
                if (attributes.TryGetValue(normalized, out string? existing))
                {
                    throw new InvalidOperationException(
                        $"Properties '{existing}' and '{property.Name}' on '{dataType.Name}' both normalize to '{normalized}'.");
                }
                attributes[normalized] = property.Name;
            }
        }
    }

    private static void ValidatePythonIdentifier(string value, string kind)
    {
        if (!Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)
            || PythonKeywords.Contains(value))
        {
            throw new InvalidOperationException($"COGS {kind} name '{value}' is not a valid Python identifier.");
        }
    }

    internal static string ToSnakeCase(string value)
    {
        string normalized = Regex.Replace(value, @"([A-Z]+)([A-Z][a-z])", "$1_$2", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"([a-z0-9])([A-Z])", "$1_$2", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"[^A-Za-z0-9_]", "_", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"_+", "_", RegexOptions.CultureInvariant).Trim('_').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"Property name '{value}' cannot be normalized to a Python attribute.");
        }
        if (char.IsDigit(normalized[0])) normalized = "field_" + normalized;
        if (PythonKeywords.Contains(normalized)) normalized += "_";
        return normalized;
    }

    private static string NormalizeModuleName(string slug)
    {
        string normalized = Regex.Replace(slug.ToLowerInvariant(), @"[^a-z0-9_]", "_", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"_+", "_", RegexOptions.CultureInvariant).Trim('_');
        if (string.IsNullOrWhiteSpace(normalized)) normalized = "cogs_model";
        if (char.IsDigit(normalized[0])) normalized = "cogs_" + normalized;
        if (PythonKeywords.Contains(normalized)) normalized += "_model";
        return normalized;
    }

    private static string NormalizeDistributionName(string slug)
    {
        string normalized = Regex.Replace(slug.ToLowerInvariant(), @"[^a-z0-9]+", "-", RegexOptions.CultureInvariant).Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "cogs-model" : normalized;
    }

    private static string Quote(string? value)
    {
        return System.Text.Json.JsonSerializer.Serialize(value ?? string.Empty);
    }

    private static string PythonBool(bool value) => value ? "True" : "False";

    private void AppendHeader(StringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(model.HeaderInclude)) return;
        foreach (string line in model.HeaderInclude.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            builder.Append("# ").AppendLine(line);
        }
        builder.AppendLine();
    }
}
