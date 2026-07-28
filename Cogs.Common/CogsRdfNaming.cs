using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cogs.Common
{
    /// <summary>
    /// Defines the common RDF term naming contract used by every COGS publisher.
    /// Class terms retain their model spelling; property terms use word-aware camel case.
    /// </summary>
    public static class CogsRdfNaming
    {
        /// <summary>Converts a COGS property name to its RDF local name.</summary>
        /// <exception cref="ArgumentException">The value contains no letters or digits.</exception>
        public static string ToPropertyLocalName(string name)
        {
            if (!TryToPropertyLocalName(name, out string result))
            {
                throw new ArgumentException(
                    $"Property name '{name}' cannot be normalized to an RDF term.",
                    nameof(name));
            }

            return result;
        }

        /// <summary>Attempts to convert a COGS property name to its RDF local name.</summary>
        public static bool TryToPropertyLocalName(string name, out string result)
        {
            result = string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string normalized = name.Normalize(NormalizationForm.FormC);
            List<List<Rune>> words = SplitWords(normalized);
            if (words.Count == 0)
            {
                return false;
            }

            var builder = new StringBuilder(normalized.Length);
            for (int wordIndex = 0; wordIndex < words.Count; wordIndex++)
            {
                IReadOnlyList<Rune> word = words[wordIndex];
                for (int runeIndex = 0; runeIndex < word.Count; runeIndex++)
                {
                    Rune rune = word[runeIndex];
                    if (wordIndex > 0 && runeIndex == 0 && IsCasedLetter(rune))
                    {
                        builder.Append(Rune.ToUpperInvariant(rune));
                    }
                    else
                    {
                        builder.Append(Rune.ToLowerInvariant(rune));
                    }
                }
            }

            result = builder.ToString();
            return result.Length > 0;
        }

        /// <summary>
        /// Returns the namespace base used for RDF terms. Existing hash or slash
        /// delimiters are retained; otherwise a hash delimiter is appended.
        /// </summary>
        public static string GetTermBase(string namespaceUrl)
        {
            if (string.IsNullOrWhiteSpace(namespaceUrl))
            {
                throw new ArgumentException("An RDF namespace URL is required.", nameof(namespaceUrl));
            }

            return namespaceUrl.EndsWith("#", StringComparison.Ordinal) ||
                   namespaceUrl.EndsWith("/", StringComparison.Ordinal)
                ? namespaceUrl
                : namespaceUrl + "#";
        }

        /// <summary>Builds an RDF class IRI while retaining the exact COGS class name.</summary>
        public static string ClassIri(string namespaceUrl, string className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                throw new ArgumentException("An RDF class name is required.", nameof(className));
            }

            return GetTermBase(namespaceUrl) + className;
        }

        /// <summary>Builds an RDF property IRI using the common camel-case local name.</summary>
        public static string PropertyIri(string namespaceUrl, string propertyName) =>
            GetTermBase(namespaceUrl) + ToPropertyLocalName(propertyName);

        private static List<List<Rune>> SplitWords(string value)
        {
            Rune[] runes = value.EnumerateRunes().ToArray();
            var words = new List<List<Rune>>();
            var current = new List<Rune>();

            for (int index = 0; index < runes.Length; index++)
            {
                Rune rune = runes[index];
                if (!IsTermCharacter(rune))
                {
                    Flush(current, words);
                    continue;
                }

                if (IsMark(rune))
                {
                    if (current.Count > 0)
                    {
                        current.Add(rune);
                    }
                    continue;
                }

                Rune? previous = PreviousBaseRune(current);
                Rune? next = NextBaseRune(runes, index + 1);
                if (previous.HasValue && StartsNewWord(previous.Value, rune, next))
                {
                    Flush(current, words);
                }

                current.Add(rune);
            }

            Flush(current, words);
            return words;
        }

        private static bool StartsNewWord(Rune previous, Rune current, Rune? next)
        {
            if (Rune.IsDigit(previous) != Rune.IsDigit(current))
            {
                return true;
            }

            if (!Rune.IsLetter(previous) || !Rune.IsLetter(current))
            {
                return false;
            }

            if (IsUpperLike(current) && !IsUpperLike(previous))
            {
                return true;
            }

            return IsUpperLike(previous) && IsUpperLike(current) &&
                   next.HasValue && IsLowerLike(next.Value);
        }

        private static Rune? PreviousBaseRune(IReadOnlyList<Rune> runes)
        {
            for (int index = runes.Count - 1; index >= 0; index--)
            {
                if (!IsMark(runes[index]))
                {
                    return runes[index];
                }
            }

            return null;
        }

        private static Rune? NextBaseRune(IReadOnlyList<Rune> runes, int start)
        {
            for (int index = start; index < runes.Count; index++)
            {
                Rune rune = runes[index];
                if (!IsTermCharacter(rune))
                {
                    return null;
                }
                if (!IsMark(rune))
                {
                    return rune;
                }
            }

            return null;
        }

        private static void Flush(List<Rune> current, ICollection<List<Rune>> words)
        {
            if (current.Count == 0)
            {
                return;
            }

            words.Add(new List<Rune>(current));
            current.Clear();
        }

        private static bool IsTermCharacter(Rune rune) => Rune.IsLetterOrDigit(rune) || IsMark(rune);

        private static bool IsMark(Rune rune) => Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.EnclosingMark;

        private static bool IsUpperLike(Rune rune) => Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter;

        private static bool IsLowerLike(Rune rune) =>
            Rune.GetUnicodeCategory(rune) == UnicodeCategory.LowercaseLetter;

        private static bool IsCasedLetter(Rune rune) => IsUpperLike(rune) || IsLowerLike(rune);
    }
}
