#nullable enable

using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Cogs.Common
{
    /// <summary>
    /// The lossless component representation used by the JSON contract for the
    /// five XSD Gregorian datatypes.
    /// </summary>
    public readonly record struct CogsGregorianValue(
        int? Year,
        int? Month,
        int? Day,
        string? Timezone);

    /// <summary>
    /// Converts between the structured COGS JSON representation and the native
    /// XSD lexical representation used by XML, RDF, facets, and enumerations.
    /// </summary>
    public static class CogsGregorianLexical
    {
        private static readonly Regex TimezoneRegex = new(
            @"^(?:Z|[+-](?:(?:0[0-9]|1[0-3]):[0-5][0-9]|14:00))$",
            RegexOptions.CultureInvariant);

        public static bool IsGregorianType(string? dataType) => dataType is
            "gYearMonth" or "gYear" or "gMonthDay" or "gDay" or "gMonth";

        public static bool TryParse(string dataType, string lexical, out CogsGregorianValue value)
        {
            value = default;
            if (!IsGregorianType(dataType) || string.IsNullOrEmpty(lexical) ||
                !TrySplitTimezone(lexical, out string core, out string? timezone))
            {
                return false;
            }

            int? year = null;
            int? month = null;
            int? day = null;
            Match match;
            switch (dataType)
            {
                case "gYearMonth":
                    match = Regex.Match(core, @"^(?<year>-?[0-9]{4,})-(?<month>[0-9]{2})$",
                        RegexOptions.CultureInvariant);
                    if (!match.Success ||
                        !TryYear(match.Groups["year"].Value, out int parsedYear) ||
                        !TryComponent(match.Groups["month"].Value, out int parsedMonth))
                    {
                        return false;
                    }
                    year = parsedYear;
                    month = parsedMonth;
                    break;
                case "gYear":
                    if (!TryYear(core, out int parsedYearOnly)) return false;
                    year = parsedYearOnly;
                    break;
                case "gMonthDay":
                    match = Regex.Match(core, @"^--(?<month>[0-9]{2})-(?<day>[0-9]{2})$",
                        RegexOptions.CultureInvariant);
                    if (!match.Success ||
                        !TryComponent(match.Groups["month"].Value, out int parsedMonthDayMonth) ||
                        !TryComponent(match.Groups["day"].Value, out int parsedMonthDayDay))
                    {
                        return false;
                    }
                    month = parsedMonthDayMonth;
                    day = parsedMonthDayDay;
                    break;
                case "gDay":
                    match = Regex.Match(core, @"^---(?<day>[0-9]{2})$", RegexOptions.CultureInvariant);
                    if (!match.Success || !TryComponent(match.Groups["day"].Value, out int parsedDay))
                    {
                        return false;
                    }
                    day = parsedDay;
                    break;
                case "gMonth":
                    match = Regex.Match(core, @"^--(?<month>[0-9]{2})--$", RegexOptions.CultureInvariant);
                    if (!match.Success || !TryComponent(match.Groups["month"].Value, out int parsedMonthOnly))
                    {
                        return false;
                    }
                    month = parsedMonthOnly;
                    break;
                default:
                    return false;
            }

            var candidate = new CogsGregorianValue(year, month, day, timezone);
            if (!TryFormat(dataType, candidate, out string canonical) ||
                !string.Equals(canonical, lexical, StringComparison.Ordinal))
            {
                return false;
            }
            value = candidate;
            return true;
        }

        public static bool TryFormat(string dataType, CogsGregorianValue value, out string lexical)
        {
            lexical = string.Empty;
            if (!IsGregorianType(dataType) || !IsTimezone(value.Timezone)) return false;

            string core;
            switch (dataType)
            {
                case "gYearMonth":
                    if (!IsYear(value.Year) || !IsMonth(value.Month) || value.Day.HasValue) return false;
                    core = $"{FormatYear(value.Year!.Value)}-{value.Month!.Value:00}";
                    break;
                case "gYear":
                    if (!IsYear(value.Year) || value.Month.HasValue || value.Day.HasValue) return false;
                    core = FormatYear(value.Year!.Value);
                    break;
                case "gMonthDay":
                    if (value.Year.HasValue || !IsMonth(value.Month) || !IsMonthDay(value.Month!.Value, value.Day))
                    {
                        return false;
                    }
                    core = $"--{value.Month.Value:00}-{value.Day!.Value:00}";
                    break;
                case "gDay":
                    if (value.Year.HasValue || value.Month.HasValue ||
                        !value.Day.HasValue || value.Day.Value is < 1 or > 31)
                    {
                        return false;
                    }
                    core = $"---{value.Day.Value:00}";
                    break;
                case "gMonth":
                    if (value.Year.HasValue || !IsMonth(value.Month) || value.Day.HasValue) return false;
                    core = $"--{value.Month!.Value:00}--";
                    break;
                default:
                    return false;
            }

            lexical = core + (value.Timezone ?? string.Empty);
            return true;
        }

        private static bool TrySplitTimezone(string lexical, out string core, out string? timezone)
        {
            core = lexical;
            timezone = null;
            if (lexical.EndsWith("Z", StringComparison.Ordinal))
            {
                core = lexical[..^1];
                timezone = "Z";
                return core.Length > 0;
            }
            if (lexical.Length >= 6)
            {
                string candidate = lexical[^6..];
                if ((candidate[0] == '+' || candidate[0] == '-') && TimezoneRegex.IsMatch(candidate))
                {
                    core = lexical[..^6];
                    timezone = candidate;
                }
            }
            return core.Length > 0;
        }

        private static bool TryYear(string lexical, out int year)
        {
            year = 0;
            string digits = lexical.StartsWith("-", StringComparison.Ordinal) ? lexical[1..] : lexical;
            return digits.Length >= 4 &&
                digits.Any(character => character != '0') &&
                (digits.Length == 4 || digits[0] != '0') &&
                int.TryParse(lexical, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out year) &&
                year != 0;
        }

        private static bool TryComponent(string lexical, out int component) =>
            int.TryParse(lexical, NumberStyles.None, CultureInfo.InvariantCulture, out component);

        private static bool IsYear(int? year) => year.HasValue && year.Value != 0;
        private static bool IsMonth(int? month) => month is >= 1 and <= 12;

        private static bool IsMonthDay(int month, int? day)
        {
            if (!day.HasValue || day.Value < 1) return false;
            int maximum = month switch
            {
                2 => 29,
                4 or 6 or 9 or 11 => 30,
                _ => 31
            };
            return day.Value <= maximum;
        }

        private static bool IsTimezone(string? timezone) =>
            timezone is null || TimezoneRegex.IsMatch(timezone);

        private static string FormatYear(int year)
        {
            long magnitude = Math.Abs((long)year);
            string digits = magnitude.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
            return year < 0 ? "-" + digits : digits;
        }
    }
}
