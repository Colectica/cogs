using Cogs.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Cogs.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CogsTypeAttribute : Attribute
    {
        public CogsTypeAttribute(string name, bool isItem, bool isAbstract)
        {
            Name = name;
            IsItem = isItem;
            IsAbstract = isAbstract;
        }

        public string Name { get; }
        public bool IsItem { get; }
        public bool IsAbstract { get; }
    }

    public enum CogsPropertyKind
    {
        Primitive,
        Composite,
        ItemReference,
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public sealed class CogsPropertyAttribute : Attribute
    {
        public CogsPropertyAttribute(
            string name,
            string dataType,
            CogsPropertyKind kind,
            int order,
            bool allowSubtypes,
            bool isIdentification,
            string minimum,
            string maximum)
        {
            Name = name;
            DataType = dataType;
            Kind = kind;
            Order = order;
            AllowSubtypes = allowSubtypes;
            IsIdentification = isIdentification;
            Minimum = minimum;
            Maximum = maximum;
        }

        public string Name { get; }
        public string DataType { get; }
        public CogsPropertyKind Kind { get; }
        public int Order { get; }
        public bool AllowSubtypes { get; }
        public bool IsIdentification { get; }
        public string Minimum { get; }
        public string Maximum { get; }
        public bool Ordered { get; set; }
        public int MinLength { get; set; } = -1;
        public int MaxLength { get; set; } = -1;
        public string[] Enumeration { get; set; } = Array.Empty<string>();
        public string Pattern { get; set; } = string.Empty;
        public string MinInclusive { get; set; } = string.Empty;
        public string MinExclusive { get; set; } = string.Empty;
        public string MaxInclusive { get; set; } = string.Empty;
        public string MaxExclusive { get; set; } = string.Empty;
    }
}

namespace Cogs.SimpleTypes
{
    public interface IXsdLexicalValue
    {
        string LexicalValue { get; }
    }

    public abstract class XsdLexicalValue : IXsdLexicalValue, IEquatable<XsdLexicalValue>
    {
        protected XsdLexicalValue(string lexicalValue)
        {
            if (string.IsNullOrEmpty(lexicalValue) || !IsValid(lexicalValue))
            {
                throw new FormatException($"'{lexicalValue}' is not a valid {GetType().Name} lexical value.");
            }

            LexicalValue = lexicalValue;
        }

        public string LexicalValue { get; }
        protected abstract bool IsValid(string value);
        public sealed override string ToString() => LexicalValue;
        public bool Equals(XsdLexicalValue? other) =>
            other is not null && other.GetType() == GetType() && other.LexicalValue == LexicalValue;
        public sealed override bool Equals(object? obj) => Equals(obj as XsdLexicalValue);
        public sealed override int GetHashCode() => HashCode.Combine(GetType(), LexicalValue);
    }

    internal static class XsdLexical
    {
        internal const string TimeZone = @"(?:Z|[+-](?:(?:0[0-9]|1[0-3]):[0-5][0-9]|14:00))";
        internal const string Year = @"-?(?:[0-9]{4}|[1-9][0-9]{4,})";
        internal static readonly Regex Language = new(
            @"^(?:(?:(?:[A-Za-z]{2,3}(?:-[A-Za-z]{3}){0,3}|[A-Za-z]{4}|[A-Za-z]{5,8})(?:-[A-Za-z]{4})?(?:-(?:[A-Za-z]{2}|[0-9]{3}))?(?:-(?:[A-Za-z0-9]{5,8}|[0-9][A-Za-z0-9]{3}))*(?:-[0-9A-WY-Za-wy-z](?:-[A-Za-z0-9]{2,8})+)*(?:-x(?:-[A-Za-z0-9]{1,8})+)?)|(?:x(?:-[A-Za-z0-9]{1,8})+)|(?:en-GB-oed|i-ami|i-bnn|i-default|i-enochian|i-hak|i-klingon|i-lux|i-mingo|i-navajo|i-pwn|i-tao|i-tay|i-tsu|sgn-BE-FR|sgn-BE-NL|sgn-CH-DE|art-lojban|cel-gaulish|no-bok|no-nyn|zh-guoyu|zh-hakka|zh-min|zh-min-nan|zh-xiang))\z",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        internal static bool IsYear(string value)
        {
            string year = RemoveTimeZone(value);
            return Regex.IsMatch(year, $@"^{Year}\z", RegexOptions.CultureInvariant)
                && int.TryParse(year, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int parsed)
                && parsed != 0;
        }

        internal static bool IsCalendarDate(string yearText, int month, int day)
        {
            if (!int.TryParse(
                    yearText,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out int year)
                || year == 0)
            {
                return false;
            }

            int max = month switch
            {
                2 => IsLeapYear(year) ? 29 : 28,
                4 or 6 or 9 or 11 => 30,
                _ => 31,
            };
            return day <= max;
        }

        private static bool IsLeapYear(int year)
        {
            int astronomical = year < 0 ? year + 1 : year;
            return astronomical % 400 == 0 || (astronomical % 4 == 0 && astronomical % 100 != 0);
        }

        internal static string NormalizeTimeZone(string? timezone)
        {
            if (string.IsNullOrEmpty(timezone))
            {
                return string.Empty;
            }
            if (timezone == "Z" || Regex.IsMatch(timezone, $@"^{TimeZone}\z", RegexOptions.CultureInvariant))
            {
                return timezone;
            }
            if (Regex.IsMatch(timezone, @"^(?:0[0-9]|1[0-3]):[0-5][0-9]\z", RegexOptions.CultureInvariant))
            {
                return "+" + timezone;
            }
            throw new FormatException($"'{timezone}' is not a valid XSD timezone.");
        }

        internal static string RemoveTimeZone(string value)
        {
            Match match = Regex.Match(value, $@"(?<tz>{TimeZone})\z", RegexOptions.CultureInvariant);
            return match.Success ? value[..match.Index] : value;
        }

        internal static string? GetTimeZone(string value)
        {
            Match match = Regex.Match(value, $@"(?<tz>{TimeZone})\z", RegexOptions.CultureInvariant);
            return match.Success ? match.Value : null;
        }

    }

    public sealed class GYear : XsdLexicalValue
    {
        public GYear(string value) : base(value) { }
        public GYear(int year, string? timezone = null) : this(FormatYear(year) + XsdLexical.NormalizeTimeZone(timezone)) { }
        protected override bool IsValid(string value) => XsdLexical.IsYear(value);
        public int Year => int.Parse(XsdLexical.RemoveTimeZone(LexicalValue), CultureInfo.InvariantCulture);
        public string? Timezone => XsdLexical.GetTimeZone(LexicalValue);
        internal static string FormatYear(int year)
        {
            if (year == 0) throw new ArgumentOutOfRangeException(nameof(year), "XSD years do not include year zero.");
            string digits = Math.Abs((long)year).ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
            return year < 0 ? "-" + digits : digits;
        }
    }

    public sealed class GYearMonth : XsdLexicalValue
    {
        private static readonly Regex Pattern = new($@"^(?<year>{XsdLexical.Year})-(?<month>0[1-9]|1[0-2])(?:{XsdLexical.TimeZone})?\z", RegexOptions.CultureInvariant);
        public GYearMonth(string value) : base(value) { }
        public GYearMonth(int year, int month, string? timezone = null) : this($"{GYear.FormatYear(year)}-{month:00}" + XsdLexical.NormalizeTimeZone(timezone)) { }
        protected override bool IsValid(string value)
        {
            Match match = Pattern.Match(value);
            return match.Success
                && int.TryParse(
                    match.Groups["year"].Value,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out int year)
                && year != 0;
        }
        public int Year => int.Parse(Pattern.Match(LexicalValue).Groups["year"].Value, CultureInfo.InvariantCulture);
        public int Month => int.Parse(Pattern.Match(LexicalValue).Groups["month"].Value, CultureInfo.InvariantCulture);
        public string? Timezone => XsdLexical.GetTimeZone(LexicalValue);
    }

    public sealed class GMonthDay : XsdLexicalValue
    {
        private static readonly Regex Pattern = new($@"^--(?<month>0[1-9]|1[0-2])-(?<day>0[1-9]|[12][0-9]|3[01])(?:{XsdLexical.TimeZone})?\z", RegexOptions.CultureInvariant);
        public GMonthDay(string value) : base(value) { }
        public GMonthDay(int month, int day, string? timezone = null) : this($"--{month:00}-{day:00}" + XsdLexical.NormalizeTimeZone(timezone)) { }
        protected override bool IsValid(string value)
        {
            Match match = Pattern.Match(value);
            return match.Success && XsdLexical.IsCalendarDate("2000", int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture));
        }
        public int Month => int.Parse(Pattern.Match(LexicalValue).Groups["month"].Value, CultureInfo.InvariantCulture);
        public int Day => int.Parse(Pattern.Match(LexicalValue).Groups["day"].Value, CultureInfo.InvariantCulture);
        public string? Timezone => XsdLexical.GetTimeZone(LexicalValue);
    }

    public sealed class GDay : XsdLexicalValue
    {
        private static readonly Regex Pattern = new($@"^---(?<day>0[1-9]|[12][0-9]|3[01])(?:{XsdLexical.TimeZone})?\z", RegexOptions.CultureInvariant);
        public GDay(string value) : base(value) { }
        public GDay(int day, string? timezone = null) : this($"---{day:00}" + XsdLexical.NormalizeTimeZone(timezone)) { }
        protected override bool IsValid(string value) => Pattern.IsMatch(value);
        public int Day => int.Parse(Pattern.Match(LexicalValue).Groups["day"].Value, CultureInfo.InvariantCulture);
        public string? Timezone => XsdLexical.GetTimeZone(LexicalValue);
    }

    public sealed class GMonth : XsdLexicalValue
    {
        private static readonly Regex Pattern = new($@"^--(?<month>0[1-9]|1[0-2])--(?:{XsdLexical.TimeZone})?\z", RegexOptions.CultureInvariant);
        public GMonth(string value) : base(value) { }
        public GMonth(int month, string? timezone = null) : this($"--{month:00}--" + XsdLexical.NormalizeTimeZone(timezone)) { }
        protected override bool IsValid(string value) => Pattern.IsMatch(value);
        public int Month => int.Parse(Pattern.Match(LexicalValue).Groups["month"].Value, CultureInfo.InvariantCulture);
        public string? Timezone => XsdLexical.GetTimeZone(LexicalValue);
    }

    public sealed class LangString : IEquatable<LangString>
    {
        public LangString(string languageTag, string value)
        {
            if (!XsdLexical.Language.IsMatch(languageTag))
            {
                throw new FormatException($"'{languageTag}' is not a syntactically valid BCP 47 language tag.");
            }
            LanguageTag = languageTag;
            Value = CogsScalarValues.Text(value);
        }

        public string Value { get; }
        public string LanguageTag { get; }
        public bool Equals(LangString? other) => other is not null && other.Value == Value && other.LanguageTag == LanguageTag;
        public override bool Equals(object? obj) => Equals(obj as LangString);
        public override int GetHashCode() => HashCode.Combine(Value, LanguageTag);
        public override string ToString() => Value;
    }

    public enum CogsDateType
    {
        None,
        DateTime,
        Date,
        GYearMonth,
        GYear,
        Duration,
    }

    public sealed class CogsDate
    {
        private object? value;
        public CogsDateType UsedType { get; private set; }
        public CogsDate() { }
        public CogsDate(DateTimeOffset item) => DateTime = item;
        public CogsDate(DateOnly item) => Date = item;
        public CogsDate(GYearMonth item) => GYearMonth = item;
        public CogsDate(GYear item) => GYear = item;
        public CogsDate(TimeSpan item) => Duration = item;

        public DateTimeOffset? DateTime { get => UsedType == CogsDateType.DateTime ? (DateTimeOffset?)value : null; set => Set(CogsDateType.DateTime, value); }
        public DateOnly? Date { get => UsedType == CogsDateType.Date ? (DateOnly?)value : null; set => Set(CogsDateType.Date, value); }
        public GYearMonth? GYearMonth { get => UsedType == CogsDateType.GYearMonth ? (GYearMonth?)value : null; set => Set(CogsDateType.GYearMonth, value); }
        public GYear? GYear { get => UsedType == CogsDateType.GYear ? (GYear?)value : null; set => Set(CogsDateType.GYear, value); }
        public TimeSpan? Duration { get => UsedType == CogsDateType.Duration ? (TimeSpan?)value : null; set => Set(CogsDateType.Duration, value); }

        public string? GetUsedType() => UsedType switch
        {
            CogsDateType.DateTime => "DateTime",
            CogsDateType.Date => "Date",
            CogsDateType.GYearMonth => "GYearMonth",
            CogsDateType.GYear => "GYear",
            CogsDateType.Duration => "Duration",
            _ => null,
        };

        public object? GetValue() => value;

        public override string? ToString() => value switch
        {
            DateTimeOffset instant => CogsScalarValues.DateTimeText(instant),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeSpan duration => CogsScalarValues.DurationText(duration),
            _ => value?.ToString()
        };

        private void Set(CogsDateType type, object? item)
        {
            value = item;
            UsedType = item is null ? CogsDateType.None : type;
        }
    }
}

namespace Cogs.Converters
{
    // Retained as an empty compatibility namespace. JSON conversion is provided
    // by the generated strict System.Text.Json runtime.
}
