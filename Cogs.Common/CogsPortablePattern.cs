using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Cogs.Common
{
    /// <summary>Compiles the portable scalar-character grammar for .NET's UTF-16 regex engine.</summary>
    public static class CogsPortablePattern
    {
        public static bool IsMatch(string value, string pattern)
        {
            return Regex.IsMatch(value, ForDotNet(pattern), RegexOptions.CultureInvariant);
        }

        public static string ForDotNet(string pattern)
        {
            StringBuilder result = new StringBuilder();
            for (int index = 0; index < pattern.Length; index++)
            {
                char current = pattern[index];
                if (current == '\\')
                {
                    result.Append(current).Append(pattern[++index]);
                }
                else if (current == '.')
                {
                    result.Append(Ranges([(0, 9), (11, 12), (14, 0x2027), (0x202A, 0x10FFFF)]));
                }
                else if (current == '[')
                {
                    bool negative = index + 1 < pattern.Length && pattern[index + 1] == '^';
                    index += negative ? 2 : 1;
                    List<(int Start, int End)> ranges = new List<(int, int)>();
                    while (index < pattern.Length && pattern[index] != ']')
                    {
                        int start = Character(pattern, ref index);
                        int end = start;
                        if (index + 1 < pattern.Length && pattern[index] == '-' && pattern[index + 1] != ']')
                        {
                            index++;
                            end = Character(pattern, ref index);
                        }
                        if (end < start)
                        {
                            throw new ArgumentException("A character-class range must be in Unicode scalar order.");
                        }
                        ranges.Add((start, end));
                    }
                    if (index >= pattern.Length || ranges.Count == 0)
                    {
                        throw new ArgumentException("A character class must contain characters and a closing bracket.");
                    }
                    if (negative)
                    {
                        List<(int, int)> complement = new List<(int, int)>();
                        int next = 0;
                        foreach ((int start, int end) in ranges.OrderBy(range => range.Start))
                        {
                            if (start > next)
                            {
                                complement.Add((next, start - 1));
                            }
                            next = Math.Max(next, end + 1);
                        }
                        if (next <= 0x10FFFF)
                        {
                            complement.Add((next, 0x10FFFF));
                        }
                        ranges = complement;
                    }
                    result.Append(Ranges(ranges));
                }
                else if (char.IsHighSurrogate(current))
                {
                    int scalar = Character(pattern, ref index);
                    index--;
                    result.Append(Ranges([(scalar, scalar)]));
                }
                else
                {
                    result.Append(current);
                }
            }
            return result.ToString();
        }

        private static int Character(string pattern, ref int index)
        {
            char current = pattern[index++];
            if (current == '\\')
            {
                return pattern[index++] switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    char literal => literal
                };
            }
            if (char.IsHighSurrogate(current) && index < pattern.Length && char.IsLowSurrogate(pattern[index]))
            {
                return char.ConvertToUtf32(current, pattern[index++]);
            }
            if (char.IsSurrogate(current))
            {
                throw new ArgumentException("Patterns must contain Unicode scalar characters.");
            }
            return current;
        }

        private static string Ranges(IEnumerable<(int Start, int End)> ranges)
        {
            List<string> alternatives = new List<string>();
            foreach ((int start, int end) in ranges)
            {
                Add(start, Math.Min(end, 0xD7FF));
                Add(Math.Max(start, 0xE000), Math.Min(end, 0xFFFF));
                int lower = Math.Max(start, 0x10000);
                if (lower > end)
                {
                    continue;
                }
                int firstHigh = 0xD800 + ((lower - 0x10000) >> 10);
                int lastHigh = 0xD800 + ((end - 0x10000) >> 10);
                int firstLow = 0xDC00 + ((lower - 0x10000) & 0x3FF);
                int lastLow = 0xDC00 + ((end - 0x10000) & 0x3FF);
                if (firstHigh == lastHigh)
                {
                    alternatives.Add(Unit(firstHigh) + Class(firstLow, lastLow));
                }
                else
                {
                    alternatives.Add(Unit(firstHigh) + Class(firstLow, 0xDFFF));
                    if (firstHigh + 1 < lastHigh)
                    {
                        alternatives.Add(Class(firstHigh + 1, lastHigh - 1) + Class(0xDC00, 0xDFFF));
                    }
                    alternatives.Add(Unit(lastHigh) + Class(0xDC00, lastLow));
                }
            }
            return alternatives.Count == 0 ? "(?!)" : "(?:" + string.Join("|", alternatives) + ")";

            void Add(int start, int end)
            {
                if (start <= end)
                {
                    alternatives.Add(Class(start, end));
                }
            }
        }

        private static string Unit(int value)
        {
            return "\\u" + value.ToString("X4", CultureInfo.InvariantCulture);
        }

        private static string Class(int start, int end)
        {
            return start == end ? Unit(start) : "[" + Unit(start) + "-" + Unit(end) + "]";
        }
    }
}
