using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace Cogs.Common
{
    public enum CogsPrimitiveOrder
    {
        Less = -1,
        Equal = 0,
        Greater = 1,
        Indeterminate = 2
    }

    /// <summary>
    /// Canonical lexical domains and partial-order comparison for COGS 2.0
    /// primitives. Publishers and instance validators should use these rules
    /// instead of target-language date, URI, or floating-point parsers.
    /// </summary>
    public static class CogsPrimitiveLexical
    {
        private const string JsonIntegerPattern = @"^-?(?:0|[1-9][0-9]*)$";
        private const string JsonDecimalPattern = @"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?$";
        private const string JsonNumberPattern = @"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$";
        public const string DurationPattern = @"^-?P(?=[0-9]|T(?:[0-9]|\.[0-9]))(?:[0-9]+Y)?(?:[0-9]+M)?(?:[0-9]+D)?(?:T(?=[0-9]|\.[0-9])(?:[0-9]+H)?(?:[0-9]+M)?(?:(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)S)?)?$";
        public const string UriReferenceCharacterPattern = @"^(?:[A-Za-z0-9._~:/?#\[\]@!$&'()*+,;=-]|%[0-9A-Fa-f]{2})*$";

        private static readonly string[] GrandfatheredTags =
        {
            "en-GB-oed", "i-ami", "i-bnn", "i-default", "i-enochian", "i-hak", "i-klingon",
            "i-lux", "i-mingo", "i-navajo", "i-pwn", "i-tao", "i-tay", "i-tsu", "sgn-BE-FR",
            "sgn-BE-NL", "sgn-CH-DE", "art-lojban", "cel-gaulish", "no-bok", "no-nyn",
            "zh-guoyu", "zh-hakka", "zh-min", "zh-min-nan", "zh-xiang"
        };

        public static readonly string Bcp47Pattern = BuildBcp47Pattern();
        private static readonly Regex Bcp47Regex = new Regex(Bcp47Pattern, RegexOptions.CultureInvariant);
        private static readonly Regex UriCharactersRegex = new Regex(UriReferenceCharacterPattern, RegexOptions.CultureInvariant);
        private static readonly Regex DurationRegex = new Regex(DurationPattern, RegexOptions.CultureInvariant);

        public static bool IsValid(string dataType, string lexical)
        {
            if (lexical == null) return false;
            return dataType switch
            {
                "string" or "langString" => true,
                "boolean" => lexical is "true" or "false",
                "decimal" => Regex.IsMatch(lexical, JsonDecimalPattern, RegexOptions.CultureInvariant),
                "float" or "double" => IsFiniteJsonNumber(lexical),
                "nonPositiveInteger" or "negativeInteger" or "long" or "int" or
                    "nonNegativeInteger" or "unsignedLong" or "positiveInteger" => IsInteger(dataType, lexical),
                "duration" => DurationRegex.IsMatch(lexical),
                "dateTime" => TryParseTemporal("dateTime", lexical, out _),
                "time" => TryParseTemporal("time", lexical, out _),
                "date" => TryParseTemporal("date", lexical, out _),
                "gYearMonth" or "gYear" or "gMonthDay" or "gDay" or "gMonth" =>
                    CogsGregorianLexical.TryParse(dataType, lexical, out _),
                "anyURI" => IsUriReference(lexical),
                "language" => Bcp47Regex.IsMatch(lexical),
                _ => false
            };
        }

        public static CogsPrimitiveOrder Compare(string dataType, string left, string right)
        {
            if (!IsValid(dataType, left) || !IsValid(dataType, right)) return CogsPrimitiveOrder.Indeterminate;
            if (IsIntegerType(dataType))
            {
                BigInteger.TryParse(left, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var a);
                BigInteger.TryParse(right, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var b);
                return FromSign(a.CompareTo(b));
            }
            if (dataType == "decimal") return FromSign(ParseExactDecimal(left).CompareTo(ParseExactDecimal(right)));
            if (dataType is "float" or "double")
            {
                var a = double.Parse(left, NumberStyles.Float, CultureInfo.InvariantCulture);
                var b = double.Parse(right, NumberStyles.Float, CultureInfo.InvariantCulture);
                return FromSign(a.CompareTo(b));
            }
            if (dataType == "duration") return CompareDurations(left, right);
            if (IsTemporalType(dataType))
            {
                TryParseTemporal(dataType, left, out var a);
                TryParseTemporal(dataType, right, out var b);
                return CompareIntervals(a, b);
            }
            return string.Equals(left, right, StringComparison.Ordinal) ? CogsPrimitiveOrder.Equal : CogsPrimitiveOrder.Indeterminate;
        }

        public static bool IsUriReference(string value)
        {
            if (value == null || !UriCharactersRegex.IsMatch(value)) return false;
            var fragment = value.IndexOf('#');
            if (fragment >= 0 && value.IndexOf('#', fragment + 1) >= 0) return false;

            var firstDelimiter = new[] { value.IndexOf('/'), value.IndexOf('?'), fragment }
                .Where(index => index >= 0)
                .DefaultIfEmpty(value.Length)
                .Min();
            var colon = value.IndexOf(':');
            if (colon >= 0 && colon < firstDelimiter &&
                !Regex.IsMatch(value.Substring(0, colon), @"^[A-Za-z][A-Za-z0-9+.-]*$", RegexOptions.CultureInvariant))
            {
                return false;
            }

            return value.Count(x => x == '[') == value.Count(x => x == ']');
        }

        public static bool TryGetCogsDateDataType(string lexical, out string dataType)
        {
            foreach (string candidate in new[] { "dateTime", "date", "gYearMonth", "gYear", "duration" })
            {
                if (IsValid(candidate, lexical))
                {
                    dataType = candidate;
                    return true;
                }
            }
            dataType = string.Empty;
            return false;
        }

        private static bool IsFiniteJsonNumber(string lexical) =>
            Regex.IsMatch(lexical, JsonNumberPattern, RegexOptions.CultureInvariant) &&
            double.TryParse(lexical, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
            double.IsFinite(value);

        private static bool IsInteger(string dataType, string lexical)
        {
            if (!Regex.IsMatch(lexical, JsonIntegerPattern, RegexOptions.CultureInvariant) ||
                !BigInteger.TryParse(lexical, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)) return false;
            return dataType switch
            {
                "nonPositiveInteger" => value <= 0,
                "negativeInteger" => value < 0,
                "long" => value >= long.MinValue && value <= long.MaxValue,
                "int" => value >= int.MinValue && value <= int.MaxValue,
                "nonNegativeInteger" => value >= 0,
                "unsignedLong" => value >= 0 && value <= ulong.MaxValue,
                "positiveInteger" => value > 0,
                _ => false
            };
        }

        private static bool IsIntegerType(string dataType) => dataType is
            "nonPositiveInteger" or "negativeInteger" or "long" or "int" or
            "nonNegativeInteger" or "unsignedLong" or "positiveInteger";

        private static bool IsTemporalType(string dataType) => dataType is
            "dateTime" or "time" or "date" or "gYearMonth" or "gYear" or
            "gMonthDay" or "gDay" or "gMonth";

        private static bool TryParseTemporal(string dataType, string lexical, out Interval interval)
        {
            interval = default;
            if (!TrySplitTimezone(lexical, out var core, out var offsetMinutes)) return false;

            BigInteger year = 2000;
            var month = 1;
            var day = 1;
            var hour = 0;
            var minute = 0;
            var second = ExactDecimal.Zero;
            Match match;

            switch (dataType)
            {
                case "dateTime":
                    match = Regex.Match(core, @"^(?<year>-?[0-9]{4,})-(?<month>[0-9]{2})-(?<day>[0-9]{2})T(?<time>.+)$", RegexOptions.CultureInvariant);
                    if (!match.Success || !TryYear(match.Groups["year"].Value, out year) ||
                        !int.TryParse(match.Groups["month"].Value, out month) || !int.TryParse(match.Groups["day"].Value, out day) ||
                        !TryTime(match.Groups["time"].Value, out hour, out minute, out second) || !IsDate(ToAstronomicalYear(year), month, day)) return false;
                    break;
                case "date":
                    match = Regex.Match(core, @"^(?<year>-?[0-9]{4,})-(?<month>[0-9]{2})-(?<day>[0-9]{2})$", RegexOptions.CultureInvariant);
                    if (!match.Success || !TryYear(match.Groups["year"].Value, out year) ||
                        !int.TryParse(match.Groups["month"].Value, out month) || !int.TryParse(match.Groups["day"].Value, out day) || !IsDate(ToAstronomicalYear(year), month, day)) return false;
                    break;
                case "time":
                    if (!TryTime(core, out hour, out minute, out second)) return false;
                    break;
                case "gYearMonth":
                    match = Regex.Match(core, @"^(?<year>-?[0-9]{4,})-(?<month>[0-9]{2})$", RegexOptions.CultureInvariant);
                    if (!match.Success || !TryYear(match.Groups["year"].Value, out year) ||
                        !int.TryParse(match.Groups["month"].Value, out month) || month is < 1 or > 12) return false;
                    break;
                case "gYear":
                    if (!TryYear(core, out year)) return false;
                    break;
                case "gMonthDay":
                    match = Regex.Match(core, @"^--(?<month>[0-9]{2})-(?<day>[0-9]{2})$", RegexOptions.CultureInvariant);
                    if (!match.Success || !int.TryParse(match.Groups["month"].Value, out month) ||
                        !int.TryParse(match.Groups["day"].Value, out day) || !IsDate(year, month, day)) return false;
                    break;
                case "gDay":
                    match = Regex.Match(core, @"^---(?<day>[0-9]{2})$", RegexOptions.CultureInvariant);
                    if (!match.Success || !int.TryParse(match.Groups["day"].Value, out day) || day is < 1 or > 31) return false;
                    break;
                case "gMonth":
                    match = Regex.Match(core, @"^--(?<month>[0-9]{2})--$", RegexOptions.CultureInvariant);
                    if (!match.Success || !int.TryParse(match.Groups["month"].Value, out month) || month is < 1 or > 12) return false;
                    break;
                default:
                    return false;
            }

            var astronomicalYear = ToAstronomicalYear(year);
            var local = ExactDecimal.FromInteger(DaysFromCivil(astronomicalYear, month, day) * 86400 + hour * 3600 + minute * 60) + second;
            if (offsetMinutes.HasValue)
            {
                var utc = local - ExactDecimal.FromInteger(offsetMinutes.Value * 60);
                interval = new Interval(utc, utc);
            }
            else
            {
                interval = new Interval(local - ExactDecimal.FromInteger(14 * 3600), local + ExactDecimal.FromInteger(14 * 3600));
            }
            return true;
        }

        private static bool TrySplitTimezone(string lexical, out string core, out int? offsetMinutes)
        {
            core = lexical;
            offsetMinutes = null;
            if (string.IsNullOrEmpty(lexical)) return false;
            if (lexical.EndsWith("Z", StringComparison.Ordinal))
            {
                core = lexical.Substring(0, lexical.Length - 1);
                offsetMinutes = 0;
                return core.Length > 0;
            }

            var match = Regex.Match(lexical, @"(?<sign>[+-])(?<hour>[0-9]{2}):(?<minute>[0-9]{2})$", RegexOptions.CultureInvariant);
            if (!match.Success) return true;
            if (!int.TryParse(match.Groups["hour"].Value, out var hour) || !int.TryParse(match.Groups["minute"].Value, out var minute) ||
                hour > 14 || minute > 59 || hour == 14 && minute != 0) return false;
            core = lexical.Substring(0, match.Index);
            offsetMinutes = (hour * 60 + minute) * (match.Groups["sign"].Value == "-" ? -1 : 1);
            return core.Length > 0;
        }

        private static bool TryYear(string lexical, out BigInteger year)
        {
            year = 0;
            var digits = lexical.StartsWith("-", StringComparison.Ordinal) ? lexical.Substring(1) : lexical;
            return digits.Length >= 4 && digits.Any(x => x != '0') && (digits.Length == 4 || digits[0] != '0') &&
                BigInteger.TryParse(lexical, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out year) &&
                year >= int.MinValue && year <= int.MaxValue;
        }

        private static bool TryTime(string lexical, out int hour, out int minute, out ExactDecimal second)
        {
            hour = minute = 0;
            second = ExactDecimal.Zero;
            var match = Regex.Match(lexical, @"^(?<hour>[0-9]{2}):(?<minute>[0-9]{2}):(?<second>[0-9]{2}(?:\.[0-9]+)?)$", RegexOptions.CultureInvariant);
            if (!match.Success || !int.TryParse(match.Groups["hour"].Value, out hour) ||
                !int.TryParse(match.Groups["minute"].Value, out minute) || minute > 59) return false;
            second = ParseExactDecimal(match.Groups["second"].Value);
            if (second.CompareTo(ExactDecimal.FromInteger(60)) >= 0 || hour > 24) return false;
            return hour != 24 || minute == 0 && second.CompareTo(ExactDecimal.Zero) == 0;
        }

        private static BigInteger ToAstronomicalYear(BigInteger lexicalYear) => lexicalYear.Sign < 0 ? lexicalYear + 1 : lexicalYear;

        private static bool IsDate(BigInteger astronomicalYear, int month, int day) =>
            month is >= 1 and <= 12 && day >= 1 && day <= DaysInMonth(astronomicalYear, month);

        private static int DaysInMonth(BigInteger astronomicalYear, int month)
        {
            if (month == 2)
            {
                var leap = astronomicalYear % 4 == 0 && (astronomicalYear % 100 != 0 || astronomicalYear % 400 == 0);
                return leap ? 29 : 28;
            }
            return month is 4 or 6 or 9 or 11 ? 30 : 31;
        }

        private static BigInteger DaysFromCivil(BigInteger year, int month, int day)
        {
            year -= month <= 2 ? 1 : 0;
            var era = FloorDivide(year, 400);
            var yearOfEra = year - era * 400;
            var adjustedMonth = month + (month > 2 ? -3 : 9);
            var dayOfYear = (153 * adjustedMonth + 2) / 5 + day - 1;
            var dayOfEra = yearOfEra * 365 + yearOfEra / 4 - yearOfEra / 100 + dayOfYear;
            return era * 146097 + dayOfEra;
        }

        private static CogsPrimitiveOrder CompareIntervals(Interval left, Interval right)
        {
            if (left.High.CompareTo(right.Low) < 0) return CogsPrimitiveOrder.Less;
            if (left.Low.CompareTo(right.High) > 0) return CogsPrimitiveOrder.Greater;
            if (left.Low.Equals(left.High) && right.Low.Equals(right.High) && left.Low.Equals(right.Low)) return CogsPrimitiveOrder.Equal;
            return CogsPrimitiveOrder.Indeterminate;
        }

        private static CogsPrimitiveOrder CompareDurations(string left, string right)
        {
            var a = ParseDuration(left);
            var b = ParseDuration(right);
            CogsPrimitiveOrder? result = null;
            foreach (var reference in new[] { (1696, 9), (1697, 2), (1903, 3), (1903, 7) })
            {
                var comparison = FromSign(AddDuration(reference.Item1, reference.Item2, a).CompareTo(AddDuration(reference.Item1, reference.Item2, b)));
                if (result.HasValue && result.Value != comparison) return CogsPrimitiveOrder.Indeterminate;
                result = comparison;
            }
            return result ?? CogsPrimitiveOrder.Equal;
        }

        private static DurationValue ParseDuration(string lexical)
        {
            var match = Regex.Match(lexical, @"^(?<negative>-)?P(?:(?<years>[0-9]+)Y)?(?:(?<months>[0-9]+)M)?(?:(?<days>[0-9]+)D)?(?:T(?:(?<hours>[0-9]+)H)?(?:(?<minutes>[0-9]+)M)?(?:(?<seconds>(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+))S)?)?$", RegexOptions.CultureInvariant);
            var sign = match.Groups["negative"].Success ? -1 : 1;
            var months = (ParseInteger(match.Groups["years"].Value) * 12 + ParseInteger(match.Groups["months"].Value)) * sign;
            var seconds = ExactDecimal.FromInteger(ParseInteger(match.Groups["days"].Value) * 86400 + ParseInteger(match.Groups["hours"].Value) * 3600 + ParseInteger(match.Groups["minutes"].Value) * 60);
            if (match.Groups["seconds"].Success) seconds += ParseExactDecimal(match.Groups["seconds"].Value);
            return new DurationValue(months, sign < 0 ? -seconds : seconds);
        }

        private static ExactDecimal AddDuration(int referenceYear, int referenceMonth, DurationValue duration)
        {
            var totalMonths = new BigInteger(referenceYear) * 12 + referenceMonth - 1 + duration.Months;
            var year = FloorDivide(totalMonths, 12);
            var month = (int)(totalMonths - year * 12) + 1;
            return ExactDecimal.FromInteger(DaysFromCivil(year, month, 1) * 86400) + duration.Seconds;
        }

        private static BigInteger ParseInteger(string value) =>
            string.IsNullOrEmpty(value) ? BigInteger.Zero : BigInteger.Parse(value, CultureInfo.InvariantCulture);

        private static ExactDecimal ParseExactDecimal(string lexical)
        {
            var negative = lexical.StartsWith("-", StringComparison.Ordinal);
            var unsigned = negative ? lexical.Substring(1) : lexical;
            var point = unsigned.IndexOf('.');
            var scale = point < 0 ? 0 : unsigned.Length - point - 1;
            var digits = point < 0 ? unsigned : unsigned.Remove(point, 1);
            var coefficient = BigInteger.Parse(digits, CultureInfo.InvariantCulture);
            return new ExactDecimal(negative ? -coefficient : coefficient, scale);
        }

        private static BigInteger FloorDivide(BigInteger value, BigInteger divisor)
        {
            var quotient = BigInteger.DivRem(value, divisor, out var remainder);
            return remainder.Sign < 0 ? quotient - 1 : quotient;
        }

        private static CogsPrimitiveOrder FromSign(int sign) => sign < 0
            ? CogsPrimitiveOrder.Less
            : sign > 0 ? CogsPrimitiveOrder.Greater : CogsPrimitiveOrder.Equal;

        private static string BuildBcp47Pattern()
        {
            const string regular = @"(?:(?:[A-Za-z]{2,3}(?:-[A-Za-z]{3}){0,3}|[A-Za-z]{4}|[A-Za-z]{5,8})(?:-[A-Za-z]{4})?(?:-(?:[A-Za-z]{2}|[0-9]{3}))?(?:-(?:[A-Za-z0-9]{5,8}|[0-9][A-Za-z0-9]{3}))*(?:-[0-9A-WY-Za-wy-z](?:-[A-Za-z0-9]{2,8})+)*(?:-[xX](?:-[A-Za-z0-9]{1,8})+)?|[xX](?:-[A-Za-z0-9]{1,8})+)";
            return "^(?:" + regular + "|" + string.Join("|", GrandfatheredTags.Select(CaseInsensitiveLiteral)) + ")$";
        }

        private static string CaseInsensitiveLiteral(string text)
        {
            var builder = new StringBuilder();
            foreach (var character in text)
            {
                if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                {
                    builder.Append('[').Append(char.ToLowerInvariant(character)).Append(char.ToUpperInvariant(character)).Append(']');
                }
                else builder.Append(character);
            }
            return builder.ToString();
        }

        private readonly record struct Interval(ExactDecimal Low, ExactDecimal High);
        private readonly record struct DurationValue(BigInteger Months, ExactDecimal Seconds);

        private readonly record struct ExactDecimal(BigInteger Coefficient, int Scale) : IComparable<ExactDecimal>
        {
            public static ExactDecimal Zero => new ExactDecimal(BigInteger.Zero, 0);
            public static ExactDecimal FromInteger(BigInteger value) => new ExactDecimal(value, 0);
            public int CompareTo(ExactDecimal other)
            {
                if (Scale == other.Scale) return Coefficient.CompareTo(other.Coefficient);
                var scale = Math.Max(Scale, other.Scale);
                return (Coefficient * BigInteger.Pow(10, scale - Scale)).CompareTo(other.Coefficient * BigInteger.Pow(10, scale - other.Scale));
            }
            public static ExactDecimal operator +(ExactDecimal left, ExactDecimal right) => Align(left, right, (a, b) => a + b);
            public static ExactDecimal operator -(ExactDecimal left, ExactDecimal right) => Align(left, right, (a, b) => a - b);
            public static ExactDecimal operator -(ExactDecimal value) => new ExactDecimal(-value.Coefficient, value.Scale);
            private static ExactDecimal Align(ExactDecimal left, ExactDecimal right, Func<BigInteger, BigInteger, BigInteger> operation)
            {
                var scale = Math.Max(left.Scale, right.Scale);
                return new ExactDecimal(operation(left.Coefficient * BigInteger.Pow(10, scale - left.Scale), right.Coefficient * BigInteger.Pow(10, scale - right.Scale)), scale);
            }
        }
    }
}
