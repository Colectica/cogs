using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Cogs.Common
{
    /// <summary>
    /// Parsers for convention values shared by validation and model construction.
    /// They are deliberately strict so publishers never reinterpret CSV text.
    /// </summary>
    public static class CogsConventions
    {
        private static readonly Regex SemVerPattern = new Regex(
            @"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$",
            RegexOptions.CultureInvariant);

        public static bool TryParseCardinality(
            string minimumText,
            string maximumText,
            out BigInteger minimum,
            out BigInteger? maximum,
            out string error)
        {
            var min = string.IsNullOrWhiteSpace(minimumText) ? "0" : minimumText;
            var max = string.IsNullOrWhiteSpace(maximumText) ? "n" : maximumText;

            if (!TryParseCanonicalNonNegativeInteger(min, out minimum))
            {
                maximum = null;
                error = $"minimum cardinality '{minimumText}' is not a canonical non-negative integer";
                return false;
            }

            if (max == "n")
            {
                maximum = null;
            }
            else if (TryParseCanonicalNonNegativeInteger(max, out var finiteMaximum))
            {
                maximum = finiteMaximum;
            }
            else
            {
                maximum = null;
                error = $"maximum cardinality '{maximumText}' is not a canonical non-negative integer or 'n'";
                return false;
            }

            if (maximum.HasValue && minimum > maximum.Value)
            {
                error = $"minimum cardinality {minimum} exceeds maximum cardinality {maximum.Value}";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryParseFlag(string text, out bool value)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            if (text.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            value = false;
            return false;
        }

        public static bool IsCanonicalSemVer(string value) =>
            !string.IsNullOrWhiteSpace(value) && SemVerPattern.IsMatch(value);

        public static IReadOnlyList<string> ParseEnumeration(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            return text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        public static bool IsPortablePattern(string pattern, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(pattern))
            {
                return true;
            }

            var inClass = false;
            var escaped = false;
            for (var index = 0; index < pattern.Length; index++)
            {
                var current = pattern[index];
                if (escaped)
                {
                    const string allowed = @".[](){}?*+|\\-^$trn";
                    if (!allowed.Contains(current))
                    {
                        error = $"escape '\\{current}' is not in the portable pattern subset";
                        return false;
                    }
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (current == '[')
                {
                    if (inClass)
                    {
                        error = "nested character classes and class subtraction are not portable";
                        return false;
                    }
                    inClass = true;
                    continue;
                }
                if (current == ']')
                {
                    if (!inClass)
                    {
                        error = "a literal closing bracket must be escaped";
                        return false;
                    }
                    inClass = false;
                    continue;
                }
                if (!inClass && current == '^' || !inClass && current == '$')
                {
                    error = "anchors are not in the portable pattern subset; COGS patterns use substring semantics";
                    return false;
                }
                if (!inClass && current == '(' && index + 1 < pattern.Length && pattern[index + 1] == '?')
                {
                    error = "special groups and lookarounds are not in the portable pattern subset";
                    return false;
                }
                if (!inClass && current == '?' && index > 0 && pattern[index - 1] is '?' or '*' or '+' or '}' && !IsEscaped(pattern, index - 1))
                {
                    error = "lazy quantifiers are not in the portable pattern subset";
                    return false;
                }
            }

            if (escaped || inClass)
            {
                error = "pattern contains an unterminated escape or character class";
                return false;
            }

            try
            {
                _ = new Regex(CogsPortablePattern.ForDotNet(pattern), RegexOptions.CultureInvariant);
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool IsEscaped(string pattern, int index)
        {
            int backslashes = 0;
            while (--index >= 0 && pattern[index] == '\\')
            {
                backslashes++;
            }
            return backslashes % 2 != 0;
        }

        public static string TranslatePortablePattern(string pattern, bool xsd = false)
        {
            System.Text.StringBuilder result = new System.Text.StringBuilder();
            bool inClass = false;
            for (int index = 0; index < pattern.Length; index++)
            {
                char current = pattern[index];
                if (current == '\\' && index + 1 < pattern.Length)
                {
                    char escaped = pattern[++index];
                    if (!xsd || escaped is not '$' and not '^')
                    {
                        result.Append('\\');
                    }
                    result.Append(escaped);
                }
                else if (current == '.' && !inClass)
                {
                    result.Append("[^\\r\\n\u2028\u2029]");
                }
                else
                {
                    result.Append(current);
                    if (current == '[')
                    {
                        inClass = true;
                    }
                    else if (current == ']')
                    {
                        inClass = false;
                    }
                }
            }
            return result.ToString();
        }

        private static bool TryParseCanonicalNonNegativeInteger(string text, out BigInteger value)
        {
            if (string.IsNullOrEmpty(text) || (text.Length > 1 && text[0] == '0'))
            {
                value = 0;
                return false;
            }

            return BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;
        }
    }
}
