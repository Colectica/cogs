// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Cogs.Dto
{
    public class CogsDirectoryReader
    {
        private static readonly string[] PropertyHeaders =
        {
            "Name", "DataType", "MinCardinality", "MaxCardinality", "Description",
            "Ordered", "AllowSubtypes", "MinLength", "MaxLength", "Enumeration",
            "Pattern", "MinInclusive", "MinExclusive", "MaxInclusive", "MaxExclusive",
            "DeprecatedNamespace", "DeprecatedElementOrAttribute", "DeprecatedChoiceGroup"
        };

        private static readonly string[] SettingHeaders = { "Key", "Value" };

        public string SettingsDirectoryName { get; set; }
        public List<CogsError> Errors { get; } = new List<CogsError>();

        private IReadOnlyList<Property> dcTerms = Array.Empty<Property>();

        /// <summary>Loads a directory and returns both the DTO and all recoverable diagnostics.</summary>
        public CogsLoadResult LoadResult(string directory)
        {
            Errors.Clear();
            var model = new CogsDtoModel();

            if (string.IsNullOrWhiteSpace(directory))
            {
                AddError("COGS-READ-001", "A COGS model directory is required.");
                return new CogsLoadResult(model, OrderedErrors());
            }

            try
            {
                directory = Path.GetFullPath(directory);
                model.SourceDirectory = directory;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                AddError("COGS-READ-002", $"The model directory path is invalid: {exception.Message}", directory, exception: exception);
                return new CogsLoadResult(model, OrderedErrors());
            }

            if (!Directory.Exists(directory))
            {
                AddError("COGS-READ-003", "The COGS model directory does not exist.", directory);
                return new CogsLoadResult(model, OrderedErrors());
            }

            TryGetExactDirectory(directory, "Settings", true, out var settingsDirectory);
            SettingsDirectoryName = settingsDirectory;
            if (settingsDirectory == null || !LoadSettingsVersion(settingsDirectory, model))
            {
                return new CogsLoadResult(model, OrderedErrors());
            }

            dcTerms = LoadEmbeddedDcTerms();
            TryGetExactDirectory(directory, "ItemTypes", true, out var itemTypesDirectory);
            TryGetExactDirectory(directory, "CompositeTypes", true, out var compositeTypesDirectory);
            TryGetExactDirectory(directory, "Topics", false, out var topicsDirectory);
            TryGetExactDirectory(directory, "Articles", false, out var articlesDirectory);

            LoadRemainingSettings(settingsDirectory, model);

            if (itemTypesDirectory != null)
            {
                LoadDataTypes(itemTypesDirectory, true, model.ItemTypes);
            }

            if (compositeTypesDirectory != null)
            {
                LoadDataTypes(compositeTypesDirectory, false, model.ReusableDataTypes);
            }

            if (topicsDirectory != null)
            {
                LoadTopics(topicsDirectory, model);
            }

            if (articlesDirectory != null)
            {
                LoadArticles(articlesDirectory, model);
            }

            return new CogsLoadResult(model, OrderedErrors());
        }

        /// <summary>
        /// Compatibility adapter. Prefer <see cref="LoadResult"/> so diagnostics cannot be overlooked.
        /// Invalid input returns null rather than a partial DTO.
        /// </summary>
        [Obsolete("Use LoadResult and inspect its diagnostics. This compatibility adapter will be removed in COGS 3.0.")]
        public CogsDtoModel Load(string directory)
        {
            CogsLoadResult result = LoadResult(directory);
            return result.Success ? result.Model : null;
        }

        private bool LoadSettingsVersion(string directory, CogsDtoModel model)
        {
            if (!TryGetExactFile(directory, "Settings.csv", true, out var settingsFile) ||
                !TryReadCsv(settingsFile, SettingHeaders, out List<Setting> settings))
            {
                return false;
            }

            model.Settings.AddRange(settings);
            List<Setting> versions = settings.Where(setting => setting.Key == "CogsVersion").ToList();
            if (versions.Count == 0)
            {
                AddError("COGS-READ-090", "Settings.csv must declare CogsVersion exactly once before other model files can be interpreted.", settingsFile, modelPath: "Settings.CogsVersion");
                return false;
            }
            if (versions.Count > 1)
            {
                AddError("COGS-READ-091", "Settings.csv declares CogsVersion more than once.", versions[1].SourcePath, versions[1].SourceLine, modelPath: "Settings.CogsVersion");
                return false;
            }
            if (versions[0].Value != "2.0")
            {
                AddError("COGS-READ-092", $"Unsupported CogsVersion '{versions[0].Value}'; this tool requires CogsVersion 2.0.", versions[0].SourcePath, versions[0].SourceLine, modelPath: "Settings.CogsVersion");
                return false;
            }
            return true;
        }

        private void LoadRemainingSettings(string directory, CogsDtoModel model)
        {
            if (TryGetExactFile(directory, "Identification.csv", true, out var identificationFile) &&
                TryReadCsv(identificationFile, PropertyHeaders, out List<Property> identification))
            {
                model.Identification.AddRange(identification);
            }

            if (TryGetExactFile(directory, "Identification.Mixin.csv", false, out var mixinFile) &&
                mixinFile != null && TryReadCsv(mixinFile, PropertyHeaders, out List<Property> mixin))
            {
                model.IdentificationMixin.AddRange(mixin);
            }

            if (TryGetExactFile(directory, "HeaderInclude.txt", false, out var headerFile) && headerFile != null)
            {
                model.HeaderInclude = SafeReadAllText(headerFile);
            }
        }

        private void LoadDataTypes<T>(string directory, bool isItemType, IList<T> target) where T : DataType, new()
        {
            foreach (var typeDirectory in SafeEnumerateDirectories(directory).OrderBy(x => Path.GetFileName(x), StringComparer.Ordinal))
            {
                var typeName = Path.GetFileName(typeDirectory);
                ValidateTypeDirectoryFiles(typeDirectory, typeName);
                var type = new T
                {
                    Name = typeName,
                    SourcePath = typeDirectory
                };

                type.IsAbstract = ReadMarker(typeDirectory, "Abstract");
                type.IsPrimitive = ReadMarker(typeDirectory, "Primitive");
                type.Extends = ReadExtendsMarker(typeDirectory);

                if (TryGetExactFile(typeDirectory, "readme.markdown", false, out var readme) && readme != null)
                {
                    type.Description = SafeReadAllText(readme) ?? string.Empty;
                }

                var csvName = typeName + ".csv";
                var hasCsv = TryGetExactFile(typeDirectory, csvName, false, out var propertiesFile) && propertiesFile != null;
                if (!hasCsv && !type.IsAbstract)
                {
                    AddError("COGS-READ-020", $"Concrete type '{typeName}' requires '{csvName}'.", Path.Combine(typeDirectory, csvName), modelPath: TypeModelPath(isItemType, typeName));
                    continue;
                }

                if (hasCsv)
                {
                    if (!TryReadCsv(propertiesFile, PropertyHeaders, out List<Property> properties))
                    {
                        continue;
                    }
                    type.Properties = ExpandDcTerms(properties, typeName);
                }

                foreach (var markdown in SafeEnumerateFiles(typeDirectory, "*.markdown").OrderBy(x => x, StringComparer.Ordinal))
                {
                    if (string.Equals(Path.GetFileName(markdown), "readme.markdown", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var content = SafeReadAllText(markdown);
                    var name = Path.GetFileNameWithoutExtension(markdown);
                    if (!string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(name))
                    {
                        type.AdditionalText.Add(new AdditionalText
                        {
                            FilePath = markdown,
                            Content = content,
                            Format = "markdown",
                            Name = name
                        });
                    }
                }

                target.Add(type);
            }
        }

        private List<Property> ExpandDcTerms(List<Property> properties, string typeName)
        {
            var markerIndexes = properties
                .Select((property, index) => (property, index))
                .Where(x => string.Equals(x.property.Name, "DcTerms", StringComparison.Ordinal))
                .ToList();

            if (markerIndexes.Count == 0)
            {
                return properties;
            }

            if (markerIndexes.Count > 1)
            {
                AddError("COGS-READ-021", $"Type '{typeName}' contains more than one DcTerms marker.", markerIndexes[1].property.SourcePath, markerIndexes[1].property.SourceLine, modelPath: $"{typeName}.DcTerms");
                return properties;
            }

            var marker = markerIndexes[0].property;
            if (!IsExactDcTermsMarker(marker))
            {
                AddError("COGS-READ-022", "The Dublin Core marker must use Name=DcTerms, DataType=dcTerms, MinCardinality=0, MaxCardinality=1, with flags and facets blank.", marker.SourcePath, marker.SourceLine, modelPath: $"{typeName}.{marker.Name}");
                return properties;
            }

            var expanded = new List<Property>(properties.Count + dcTerms.Count - 1);
            foreach (var property in properties)
            {
                if (!ReferenceEquals(property, marker))
                {
                    expanded.Add(property);
                    continue;
                }

                expanded.AddRange(dcTerms.Select(CloneProperty));
            }
            return expanded;
        }

        private static bool IsExactDcTermsMarker(Property property) =>
            property.Name == "DcTerms" &&
            property.DataType == "dcTerms" &&
            property.MinCardinality == "0" &&
            property.MaxCardinality == "1" &&
            string.IsNullOrWhiteSpace(property.Description) &&
            string.IsNullOrWhiteSpace(property.Ordered) &&
            string.IsNullOrWhiteSpace(property.AllowSubtypes) &&
            !property.MinLength.HasValue && !property.MaxLength.HasValue &&
            string.IsNullOrWhiteSpace(property.Enumeration) &&
            string.IsNullOrWhiteSpace(property.Pattern) &&
            string.IsNullOrWhiteSpace(property.MinInclusive) &&
            string.IsNullOrWhiteSpace(property.MinExclusive) &&
            string.IsNullOrWhiteSpace(property.MaxInclusive) &&
            string.IsNullOrWhiteSpace(property.MaxExclusive);

        private void LoadTopics(string directory, CogsDtoModel model)
        {
            if (!TryGetExactFile(directory, "index.txt", true, out var indexFile))
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            string[] topicLines = SafeReadAllLines(indexFile);
            for (int topicIndex = 0; topicIndex < topicLines.Length; topicIndex++)
            {
                string rawName = topicLines[topicIndex];
                int sourceLine = topicIndex + 1;
                var topicName = rawName.Trim();
                if (topicName.Length == 0)
                {
                    AddError("COGS-READ-030", "Topics/index.txt contains a blank topic name.", indexFile, sourceLine, 1, modelPath: "Topics");
                    continue;
                }
                if (!string.Equals(rawName, topicName, StringComparison.Ordinal))
                {
                    AddError("COGS-READ-033", $"Topic name '{rawName}' is not normalized; surrounding whitespace is not allowed.", indexFile, sourceLine, 1, modelPath: $"Topics.{topicName}");
                    continue;
                }
                if (!IsSingleRelativeName(topicName))
                {
                    AddError("COGS-READ-031", $"Topic name '{rawName}' must be a single relative directory name.", indexFile, sourceLine, 1, modelPath: $"Topics.{topicName}");
                    continue;
                }
                if (!seen.Add(topicName))
                {
                    AddError("COGS-READ-032", $"Topic '{topicName}' appears more than once.", indexFile, sourceLine, 1, modelPath: $"Topics.{topicName}");
                    continue;
                }
                if (!TryGetTopicDirectory(directory, topicName, indexFile, sourceLine, out var topicDirectory))
                {
                    continue;
                }

                var topic = new TopicIndex { Name = topicName, SourcePath = topicDirectory, IndexSourcePath = indexFile, SourceLine = sourceLine, SourceColumn = 1 };
                if (TryGetExactFile(topicDirectory, "readme.markdown", false, out var readme) && readme != null)
                {
                    topic.Description = SafeReadAllText(readme);
                }
                if (TryGetExactFile(topicDirectory, "items.txt", true, out var itemsFile))
                {
                    LoadTopicItems(itemsFile, topic);
                }
                if (TryGetExactFile(topicDirectory, "toc.txt", false, out var tocFile) && tocFile != null)
                {
                    if (TryGetExactDirectory(topicDirectory, "Articles", true, out var articlesDirectory) && articlesDirectory != null)
                    {
                        topic.ArticlesPath = articlesDirectory;
                        LoadArticleToc(tocFile, articlesDirectory, $"Topics.{topicName}.Articles", topic.ArticleTocEntries, topic.ArticleTocEntrySources);
                    }
                }
                model.TopicIndices.Add(topic);
            }
        }

        private void LoadArticles(string directory, CogsDtoModel model)
        {
            model.ArticlesPath = directory;
            if (TryGetExactFile(directory, "toc.txt", true, out var tocFile))
            {
                LoadArticleToc(tocFile, directory, "Articles", model.ArticleTocEntries, model.ArticleTocEntrySources);
            }
        }

        private void LoadTopicItems(string itemsFile, TopicIndex topic)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string[] lines = SafeReadAllLines(itemsFile);
            for (int itemIndex = 0; itemIndex < lines.Length; itemIndex++)
            {
                string rawItem = lines[itemIndex];
                int sourceLine = itemIndex + 1;
                string item = rawItem.Trim();
                if (item.Length == 0)
                {
                    AddError("COGS-READ-036", $"Topic '{topic.Name}' contains a blank item entry.", itemsFile, sourceLine, 1, modelPath: $"Topics.{topic.Name}.Items");
                    continue;
                }
                if (!string.Equals(rawItem, item, StringComparison.Ordinal))
                {
                    AddError("COGS-READ-037", $"Topic item '{rawItem}' is not normalized; surrounding whitespace is not allowed.", itemsFile, sourceLine, 1, modelPath: $"Topics.{topic.Name}.Items");
                    continue;
                }
                if (!IsSingleRelativeName(item))
                {
                    AddError("COGS-READ-038", $"Topic item '{item}' must be a single item type name, not a path.", itemsFile, sourceLine, 1, modelPath: $"Topics.{topic.Name}.Items");
                    continue;
                }
                if (!seen.Add(item))
                {
                    AddError("COGS-READ-039", $"Topic '{topic.Name}' lists item '{item}' more than once.", itemsFile, sourceLine, 1, modelPath: $"Topics.{topic.Name}.Items");
                    continue;
                }

                topic.ItemTypes.Add(item);
                topic.ItemTypeSources.Add(new SourceTextEntry { Value = item, SourcePath = itemsFile, SourceLine = sourceLine, SourceColumn = 1 });
            }
        }

        private void LoadArticleToc(
            string tocFile,
            string articleRoot,
            string modelPath,
            IList<string> entries,
            IList<SourceTextEntry> sources)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var seenDocuments = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            string[] lines = SafeReadAllLines(tocFile);
            for (int entryIndex = 0; entryIndex < lines.Length; entryIndex++)
            {
                string rawEntry = lines[entryIndex];
                int sourceLine = entryIndex + 1;
                if (string.IsNullOrWhiteSpace(rawEntry))
                {
                    continue;
                }
                CogsDocumentationPathStatus normalization = CogsDocumentationPath.Normalize(rawEntry, out string entry);
                if (normalization != CogsDocumentationPathStatus.Valid)
                {
                    AddArticleTocError(normalization, rawEntry, tocFile, sourceLine, modelPath);
                    continue;
                }
                if (!seen.Add(entry))
                {
                    AddError("COGS-READ-086", $"Article TOC entry '{entry}' appears more than once.", tocFile, sourceLine, 1, modelPath: modelPath);
                    continue;
                }

                CogsDocumentationPathStatus resolution = CogsDocumentationPath.Resolve(articleRoot, entry, out string resolvedPath);
                if (resolution != CogsDocumentationPathStatus.Valid)
                {
                    AddArticleTocError(resolution, entry, tocFile, sourceLine, modelPath);
                    continue;
                }
                if (!seenDocuments.Add(resolvedPath))
                {
                    AddError("COGS-READ-086", $"Article TOC entry '{entry}' resolves to an article that is already listed.", tocFile, sourceLine, 1, modelPath: modelPath);
                    continue;
                }

                entries.Add(entry);
                sources.Add(new SourceTextEntry { Value = entry, SourcePath = tocFile, SourceLine = sourceLine, SourceColumn = 1 });
            }
        }

        private void AddArticleTocError(
            CogsDocumentationPathStatus status,
            string entry,
            string tocFile,
            int sourceLine,
            string modelPath)
        {
            string code = status switch
            {
                CogsDocumentationPathStatus.Blank or CogsDocumentationPathStatus.NotNormalized or CogsDocumentationPathStatus.UnsupportedExtension => "COGS-READ-084",
                CogsDocumentationPathStatus.DirectiveSyntax => "COGS-READ-085",
                CogsDocumentationPathStatus.OutsideRoot or CogsDocumentationPathStatus.RootMissing or CogsDocumentationPathStatus.LinkTraversal => "COGS-READ-087",
                CogsDocumentationPathStatus.Missing => "COGS-READ-088",
                _ => "COGS-READ-089"
            };
            AddError(code, $"Article TOC entry '{entry}' {CogsDocumentationPath.Describe(status)}.", tocFile, sourceLine, 1, modelPath: modelPath);
        }

        private bool TryGetTopicDirectory(string parent, string name, string indexFile, int sourceLine, out string path)
        {
            path = null;
            try
            {
                string[] matches = Directory.EnumerateDirectories(parent)
                    .Where(candidate => string.Equals(Path.GetFileName(candidate), name, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                string exact = matches.FirstOrDefault(candidate => string.Equals(Path.GetFileName(candidate), name, StringComparison.Ordinal));
                if (matches.Length > 1)
                {
                    AddError("COGS-READ-034", $"Topic '{name}' is ambiguous because multiple case-equivalent directories exist.", indexFile, sourceLine, 1, modelPath: $"Topics.{name}");
                    return false;
                }
                if (exact != null)
                {
                    path = exact;
                    return true;
                }
                if (matches.Length == 1)
                {
                    AddError("COGS-READ-034", $"Topic '{name}' has incorrect directory casing; found '{Path.GetFileName(matches[0])}'.", indexFile, sourceLine, 1, modelPath: $"Topics.{name}");
                    return false;
                }

                AddError("COGS-READ-035", $"Topic directory '{name}' does not exist.", indexFile, sourceLine, 1, modelPath: $"Topics.{name}");
                return false;
            }
            catch (Exception exception)
            {
                AddError("COGS-READ-072", $"Directory cannot be enumerated: {exception.Message}", indexFile, sourceLine, 1, modelPath: $"Topics.{name}", exception: exception);
                return false;
            }
        }

        private static bool IsSingleRelativeName(string value) =>
            CogsDocumentationPath.IsPortableSingleSegment(value);

        private bool ReadMarker(string directory, string name)
        {
            var entries = SafeEnumerateFiles(directory).Select(Path.GetFileName).ToList();
            var candidates = entries
                .Where(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (candidates.Count > 1)
            {
                AddError("COGS-READ-045", $"A type directory may contain only one {name} marker.", directory);
                return true;
            }
            if (candidates.Count == 0)
            {
                return false;
            }
            if (!string.Equals(candidates[0], name, StringComparison.Ordinal))
            {
                AddWarning("COGS-READ-040", $"Marker '{candidates[0]}' has noncanonical casing; use '{name}'. Run rewrite --upgrade-cogs-2 to normalize it.", Path.Combine(directory, candidates[0]));
            }
            return true;
        }

        private string ReadExtendsMarker(string directory)
        {
            var entries = SafeEnumerateFiles(directory).Select(Path.GetFileName).ToList();
            var candidates = entries
                .Where(x => x.StartsWith("Extends.", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var wrongCase in candidates.Where(x => !x.StartsWith("Extends.", StringComparison.Ordinal)))
            {
                AddWarning("COGS-READ-041", $"Inheritance marker '{wrongCase}' has noncanonical casing; use the prefix 'Extends.'. Run rewrite --upgrade-cogs-2 to normalize it.", Path.Combine(directory, wrongCase));
            }
            if (candidates.Count > 1)
            {
                AddError("COGS-READ-042", "A type directory may contain only one Extends.<Parent> marker.", directory);
                return string.Empty;
            }
            if (candidates.Count == 0)
            {
                return string.Empty;
            }
            var parent = candidates[0].Substring("Extends.".Length);
            if (string.IsNullOrWhiteSpace(parent))
            {
                AddError("COGS-READ-043", "An Extends marker must name its parent type.", Path.Combine(directory, candidates[0]));
                return string.Empty;
            }
            return parent;
        }

        private void ValidateTypeDirectoryFiles(string directory, string typeName)
        {
            string csvName = typeName + ".csv";
            foreach (string file in SafeEnumerateFiles(directory))
            {
                string name = Path.GetFileName(file);
                bool allowed = name == csvName || name == "readme.markdown" ||
                    name.EndsWith(".markdown", StringComparison.Ordinal) ||
                    name == "Abstract" || name == "Primitive" ||
                    name.StartsWith("Extends.", StringComparison.Ordinal);
                bool knownWrongCase = string.Equals(name, "Abstract", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Primitive", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Extends.", StringComparison.OrdinalIgnoreCase);
                if (!allowed && !knownWrongCase)
                {
                    AddError("COGS-READ-044", $"Unrecognized file '{name}' in type directory '{typeName}'; marker files must be Abstract, Primitive, or Extends.<Parent> (noncanonical keyword casing is accepted with a warning).", file,
                        modelPath: typeName);
                }
            }
        }

        private IReadOnlyList<Property> LoadEmbeddedDcTerms()
        {
            try
            {
                using var stream = GetType().GetTypeInfo().Assembly.GetManifestResourceStream("Cogs.Dto.DcTerms.csv");
                if (stream == null)
                {
                    AddError("COGS-READ-050", "The embedded Dublin Core property catalog is missing.");
                    return Array.Empty<Property>();
                }
                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                return csv.GetRecords<Property>().Select(CloneProperty).ToArray();
            }
            catch (Exception exception)
            {
                AddError("COGS-READ-051", $"The embedded Dublin Core property catalog is invalid: {exception.Message}", exception: exception);
                return Array.Empty<Property>();
            }
        }

        private bool TryReadCsv<T>(string path, IReadOnlyList<string> expectedHeaders, out List<T> records)
        {
            records = new List<T>();
            try
            {
                var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    IgnoreBlankLines = false,
                    HeaderValidated = null,
                    MissingFieldFound = null
                };
                using var reader = new StreamReader(path, Encoding.UTF8, true);
                using var csv = new CsvReader(reader, configuration);
                if (!csv.Read() || !csv.ReadHeader())
                {
                    AddError("COGS-READ-060", "CSV file has no header row.", path, 1);
                    return false;
                }

                var headers = csv.HeaderRecord ?? Array.Empty<string>();
                var headerValid = true;
                foreach (var duplicate in headers.GroupBy(x => x, StringComparer.Ordinal).Where(x => x.Count() > 1))
                {
                    AddError("COGS-READ-061", $"CSV header '{duplicate.Key}' occurs more than once.", path, 1);
                    headerValid = false;
                }
                foreach (var header in headers.Except(expectedHeaders, StringComparer.Ordinal))
                {
                    AddError("COGS-READ-062", $"Unknown CSV header '{header}'.", path, 1);
                    headerValid = false;
                }
                foreach (var missing in expectedHeaders.Except(headers, StringComparer.Ordinal))
                {
                    AddError("COGS-READ-063", $"Required CSV header '{missing}' is missing or incorrectly cased.", path, 1);
                    headerValid = false;
                }
                if (!headerValid)
                {
                    return false;
                }

                while (csv.Read())
                {
                    var line = csv.Context.Parser.Row;
                    try
                    {
                        var record = csv.GetRecord<T>();
                        if (record is Property property)
                        {
                            property.SourcePath = path;
                            property.SourceLine = line;
                            if (IsBlank(property))
                            {
                                AddError("COGS-READ-064", "Blank property rows are not permitted.", path, line);
                                continue;
                            }
                        }
                        else if (record is Setting setting)
                        {
                            setting.SourcePath = path;
                            setting.SourceLine = line;
                            if (string.IsNullOrWhiteSpace(setting.Key) && string.IsNullOrWhiteSpace(setting.Value))
                            {
                                AddError("COGS-READ-065", "Blank setting rows are not permitted.", path, line);
                                continue;
                            }
                        }
                        records.Add(record);
                    }
                    catch (Exception exception)
                    {
                        AddError("COGS-READ-066", $"CSV row cannot be parsed: {exception.Message}", path, line, exception: exception);
                    }
                }
                return !Errors.Any(x => x.Level == ErrorLevel.Error && string.Equals(x.SourcePath, path, StringComparison.Ordinal));
            }
            catch (Exception exception)
            {
                AddError("COGS-READ-067", $"CSV file cannot be read: {exception.Message}", path, exception: exception);
                return false;
            }
        }

        private bool TryGetExactDirectory(string parent, string name, bool required, out string path)
        {
            path = null;
            try
            {
                var entries = Directory.EnumerateDirectories(parent).ToList();
                var exact = entries.FirstOrDefault(x => string.Equals(Path.GetFileName(x), name, StringComparison.Ordinal));
                if (exact != null)
                {
                    path = exact;
                    return true;
                }
                var wrongCase = entries.FirstOrDefault(x => string.Equals(Path.GetFileName(x), name, StringComparison.OrdinalIgnoreCase));
                if (wrongCase != null)
                {
                    AddError("COGS-READ-070", $"Directory '{Path.GetFileName(wrongCase)}' has incorrect casing; expected '{name}'.", wrongCase);
                    return false;
                }
                if (required)
                {
                    AddError("COGS-READ-071", $"Required directory '{name}' is missing.", Path.Combine(parent, name));
                }
                return !required;
            }
            catch (Exception exception)
            {
                AddError("COGS-READ-072", $"Directory cannot be enumerated: {exception.Message}", parent, exception: exception);
                return false;
            }
        }

        private bool TryGetExactFile(string parent, string name, bool required, out string path)
        {
            path = null;
            try
            {
                var entries = Directory.EnumerateFiles(parent).ToList();
                var exact = entries.FirstOrDefault(x => string.Equals(Path.GetFileName(x), name, StringComparison.Ordinal));
                if (exact != null)
                {
                    path = exact;
                    return true;
                }
                var wrongCase = entries.FirstOrDefault(x => string.Equals(Path.GetFileName(x), name, StringComparison.OrdinalIgnoreCase));
                if (wrongCase != null)
                {
                    AddError("COGS-READ-073", $"File '{Path.GetFileName(wrongCase)}' has incorrect casing; expected '{name}'.", wrongCase);
                    return false;
                }
                if (required)
                {
                    AddError("COGS-READ-074", $"Required file '{name}' is missing.", Path.Combine(parent, name));
                }
                return !required;
            }
            catch (Exception exception)
            {
                AddError("COGS-READ-075", $"Directory cannot be enumerated: {exception.Message}", parent, exception: exception);
                return false;
            }
        }

        private string SafeReadAllText(string path)
        {
            try { return File.ReadAllText(path, Encoding.UTF8); }
            catch (Exception exception)
            {
                AddError("COGS-READ-080", $"File cannot be read: {exception.Message}", path, exception: exception);
                return null;
            }
        }

        private string[] SafeReadAllLines(string path)
        {
            try { return File.ReadAllLines(path, Encoding.UTF8); }
            catch (Exception exception)
            {
                AddError("COGS-READ-081", $"File cannot be read: {exception.Message}", path, exception: exception);
                return Array.Empty<string>();
            }
        }

        private IEnumerable<string> SafeEnumerateDirectories(string path)
        {
            try { return Directory.EnumerateDirectories(path).ToArray(); }
            catch (Exception exception)
            {
                AddError("COGS-READ-082", $"Directory cannot be enumerated: {exception.Message}", path, exception: exception);
                return Array.Empty<string>();
            }
        }

        private IEnumerable<string> SafeEnumerateFiles(string path, string pattern = "*")
        {
            try { return Directory.EnumerateFiles(path, pattern).ToArray(); }
            catch (Exception exception)
            {
                AddError("COGS-READ-083", $"Directory cannot be enumerated: {exception.Message}", path, exception: exception);
                return Array.Empty<string>();
            }
        }

        private void AddError(string code, string message, string path = null, int? line = null, int? column = null, string modelPath = null, Exception exception = null) =>
            Errors.Add(new CogsError(ErrorLevel.Error, code, message, path, line, column, modelPath, exception));

        private void AddWarning(string code, string message, string path = null, int? line = null, int? column = null, string modelPath = null, Exception exception = null) =>
            Errors.Add(new CogsError(ErrorLevel.Warning, code, message, path, line, column, modelPath, exception));

        private IReadOnlyList<CogsError> OrderedErrors() => Errors
            .OrderBy(x => x.SourcePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(x => x.Line ?? 0)
            .ThenBy(x => x.Column ?? 0)
            .ThenBy(x => x.Code, StringComparer.Ordinal)
            .ToArray();

        private static bool IsBlank(Property property) =>
            string.IsNullOrWhiteSpace(property.Name) && string.IsNullOrWhiteSpace(property.DataType) &&
            string.IsNullOrWhiteSpace(property.MinCardinality) && string.IsNullOrWhiteSpace(property.MaxCardinality) &&
            string.IsNullOrWhiteSpace(property.Description);

        private static string TypeModelPath(bool isItemType, string name) =>
            (isItemType ? "ItemTypes." : "CompositeTypes.") + name;

        private static Property CloneProperty(Property source) => new Property
        {
            SourcePath = source.SourcePath,
            SourceLine = source.SourceLine,
            Name = source.Name,
            DataType = source.DataType,
            MinCardinality = source.MinCardinality,
            MaxCardinality = source.MaxCardinality,
            Description = source.Description,
            Ordered = source.Ordered,
            AllowSubtypes = source.AllowSubtypes,
            MinLength = source.MinLength,
            MaxLength = source.MaxLength,
            Enumeration = source.Enumeration,
            Pattern = source.Pattern,
            MinInclusive = source.MinInclusive,
            MinExclusive = source.MinExclusive,
            MaxInclusive = source.MaxInclusive,
            MaxExclusive = source.MaxExclusive,
            DeprecatedNamespace = source.DeprecatedNamespace,
            DeprecatedElementOrAttribute = source.DeprecatedElementOrAttribute,
            DeprecatedChoiceGroup = source.DeprecatedChoiceGroup
        };
    }
}
