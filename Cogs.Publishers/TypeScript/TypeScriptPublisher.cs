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

namespace Cogs.Publishers.TypeScript;

public sealed class TypeScriptPublisher
{
    private const string RuntimeResourceName = "Cogs.Publishers.TypeScript.Runtime.ts";

    private static readonly HashSet<string> TypeScriptKeywords = new(StringComparer.Ordinal)
    {
        "break", "case", "catch", "class", "const", "continue", "debugger", "default",
        "delete", "do", "else", "enum", "export", "extends", "false", "finally", "for",
        "function", "if", "import", "in", "instanceof", "new", "null", "return", "super",
        "switch", "this", "throw", "true", "try", "typeof", "var", "void", "while", "with",
        "as", "implements", "interface", "let", "package", "private", "protected", "public",
        "static", "yield", "any", "boolean", "constructor", "declare", "get", "module",
        "require", "number", "set", "string", "symbol", "type", "from", "of", "unknown",
        "never", "object", "readonly", "keyof", "namespace", "abstract", "async", "await",
    };

    private static readonly HashSet<string> RuntimeTypeNames = new(StringComparer.Ordinal)
    {
        "CogsDate", "CogsDateOnly", "CogsDateTime", "CogsDecimal", "CogsDuration", "CogsItem",
        "CogsTime", "CogsValue", "CogsConstructor", "CogsDateKind", "CogsDateValue", "Context",
        "DecimalParts", "Document", "DOMImplementation", "DOMParser", "Element", "FieldSpec", "GDay",
        "GMonth", "GMonthDay", "GYear", "GYearMonth", "IdentificationField", "ItemContainer",
        "JsonNumber", "JsonObject", "LangString", "Node", "PathLike", "Readable", "Writable", "XMLSerializer",
    };

    private static readonly HashSet<string> RuntimeMemberNames = new(StringComparer.Ordinal)
    {
        "constructor", "fromElement", "fromJson", "fromObject", "fromXml", "toElement", "toJson",
        "toObject", "toReferenceObject", "toXml", "cogsType", "declaredFields", "emitTypeField",
        "isAbstract", "isItem",
    };

    private static readonly HashSet<string> StringTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "string", "language", "anyURI",
    };

    private readonly CogsModel model;

    public string TargetDirectory { get; }
    public string? TargetNamespace { get; set; }
    public bool Overwrite { get; set; }

    public TypeScriptPublisher(CogsModel model, string targetDirectory)
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
        string packageName = NormalizePackageName(model.Settings.Slug);
        string version = NormalizePackageVersion(model.Settings.Version);
        string targetNamespace = TargetNamespace ?? model.Settings.NamespaceUrl;
        if (string.IsNullOrWhiteSpace(targetNamespace))
        {
            throw new InvalidOperationException("An XML target namespace must be specified.");
        }

        string namespacePrefix = string.IsNullOrWhiteSpace(model.Settings.NamespacePrefix)
            ? "model"
            : model.Settings.NamespacePrefix;
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

        if (Directory.Exists(TargetDirectory))
        {
            if (!Overwrite)
            {
                throw new InvalidOperationException("Target directory already exists.");
            }
            Directory.Delete(TargetDirectory, true);
        }

        string sourceDirectory = Path.Combine(TargetDirectory, "src");
        Directory.CreateDirectory(sourceDirectory);
        var utf8 = new UTF8Encoding(false);
        File.WriteAllText(Path.Combine(TargetDirectory, "package.json"), GeneratePackageJson(packageName, version), utf8);
        File.WriteAllText(Path.Combine(TargetDirectory, "tsconfig.json"), GenerateTsConfig(), utf8);
        File.WriteAllText(Path.Combine(sourceDirectory, "index.ts"), GenerateIndex(), utf8);
        File.WriteAllText(Path.Combine(sourceDirectory, "model.ts"), GenerateModel(targetNamespace, namespacePrefix), utf8);
    }

    private string GeneratePackageJson(string packageName, string version)
    {
        string description = string.IsNullOrWhiteSpace(model.Settings.Description)
            ? model.Settings.ShortTitle
            : model.Settings.Description;
        return $$"""
            {
              "name": {{Quote(packageName)}},
              "version": {{Quote(version)}},
              "description": {{Quote(description)}},
              "type": "module",
              "sideEffects": false,
              "engines": {
                "node": ">=22"
              },
              "exports": {
                ".": {
                  "types": "./dist/index.d.ts",
                  "import": "./dist/index.js"
                }
              },
              "files": [
                "dist"
              ],
              "scripts": {
                "build": "tsc -p tsconfig.json"
              },
              "dependencies": {
                "@xmldom/xmldom": "^0.9.10"
              },
              "devDependencies": {
                "@types/node": "^22.0.0",
                "typescript": "^6.0.0"
              }
            }
            """;
    }

    private static string GenerateTsConfig()
    {
        return """
            {
              "compilerOptions": {
                "target": "ES2022",
                "module": "NodeNext",
                "moduleResolution": "NodeNext",
                "rootDir": "src",
                "outDir": "dist",
                "strict": true,
                "declaration": true,
                "declarationMap": true,
                "sourceMap": true,
                "verbatimModuleSyntax": true,
                "exactOptionalPropertyTypes": true,
                "noUncheckedIndexedAccess": true,
                "noImplicitOverride": true,
                "skipLibCheck": true,
                "types": ["node"]
              },
              "include": ["src/**/*.ts"]
            }
            """;
    }

    private string GenerateIndex()
    {
        var builder = new StringBuilder();
        AppendHeader(builder);
        builder.AppendLine("export * from \"./model.js\";");
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
            .Replace("__IDENTIFICATION_FIELDS__", GetIdentificationLiteral(), StringComparison.Ordinal);

        var builder = new StringBuilder();
        builder.AppendLine("// Generated by COGS. Do not edit by hand.");
        AppendHeader(builder);
        builder.Append(runtime.TrimStart());
        builder.AppendLine();

        foreach (DataType dataType in GetOrderedTypes())
        {
            AppendDataType(builder, dataType);
        }

        builder.AppendLine("registerTypes(");
        builder.AppendLine("  [");
        foreach (ItemType item in model.ItemTypes.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            builder.AppendLine($"    [{Quote(item.Name)}, {item.Name}],");
        }
        builder.AppendLine("  ],");
        builder.AppendLine("  [");
        foreach (DataType dataType in GetOrderedTypes().OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            builder.AppendLine($"    [{Quote(dataType.Name)}, {dataType.Name}],");
        }
        builder.AppendLine("  ],");
        builder.AppendLine(");");
        return builder.ToString();
    }

    private void AppendDataType(StringBuilder builder, DataType dataType)
    {
        string baseType = string.IsNullOrWhiteSpace(dataType.ExtendsTypeName)
            ? dataType is ItemType ? "CogsItem" : "CogsValue"
            : dataType.ExtendsTypeName;
        string abstractModifier = dataType.IsAbstract ? "abstract " : string.Empty;
        AppendJsDoc(builder, dataType.Description);
        builder.AppendLine($"export {abstractModifier}class {dataType.Name} extends {baseType} {{");
        builder.AppendLine($"  static override readonly cogsType: string = {Quote(dataType.Name)};");
        builder.AppendLine($"  static override readonly isAbstract: boolean = {TsBool(dataType.IsAbstract)};");
        if (dataType is not ItemType)
        {
            builder.AppendLine($"  static override readonly emitTypeField: boolean = {TsBool(dataType.IsSubstitute)};");
        }
        builder.AppendLine("  static override readonly declaredFields: readonly FieldSpec[] = [");
        foreach (Property property in dataType.Properties)
        {
            builder.AppendLine("    {");
            builder.AppendLine($"      cogsName: {Quote(property.Name)},");
            builder.AppendLine($"      attributeName: {Quote(ToCamelCase(property.Name))},");
            builder.AppendLine($"      description: {Quote(property.Description)},");
            builder.AppendLine($"      typeName: {Quote(property.DataType.Name)},");
            builder.AppendLine($"      kind: {Quote(GetKind(property))},");
            builder.AppendLine($"      many: {TsBool(IsMany(property))},");
            builder.AppendLine($"      ordered: {TsBool(property.Ordered)},");
            builder.AppendLine($"      allowSubtypes: {TsBool(property.AllowSubtypes)},");
            builder.AppendLine("    },");
        }
        builder.AppendLine("  ];");
        if (dataType.Properties.Count > 0)
        {
            builder.AppendLine();
        }
        foreach (Property property in dataType.Properties)
        {
            AppendJsDoc(builder, property.Description, "  ");
            string attributeName = ToCamelCase(property.Name);
            string typeName = GetTypeScriptType(property.DataType);
            if (IsMany(property))
            {
                builder.AppendLine($"  {attributeName}: {typeName}[] = [];");
            }
            else
            {
                builder.AppendLine($"  {attributeName}: {typeName} | undefined;");
            }
        }
        builder.AppendLine();
        builder.AppendLine($"  constructor(initial: Partial<{dataType.Name}> = {{}}) {{");
        builder.AppendLine("    super();");
        builder.AppendLine("    Object.assign(this, initial);");
        builder.AppendLine("  }");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private IEnumerable<DataType> GetOrderedTypes()
    {
        return model.ReusableDataTypes
            .Concat<DataType>(model.ItemTypes)
            .OrderBy(x => x.ParentTypes.Count)
            .ThenBy(x => x.Name, StringComparer.Ordinal);
    }

    private string GetIdentificationLiteral()
    {
        return "[" + string.Join(", ", model.Identification.Select(x =>
            $"{{ cogsName: {Quote(x.Name)}, attributeName: {Quote(ToCamelCase(x.Name))} }}")) + "]";
    }

    private static string GetTypeScriptType(DataType dataType)
    {
        if (StringTypes.Contains(dataType.Name)) return "string";
        return dataType.Name.ToLowerInvariant() switch
        {
            "boolean" => "boolean",
            "int" => "number",
            "nonpositiveinteger" or "negativeinteger" or "long" or "nonnegativeinteger"
                or "unsignedlong" or "positiveinteger" => "bigint",
            "float" or "double" => "number",
            "decimal" => "CogsDecimal",
            "datetime" => "CogsDateTime",
            "date" => "CogsDateOnly",
            "time" => "CogsTime",
            "duration" => "CogsDuration",
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
            ValidateTypeScriptIdentifier(dataType.Name, "datatype");
            if (!typeNames.Add(dataType.Name))
            {
                throw new InvalidOperationException($"COGS datatype name '{dataType.Name}' is duplicated.");
            }
            if (RuntimeTypeNames.Contains(dataType.Name))
            {
                throw new InvalidOperationException(
                    $"COGS datatype name '{dataType.Name}' conflicts with the generated TypeScript runtime.");
            }

            var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
            IEnumerable<Property> inherited = dataType.ParentTypes.SelectMany(x => x.Properties);
            foreach (Property property in inherited.Concat(dataType.Properties))
            {
                string normalized = ToCamelCase(property.Name);
                if (RuntimeMemberNames.Contains(normalized))
                {
                    throw new InvalidOperationException(
                        $"Property '{property.Name}' on '{dataType.Name}' conflicts with generated TypeScript member '{normalized}'.");
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

    private static void ValidateTypeScriptIdentifier(string value, string kind)
    {
        if (!Regex.IsMatch(value, @"^[A-Za-z_$][A-Za-z0-9_$]*$", RegexOptions.CultureInvariant)
            || TypeScriptKeywords.Contains(value))
        {
            throw new InvalidOperationException($"COGS {kind} name '{value}' is not a valid TypeScript identifier.");
        }
    }

    internal static string ToCamelCase(string value)
    {
        string[] words = Regex.Matches(value, @"[A-Z]+(?=[A-Z][a-z]|\d|$)|[A-Z]?[a-z]+|\d+", RegexOptions.CultureInvariant)
            .Select(x => x.Value)
            .ToArray();
        if (words.Length == 0)
        {
            throw new InvalidOperationException($"Property name '{value}' cannot be normalized to a TypeScript member.");
        }
        string normalized = words[0].ToLowerInvariant()
            + string.Concat(words.Skip(1).Select(x => char.ToUpperInvariant(x[0]) + x[1..].ToLowerInvariant()));
        if (char.IsDigit(normalized[0])) normalized = "field" + normalized;
        if (!Regex.IsMatch(normalized, @"^[A-Za-z_$][A-Za-z0-9_$]*$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException($"Property name '{value}' cannot be normalized to a TypeScript member.");
        }
        if (TypeScriptKeywords.Contains(normalized)) normalized += "_";
        return normalized;
    }

    internal static string NormalizePackageName(string slug)
    {
        string normalized = Regex.Replace((slug ?? string.Empty).ToLowerInvariant(), @"[^a-z0-9._-]+", "-", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"-+", "-", RegexOptions.CultureInvariant).Trim('-', '.', '_');
        if (string.IsNullOrWhiteSpace(normalized)) normalized = "cogs-model";
        if (normalized.Length > 214)
        {
            throw new InvalidOperationException("The normalized npm package name exceeds 214 characters.");
        }
        if (normalized is "node_modules" or "favicon.ico") normalized = "cogs-" + normalized.Replace('.', '-');
        return normalized;
    }

    internal static string NormalizePackageVersion(string version)
    {
        string value = (version ?? string.Empty).Trim();
        Match match = Regex.Match(value,
            @"^(0|[1-9]\d*)(?:\.(0|[1-9]\d*))?(?:\.(0|[1-9]\d*))?(?:(a|b|rc)(0|[1-9]\d*))?(?:-([0-9A-Za-z.-]+))?(?:\+([0-9A-Za-z.-]+))?$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Model version '{version}' cannot be safely normalized to npm SemVer.");
        }
        string major = match.Groups[1].Value;
        string minor = match.Groups[2].Success ? match.Groups[2].Value : "0";
        string patch = match.Groups[3].Success ? match.Groups[3].Value : "0";
        string prerelease = string.Empty;
        if (match.Groups[4].Success)
        {
            prerelease = "-" + match.Groups[4].Value.ToLowerInvariant() + "." + match.Groups[5].Value;
        }
        else if (match.Groups[6].Success)
        {
            string[] identifiers = match.Groups[6].Value.Split('.');
            if (identifiers.Any(x => string.IsNullOrWhiteSpace(x)
                || (x.All(char.IsDigit) && x.Length > 1 && x[0] == '0')))
            {
                throw new InvalidOperationException($"Model version '{version}' cannot be safely normalized to npm SemVer.");
            }
            prerelease = "-" + match.Groups[6].Value;
        }
        if (match.Groups[7].Success && match.Groups[7].Value.Split('.').Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"Model version '{version}' cannot be safely normalized to npm SemVer.");
        }
        string build = match.Groups[7].Success ? "+" + match.Groups[7].Value : string.Empty;
        return $"{major}.{minor}.{patch}{prerelease}{build}";
    }

    private static string Quote(string? value) => System.Text.Json.JsonSerializer.Serialize(value ?? string.Empty);
    private static string TsBool(bool value) => value ? "true" : "false";

    private static void AppendJsDoc(StringBuilder builder, string? text, string indent = "")
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        builder.Append(indent).AppendLine("/**");
        foreach (string line in text.Replace("*/", "* /", StringComparison.Ordinal)
                     .Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            builder.Append(indent).Append(" * ").AppendLine(line);
        }
        builder.Append(indent).AppendLine(" */");
    }

    private void AppendHeader(StringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(model.HeaderInclude)) return;
        foreach (string line in model.HeaderInclude.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            builder.Append("// ").AppendLine(line);
        }
        builder.AppendLine();
    }
}
