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
        "isAbstract", "isDefined", "isItem",
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

        DirectoryPublication.Publish(TargetDirectory, Overwrite, PublishToDirectory, model.SourceDirectory);
    }

    private void PublishToDirectory(string targetDirectory)
    {

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
            || namespacePrefix.Equals("xmlns", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"XML namespace prefix '{namespacePrefix}' is reserved.");
        }

        string sourceDirectory = Path.Combine(targetDirectory, "src");
        Directory.CreateDirectory(sourceDirectory);
        var utf8 = new UTF8Encoding(false);
        File.WriteAllText(Path.Combine(targetDirectory, "package.json"), GeneratePackageJson(packageName, version), utf8);
        File.WriteAllText(Path.Combine(targetDirectory, "tsconfig.json"), GenerateTsConfig(), utf8);
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
              "cogs": {
                "cogsVersion": "2.0",
                "modelVersion": {{Quote(model.Settings.Version)}}
              },
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
            builder.AppendLine($"    [{Quote(item.Name)}, {ToPascalCase(item.Name)}],");
        }
        builder.AppendLine("  ],");
        builder.AppendLine("  [");
        foreach (DataType dataType in GetOrderedTypes().OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            builder.AppendLine($"    [{Quote(dataType.Name)}, {ToPascalCase(dataType.Name)}],");
        }
        builder.AppendLine("  ],");
        builder.AppendLine(");");
        return builder.ToString();
    }

    private void AppendDataType(StringBuilder builder, DataType dataType)
    {
        string baseType = string.IsNullOrWhiteSpace(dataType.ExtendsTypeName)
            ? dataType is ItemType ? "CogsItem" : "CogsValue"
            : ToPascalCase(dataType.ExtendsTypeName);
        string className = ToPascalCase(dataType.Name);
        string abstractModifier = dataType.IsAbstract ? "abstract " : string.Empty;
        AppendJsDoc(builder, dataType.Description);
        builder.AppendLine($"export {abstractModifier}class {className} extends {baseType} {{");
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
            builder.AppendLine($"      allowSubtypes: {TsBool(CogsTypeSystem.AllowsSubtypes(property))},");
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
        builder.AppendLine($"  constructor(initial: Partial<{className}> = {{}}) {{");
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
            _ => ToPascalCase(dataType.Name),
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
        var typeNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DataType dataType in GetOrderedTypes())
        {
            string className = ToPascalCase(dataType.Name);
            ValidateTypeScriptIdentifier(className, "datatype");
            if (typeNames.TryGetValue(className, out var existingType))
            {
                throw new InvalidOperationException(
                    $"COGS datatype names '{existingType}' and '{dataType.Name}' both normalize to TypeScript class '{className}'.");
            }
            typeNames[className] = dataType.Name;
            if (RuntimeTypeNames.Contains(className))
            {
                throw new InvalidOperationException(
                    $"COGS datatype name '{dataType.Name}' conflicts with the generated TypeScript runtime.");
            }

            var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Property property in CogsTypeSystem.EffectiveProperties(dataType))
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
        if (!Regex.IsMatch(value, @"^[\p{L}_$][\p{L}\p{M}\p{Nd}_$]*$", RegexOptions.CultureInvariant)
            || TypeScriptKeywords.Contains(value))
        {
            throw new InvalidOperationException($"COGS {kind} name '{value}' is not a valid TypeScript identifier.");
        }
    }

    internal static string ToPascalCase(string value)
    {
        string normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormC);
        string[] segments = Regex.Split(normalized, @"[^\p{L}\p{M}\p{Nd}_$]+", RegexOptions.CultureInvariant)
            .Where(segment => segment.Length > 0)
            .ToArray();
        if (segments.Length == 0)
        {
            throw new InvalidOperationException($"Datatype name '{value}' cannot be normalized to a TypeScript class.");
        }
        string result = string.Concat(segments.Select(UppercaseFirstRune));
        if (TypeScriptKeywords.Contains(result)) result += "Type";
        ValidateTypeScriptIdentifier(result, "datatype");
        return result;
    }

    internal static string ToCamelCase(string value)
    {
        string normalizedInput = (value ?? string.Empty).Normalize(NormalizationForm.FormC);
        string[] words;
        bool ascii = Regex.IsMatch(normalizedInput, @"^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant);
        if (ascii)
        {
            words = Regex.Matches(normalizedInput, @"[A-Z]+(?=[A-Z][a-z]|\d|$)|[A-Z]?[a-z]+|\d+", RegexOptions.CultureInvariant)
                .Select(x => x.Value)
                .ToArray();
        }
        else
        {
            words = Regex.Split(normalizedInput, @"[^\p{L}\p{M}\p{Nd}]+", RegexOptions.CultureInvariant)
                .Where(word => word.Length > 0)
                .ToArray();
        }
        if (words.Length == 0)
        {
            throw new InvalidOperationException($"Property name '{value}' cannot be normalized to a TypeScript member.");
        }
        string normalized = ascii
            ? words[0].ToLowerInvariant()
                + string.Concat(words.Skip(1).Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()))
            : LowercaseFirstRune(words[0]) + string.Concat(words.Skip(1).Select(UppercaseFirstRune));
        if (Rune.GetRuneAt(normalized, 0).Value is >= '0' and <= '9') normalized = "field" + normalized;
        if (!Regex.IsMatch(normalized, @"^[\p{L}_$][\p{L}\p{M}\p{Nd}_$]*$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException($"Property name '{value}' cannot be normalized to a TypeScript member.");
        }
        if (TypeScriptKeywords.Contains(normalized)) normalized += "_";
        return normalized;
    }

    private static string UppercaseFirstRune(string value)
    {
        Rune first = Rune.GetRuneAt(value, 0);
        return Rune.ToUpperInvariant(first).ToString() + value[first.Utf16SequenceLength..];
    }

    private static string LowercaseFirstRune(string value)
    {
        Rune first = Rune.GetRuneAt(value, 0);
        return Rune.ToLowerInvariant(first).ToString() + value[first.Utf16SequenceLength..];
    }

    internal static string NormalizePackageName(string slug)
    {
        string normalized = slug ?? string.Empty;
        if (!Regex.IsMatch(normalized, @"^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException($"Model slug '{slug}' must match [a-z][a-z0-9_]*.");
        }
        if (normalized.Length > 214)
        {
            throw new InvalidOperationException("The normalized npm package name exceeds 214 characters.");
        }
        if (normalized is "node_modules" or "favicon")
        {
            throw new InvalidOperationException($"Model slug '{slug}' is reserved by npm.");
        }
        return normalized;
    }

    internal static string NormalizePackageVersion(string version)
    {
        string value = (version ?? string.Empty).Trim();
        if (!CogsConventions.IsCanonicalSemVer(value))
        {
            throw new InvalidOperationException($"Model version '{version}' must be canonical SemVer 2.0.");
        }
        return value;
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
