using Cogs.DataAnnotations;
using Cogs.SimpleTypes;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using VDS.RDF;

namespace __CogsGeneratedNamespace
{
    public static class CogsModelMetadata
    {
        public const string CogsVersion = "__CogsVersionLiteral__";
        public const string ModelVersion = "__CogsModelVersionLiteral__";
        public const string Slug = "__CogsSlugLiteral__";
    }

    internal readonly struct CogsIdentityKey : IEquatable<CogsIdentityKey>
    {
        internal CogsIdentityKey(Type concreteType, IReadOnlyList<string> values)
        {
            ConcreteType = concreteType;
            Values = values.ToArray();
        }

        internal Type ConcreteType { get; }
        internal string[] Values { get; }
        public bool Equals(CogsIdentityKey other) =>
            ConcreteType == other.ConcreteType && Values.SequenceEqual(other.Values, StringComparer.Ordinal);
        public override bool Equals(object? obj) => obj is CogsIdentityKey other && Equals(other);
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(ConcreteType);
            foreach (string value in Values) hash.Add(value, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }

    internal sealed class CogsObjectState
    {
        internal bool IsDefined { get; set; }
    }

    internal sealed class CogsIdentityMap
    {
        private readonly Dictionary<CogsIdentityKey, IIdentifiable> items = new();
        private readonly ConditionalWeakTable<IIdentifiable, CogsObjectState> states;

        internal CogsIdentityMap(ConditionalWeakTable<IIdentifiable, CogsObjectState> states) => this.states = states;

        internal IIdentifiable GetOrCreate(Type concreteType, IReadOnlyList<string> lexicalValues, bool definition)
        {
            var key = new CogsIdentityKey(concreteType, lexicalValues);
            if (items.TryGetValue(key, out IIdentifiable? existing))
            {
                CogsObjectState state = states.GetOrCreateValue(existing);
                if (definition && state.IsDefined)
                    throw new JsonException($"Duplicate definition of '{concreteType.Name}' with the same identification tuple.");
                if (definition) state.IsDefined = true;
                return existing;
            }

            if (concreteType.IsAbstract || Activator.CreateInstance(concreteType) is not IIdentifiable created)
                throw new JsonException($"Type '{concreteType.Name}' cannot be instantiated as an item.");
            items.Add(key, created);
            states.GetOrCreateValue(created).IsDefined = definition;
            return created;
        }
    }

    public static class CogsIdentity
    {
        /// <summary>
        /// Returns an unambiguous display form of the structured COGS identity tuple.
        /// Runtime identity matching uses <see cref="CogsIdentityKey"/> directly.
        /// </summary>
        public static string Format(IIdentifiable item)
        {
            ArgumentNullException.ThrowIfNull(item);
            CogsTypeAttribute type = CogsReflection.GetTypeContract(item.GetType());
            var result = new StringBuilder(type.Name.Length + 16);
            Append(result, type.Name);
            foreach (string lexical in GetLexicalValues(item)) Append(result, lexical);
            return result.ToString();
        }

        internal static CogsIdentityKey GetKey(IIdentifiable item) =>
            new(item.GetType(), GetLexicalValues(item));

        internal static IReadOnlyList<string> GetLexicalValues(IIdentifiable item)
        {
            IReadOnlyList<CogsPropertyMetadata> metadata = CogsReflection.GetIdentification(item.GetType());
            if (metadata.Count == 0)
                throw new InvalidOperationException($"Item type '{item.GetType().Name}' has no identification metadata.");
            var values = new List<string>(metadata.Count);
            foreach (CogsPropertyMetadata identity in metadata)
            {
                object? value = identity.Property.GetValue(item);
                string lexical = value switch
                {
                    Uri uri => uri.OriginalString,
                    string text => text,
                    null => throw new InvalidOperationException($"Identity field '{identity.Contract.Name}' is required."),
                    _ => throw new InvalidOperationException($"Identity field '{identity.Contract.Name}' must be string or anyURI."),
                };
                if (lexical.Length == 0)
                    throw new InvalidOperationException($"Identity field '{identity.Contract.Name}' is required and nonempty.");
                values.Add(lexical);
            }
            return values;
        }

        private static void Append(StringBuilder builder, string value) =>
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
    }

    internal sealed record CogsPropertyMetadata(PropertyInfo Property, CogsPropertyAttribute Contract, Type ValueType, bool IsList);

    internal static class CogsReflection
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<CogsPropertyMetadata>> PropertyCache = new();
        private static readonly Lazy<IReadOnlyDictionary<string, Type>> Types = new(BuildTypes);

        internal static CogsTypeAttribute GetTypeContract(Type type) =>
            type.GetCustomAttribute<CogsTypeAttribute>(inherit: false)
            ?? throw new InvalidOperationException($"Generated type '{type.FullName}' has no COGS type metadata.");

        internal static Type ResolveType(string name, bool item)
        {
            if (!Types.Value.TryGetValue(name, out Type? type))
                throw new JsonException($"Unknown COGS type discriminator '{name}'.");
            CogsTypeAttribute contract = GetTypeContract(type);
            if (contract.IsItem != item)
                throw new JsonException($"COGS type '{name}' is not a{(item ? "n item" : " composite")} type.");
            if (contract.IsAbstract || type.IsAbstract)
                throw new JsonException($"Abstract COGS type '{name}' cannot be instantiated.");
            return type;
        }

        internal static IReadOnlyList<CogsPropertyMetadata> GetProperties(Type type) =>
            PropertyCache.GetOrAdd(type, BuildProperties);

        internal static IReadOnlyList<CogsPropertyMetadata> GetIdentification(Type type) =>
            GetProperties(type).Where(x => x.Contract.IsIdentification).ToArray();

        private static IReadOnlyDictionary<string, Type> BuildTypes()
        {
            var result = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (Type type in typeof(ItemContainer).Assembly.GetTypes())
            {
                CogsTypeAttribute? contract = type.GetCustomAttribute<CogsTypeAttribute>(inherit: false);
                if (contract is not null && !result.TryAdd(contract.Name, type))
                    throw new InvalidOperationException($"Generated COGS type name '{contract.Name}' is duplicated.");
            }
            return result;
        }

        private static IReadOnlyList<CogsPropertyMetadata> BuildProperties(Type type)
        {
            var hierarchy = new Stack<Type>();
            for (Type? current = type; current is not null && current != typeof(object); current = current.BaseType)
                hierarchy.Push(current);

            var result = new List<CogsPropertyMetadata>();
            while (hierarchy.Count > 0)
            {
                Type current = hierarchy.Pop();
                foreach (PropertyInfo property in current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    CogsPropertyAttribute? contract = property.GetCustomAttribute<CogsPropertyAttribute>(inherit: false);
                    if (contract is null) continue;
                    bool isList = property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>);
                    Type valueType = isList ? property.PropertyType.GetGenericArguments()[0] : Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    result.Add(new CogsPropertyMetadata(property, contract, valueType, isList));
                }
                result.Sort((left, right) => left.Contract.Order.CompareTo(right.Contract.Order));
            }
            return result;
        }
    }

    internal static class CogsPrimitiveCodec
    {
        internal static object ReadJson(JsonElement element, string dataType, Type targetType)
        {
            try
            {
                return dataType switch
                {
                    "string" => RequireString(element, dataType),
                    "language" => ReadLanguage(element),
                    "anyURI" => ReadUri(element),
                    "boolean" => element.ValueKind is JsonValueKind.True or JsonValueKind.False ? element.GetBoolean() : throw Expected(dataType),
                    "int" => ReadInt32(element),
                    "long" => ReadInt64(element),
                    "unsignedLong" => ReadUInt64(element),
                    "nonPositiveInteger" or "negativeInteger" or "nonNegativeInteger" or "positiveInteger" => ReadBigInteger(element, dataType),
                    "float" => ReadFiniteSingle(element),
                    "double" => ReadFiniteDouble(element),
                    "decimal" => new CogsDecimal(RequireNumber(element, dataType)),
                    "dateTime" => new CogsDateTime(RequireString(element, dataType)),
                    "date" => new CogsDateOnly(RequireString(element, dataType)),
                    "time" => new CogsTime(RequireString(element, dataType)),
                    "duration" => new CogsDuration(RequireString(element, dataType)),
                    "gYear" => ReadGYear(element),
                    "gYearMonth" => ReadGYearMonth(element),
                    "gMonthDay" => ReadGMonthDay(element),
                    "gDay" => ReadGDay(element),
                    "gMonth" => ReadGMonth(element),
                    "langString" => ReadLangString(element),
                    "cogsDate" => ReadCogsDate(element),
                    _ => throw new JsonException($"Unsupported generated primitive '{dataType}'."),
                };
            }
            catch (JsonException) { throw; }
            catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
            {
                throw new JsonException($"Malformed {dataType} value.", exception);
            }
        }

        internal static object ReadXml(string lexical, string dataType, Type targetType, XAttribute? languageAttribute = null)
        {
            try
            {
                return dataType switch
                {
                    "string" => lexical,
                    "language" => XsdLanguage(lexical),
                    "anyURI" => ReadUriLexical(lexical),
                    "boolean" => XmlConvert.ToBoolean(lexical),
                    "int" => XmlConvert.ToInt32(lexical),
                    "long" => XmlConvert.ToInt64(lexical),
                    "unsignedLong" => XmlConvert.ToUInt64(lexical),
                    "nonPositiveInteger" or "negativeInteger" or "nonNegativeInteger" or "positiveInteger" => ReadBigIntegerLexical(lexical, dataType),
                    "float" => Finite(XmlConvert.ToSingle(lexical)),
                    "double" => Finite(XmlConvert.ToDouble(lexical)),
                    "decimal" => new CogsDecimal(lexical),
                    "dateTime" => new CogsDateTime(lexical),
                    "date" => new CogsDateOnly(lexical),
                    "time" => new CogsTime(lexical),
                    "duration" => new CogsDuration(lexical),
                    "gYear" => new GYear(lexical),
                    "gYearMonth" => new GYearMonth(lexical),
                    "gMonthDay" => new GMonthDay(lexical),
                    "gDay" => new GDay(lexical),
                    "gMonth" => new GMonth(lexical),
                    "langString" => new LangString(languageAttribute?.Value ?? throw new XmlException("langString requires xml:lang."), lexical),
                    "cogsDate" => ReadCogsDateLexical(lexical),
                    _ => throw new XmlException($"Unsupported generated primitive '{dataType}'."),
                };
            }
            catch (XmlException) { throw; }
            catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
            {
                throw new XmlException($"Malformed {dataType} value '{lexical}'.", exception);
            }
        }

        internal static void WriteJson(Utf8JsonWriter writer, object value, string dataType)
        {
            switch (dataType)
            {
                case "string": writer.WriteStringValue((string)value); return;
                case "language": writer.WriteStringValue(XsdLanguage((string)value)); return;
                case "anyURI": writer.WriteStringValue(ValidateUri((Uri)value)); return;
                case "boolean": writer.WriteBooleanValue((bool)value); return;
                case "int": writer.WriteNumberValue((int)value); return;
                case "long": writer.WriteNumberValue((long)value); return;
                case "unsignedLong": writer.WriteNumberValue((ulong)value); return;
                case "nonPositiveInteger": case "negativeInteger": case "nonNegativeInteger": case "positiveInteger":
                    writer.WriteRawValue(((BigInteger)value).ToString(CultureInfo.InvariantCulture), skipInputValidation: false); return;
                case "float": writer.WriteNumberValue(Finite((float)value)); return;
                case "double": writer.WriteNumberValue(Finite((double)value)); return;
                case "decimal": writer.WriteRawValue(((CogsDecimal)value).LexicalValue, skipInputValidation: false); return;
                case "dateTime": case "date": case "time": case "duration":
                    writer.WriteStringValue(((IXsdLexicalValue)value).LexicalValue); return;
                case "gYear": WriteGYear(writer, (GYear)value); return;
                case "gYearMonth": WriteGYearMonth(writer, (GYearMonth)value); return;
                case "gMonthDay": WriteGMonthDay(writer, (GMonthDay)value); return;
                case "gDay": WriteGDay(writer, (GDay)value); return;
                case "gMonth": WriteGMonth(writer, (GMonth)value); return;
                case "langString":
                    var lang = (LangString)value;
                    writer.WriteStartObject(); writer.WriteString("@language", lang.LanguageTag); writer.WriteString("@value", lang.Value); writer.WriteEndObject(); return;
                case "cogsDate": WriteCogsDate(writer, (CogsDate)value); return;
                default: throw new JsonException($"Unsupported generated primitive '{dataType}'.");
            }
        }

        internal static string WriteXml(object value, string dataType) => dataType switch
        {
            "language" => XsdLanguage((string)value),
            "anyURI" => ValidateUri((Uri)value),
            "boolean" => XmlConvert.ToString((bool)value),
            "int" => XmlConvert.ToString((int)value),
            "long" => XmlConvert.ToString((long)value),
            "unsignedLong" => XmlConvert.ToString((ulong)value),
            "float" => XmlConvert.ToString(Finite((float)value)),
            "double" => XmlConvert.ToString(Finite((double)value)),
            "nonPositiveInteger" or "negativeInteger" or "nonNegativeInteger" or "positiveInteger" => ((BigInteger)value).ToString(CultureInfo.InvariantCulture),
            "decimal" => ((CogsDecimal)value).LexicalValue,
            "dateTime" or "date" or "time" or "duration" or "gYear" or "gYearMonth" or "gMonthDay" or "gDay" or "gMonth" => ((IXsdLexicalValue)value).LexicalValue,
            "cogsDate" => ((CogsDate)value).ToString() ?? throw new XmlException("cogsDate has no selected value."),
            "langString" => ((LangString)value).Value,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };

        internal static ILiteralNode CreateRdfLiteral(IGraph graph, object value, string dataType)
        {
            ArgumentNullException.ThrowIfNull(graph);
            ArgumentNullException.ThrowIfNull(value);

            if (dataType == "langString")
            {
                var langString = (LangString)value;
                return graph.CreateLiteralNode(langString.Value, langString.LanguageTag);
            }

            string datatypeUri = dataType == "cogsDate"
                ? CogsDateDatatypeUri((CogsDate)value)
                : NamespaceMapper.XMLSCHEMA + dataType;
            return graph.CreateLiteralNode(
                WriteXml(value, dataType),
                UriFactory.Create(datatypeUri));
        }

        private static string CogsDateDatatypeUri(CogsDate value) => value.UsedType switch
        {
            CogsDateType.DateTime => NamespaceMapper.XMLSCHEMA + "dateTime",
            CogsDateType.Date => NamespaceMapper.XMLSCHEMA + "date",
            CogsDateType.GYearMonth => NamespaceMapper.XMLSCHEMA + "gYearMonth",
            CogsDateType.GYear => NamespaceMapper.XMLSCHEMA + "gYear",
            CogsDateType.Duration => NamespaceMapper.XMLSCHEMA + "duration",
            _ => throw new InvalidOperationException("cogsDate requires exactly one selected value arm."),
        };

        private static string RequireString(JsonElement element, string type) =>
            element.ValueKind == JsonValueKind.String ? element.GetString()! : throw Expected(type);
        private static string RequireNumber(JsonElement element, string type) =>
            element.ValueKind == JsonValueKind.Number ? element.GetRawText() : throw Expected(type);
        private static JsonException Expected(string type) => new($"Expected a valid JSON value for COGS type '{type}'.");
        private static string ReadLanguage(JsonElement element) => XsdLanguage(RequireString(element, "language"));
        private static string XsdLanguage(string value)
        {
            _ = new LangString(value, string.Empty);
            return value;
        }
        private static Uri ReadUri(JsonElement element) => ReadUriLexical(RequireString(element, "anyURI"));
        private static Uri ReadUriLexical(string lexical)
        {
            if (!XsdLexical.IsUriReference(lexical)) throw new FormatException($"'{lexical}' is not an RFC 3986 URI reference.");
            return new Uri(lexical, UriKind.RelativeOrAbsolute);
        }
        private static string ValidateUri(Uri value)
        {
            string lexical = value.OriginalString;
            if (!XsdLexical.IsUriReference(lexical)) throw new FormatException($"'{lexical}' is not an RFC 3986 URI reference.");
            return lexical;
        }
        private static BigInteger ReadCanonicalJsonInteger(JsonElement element, string dataType)
        {
            string raw = RequireNumber(element, dataType);
            if (!System.Text.RegularExpressions.Regex.IsMatch(raw, @"^-?(?:0|[1-9][0-9]*)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                throw Expected(dataType);
            return BigInteger.Parse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }
        private static int ReadInt32(JsonElement element)
        {
            BigInteger value = ReadCanonicalJsonInteger(element, "int");
            return value >= int.MinValue && value <= int.MaxValue ? (int)value : throw Expected("int");
        }
        private static long ReadInt64(JsonElement element)
        {
            BigInteger value = ReadCanonicalJsonInteger(element, "long");
            return value >= long.MinValue && value <= long.MaxValue ? (long)value : throw Expected("long");
        }
        private static ulong ReadUInt64(JsonElement element)
        {
            BigInteger value = ReadCanonicalJsonInteger(element, "unsignedLong");
            return value >= ulong.MinValue && value <= ulong.MaxValue ? (ulong)value : throw Expected("unsignedLong");
        }
        private static BigInteger ReadBigInteger(JsonElement element, string dataType)
        {
            BigInteger value = ReadCanonicalJsonInteger(element, dataType);
            bool valid = dataType switch
            {
                "nonPositiveInteger" => value <= 0,
                "negativeInteger" => value < 0,
                "nonNegativeInteger" => value >= 0,
                "positiveInteger" => value > 0,
                _ => false,
            };
            return valid ? value : throw Expected(dataType);
        }
        private static BigInteger ReadBigIntegerLexical(string lexical, string dataType)
        {
            if (!RegexInteger().IsMatch(lexical)) throw new FormatException($"Invalid {dataType} lexical value.");
            BigInteger value = BigInteger.Parse(lexical, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            bool valid = dataType switch
            {
                "nonPositiveInteger" => value <= 0,
                "negativeInteger" => value < 0,
                "nonNegativeInteger" => value >= 0,
                "positiveInteger" => value > 0,
                _ => false,
            };
            return valid ? value : throw new FormatException($"Value is outside the {dataType} domain.");
        }
        private static System.Text.RegularExpressions.Regex RegexInteger() =>
            new(@"^[+-]?[0-9]+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        private static float ReadFiniteSingle(JsonElement element) => element.ValueKind == JsonValueKind.Number ? Finite(element.GetSingle()) : throw Expected("float");
        private static double ReadFiniteDouble(JsonElement element) => element.ValueKind == JsonValueKind.Number ? Finite(element.GetDouble()) : throw Expected("double");
        private static float Finite(float value) => float.IsFinite(value) ? value : throw new FormatException("Non-finite floating point value.");
        private static double Finite(double value) => double.IsFinite(value) ? value : throw new FormatException("Non-finite floating point value.");

        private static LangString ReadLangString(JsonElement element)
        {
            EnsureObject(element, "langString");
            EnsureExactFields(element, "@language", "@value");
            return new LangString(RequireString(element.GetProperty("@language"), "language"), RequireString(element.GetProperty("@value"), "string"));
        }

        private static GYear ReadGYear(JsonElement element)
        {
            EnsureFields(element, "gYear", ["Year"], ["Timezone"]);
            return new GYear(
                ReadInt32(element.GetProperty("Year")),
                ReadOptionalTimezone(element, "gYear"));
        }

        private static GYearMonth ReadGYearMonth(JsonElement element)
        {
            EnsureFields(element, "gYearMonth", ["Year", "Month"], ["Timezone"]);
            return new GYearMonth(
                ReadInt32(element.GetProperty("Year")),
                ReadInt32(element.GetProperty("Month")),
                ReadOptionalTimezone(element, "gYearMonth"));
        }

        private static GMonthDay ReadGMonthDay(JsonElement element)
        {
            EnsureFields(element, "gMonthDay", ["Month", "Day"], ["Timezone"]);
            return new GMonthDay(
                ReadInt32(element.GetProperty("Month")),
                ReadInt32(element.GetProperty("Day")),
                ReadOptionalTimezone(element, "gMonthDay"));
        }

        private static GDay ReadGDay(JsonElement element)
        {
            EnsureFields(element, "gDay", ["Day"], ["Timezone"]);
            return new GDay(
                ReadInt32(element.GetProperty("Day")),
                ReadOptionalTimezone(element, "gDay"));
        }

        private static GMonth ReadGMonth(JsonElement element)
        {
            EnsureFields(element, "gMonth", ["Month"], ["Timezone"]);
            return new GMonth(
                ReadInt32(element.GetProperty("Month")),
                ReadOptionalTimezone(element, "gMonth"));
        }

        private static string? ReadOptionalTimezone(JsonElement element, string dataType)
        {
            if (!element.TryGetProperty("Timezone", out JsonElement timezoneElement))
            {
                return null;
            }

            string timezone = RequireString(timezoneElement, dataType);
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    timezone,
                    $@"^(?:{XsdLexical.TimeZone})$",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            {
                throw Expected(dataType);
            }
            return timezone;
        }

        private static CogsDate ReadCogsDate(JsonElement element)
        {
            EnsureObject(element, "cogsDate");
            JsonProperty[] fields = element.EnumerateObject().ToArray();
            if (fields.Length != 1) throw new JsonException("cogsDate requires exactly one value arm.");
            JsonProperty field = fields[0];
            return field.Name switch
            {
                "DateTime" => new CogsDate(new CogsDateTime(RequireString(field.Value, "dateTime"))),
                "Date" => new CogsDate(new CogsDateOnly(RequireString(field.Value, "date"))),
                "GYearMonth" => new CogsDate(ReadGYearMonth(field.Value)),
                "GYear" => new CogsDate(ReadGYear(field.Value)),
                "Duration" => new CogsDate(new CogsDuration(RequireString(field.Value, "duration"))),
                _ => throw new JsonException($"Unknown cogsDate arm '{field.Name}'."),
            };
        }

        private static CogsDate ReadCogsDateLexical(string lexical)
        {
            Func<CogsDate>[] parsers =
            {
                () => new CogsDate(new CogsDateTime(lexical)), () => new CogsDate(new CogsDateOnly(lexical)),
                () => new CogsDate(new GYearMonth(lexical)), () => new CogsDate(new GYear(lexical)),
                () => new CogsDate(new CogsDuration(lexical)),
            };
            foreach (Func<CogsDate> parser in parsers)
                try { return parser(); } catch (FormatException) { }
            throw new FormatException("Invalid cogsDate lexical value.");
        }

        private static void WriteCogsDate(Utf8JsonWriter writer, CogsDate value)
        {
            string name = value.GetUsedType() ?? throw new JsonException("cogsDate requires exactly one value arm.");
            writer.WriteStartObject();
            writer.WritePropertyName(name);
            switch (value.UsedType)
            {
                case CogsDateType.GYearMonth: WriteGYearMonth(writer, value.GYearMonth!); break;
                case CogsDateType.GYear: WriteGYear(writer, value.GYear!); break;
                default: writer.WriteStringValue(value.ToString()); break;
            }
            writer.WriteEndObject();
        }

        private static void WriteGYear(Utf8JsonWriter writer, GYear value)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Year", value.Year);
            WriteOptionalTimezone(writer, value.Timezone);
            writer.WriteEndObject();
        }

        private static void WriteGYearMonth(Utf8JsonWriter writer, GYearMonth value)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Year", value.Year);
            writer.WriteNumber("Month", value.Month);
            WriteOptionalTimezone(writer, value.Timezone);
            writer.WriteEndObject();
        }

        private static void WriteGMonthDay(Utf8JsonWriter writer, GMonthDay value)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Month", value.Month);
            writer.WriteNumber("Day", value.Day);
            WriteOptionalTimezone(writer, value.Timezone);
            writer.WriteEndObject();
        }

        private static void WriteGDay(Utf8JsonWriter writer, GDay value)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Day", value.Day);
            WriteOptionalTimezone(writer, value.Timezone);
            writer.WriteEndObject();
        }

        private static void WriteGMonth(Utf8JsonWriter writer, GMonth value)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Month", value.Month);
            WriteOptionalTimezone(writer, value.Timezone);
            writer.WriteEndObject();
        }

        private static void WriteOptionalTimezone(Utf8JsonWriter writer, string? timezone)
        {
            if (timezone is not null)
            {
                writer.WriteString("Timezone", timezone);
            }
        }

        private static void EnsureObject(JsonElement element, string type)
        {
            if (element.ValueKind != JsonValueKind.Object) throw Expected(type);
        }

        private static void EnsureFields(
            JsonElement element,
            string type,
            IReadOnlyCollection<string> required,
            IReadOnlyCollection<string> optional)
        {
            EnsureObject(element, type);
            string[] names = element.EnumerateObject().Select(property => property.Name).ToArray();
            if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            {
                throw new JsonException($"Duplicate JSON field in {type} value.");
            }

            var allowed = new HashSet<string>(required, StringComparer.Ordinal);
            allowed.UnionWith(optional);
            string? unknown = names.FirstOrDefault(name => !allowed.Contains(name));
            if (unknown is not null)
            {
                throw new JsonException($"Unknown field '{unknown}' in {type} value.");
            }
            string? missing = required.FirstOrDefault(name => !names.Contains(name, StringComparer.Ordinal));
            if (missing is not null)
            {
                throw new JsonException($"Missing required field '{missing}' in {type} value.");
            }
        }

        private static void EnsureExactFields(JsonElement element, params string[] expected)
        {
            var names = element.EnumerateObject().Select(x => x.Name).ToArray();
            if (names.Length != expected.Length || names.Except(expected, StringComparer.Ordinal).Any())
                throw new JsonException($"Expected exactly fields: {string.Join(", ", expected)}.");
        }
    }

    internal static class CogsJsonCodec
    {
        internal static ItemContainer Read(JsonElement root)
        {
            EnsureNoDuplicates(root);
            if (root.ValueKind != JsonValueKind.Object) throw new JsonException("ItemContainer must be a JSON object.");
            EnsureAllowedFields(root, "items", "topLevelReferences");
            if (!root.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
                throw new JsonException("ItemContainer requires an 'items' array.");
            if (root.TryGetProperty("topLevelReferences", out JsonElement top) && top.ValueKind != JsonValueKind.Array)
                throw new JsonException("topLevelReferences must be an array.");

            var container = new ItemContainer();
            var map = new CogsIdentityMap(container.ObjectStates);
            var definitions = new List<(JsonElement Json, IIdentifiable Item)>();
            foreach (JsonElement definition in items.EnumerateArray())
            {
                Type type = ReadDiscriminator(definition, item: true);
                IReadOnlyList<string> ids = ReadIdentityLexicals(definition, type, referenceOnly: false);
                IIdentifiable item = map.GetOrCreate(type, ids, definition: true);
                SetIdentityValues(item, type, definition);
                container.Items.Add(item);
                definitions.Add((definition, item));
            }
            foreach ((JsonElement definition, IIdentifiable item) in definitions) PopulateObject(item, definition, map, fullItem: true);

            if (root.TryGetProperty("topLevelReferences", out top))
            {
                foreach (JsonElement reference in top.EnumerateArray())
                    container.TopLevelReferences.Add(ReadReference(reference, typeof(IIdentifiable), map, allowSubtypes: true));
            }
            return container;
        }

        internal static void Write(Utf8JsonWriter writer, ItemContainer container)
        {
            var definitions = new HashSet<CogsIdentityKey>();
            foreach (IIdentifiable? item in container.Items)
            {
                if (item is null) throw new JsonException("The items array cannot contain null entries.");
                if (!definitions.Add(CogsIdentity.GetKey(item)))
                    throw new JsonException($"Duplicate definition of '{item.GetType().Name}' with the same identification tuple.");
            }
            writer.WriteStartObject();
            if (container.TopLevelReferences.Count > 0)
            {
                writer.WritePropertyName("topLevelReferences"); writer.WriteStartArray();
                foreach (IIdentifiable? item in container.TopLevelReferences)
                {
                    if (item is null) throw new JsonException("The topLevelReferences array cannot contain null entries.");
                    WriteReference(writer, item);
                }
                writer.WriteEndArray();
            }
            writer.WritePropertyName("items"); writer.WriteStartArray();
            foreach (IIdentifiable? item in container.Items)
            {
                if (item is null) throw new JsonException("The items array cannot contain null entries.");
                WriteObject(writer, item, includeType: true, fullItem: true);
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }

        private static void PopulateObject(object target, JsonElement element, CogsIdentityMap map, bool fullItem)
        {
            IReadOnlyList<CogsPropertyMetadata> properties = CogsReflection.GetProperties(target.GetType());
            var allowed = new HashSet<string>(properties.Select(x => x.Contract.Name), StringComparer.Ordinal);
            if (fullItem) allowed.Add("$type");
            foreach (JsonProperty jsonProperty in element.EnumerateObject())
                if (!allowed.Contains(jsonProperty.Name)) throw new JsonException($"Unknown field '{jsonProperty.Name}' on '{target.GetType().Name}'.");

            foreach (CogsPropertyMetadata property in properties)
            {
                if (!element.TryGetProperty(property.Contract.Name, out JsonElement value)) continue;
                if (property.IsList)
                {
                    if (value.ValueKind != JsonValueKind.Array) throw new JsonException($"'{property.Contract.Name}' must be an array.");
                    IList list = (IList)(property.Property.GetValue(target) ?? Activator.CreateInstance(property.Property.PropertyType)!);
                    list.Clear();
                    foreach (JsonElement entry in value.EnumerateArray()) list.Add(ReadValue(entry, property, map));
                    property.Property.SetValue(target, list);
                }
                else
                {
                    property.Property.SetValue(target, ReadValue(value, property, map));
                }
            }
        }

        private static object ReadValue(JsonElement value, CogsPropertyMetadata property, CogsIdentityMap map) => property.Contract.Kind switch
        {
            CogsPropertyKind.ItemReference => ReadReference(value, property.ValueType, map, property.Contract.AllowSubtypes),
            CogsPropertyKind.Composite => ReadComposite(value, property, map),
            _ => CogsPrimitiveCodec.ReadJson(value, property.Contract.DataType, property.ValueType),
        };

        private static object ReadComposite(JsonElement element, CogsPropertyMetadata property, CogsIdentityMap map)
        {
            if (element.ValueKind != JsonValueKind.Object) throw new JsonException($"'{property.Contract.Name}' must be an object.");
            Type concrete;
            if (property.Contract.AllowSubtypes)
            {
                concrete = ReadDiscriminator(element, item: false);
                if (!property.ValueType.IsAssignableFrom(concrete)) throw new JsonException($"Composite type '{concrete.Name}' is not assignable to '{property.Contract.DataType}'.");
            }
            else
            {
                if (element.TryGetProperty("$type", out _)) throw new JsonException($"Composite property '{property.Contract.Name}' does not allow a $type discriminator.");
                concrete = property.ValueType;
                if (concrete.IsAbstract) throw new JsonException($"Composite property '{property.Contract.Name}' has an abstract exact type.");
            }
            object result = Activator.CreateInstance(concrete) ?? throw new JsonException($"Could not instantiate '{concrete.Name}'.");
            PopulateObject(result, element, map, fullItem: property.Contract.AllowSubtypes);
            return result;
        }

        private static IIdentifiable ReadReference(JsonElement element, Type expectedType, CogsIdentityMap map, bool allowSubtypes)
        {
            Type concrete = ReadDiscriminator(element, item: true);
            if (expectedType != typeof(IIdentifiable) &&
                (allowSubtypes ? !expectedType.IsAssignableFrom(concrete) : concrete != expectedType))
                throw new JsonException(allowSubtypes
                    ? $"Referenced item type '{concrete.Name}' is not assignable to '{expectedType.Name}'."
                    : $"Item reference '{expectedType.Name}' requires the exact type; found '{concrete.Name}'.");
            IReadOnlyList<string> ids = ReadIdentityLexicals(element, concrete, referenceOnly: true);
            IIdentifiable item = map.GetOrCreate(concrete, ids, definition: false);
            SetIdentityValues(item, concrete, element);
            return item;
        }

        private static Type ReadDiscriminator(JsonElement element, bool item)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("$type", out JsonElement discriminator) || discriminator.ValueKind != JsonValueKind.String)
                throw new JsonException("A string $type discriminator is required.");
            return CogsReflection.ResolveType(discriminator.GetString()!, item);
        }

        private static IReadOnlyList<string> ReadIdentityLexicals(JsonElement element, Type concrete, bool referenceOnly)
        {
            IReadOnlyList<CogsPropertyMetadata> ids = CogsReflection.GetIdentification(concrete);
            if (ids.Count == 0) throw new JsonException($"Item type '{concrete.Name}' has no identification metadata.");
            var allowed = new HashSet<string>(ids.Select(x => x.Contract.Name), StringComparer.Ordinal) { "$type" };
            if (referenceOnly) foreach (JsonProperty field in element.EnumerateObject()) if (!allowed.Contains(field.Name)) throw new JsonException($"Unknown reference field '{field.Name}'.");
            var values = new List<string>(ids.Count);
            foreach (CogsPropertyMetadata id in ids)
            {
                if (!element.TryGetProperty(id.Contract.Name, out JsonElement value) || value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString()))
                    throw new JsonException($"Reference identity field '{id.Contract.Name}' is required and nonempty.");
                values.Add(value.GetString()!);
            }
            return values;
        }

        private static void SetIdentityValues(IIdentifiable item, Type concrete, JsonElement element)
        {
            foreach (CogsPropertyMetadata id in CogsReflection.GetIdentification(concrete))
                id.Property.SetValue(item, CogsPrimitiveCodec.ReadJson(element.GetProperty(id.Contract.Name), id.Contract.DataType, id.ValueType));
        }

        private static void WriteObject(Utf8JsonWriter writer, object value, bool includeType, bool fullItem)
        {
            CogsTypeAttribute type = CogsReflection.GetTypeContract(value.GetType());
            if (type.IsAbstract) throw new JsonException($"Cannot serialize abstract runtime type '{type.Name}'.");
            if (type.IsItem)
            {
                if (value is not IIdentifiable item) throw new JsonException($"Generated item '{type.Name}' does not implement IIdentifiable.");
                try { _ = CogsIdentity.GetKey(item); }
                catch (InvalidOperationException exception) { throw new JsonException(exception.Message, exception); }
            }
            writer.WriteStartObject();
            if (includeType) writer.WriteString("$type", type.Name);
            foreach (CogsPropertyMetadata property in CogsReflection.GetProperties(value.GetType()))
            {
                object? propertyValue = property.Property.GetValue(value);
                if (!ShouldWrite(property, propertyValue)) continue;
                writer.WritePropertyName(property.Contract.Name);
                if (property.IsList)
                {
                    writer.WriteStartArray();
                    foreach (object? entry in (IEnumerable)propertyValue!)
                    {
                        if (entry is null) throw new JsonException($"Property '{property.Contract.Name}' cannot contain null entries.");
                        WriteValue(writer, entry, property);
                    }
                    writer.WriteEndArray();
                }
                else WriteValue(writer, propertyValue!, property);
            }
            writer.WriteEndObject();
        }

        private static void WriteValue(Utf8JsonWriter writer, object value, CogsPropertyMetadata property)
        {
            switch (property.Contract.Kind)
            {
                case CogsPropertyKind.ItemReference:
                    Type itemType = value.GetType();
                    if (property.Contract.AllowSubtypes
                        ? !property.ValueType.IsAssignableFrom(itemType)
                        : itemType != property.ValueType)
                        throw new JsonException(property.Contract.AllowSubtypes
                            ? $"Item type '{itemType.Name}' is not assignable to '{property.ValueType.Name}'."
                            : $"Item reference '{property.Contract.Name}' requires exact type '{property.ValueType.Name}'; found '{itemType.Name}'.");
                    WriteReference(writer, (IIdentifiable)value);
                    break;
                case CogsPropertyKind.Composite:
                    if (!property.Contract.AllowSubtypes && value.GetType() != property.ValueType)
                        throw new JsonException($"Composite property '{property.Contract.Name}' does not allow subtype '{value.GetType().Name}'.");
                    WriteObject(writer, value, includeType: property.Contract.AllowSubtypes, fullItem: false); break;
                default: CogsPrimitiveCodec.WriteJson(writer, value, property.Contract.DataType); break;
            }
        }

        private static void WriteReference(Utf8JsonWriter writer, IIdentifiable item)
        {
            CogsTypeAttribute type = CogsReflection.GetTypeContract(item.GetType());
            if (!type.IsItem || type.IsAbstract) throw new JsonException($"Reference runtime type '{type.Name}' must be a concrete item.");
            writer.WriteStartObject(); writer.WriteString("$type", type.Name);
            foreach (CogsPropertyMetadata id in CogsReflection.GetIdentification(item.GetType()))
            {
                object? value = id.Property.GetValue(item);
                if (value is null || string.IsNullOrEmpty(value is Uri uri ? uri.OriginalString : value.ToString()))
                    throw new JsonException($"Reference identity field '{id.Contract.Name}' is required and nonempty.");
                writer.WritePropertyName(id.Contract.Name); CogsPrimitiveCodec.WriteJson(writer, value, id.Contract.DataType);
            }
            writer.WriteEndObject();
        }

        private static bool ShouldWrite(CogsPropertyMetadata property, object? value)
        {
            if (value is null) return false;
            if (property.IsList) return ((ICollection)value).Count > 0;
            // A boxed Nullable<T> with a value is boxed as T. Suppressing CLR defaults here
            // therefore loses explicit optional false, zero, and zero-valued floating point
            // values. Null alone represents absence in the generated object model.
            return true;
        }

        internal static void EnsureNoDuplicates(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new JsonException($"Duplicate JSON field '{property.Name}'.");
                    EnsureNoDuplicates(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (JsonElement child in element.EnumerateArray()) EnsureNoDuplicates(child);
        }

        private static void EnsureAllowedFields(JsonElement element, params string[] allowed)
        {
            var set = new HashSet<string>(allowed, StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject()) if (!set.Contains(property.Name)) throw new JsonException($"Unknown field '{property.Name}'.");
        }
    }

    public sealed class ItemContainerJsonConverter : JsonConverter<ItemContainer>
    {
        public override ItemContainer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return CogsJsonCodec.Read(document.RootElement);
        }
        public override void Write(Utf8JsonWriter writer, ItemContainer value, JsonSerializerOptions options) => CogsJsonCodec.Write(writer, value);
    }

    public partial interface IIdentifiable
    {
        string ReferenceId { get; }
        XElement ToXml();
        INode AddTriples(IGraph graph, INode? itemNode = null);
        string GetUri() => RdfUriFactory.GetUri(this);
    }

    public static class RdfUriFactory
    {
        public static string Prefix { get; set; } = "__CogsRdfInstanceBase__";
        public static string GetUri(IIdentifiable identifiable) =>
            Prefix + Uri.EscapeDataString(identifiable.ReferenceId);
    }

    [JsonConverter(typeof(ItemContainerJsonConverter))]
    public partial class ItemContainer
    {
        internal ConditionalWeakTable<IIdentifiable, CogsObjectState> ObjectStates { get; } = new();
        public List<IIdentifiable> Items { get; } = new();
        public List<IIdentifiable> TopLevelReferences { get; } = new();
        public static string ModelNamespace { get; } = "__CogsGeneratedNamespace";
        public static string XmlNamespace { get; } = "__CogsXmlNamespace__";
        public static string XmlNamespacePrefix { get; } = "__CogsXmlPrefix__";

        public bool IsDefined(IIdentifiable item) => ObjectStates.TryGetValue(item, out CogsObjectState? state) && state.IsDefined;
        public string ToJson(bool indented = false) => JsonSerializer.Serialize(this, CreateJsonOptions(indented));
        public static ItemContainer FromJson(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowDuplicateProperties = false, CommentHandling = JsonCommentHandling.Disallow });
            return CogsJsonCodec.Read(document.RootElement);
        }
        public static ItemContainer LoadJson(string path) => FromJson(File.ReadAllText(path, Encoding.UTF8));
        public static ItemContainer LoadJson(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return FromJson(reader.ReadToEnd());
        }
        public static async Task<ItemContainer> LoadJsonAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return FromJson(await reader.ReadToEndAsync(cancellationToken));
        }
        public void DumpJson(string path, bool indented = false) => File.WriteAllText(path, ToJson(indented), new UTF8Encoding(false));
        public void DumpJson(Stream stream, bool indented = false)
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true); writer.Write(ToJson(indented)); writer.Flush();
        }
        public async Task DumpJsonAsync(Stream stream, bool indented = false, CancellationToken cancellationToken = default)
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true); await writer.WriteAsync(ToJson(indented).AsMemory(), cancellationToken); await writer.FlushAsync(cancellationToken);
        }
        public XDocument MakeXml() => CogsXmlCodec.Write(this);
        public string ToXml(bool indented = false) => MakeXml().ToString(indented ? SaveOptions.None : SaveOptions.DisableFormatting);
        public static ItemContainer FromXml(string xml) => CogsXmlCodec.Read(ParseXml(xml));
        public static ItemContainer LoadXml(string path)
        {
            using FileStream stream = File.OpenRead(path); return LoadXml(stream);
        }
        public static ItemContainer LoadXml(Stream stream) => CogsXmlCodec.Read(ParseXml(stream));
        public static async Task<ItemContainer> LoadXmlAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return FromXml(await reader.ReadToEndAsync(cancellationToken));
        }
        public void DumpXml(string path, bool indented = false) => File.WriteAllText(path, ToXml(indented), new UTF8Encoding(false));
        public void DumpXml(Stream stream, bool indented = false)
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true); writer.Write(ToXml(indented)); writer.Flush();
        }
        public async Task DumpXmlAsync(Stream stream, bool indented = false, CancellationToken cancellationToken = default)
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true); await writer.WriteAsync(ToXml(indented).AsMemory(), cancellationToken); await writer.FlushAsync(cancellationToken);
        }
        private static JsonSerializerOptions CreateJsonOptions(bool indented) => new() { WriteIndented = indented, AllowDuplicateProperties = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        private static XDocument ParseXml(string xml)
        {
            using var reader = XmlReader.Create(new StringReader(xml), StrictXmlSettings()); return XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        private static XDocument ParseXml(Stream stream)
        {
            using var reader = XmlReader.Create(stream, StrictXmlSettings()); return XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        private static XmlReaderSettings StrictXmlSettings() => new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = false, IgnoreProcessingInstructions = false };
    }

    internal static class CogsXmlCodec
    {
        private static readonly XNamespace Ns = ItemContainer.XmlNamespace;
        private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
        private static string ModelPrefix => string.Equals(ItemContainer.XmlNamespacePrefix, "xsi", StringComparison.Ordinal)
            ? "cogs"
            : ItemContainer.XmlNamespacePrefix;

        internal static XDocument Write(ItemContainer container)
        {
            var root = new XElement(Ns + "ItemContainer");
            AddNamespaceDeclarations(root);
            foreach (IIdentifiable? reference in container.TopLevelReferences)
            {
                if (reference is null) throw new XmlException("TopLevelReference cannot be null.");
                root.Add(WriteReference("TopLevelReference", reference));
            }
            var definitions = new HashSet<CogsIdentityKey>();
            foreach (IIdentifiable? item in container.Items)
            {
                if (item is null) throw new XmlException("The item container cannot contain null definitions.");
                CogsIdentityKey key;
                try { key = CogsIdentity.GetKey(item); }
                catch (InvalidOperationException exception) { throw new XmlException(exception.Message, exception); }
                if (!definitions.Add(key)) throw new XmlException($"Duplicate definition of '{item.GetType().Name}' with the same identification tuple.");
                root.Add(WriteModelObject(item, CogsReflection.GetTypeContract(item.GetType()).Name, includeSubstitution: false));
            }
            return new XDocument(root);
        }

        internal static XElement WriteStandalone(object value, string name)
        {
            XElement element = WriteModelObject(value, name, includeSubstitution: false);
            // A standalone generated object can still contain a property-local xsi:type.
            // Namespace declarations must therefore be present without relying on an
            // enclosing ItemContainer.
            AddNamespaceDeclarations(element);
            return element;
        }

        private static void AddNamespaceDeclarations(XElement element)
        {
            element.SetAttributeValue(XNamespace.Xmlns + ModelPrefix, Ns);
            element.SetAttributeValue(XNamespace.Xmlns + "xsi", Xsi);
        }

        private static XElement WriteModelObject(object value, string name, bool includeSubstitution)
        {
            CogsTypeAttribute runtimeType = CogsReflection.GetTypeContract(value.GetType());
            if (runtimeType.IsItem)
            {
                try { _ = CogsIdentity.GetKey((IIdentifiable)value); }
                catch (InvalidOperationException exception) { throw new XmlException(exception.Message, exception); }
            }
            var element = new XElement(Ns + name);
            if (includeSubstitution) element.Add(new XAttribute(Xsi + "type", ModelPrefix + ":" + CogsReflection.GetTypeContract(value.GetType()).Name));
            foreach (CogsPropertyMetadata property in CogsReflection.GetProperties(value.GetType()))
            {
                object? propertyValue = property.Property.GetValue(value);
                if (propertyValue is null) continue;
                IEnumerable values = property.IsList ? (IEnumerable)propertyValue : new[] { propertyValue };
                foreach (object? entry in values)
                {
                    if (entry is null) throw new XmlException($"Property '{property.Contract.Name}' cannot contain null entries.");
                    if (property.Contract.Kind == CogsPropertyKind.ItemReference)
                    {
                        Type itemType = entry.GetType();
                        if (property.Contract.AllowSubtypes
                            ? !property.ValueType.IsAssignableFrom(itemType)
                            : itemType != property.ValueType)
                            throw new XmlException(property.Contract.AllowSubtypes
                                ? $"Item type '{itemType.Name}' is not assignable to '{property.ValueType.Name}'."
                                : $"Item reference '{property.Contract.Name}' requires exact type '{property.ValueType.Name}'; found '{itemType.Name}'.");
                        element.Add(WriteReference(property.Contract.Name, (IIdentifiable)entry));
                    }
                    else if (property.Contract.Kind == CogsPropertyKind.Composite)
                    {
                        if (!property.Contract.AllowSubtypes && entry.GetType() != property.ValueType) throw new XmlException($"Composite property '{property.Contract.Name}' does not allow subtype '{entry.GetType().Name}'.");
                        element.Add(WriteModelObject(entry, property.Contract.Name, property.Contract.AllowSubtypes));
                    }
                    else
                    {
                        var child = new XElement(Ns + property.Contract.Name, CogsPrimitiveCodec.WriteXml(entry, property.Contract.DataType));
                        if (entry is LangString lang) child.SetAttributeValue(XNamespace.Xml + "lang", lang.LanguageTag);
                        element.Add(child);
                    }
                }
            }
            return element;
        }

        private static XElement WriteReference(string name, IIdentifiable item)
        {
            CogsTypeAttribute type = CogsReflection.GetTypeContract(item.GetType());
            var element = new XElement(Ns + name);
            element.SetAttributeValue("isReference", "true");
            foreach (CogsPropertyMetadata id in CogsReflection.GetIdentification(item.GetType()))
            {
                object? value = id.Property.GetValue(item) ?? throw new XmlException($"Identity field '{id.Contract.Name}' is required.");
                if ((value is string text && text.Length == 0) || (value is Uri uri && uri.OriginalString.Length == 0))
                    throw new XmlException($"Identity field '{id.Contract.Name}' is required and nonempty.");
                element.Add(new XElement(Ns + id.Contract.Name, CogsPrimitiveCodec.WriteXml(value, id.Contract.DataType)));
            }
            element.Add(new XElement(Ns + "TypeOfObject", type.Name));
            return element;
        }

        internal static ItemContainer Read(XDocument document)
        {
            XElement root = document.Root ?? throw new XmlException("XML document has no root element.");
            if (root.Name != Ns + "ItemContainer") throw new XmlException($"Expected qualified ItemContainer in namespace '{Ns}'.");
            EnsureComplexContent(root, allowXsiType: false);
            var container = new ItemContainer();
            var map = new CogsIdentityMap(container.ObjectStates);
            var definitions = new List<(XElement Xml, IIdentifiable Item)>();
            bool sawItem = false;
            foreach (XElement child in root.Elements())
            {
                if (child.Name == Ns + "TopLevelReference")
                {
                    if (sawItem) throw new XmlException("TopLevelReference elements must precede full items.");
                    container.TopLevelReferences.Add(ReadReference(child, typeof(IIdentifiable), map, allowSubtypes: true));
                    continue;
                }
                sawItem = true;
                if (child.Name.Namespace != Ns) throw new XmlException($"Unexpected namespace '{child.Name.NamespaceName}'.");
                Type type = ResolveXmlType(child.Name.LocalName, item: true);
                IReadOnlyList<string> ids = ReadIdentityLexicals(child, type, reference: false);
                IIdentifiable item = GetOrCreateXml(map, type, ids, definition: true);
                SetIdentityValues(item, type, child);
                container.Items.Add(item); definitions.Add((child, item));
            }
            foreach ((XElement xml, IIdentifiable item) in definitions) PopulateObject(item, xml, map, allowXsiType: false);
            return container;
        }

        private static void PopulateObject(object target, XElement element, CogsIdentityMap map, bool allowXsiType)
        {
            EnsureComplexContent(element, allowXsiType);
            XElement[] children = element.Elements().ToArray();
            int index = 0;
            foreach (CogsPropertyMetadata property in CogsReflection.GetProperties(target.GetType()))
            {
                var values = new List<XElement>();
                while (index < children.Length && children[index].Name == Ns + property.Contract.Name)
                {
                    values.Add(children[index++]);
                    if (!property.IsList) break;
                }
                if (property.IsList)
                {
                    IList list = (IList)(property.Property.GetValue(target) ?? Activator.CreateInstance(property.Property.PropertyType)!); list.Clear();
                    foreach (XElement child in values) list.Add(ReadValue(child, property, map)); property.Property.SetValue(target, list);
                }
                else if (values.Count == 1) property.Property.SetValue(target, ReadValue(values[0], property, map));
            }
            if (index != children.Length) throw new XmlException($"Unexpected or out-of-order element '{children[index].Name}'.");
        }

        private static object ReadValue(XElement element, CogsPropertyMetadata property, CogsIdentityMap map)
        {
            if (property.Contract.Kind == CogsPropertyKind.ItemReference) return ReadReference(element, property.ValueType, map, property.Contract.AllowSubtypes);
            if (property.Contract.Kind == CogsPropertyKind.Composite)
            {
                Type concrete = property.ValueType;
                XAttribute? xsiType = element.Attribute(Xsi + "type");
                if (property.Contract.AllowSubtypes)
                {
                    if (xsiType is null) throw new XmlException($"Composite property '{property.Contract.Name}' requires a qualified xsi:type.");
                    int separator = xsiType.Value.IndexOf(':');
                    if (separator <= 0 || separator != xsiType.Value.LastIndexOf(':'))
                        throw new XmlException($"Composite property '{property.Contract.Name}' requires a qualified xsi:type.");
                    string prefix = xsiType.Value[..separator];
                    string localName = xsiType.Value[(separator + 1)..];
                    try { XmlConvert.VerifyNCName(prefix); XmlConvert.VerifyNCName(localName); }
                    catch (XmlException exception) { throw new XmlException($"Invalid xsi:type QName '{xsiType.Value}'.", exception); }
                    if (element.GetNamespaceOfPrefix(prefix) != Ns) throw new XmlException($"xsi:type prefix '{prefix}' does not identify the model namespace.");
                    concrete = ResolveXmlType(localName, item: false);
                    if (!property.ValueType.IsAssignableFrom(concrete)) throw new XmlException($"Composite xsi:type '{concrete.Name}' is not assignable to '{property.Contract.DataType}'.");
                }
                else if (xsiType is not null) throw new XmlException($"Composite property '{property.Contract.Name}' does not allow xsi:type.");
                object result = Activator.CreateInstance(concrete) ?? throw new XmlException($"Could not instantiate '{concrete.Name}'.");
                PopulateObject(result, element, map, allowXsiType: property.Contract.AllowSubtypes); return result;
            }
            EnsurePrimitiveContent(element, property.Contract.DataType == "langString");
            return CogsPrimitiveCodec.ReadXml(element.Value, property.Contract.DataType, property.ValueType, element.Attribute(XNamespace.Xml + "lang"));
        }

        private static IIdentifiable ReadReference(XElement element, Type expectedType, CogsIdentityMap map, bool allowSubtypes)
        {
            EnsureComplexContent(element, allowXsiType: false, allowReferenceMarker: true);
            XElement[] children = element.Elements().ToArray();
            XElement typeElement = children.LastOrDefault() ?? throw new XmlException("Reference is empty.");
            if (typeElement.Name != Ns + "TypeOfObject" || typeElement.HasElements) throw new XmlException("Reference must end with TypeOfObject.");
            EnsurePrimitiveContent(typeElement, langString: false);
            if (string.IsNullOrEmpty(typeElement.Value)) throw new XmlException("TypeOfObject must be nonempty.");
            Type concrete = ResolveXmlType(typeElement.Value, item: true);
            if (expectedType != typeof(IIdentifiable) &&
                (allowSubtypes ? !expectedType.IsAssignableFrom(concrete) : concrete != expectedType))
                throw new XmlException(allowSubtypes
                    ? $"Referenced type '{concrete.Name}' is not assignable to '{expectedType.Name}'."
                    : $"Item reference '{expectedType.Name}' requires the exact type; found '{concrete.Name}'.");
            IReadOnlyList<string> ids = ReadIdentityLexicals(element, concrete, reference: true);
            IIdentifiable item = GetOrCreateXml(map, concrete, ids, definition: false); SetIdentityValues(item, concrete, element); return item;
        }

        private static IReadOnlyList<string> ReadIdentityLexicals(XElement element, Type concrete, bool reference)
        {
            IReadOnlyList<CogsPropertyMetadata> ids = CogsReflection.GetIdentification(concrete);
            XElement[] children = element.Elements().ToArray();
            if (children.Length < ids.Count + (reference ? 1 : 0)) throw new XmlException("Reference or definition is missing identification fields.");
            var values = new List<string>(ids.Count);
            for (int index = 0; index < ids.Count; index++)
            {
                if (children[index].Name != Ns + ids[index].Contract.Name)
                    throw new XmlException($"Expected nonempty identification element '{ids[index].Contract.Name}'.");
                EnsurePrimitiveContent(children[index], langString: false);
                if (string.IsNullOrEmpty(children[index].Value))
                    throw new XmlException($"Expected nonempty identification element '{ids[index].Contract.Name}'.");
                values.Add(children[index].Value);
            }
            if (reference && (children.Length != ids.Count + 1 || children[^1].Name != Ns + "TypeOfObject")) throw new XmlException("Reference contains unexpected content.");
            return values;
        }

        private static void SetIdentityValues(IIdentifiable item, Type concrete, XElement element)
        {
            XElement[] children = element.Elements().ToArray(); int index = 0;
            foreach (CogsPropertyMetadata id in CogsReflection.GetIdentification(concrete))
                id.Property.SetValue(item, CogsPrimitiveCodec.ReadXml(children[index++].Value, id.Contract.DataType, id.ValueType));
        }

        private static Type ResolveXmlType(string name, bool item)
        {
            try { return CogsReflection.ResolveType(name, item); }
            catch (JsonException exception) { throw new XmlException(exception.Message, exception); }
        }

        private static IIdentifiable GetOrCreateXml(CogsIdentityMap map, Type type, IReadOnlyList<string> identities, bool definition)
        {
            try { return map.GetOrCreate(type, identities, definition); }
            catch (JsonException exception) { throw new XmlException(exception.Message, exception); }
        }

        private static void EnsureComplexContent(XElement element, bool allowXsiType, bool allowReferenceMarker = false)
        {
            foreach (XAttribute attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration || (allowXsiType && attribute.Name == Xsi + "type")) continue;
                if (allowReferenceMarker && attribute.Name == XName.Get("isReference"))
                {
                    if (attribute.Value is not ("true" or "1")) throw new XmlException("The unqualified isReference attribute must have the fixed boolean value true (lexically 'true' or '1').");
                    continue;
                }
                throw new XmlException($"Unexpected attribute '{attribute.Name}'.");
            }
            foreach (XNode node in element.Nodes())
                if (node is XText text && !string.IsNullOrWhiteSpace(text.Value)) throw new XmlException($"Mixed text is not allowed in '{element.Name}'.");
                else if (node is not XElement && node is not XText) throw new XmlException($"Unexpected XML node in '{element.Name}'.");
        }

        private static void EnsurePrimitiveContent(XElement element, bool langString)
        {
            if (element.HasElements) throw new XmlException($"Primitive element '{element.Name}' cannot have child elements.");
            foreach (XAttribute attribute in element.Attributes())
                if (!attribute.IsNamespaceDeclaration && !(langString && attribute.Name == XNamespace.Xml + "lang")) throw new XmlException($"Unexpected attribute '{attribute.Name}'.");
            if (langString && element.Attribute(XNamespace.Xml + "lang") is null) throw new XmlException("langString requires xml:lang.");
            foreach (XNode node in element.Nodes())
                if (node is not XText) throw new XmlException($"Unexpected XML node in primitive element '{element.Name}'.");
        }
    }
}
