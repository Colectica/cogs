using Cogs.Common;
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Cogs.Dto
{
    public class RewriteCsvFormat
    {

        public List<CogsError> Errors { get; } = new List<CogsError>();

        internal Action<string, int> BeforeReplace { get; set; }

        internal Func<string> GitExecutableEnvironmentReader { get; set; } =
            () => Environment.GetEnvironmentVariable("COGS_GIT");

        internal Func<string, string, IReadOnlyList<string>, GitCommandResult> GitCommandRunner { get; set; } =
            ExecuteGitCommand;

        public void Rewrite(string directory) => Rewrite(directory, upgradeCogs2: false);

        public void Rewrite(string directory, bool upgradeCogs2)
        {
            Errors.Clear();
            if (string.IsNullOrWhiteSpace(directory))
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "COGS-RW-001", "A COGS directory is required."));
                return;
            }

            string source;
            try
            {
                source = ResolveSourceDirectory(directory);
            }
            catch (Exception exception)
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "COGS-RW-002", exception.Message,
                    sourcePath: directory, exception: exception));
                return;
            }

            string parent = Path.GetDirectoryName(source)!;
            string leaf = Path.GetFileName(source);
            string staging = Path.Combine(parent, $".{leaf}.cogs-rewrite-stage-{Guid.NewGuid():N}");
            string backup = Path.Combine(parent, $".{leaf}.cogs-rewrite-backup-{Guid.NewGuid():N}");
            bool preserveBackup = false;

            try
            {
                IReadOnlyList<RewriteSourceFile> sourceFiles = StageRewriteFiles(source, staging);
                IReadOnlyList<RewriteRename> renames = Array.Empty<RewriteRename>();
                if (upgradeCogs2)
                {
                    renames = UpgradeCogs2InPlace(staging);
                }
                else
                {
                    RewriteInPlace(staging);
                }
                if (Errors.Any(error => error.Level == ErrorLevel.Error))
                {
                    return;
                }

                CommitChangedFiles(source, staging, backup, sourceFiles, renames);
                TryDeleteDirectory(backup);
            }
            catch (GitIntegrationException exception)
            {
                AddGitDiagnostic(exception);
            }
            catch (RewriteCommitException exception)
            {
                preserveBackup = !exception.RollbackComplete;
                GitIntegrationException gitException = FindGitIntegrationException(exception);
                if (gitException != null)
                {
                    AddGitDiagnostic(gitException);
                }
                string message = exception.RollbackComplete
                    ? $"Could not rewrite '{directory}'. Existing model files were preserved."
                    : $"Could not rewrite '{directory}', and rollback was incomplete. Recovery files remain below '{backup}'.";
                Errors.Add(new CogsError(
                    ErrorLevel.Error,
                    "COGS-RW-003",
                    message,
                    sourcePath: source,
                    exception: exception));
            }
            catch (Exception exception)
            {
                Errors.Add(new CogsError(
                    ErrorLevel.Error,
                    "COGS-RW-003",
                    $"Could not rewrite '{directory}'. Existing model files were preserved.",
                    sourcePath: source,
                    exception: exception));
            }
            finally
            {
                RemapDiagnosticPaths(staging, source);
                TryDeleteDirectory(staging);
                if (!preserveBackup)
                {
                    TryDeleteDirectory(backup);
                }
            }
        }

        private IReadOnlyList<RewriteRename> UpgradeCogs2InPlace(string directory)
        {
            IReadOnlyList<RewriteRename> renames = NormalizeLegacyMarkerCasing(directory);
            var settingsPath = Path.Combine(directory, "Settings", "Settings.csv");
            var settings = ReadRecords<Setting>(settingsPath);
            if (settings == null)
            {
                return renames;
            }

            var duplicates = settings
                .GroupBy(setting => setting.Key, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicates.Length > 0)
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "MIG2001",
                    $"Cannot migrate duplicate settings: {string.Join(", ", duplicates)}.", settingsPath));
                return renames;
            }

            var byKey = settings.ToDictionary(setting => setting.Key, StringComparer.Ordinal);
            foreach (var requiredValue in new[] { "Title", "ShortTitle", "Slug", "Version", "NamespaceUrl", "NamespacePrefix" })
            {
                if (!byKey.TryGetValue(requiredValue, out var setting) || string.IsNullOrWhiteSpace(setting.Value))
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, "MIG2002",
                        $"Migration requires a nonempty {requiredValue} setting; choosing it is a semantic decision.", settingsPath));
                }
            }
            if (Errors.Any(error => error.Level == ErrorLevel.Error))
            {
                return renames;
            }

            if (!TryNormalizeSemVer(byKey["Version"].Value, out var normalizedVersion))
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "MIG2003",
                    $"Version '{byKey["Version"].Value}' cannot be unambiguously converted to canonical SemVer.", settingsPath));
                return renames;
            }
            byKey["Version"].Value = normalizedVersion;

            if (byKey.TryGetValue("CogsVersion", out var cogsVersion) && cogsVersion.Value != "2.0")
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "MIG2009",
                    $"Existing CogsVersion '{cogsVersion.Value}' is not a legacy model that can be upgraded to 2.0.", settingsPath));
                return renames;
            }
            AddSetting(settings, byKey, "CogsVersion", "2.0", insertFirst: true);
            AddSetting(settings, byKey, "Description", string.Empty);
            AddSetting(settings, byKey, "Author", string.Empty);
            AddSetting(settings, byKey, "Copyright", string.Empty);
            WriteRecords(settingsPath, settings);

            var propertyFiles = new List<string>
            {
                Path.Combine(directory, "Settings", "Identification.csv")
            };
            var mixin = Path.Combine(directory, "Settings", "Identification.Mixin.csv");
            if (File.Exists(mixin))
            {
                propertyFiles.Add(mixin);
            }
            foreach (var kind in new[] { "ItemTypes", "CompositeTypes" })
            {
                var kindPath = Path.Combine(directory, kind);
                if (!Directory.Exists(kindPath))
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, "MIG2004",
                        $"Required directory '{kind}' is missing or has different casing.", kindPath));
                    continue;
                }
                foreach (var typeDirectory in Directory.EnumerateDirectories(kindPath))
                {
                    var file = Path.Combine(typeDirectory, Path.GetFileName(typeDirectory) + ".csv");
                    if (File.Exists(file))
                    {
                        propertyFiles.Add(file);
                    }
                }
            }

            foreach (var propertyFile in propertyFiles)
            {
                var properties = ReadRecords<Property>(propertyFile);
                if (properties == null)
                {
                    continue;
                }

                bool changed = false;
                foreach (var property in properties)
                {
                    if (!TryNormalizeCardinality(property.MinCardinality, property.MaxCardinality,
                            out var minimum, out var maximum))
                    {
                        Errors.Add(new CogsError(ErrorLevel.Error, "MIG2005",
                            $"Property '{property.Name}' has cardinality that cannot be unambiguously migrated.", propertyFile));
                        continue;
                    }
                    changed |= !string.Equals(property.MinCardinality, minimum, StringComparison.Ordinal) ||
                               !string.Equals(property.MaxCardinality, maximum, StringComparison.Ordinal);
                    property.MinCardinality = minimum;
                    property.MaxCardinality = maximum;

                    if (!TryNormalizeFlag(property.Ordered, out var ordered) ||
                        !TryNormalizeFlag(property.AllowSubtypes, out var allowSubtypes))
                    {
                        Errors.Add(new CogsError(ErrorLevel.Error, "MIG2006",
                            $"Property '{property.Name}' has a flag that is not blank, true, or false.", propertyFile));
                        continue;
                    }
                    changed |= !string.Equals(property.Ordered, ordered, StringComparison.Ordinal) ||
                               !string.Equals(property.AllowSubtypes, allowSubtypes, StringComparison.Ordinal);
                    property.Ordered = ordered;
                    property.AllowSubtypes = allowSubtypes;
                }

                if (changed)
                {
                    WriteRecords(propertyFile, properties);
                }
            }

            return renames;
        }

        private List<T> ReadRecords<T>(string path)
        {
            try
            {
                using var reader = File.OpenText(path);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                return csv.GetRecords<T>().ToList();
            }
            catch (Exception exception)
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "MIG2008",
                    $"Could not read '{path}' for migration: {exception.Message}", path, exception: exception));
                return null;
            }
        }

        private static void WriteRecords<T>(string path, IEnumerable<T> records)
        {
            using var writer = File.CreateText(path);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(records);
        }

        private static void AddSetting(
            List<Setting> settings,
            IDictionary<string, Setting> byKey,
            string key,
            string value,
            bool insertFirst = false)
        {
            if (byKey.TryGetValue(key, out var existing))
            {
                return;
            }

            var setting = new Setting { Key = key, Value = value };
            if (insertFirst)
            {
                settings.Insert(0, setting);
            }
            else
            {
                settings.Add(setting);
            }
            byKey[key] = setting;
        }

        private static bool TryNormalizeSemVer(string value, out string normalized)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (CogsConventions.IsCanonicalSemVer(trimmed))
            {
                normalized = trimmed;
                return true;
            }

            var match = Regex.Match(trimmed,
                @"^(0|[1-9][0-9]*)(?:\.(0|[1-9][0-9]*))?(?:\.(0|[1-9][0-9]*))?(?:(alpha|beta|rc)([0-9]+))?$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                normalized = string.Empty;
                return false;
            }

            normalized = $"{match.Groups[1].Value}.{(match.Groups[2].Success ? match.Groups[2].Value : "0")}.{(match.Groups[3].Success ? match.Groups[3].Value : "0")}";
            if (match.Groups[4].Success)
            {
                normalized += $"-{match.Groups[4].Value.ToLowerInvariant()}.{match.Groups[5].Value}";
            }
            return CogsConventions.IsCanonicalSemVer(normalized);
        }

        private static bool TryNormalizeCardinality(
            string minimumText,
            string maximumText,
            out string minimum,
            out string maximum)
        {
            var minText = string.IsNullOrWhiteSpace(minimumText) ? "0" : minimumText.Trim();
            var maxText = string.IsNullOrWhiteSpace(maximumText) ? "n" : maximumText.Trim();
            if (!BigInteger.TryParse(minText, out var min) || min < 0)
            {
                minimum = maximum = string.Empty;
                return false;
            }
            minimum = min.ToString(CultureInfo.InvariantCulture);

            if (maxText.Equals("n", StringComparison.OrdinalIgnoreCase) || maxText == "*")
            {
                maximum = "n";
                return true;
            }
            if (!BigInteger.TryParse(maxText, out var max) || max < min)
            {
                maximum = string.Empty;
                return false;
            }
            maximum = max.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        private static bool TryNormalizeFlag(string value, out string normalized)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                normalized = string.Empty;
                return true;
            }
            if (bool.TryParse(value.Trim(), out var parsed))
            {
                normalized = parsed ? "true" : "false";
                return true;
            }
            normalized = string.Empty;
            return false;
        }

        private void RewriteInPlace(string directory)
        {
            // create built in identification and reference types
            var settingsDirectoryName = Path.Combine(directory, "Settings");
            string identificationFile = Path.Combine(settingsDirectoryName, "Identification.csv");

            string idCsv = File.ReadAllText(identificationFile, Encoding.UTF8);
            List<Property> rows = new List<Property>();
            using (var textReader = new StringReader(idCsv))
            {
                try
                {
                    var config = new CsvConfiguration(CultureInfo.InvariantCulture);
                    config.HeaderValidated = null;
                    config.MissingFieldFound = null;
                    var csvReader = new CsvReader(textReader, config);

                    var records = csvReader.GetRecords<Property>();
                    rows.AddRange(records);
                }
                catch (Exception e)
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, "COGS-RW-010", e.Message,
                        sourcePath: identificationFile, exception: e));
                }
            }

            using (TextWriter textWriter = File.CreateText(identificationFile))
            {
                CsvWriter csvWriter = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
                csvWriter.WriteRecords(rows);
            }

            string identificationMixinFile = Path.Combine(settingsDirectoryName, "Identification.Mixin.csv");
            if (File.Exists(identificationMixinFile))
            {
                RewritePropertyFile(identificationMixinFile);
            }

            // settings
            string settingsFileName = Path.Combine(settingsDirectoryName, "Settings.csv");
            string settingsCsvStr = File.ReadAllText(settingsFileName, Encoding.UTF8);
            List<Setting> settings = new List<Setting>();
            using (var textReader = new StringReader(settingsCsvStr))
            {
                try
                {
                    var config = new CsvConfiguration(CultureInfo.InvariantCulture);
                    config.HeaderValidated = null;
                    config.MissingFieldFound = null;
                    var csvReader = new CsvReader(textReader, config);

                    var records = csvReader.GetRecords<Setting>();
                    settings.AddRange(records);
                }
                catch (Exception e)
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, "COGS-RW-011", e.Message,
                        sourcePath: settingsFileName, exception: e));
                }
            }

            using (TextWriter textWriter = File.CreateText(settingsFileName))
            {
                CsvWriter csvWriter = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
                csvWriter.WriteRecords(settings);
            }

            // item types from the ItemTypes directory.
            LoadDataTypes(directory, "ItemTypes");

            //reusable types from the ReusableTypes directory.
            LoadDataTypes(directory, "CompositeTypes");

        }

        private static string ResolveSourceDirectory(string directory)
        {
            string source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException($"The COGS directory '{directory}' does not exist.");
            }

            var info = new DirectoryInfo(source);
            if (info.LinkTarget is not null)
            {
                source = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? source;
                source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
            }

            string root = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(source)!);
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (string.Equals(source, root, comparison))
            {
                throw new InvalidOperationException("Rewriting a filesystem root is not allowed.");
            }

            return source;
        }

        private IReadOnlyList<RewriteRename> NormalizeLegacyMarkerCasing(string directory)
        {
            var renames = new List<RewriteRename>();
            foreach (string kind in new[] { "ItemTypes", "CompositeTypes" })
            {
                string kindDirectory = Path.Combine(directory, kind);
                if (!Directory.Exists(kindDirectory))
                {
                    continue;
                }

                foreach (string typeDirectory in Directory.EnumerateDirectories(kindDirectory)
                             .OrderBy(path => path, StringComparer.Ordinal))
                {
                    NormalizeNamedMarker(directory, typeDirectory, "Abstract", renames);
                    NormalizeNamedMarker(directory, typeDirectory, "Primitive", renames);
                    NormalizeExtendsMarker(directory, typeDirectory, renames);
                }
            }
            return renames;
        }

        private void NormalizeNamedMarker(
            string root,
            string typeDirectory,
            string canonicalName,
            ICollection<RewriteRename> renames)
        {
            string[] matches = Directory.EnumerateFiles(typeDirectory)
                .Where(path => string.Equals(Path.GetFileName(path), canonicalName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (matches.Length > 1)
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "MIG2010",
                    $"Cannot normalize marker casing because '{Path.GetFileName(typeDirectory)}' contains multiple case-equivalent '{canonicalName}' markers.",
                    typeDirectory));
                return;
            }
            if (matches.Length == 1 &&
                !string.Equals(Path.GetFileName(matches[0]), canonicalName, StringComparison.Ordinal))
            {
                RenameStagedMarker(root, matches[0], canonicalName, renames);
            }
        }

        private void NormalizeExtendsMarker(
            string root,
            string typeDirectory,
            ICollection<RewriteRename> renames)
        {
            string[] matches = Directory.EnumerateFiles(typeDirectory)
                .Where(path => Path.GetFileName(path).StartsWith("Extends.", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (matches.Length > 1)
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "MIG2010",
                    $"Cannot normalize inheritance marker casing because '{Path.GetFileName(typeDirectory)}' contains more than one Extends.<Parent> marker.",
                    typeDirectory));
                return;
            }
            if (matches.Length == 0)
            {
                return;
            }

            string existingName = Path.GetFileName(matches[0]);
            string parent = existingName.Substring("Extends.".Length);
            if (string.IsNullOrWhiteSpace(parent))
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "MIG2010",
                    "Cannot normalize an inheritance marker that does not name its parent type.", matches[0]));
                return;
            }

            string canonicalName = "Extends." + parent;
            if (!string.Equals(existingName, canonicalName, StringComparison.Ordinal))
            {
                RenameStagedMarker(root, matches[0], canonicalName, renames);
            }
        }

        private static void RenameStagedMarker(
            string root,
            string existingPath,
            string canonicalName,
            ICollection<RewriteRename> renames)
        {
            string canonicalPath = Path.Combine(Path.GetDirectoryName(existingPath)!, canonicalName);
            byte[] originalContents = File.ReadAllBytes(existingPath);
            MoveFileWithCaseChange(existingPath, canonicalPath);
            renames.Add(new RewriteRename(
                Path.GetRelativePath(root, existingPath),
                Path.GetRelativePath(root, canonicalPath),
                originalContents));
        }

        private static IReadOnlyList<RewriteSourceFile> StageRewriteFiles(string source, string staging)
        {
            Directory.CreateDirectory(staging);
            var files = new List<RewriteSourceFile>();

            string settings = Path.Combine(source, "Settings");
            if (Directory.Exists(settings))
            {
                EnsureDirectoryIsNotLink(settings);
                Directory.CreateDirectory(Path.Combine(staging, "Settings"));
                StageFileIfPresent(source, staging, Path.Combine(settings, "Identification.csv"), files);
                StageFileIfPresent(source, staging, Path.Combine(settings, "Identification.Mixin.csv"), files);
                StageFileIfPresent(source, staging, Path.Combine(settings, "Settings.csv"), files);
            }

            foreach (string kind in new[] { "ItemTypes", "CompositeTypes" })
            {
                string kindDirectory = Path.Combine(source, kind);
                if (!Directory.Exists(kindDirectory))
                {
                    continue;
                }

                EnsureDirectoryIsNotLink(kindDirectory);
                Directory.CreateDirectory(Path.Combine(staging, kind));
                foreach (string typeDirectory in Directory.EnumerateDirectories(kindDirectory)
                             .OrderBy(path => path, StringComparer.Ordinal))
                {
                    EnsureDirectoryIsNotLink(typeDirectory);
                    string typeName = Path.GetFileName(typeDirectory);
                    Directory.CreateDirectory(Path.Combine(staging, kind, typeName));
                    StageFileIfPresent(
                        source,
                        staging,
                        Path.Combine(typeDirectory, typeName + ".csv"),
                        files);
                    foreach (string marker in Directory.EnumerateFiles(typeDirectory)
                                 .Where(path => IsMarkerCandidate(Path.GetFileName(path)))
                                 .OrderBy(path => path, StringComparer.Ordinal))
                    {
                        StageFileIfPresent(source, staging, marker, files);
                    }
                }
            }

            return files;
        }

        private static bool IsMarkerCandidate(string name) =>
            string.Equals(name, "Abstract", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Primitive", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Extends.", StringComparison.OrdinalIgnoreCase);

        private static void StageFileIfPresent(
            string source,
            string staging,
            string path,
            ICollection<RewriteSourceFile> files)
        {
            if (!File.Exists(path))
            {
                return;
            }
            if (new FileInfo(path).LinkTarget is not null)
            {
                throw new InvalidOperationException(
                    $"Cannot transactionally rewrite a model containing symbolic link '{path}'.");
            }

            string relativePath = Path.GetRelativePath(source, path);
            byte[] originalContents = File.ReadAllBytes(path);
            string stagedPath = Path.Combine(staging, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
            File.WriteAllBytes(stagedPath, originalContents);
            files.Add(new RewriteSourceFile(relativePath, originalContents));
        }

        private static void EnsureDirectoryIsNotLink(string path)
        {
            if (new DirectoryInfo(path).LinkTarget is not null)
            {
                throw new InvalidOperationException(
                    $"Cannot transactionally rewrite a model containing symbolic link '{path}'.");
            }
        }

        private void CommitChangedFiles(
            string source,
            string staging,
            string backup,
            IReadOnlyList<RewriteSourceFile> sourceFiles,
            IReadOnlyList<RewriteRename> renames)
        {
            var renamedSources = renames
                .Select(rename => rename.OriginalRelativePath)
                .ToHashSet(StringComparer.Ordinal);
            RewriteSourceFile[] changes = sourceFiles
                .Where(file => !renamedSources.Contains(file.RelativePath))
                .Where(file => !file.OriginalContents.SequenceEqual(
                    File.ReadAllBytes(Path.Combine(staging, file.RelativePath))))
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .ToArray();
            if (changes.Length == 0 && renames.Count == 0)
            {
                return;
            }

            foreach (RewriteSourceFile change in changes)
            {
                string sourcePath = Path.Combine(source, change.RelativePath);
                if (!File.Exists(sourcePath) ||
                    !change.OriginalContents.SequenceEqual(File.ReadAllBytes(sourcePath)))
                {
                    throw new IOException(
                        $"'{sourcePath}' changed while the rewrite was being prepared. No files were replaced.");
                }
            }
            foreach (RewriteRename rename in renames)
            {
                string sourcePath = Path.Combine(source, rename.OriginalRelativePath);
                if (!File.Exists(sourcePath) ||
                    !rename.OriginalContents.SequenceEqual(File.ReadAllBytes(sourcePath)))
                {
                    throw new IOException(
                        $"'{sourcePath}' changed while the rewrite was being prepared. No files were replaced.");
                }
            }

            GitContext gitContext = PrepareRenameStrategies(source, renames);

            var committed = new List<RewriteSourceFile>();
            var committedRenames = new List<RewriteRename>();
            try
            {
                foreach (RewriteSourceFile change in changes)
                {
                    string sourcePath = Path.Combine(source, change.RelativePath);
                    string stagedPath = Path.Combine(staging, change.RelativePath);
                    string backupPath = Path.Combine(backup, change.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                    BeforeReplace?.Invoke(sourcePath, committed.Count);
                    File.Replace(stagedPath, sourcePath, backupPath, ignoreMetadataErrors: true);
                    committed.Add(change);
                }
                foreach (RewriteRename rename in renames)
                {
                    string sourcePath = Path.Combine(source, rename.OriginalRelativePath);
                    string destinationPath = Path.Combine(source, rename.CanonicalRelativePath);
                    BeforeReplace?.Invoke(sourcePath, committed.Count + committedRenames.Count);
                    try
                    {
                        MoveRename(source, sourcePath, destinationPath, rename, gitContext, reverse: false);
                        committedRenames.Add(rename);
                    }
                    catch
                    {
                        if (rename.Strategy == RewriteRenameStrategy.Git &&
                            ExactFileNameExists(destinationPath) &&
                            !ExactFileNameExists(sourcePath))
                        {
                            committedRenames.Add(rename);
                        }
                        throw;
                    }
                }
            }
            catch (Exception commitException)
            {
                var rollbackErrors = new List<Exception>();
                for (int index = committedRenames.Count - 1; index >= 0; index--)
                {
                    RewriteRename rename = committedRenames[index];
                    try
                    {
                        MoveRename(
                            source,
                            Path.Combine(source, rename.CanonicalRelativePath),
                            Path.Combine(source, rename.OriginalRelativePath),
                            rename,
                            gitContext,
                            reverse: true);
                    }
                    catch (Exception rollbackException)
                    {
                        rollbackErrors.Add(rollbackException);
                    }
                }
                for (int index = committed.Count - 1; index >= 0; index--)
                {
                    RewriteSourceFile change = committed[index];
                    string sourcePath = Path.Combine(source, change.RelativePath);
                    string stagedPath = Path.Combine(staging, change.RelativePath);
                    string backupPath = Path.Combine(backup, change.RelativePath);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
                        File.Replace(backupPath, sourcePath, stagedPath, ignoreMetadataErrors: true);
                    }
                    catch (Exception rollbackException)
                    {
                        rollbackErrors.Add(rollbackException);
                    }
                }

                if (rollbackErrors.Count > 0)
                {
                    rollbackErrors.Insert(0, commitException);
                    throw new RewriteCommitException(
                        rollbackComplete: false,
                        "A rewrite replacement failed and one or more earlier replacements could not be restored.",
                        new AggregateException(rollbackErrors));
                }

                throw new RewriteCommitException(
                    rollbackComplete: true,
                    "A rewrite replacement failed; all earlier replacements were restored.",
                    commitException);
            }
        }

        private GitContext PrepareRenameStrategies(string source, IReadOnlyList<RewriteRename> renames)
        {
            if (renames.Count == 0)
            {
                return null;
            }

            GitContext context = FindGitContext(source);
            if (context == null)
            {
                return null;
            }

            foreach (RewriteRename rename in renames)
            {
                string sourcePath = Path.Combine(source, rename.OriginalRelativePath);
                string repositoryRelativePath = GetRepositoryRelativePath(context, sourcePath);
                GitCommandResult result = RunGit(context, new[]
                {
                    "ls-files", "--error-unmatch", "--", repositoryRelativePath
                });
                if (result.ExitCode == 0)
                {
                    rename.Strategy = RewriteRenameStrategy.Git;
                }
                else if (result.ExitCode == 1)
                {
                    rename.Strategy = RewriteRenameStrategy.FileSystem;
                }
                else
                {
                    throw new GitIntegrationException(
                        $"Git could not determine whether marker '{repositoryRelativePath}' is tracked: {DescribeGitFailure(result)}",
                        sourcePath);
                }
            }

            return context;
        }

        private GitContext FindGitContext(string source)
        {
            bool hasGitMetadata = HasGitMetadataAncestor(source);
            var candidates = new List<string>();
            string configured = GitExecutableEnvironmentReader?.Invoke();
            if (!string.IsNullOrWhiteSpace(configured))
            {
                candidates.Add(configured.Trim().Trim('"'));
            }
            candidates.Add("git");

            StringComparer comparer = OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var failures = new List<string>();
            foreach (string executable in candidates.Where(value => value.Length > 0).Distinct(comparer))
            {
                GitCommandResult result;
                try
                {
                    result = GitCommandRunner(executable, source, new[] { "rev-parse", "--show-toplevel" });
                }
                catch (Exception exception)
                {
                    failures.Add($"'{executable}' could not start: {exception.Message}");
                    continue;
                }

                if (result.ExitCode != 0)
                {
                    failures.Add($"'{executable}' failed: {DescribeGitFailure(result)}");
                    continue;
                }

                string rootText = result.StandardOutput
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(rootText))
                {
                    failures.Add($"'{executable}' did not report a Git worktree root.");
                    continue;
                }

                string root;
                try
                {
                    root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootText.Trim()));
                }
                catch (Exception exception)
                {
                    failures.Add($"'{executable}' reported an invalid Git worktree root: {exception.Message}");
                    continue;
                }

                if (!Directory.Exists(root) || PathEscapesRoot(root, source))
                {
                    failures.Add($"'{executable}' reported Git worktree root '{root}', which does not contain the model.");
                    continue;
                }

                return new GitContext(executable, root);
            }

            if (!hasGitMetadata)
            {
                return null;
            }

            string detail = failures.Count == 0
                ? "No Git executable was available."
                : string.Join(" ", failures.Select(ConciseGitText));
            throw new GitIntegrationException(
                $"The model is inside a Git checkout, but Git could not be used. Set COGS_GIT to a working Git executable. {detail}",
                source);
        }

        private static bool HasGitMetadataAncestor(string source)
        {
            for (DirectoryInfo directory = new DirectoryInfo(source); directory != null; directory = directory.Parent)
            {
                string metadata = Path.Combine(directory.FullName, ".git");
                if (Directory.Exists(metadata) || File.Exists(metadata))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool PathEscapesRoot(string root, string path)
        {
            string relative = Path.GetRelativePath(root, path);
            return relative == ".." ||
                relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
        }

        private static string GetRepositoryRelativePath(GitContext context, string path) =>
            Path.GetRelativePath(context.WorkTreeRoot, path)
                .Replace(Path.DirectorySeparatorChar, '/');

        private void MoveRename(
            string source,
            string sourcePath,
            string destinationPath,
            RewriteRename rename,
            GitContext gitContext,
            bool reverse)
        {
            if (rename.Strategy == RewriteRenameStrategy.FileSystem)
            {
                MoveFileWithCaseChange(sourcePath, destinationPath);
                return;
            }

            if (gitContext == null)
            {
                throw new GitIntegrationException("A tracked marker rename has no Git worktree context.", sourcePath);
            }

            string originalPath = Path.Combine(source, rename.OriginalRelativePath);
            string canonicalPath = Path.Combine(source, rename.CanonicalRelativePath);
            string from = GetRepositoryRelativePath(gitContext, reverse ? canonicalPath : originalPath);
            string to = GetRepositoryRelativePath(gitContext, reverse ? originalPath : canonicalPath);
            GitCommandResult result = RunGit(gitContext, new[] { "mv", "-f", "--", from, to });
            if (result.ExitCode != 0)
            {
                throw new GitIntegrationException(
                    $"Git could not rename tracked marker '{from}' to '{to}': {DescribeGitFailure(result)}",
                    sourcePath);
            }
        }

        private GitCommandResult RunGit(GitContext context, IReadOnlyList<string> arguments)
        {
            try
            {
                return GitCommandRunner(context.Executable, context.WorkTreeRoot, arguments);
            }
            catch (Exception exception)
            {
                throw new GitIntegrationException(
                    $"Git command '{arguments.FirstOrDefault() ?? "unknown"}' could not run: {exception.Message}",
                    context.WorkTreeRoot,
                    exception);
            }
        }

        private static GitCommandResult ExecuteGitCommand(
            string executable,
            string workingDirectory,
            IReadOnlyList<string> arguments)
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Git executable '{executable}' did not start.");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // The timeout remains the primary failure.
                }
                throw new TimeoutException($"Git executable '{executable}' did not finish within 30 seconds.");
            }

            return new GitCommandResult(
                process.ExitCode,
                standardOutput.GetAwaiter().GetResult(),
                standardError.GetAwaiter().GetResult());
        }

        private static string DescribeGitFailure(GitCommandResult result)
        {
            string detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;
            detail = ConciseGitText(detail);
            return detail.Length == 0 ? $"exit code {result.ExitCode}" : detail;
        }

        private static string ConciseGitText(string text)
        {
            string concise = Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
            return concise.Length <= 1000 ? concise : concise.Substring(0, 1000) + "...";
        }

        private void AddGitDiagnostic(GitIntegrationException exception) =>
            Errors.Add(new CogsError(
                ErrorLevel.Error,
                "MIG2011",
                exception.Message,
                sourcePath: exception.SourcePath,
                exception: exception));

        private static GitIntegrationException FindGitIntegrationException(Exception exception)
        {
            if (exception is GitIntegrationException gitException)
            {
                return gitException;
            }
            if (exception is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.InnerExceptions)
                {
                    GitIntegrationException found = FindGitIntegrationException(inner);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return exception.InnerException == null
                ? null
                : FindGitIntegrationException(exception.InnerException);
        }

        private static void MoveFileWithCaseChange(string source, string destination)
        {
            if (string.Equals(source, destination, StringComparison.Ordinal))
            {
                return;
            }

            string temporary = Path.Combine(
                Path.GetDirectoryName(source)!,
                $".{Path.GetFileName(source)}.cogs-case-{Guid.NewGuid():N}");
            File.Move(source, temporary);
            try
            {
                File.Move(temporary, destination);
            }
            catch
            {
                if (File.Exists(temporary) && !File.Exists(source))
                {
                    File.Move(temporary, source);
                }
                throw;
            }
        }

        private static bool ExactFileNameExists(string path)
        {
            string directory = Path.GetDirectoryName(path)!;
            string fileName = Path.GetFileName(path);
            return Directory.Exists(directory) && Directory.EnumerateFiles(directory)
                .Any(candidate => string.Equals(Path.GetFileName(candidate), fileName, StringComparison.Ordinal));
        }

        private static void TryDeleteDirectory(string directory)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        return;
                    }

                    ClearReadOnlyAttributes(directory);
                    Directory.Delete(directory, recursive: true);
                    return;
                }
                catch when (attempt < 2)
                {
                    Thread.Sleep(25 * (attempt + 1));
                }
                catch
                {
                    // Cleanup must not hide the rewrite result.
                    return;
                }
            }
        }

        private static void ClearReadOnlyAttributes(string directory)
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                }
                catch
                {
                    // A later delete attempt reports whether cleanup is possible.
                }
            }
        }

        private sealed class RewriteSourceFile
        {
            public RewriteSourceFile(string relativePath, byte[] originalContents)
            {
                RelativePath = relativePath;
                OriginalContents = originalContents;
            }

            public string RelativePath { get; }

            public byte[] OriginalContents { get; }
        }

        private sealed class RewriteRename
        {
            public RewriteRename(
                string originalRelativePath,
                string canonicalRelativePath,
                byte[] originalContents)
            {
                OriginalRelativePath = originalRelativePath;
                CanonicalRelativePath = canonicalRelativePath;
                OriginalContents = originalContents;
            }

            public string OriginalRelativePath { get; }

            public string CanonicalRelativePath { get; }

            public byte[] OriginalContents { get; }

            public RewriteRenameStrategy Strategy { get; set; }
        }

        private enum RewriteRenameStrategy
        {
            FileSystem,
            Git
        }

        private sealed class GitContext
        {
            public GitContext(string executable, string workTreeRoot)
            {
                Executable = executable;
                WorkTreeRoot = workTreeRoot;
            }

            public string Executable { get; }

            public string WorkTreeRoot { get; }
        }

        internal readonly struct GitCommandResult
        {
            public GitCommandResult(int exitCode, string standardOutput, string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput ?? string.Empty;
                StandardError = standardError ?? string.Empty;
            }

            public int ExitCode { get; }

            public string StandardOutput { get; }

            public string StandardError { get; }
        }

        private sealed class GitIntegrationException : IOException
        {
            public GitIntegrationException(string message, string sourcePath, Exception innerException = null)
                : base(message, innerException)
            {
                SourcePath = sourcePath;
            }

            public string SourcePath { get; }
        }

        private sealed class RewriteCommitException : IOException
        {
            public RewriteCommitException(bool rollbackComplete, string message, Exception innerException)
                : base(message, innerException)
            {
                RollbackComplete = rollbackComplete;
            }

            public bool RollbackComplete { get; }
        }

        private void LoadDataTypes(string directory, string subDirectory)
        {
            string itemTypesDirectory = Path.Combine(directory, subDirectory);
            foreach (string typeDir in Directory.GetDirectories(itemTypesDirectory))
            {
                string itemTypeName = Path.GetFileName(typeDir);
                string propertiesFileName = Path.Combine(typeDir, itemTypeName + ".csv");

                var rows = new List<Property>();

                // Read the properties
                if (File.Exists(propertiesFileName))
                {
                    string csvStr = File.ReadAllText(propertiesFileName, Encoding.UTF8);
                    using (var textReader = new StringReader(csvStr))
                    {
                        try
                        {
                            var config = new CsvConfiguration(CultureInfo.InvariantCulture);
                            config.HeaderValidated = null;
                            config.MissingFieldFound = null;
                            var csvReader = new CsvReader(textReader, config);

                            rows = csvReader.GetRecords<Property>().ToList();
                        }
                        catch (Exception e)
                        {
                            Errors.Add(new CogsError(ErrorLevel.Error, "COGS-RW-012", e.Message,
                                sourcePath: propertiesFileName, exception: e));
                        }
                    }
                }
                else
                {
                    continue;
                }

                using (TextWriter textWriter = File.CreateText(propertiesFileName))
                {
                    CsvWriter csvWriter = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
                    csvWriter.WriteRecords(rows);
                }

            }
        }

        private void RewritePropertyFile(string fileName)
        {
            var rows = new List<Property>();
            try
            {
                string csvText = File.ReadAllText(fileName, Encoding.UTF8);
                using var textReader = new StringReader(csvText);
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HeaderValidated = null,
                    MissingFieldFound = null,
                };
                using var csvReader = new CsvReader(textReader, config);
                rows.AddRange(csvReader.GetRecords<Property>());
            }
            catch (Exception exception)
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "COGS-RW-013", exception.Message,
                    sourcePath: fileName, exception: exception));
                return;
            }

            using TextWriter textWriter = File.CreateText(fileName);
            using var csvWriter = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
            csvWriter.WriteRecords(rows);
        }

        private void RemapDiagnosticPaths(string staging, string source)
        {
            string stagingRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(staging));
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string prefix = stagingRoot + Path.DirectorySeparatorChar;
            foreach (CogsError error in Errors)
            {
                if (string.IsNullOrWhiteSpace(error.SourcePath)) continue;
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(error.SourcePath);
                }
                catch
                {
                    continue;
                }
                if (string.Equals(fullPath, stagingRoot, comparison))
                {
                    error.SourcePath = source;
                }
                else if (fullPath.StartsWith(prefix, comparison))
                {
                    error.SourcePath = Path.Combine(source, Path.GetRelativePath(stagingRoot, fullPath));
                }
            }
        }
    }


}
