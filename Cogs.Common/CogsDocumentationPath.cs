// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Cogs.Common
{
    /// <summary>Describes why an authored article TOC entry is not portable and safe.</summary>
    public enum CogsDocumentationPathStatus
    {
        Valid,
        Blank,
        NotNormalized,
        DirectiveSyntax,
        OutsideRoot,
        UnsupportedExtension,
        RootMissing,
        Missing,
        IncorrectCase,
        Ambiguous,
        LinkTraversal,
        FileSystemError
    }

    /// <summary>
    /// Implements the portable COGS article document-name grammar and exact-case
    /// filesystem lookup. TOC values use forward slashes and may omit .rst/.md.
    /// </summary>
    public static class CogsDocumentationPath
    {
        private static readonly char[] PortableForbiddenCharacters = { ':', '*', '?', '"', '<', '>', '|', '#', '[', ']' };

        /// <summary>Returns whether a value is one portable, exact, non-directive path segment.</summary>
        public static bool IsPortableSingleSegment(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
            value.IsNormalized(NormalizationForm.FormC) &&
            value != "." && value != ".." &&
            !value.StartsWith(".. ", StringComparison.Ordinal) &&
            !Path.IsPathRooted(value) &&
            value.IndexOf('/') < 0 && value.IndexOf('\\') < 0 &&
            value.IndexOfAny(PortableForbiddenCharacters) < 0 &&
            !value.Any(char.IsControl) &&
            !value.EndsWith(".", StringComparison.Ordinal) &&
            !IsWindowsDeviceName(value);

        public static CogsDocumentationPathStatus Normalize(string value, out string normalized)
        {
            normalized = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return CogsDocumentationPathStatus.Blank;
            }

            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                !value.IsNormalized(NormalizationForm.FormC) ||
                value.IndexOf('\\') >= 0 ||
                value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
            {
                return CogsDocumentationPathStatus.NotNormalized;
            }

            if (value.StartsWith("/", StringComparison.Ordinal) || Path.IsPathRooted(value))
            {
                return CogsDocumentationPathStatus.OutsideRoot;
            }

            string[] components = value.Split('/');
            if (components.Length == 0 || components.Any(component =>
                    component.Length == 0 ||
                    component == "." ||
                    !string.Equals(component, component.Trim(), StringComparison.Ordinal)))
            {
                return CogsDocumentationPathStatus.NotNormalized;
            }
            if (components.Any(component => component == ".."))
            {
                return CogsDocumentationPathStatus.OutsideRoot;
            }
            if (components.Any(component =>
                    !IsPortableSingleSegment(component)))
            {
                return CogsDocumentationPathStatus.DirectiveSyntax;
            }

            string extension = Path.GetExtension(components[^1]);
            if ((string.Equals(extension, ".rst", StringComparison.OrdinalIgnoreCase) && extension != ".rst") ||
                (string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) && extension != ".md"))
            {
                return CogsDocumentationPathStatus.UnsupportedExtension;
            }

            normalized = string.Join('/', components);
            return CogsDocumentationPathStatus.Valid;
        }

        public static CogsDocumentationPathStatus Resolve(
            string articleRoot,
            string normalized,
            out string resolvedPath)
        {
            resolvedPath = string.Empty;
            CogsDocumentationPathStatus normalization = Normalize(normalized, out string checkedPath);
            if (normalization != CogsDocumentationPathStatus.Valid)
            {
                return normalization;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(articleRoot) || !Directory.Exists(articleRoot))
                {
                    return CogsDocumentationPathStatus.RootMissing;
                }

                string root = Path.GetFullPath(articleRoot);
                if (IsLink(root))
                {
                    return CogsDocumentationPathStatus.LinkTraversal;
                }

                string current = root;
                string[] components = checkedPath.Split('/');
                foreach (string component in components.Take(components.Length - 1))
                {
                    string[] matches = Directory.EnumerateDirectories(current)
                        .Where(path => string.Equals(Path.GetFileName(path), component, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    string exact = matches.FirstOrDefault(path => string.Equals(Path.GetFileName(path), component, StringComparison.Ordinal));
                    if (matches.Length > 1)
                    {
                        return CogsDocumentationPathStatus.Ambiguous;
                    }
                    if (exact == null)
                    {
                        return matches.Length == 1
                            ? CogsDocumentationPathStatus.IncorrectCase
                            : CogsDocumentationPathStatus.Missing;
                    }
                    if (IsLink(exact))
                    {
                        return CogsDocumentationPathStatus.LinkTraversal;
                    }
                    current = exact;
                }

                string leaf = components[^1];
                string extension = Path.GetExtension(leaf);
                string[] expectedNames = extension is ".rst" or ".md"
                    ? new[] { leaf }
                    : new[] { leaf + ".rst", leaf + ".md" };
                string[] matchesIgnoringCase = Directory.EnumerateFiles(current)
                    .Where(path => expectedNames.Any(expected =>
                        string.Equals(Path.GetFileName(path), expected, StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
                string[] exactMatches = matchesIgnoringCase
                    .Where(path => expectedNames.Contains(Path.GetFileName(path), StringComparer.Ordinal))
                    .ToArray();
                if (matchesIgnoringCase.Length > 1 || exactMatches.Length > 1)
                {
                    return CogsDocumentationPathStatus.Ambiguous;
                }
                if (exactMatches.Length == 0)
                {
                    return matchesIgnoringCase.Length == 1
                        ? CogsDocumentationPathStatus.IncorrectCase
                        : CogsDocumentationPathStatus.Missing;
                }

                string result = Path.GetFullPath(exactMatches[0]);
                if (!IsWithin(root, result))
                {
                    return CogsDocumentationPathStatus.OutsideRoot;
                }
                if (IsLink(result))
                {
                    return CogsDocumentationPathStatus.LinkTraversal;
                }

                resolvedPath = result;
                return CogsDocumentationPathStatus.Valid;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
            {
                return CogsDocumentationPathStatus.FileSystemError;
            }
        }

        public static string Describe(CogsDocumentationPathStatus status) => status switch
        {
            CogsDocumentationPathStatus.Blank => "must not be blank",
            CogsDocumentationPathStatus.NotNormalized => "must be an NFC-normalized relative path using forward slashes with no empty or dot segments or surrounding whitespace",
            CogsDocumentationPathStatus.DirectiveSyntax => "contains Sphinx directive, option, explicit-title, control, or non-portable path syntax",
            CogsDocumentationPathStatus.OutsideRoot => "must remain inside its Articles directory",
            CogsDocumentationPathStatus.UnsupportedExtension => "must use the exact lowercase .rst or .md suffix when a source suffix is included",
            CogsDocumentationPathStatus.RootMissing => "references an Articles directory that does not exist",
            CogsDocumentationPathStatus.Missing => "does not resolve to an existing .rst or .md article",
            CogsDocumentationPathStatus.IncorrectCase => "does not use the exact filesystem casing of its article",
            CogsDocumentationPathStatus.Ambiguous => "matches more than one article or case-equivalent filesystem entry",
            CogsDocumentationPathStatus.LinkTraversal => "traverses a symbolic link or reparse point instead of remaining in its Articles directory",
            CogsDocumentationPathStatus.FileSystemError => "could not be resolved because its Articles directory could not be enumerated",
            _ => string.Empty
        };

        public static bool IsWithin(string root, string path)
        {
            string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            string fullPath = Path.GetFullPath(path);
            if (string.Equals(fullRoot, fullPath, PathComparison))
            {
                return true;
            }
            return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, PathComparison);
        }

        private static bool IsLink(string path) =>
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        private static bool IsWindowsDeviceName(string segment)
        {
            string stem = segment.Split('.')[0];
            if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("NUL", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return stem.Length == 4 && stem[3] is >= '1' and <= '9' &&
                (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase));
        }

        private static StringComparison PathComparison =>
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }
}
