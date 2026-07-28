using Cogs.Common;
using Cogs.Dto;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Cogs.Validation
{
    /// <summary>Validates the complete COGS 2.0 DTO contract without throwing.</summary>
    public static class DtoValidation
    {
        private static readonly string[] RequiredSettings =
        {
            "CogsVersion", "Title", "ShortTitle", "Slug", "Description", "Version",
            "Author", "Copyright", "NamespaceUrl", "NamespacePrefix"
        };

        private static readonly HashSet<string> EmptyValueSettings = new HashSet<string>(StringComparer.Ordinal)
        {
            "Description", "Author", "Copyright"
        };

        private static readonly HashSet<string> RuntimeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ItemContainer", "CogsValue", "CogsItem", "ReferenceType", "TopLevelReference",
            "LangString", "CogsDecimal", "CogsDate", "CogsDateTime", "CogsDateOnly", "CogsTime",
            "CogsDuration", "GregorianYear", "GregorianYearMonth", "GregorianMonthDay",
            "GregorianDay", "GregorianMonth"
        };

        private static readonly HashSet<string> RuntimeMemberNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "toobject", "fromobject", "tojson", "fromjson", "toelement", "fromelement",
            "toxml", "fromxml", "loadjson", "dumpjson", "loadxml", "dumpxml",
            "identificationfields", "cogstype", "isabstract"
        };

        private static readonly HashSet<string> StringFacetTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "string", "anyURI", "language", "langString"
        };

        private static readonly HashSet<string> NumericTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "decimal", "float", "double", "nonPositiveInteger", "negativeInteger", "long", "int",
            "nonNegativeInteger", "unsignedLong", "positiveInteger"
        };

        private static readonly HashSet<string> TemporalBoundTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "duration", "dateTime", "time", "date", "gYearMonth", "gYear", "gMonthDay", "gDay", "gMonth"
        };

        public static List<CogsError> Validate(CogsDtoModel model)
        {
            var errors = new List<CogsError>();
            if (model == null)
            {
                errors.Add(new CogsError(ErrorLevel.Error, "COGS-VAL-001", "A DTO model is required."));
                return errors;
            }

            ValidateSettings(model, errors);
            ValidateTypeNamespace(model, errors);
            ValidateIdentification(model, errors);
            ValidateProperties(model, errors);
            ValidateInheritance(model, errors);
            ValidateEffectiveProperties(model, errors);
            ValidateTopics(model, errors);

            return errors
                .OrderBy(x => x.SourcePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(x => x.Line ?? 0)
                .ThenBy(x => x.Column ?? 0)
                .ThenBy(x => x.Code, StringComparer.Ordinal)
                .ThenBy(x => x.ModelPath ?? string.Empty, StringComparer.Ordinal)
                .ToList();
        }

        private static void ValidateSettings(CogsDtoModel model, List<CogsError> errors)
        {
            var settings = model.Settings ?? new List<Setting>();
            var known = RequiredSettings.Concat(new[] { "CSharpNamespace" }).ToArray();

            foreach (var group in settings.GroupBy(x => x.Key ?? string.Empty, StringComparer.Ordinal))
            {
                if (group.Count() > 1)
                {
                    Add(errors, group.Skip(1).First(), "COGS-VAL-SET-001", $"Setting '{group.Key}' occurs more than once.", $"Settings.{group.Key}");
                }
            }

            foreach (var setting in settings)
            {
                if (string.IsNullOrWhiteSpace(setting.Key))
                {
                    Add(errors, setting, "COGS-VAL-SET-002", "A setting key may not be blank.", "Settings");
                    continue;
                }
                var canonical = known.FirstOrDefault(x => string.Equals(x, setting.Key, StringComparison.OrdinalIgnoreCase));
                if (canonical != null && setting.Key != canonical)
                {
                    Add(errors, setting, "COGS-VAL-SET-003", $"Setting '{setting.Key}' has incorrect casing; expected '{canonical}'.", $"Settings.{setting.Key}");
                }
            }

            foreach (var key in RequiredSettings)
            {
                var matches = settings.Where(x => x.Key == key).ToList();
                if (matches.Count == 0)
                {
                    errors.Add(new CogsError(ErrorLevel.Error, "COGS-VAL-SET-004", $"Required setting '{key}' is missing.", model.SourceDirectory, modelPath: $"Settings.{key}"));
                    continue;
                }
                if (!EmptyValueSettings.Contains(key) && string.IsNullOrWhiteSpace(matches[0].Value))
                {
                    Add(errors, matches[0], "COGS-VAL-SET-005", $"Setting '{key}' may not be blank.", $"Settings.{key}");
                }
            }

            var cogsVersion = FindSetting(settings, "CogsVersion");
            if (cogsVersion != null && cogsVersion.Value != "2.0")
            {
                Add(errors, cogsVersion, "COGS-VAL-SET-006", "CogsVersion must be exactly '2.0'.", "Settings.CogsVersion");
            }

            var slug = FindSetting(settings, "Slug");
            if (slug != null && !Regex.IsMatch(slug.Value ?? string.Empty, "^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant))
            {
                Add(errors, slug, "COGS-VAL-SET-007", "Slug must match [a-z][a-z0-9_]*.", "Settings.Slug");
            }

            var version = FindSetting(settings, "Version");
            if (version != null && !CogsConventions.IsCanonicalSemVer(version.Value))
            {
                Add(errors, version, "COGS-VAL-SET-008", "Version must be canonical Semantic Versioning 2.0 (major.minor.patch with optional prerelease/build metadata).", "Settings.Version");
            }

            var namespaceUrl = FindSetting(settings, "NamespaceUrl");
            if (namespaceUrl != null && !Uri.TryCreate(namespaceUrl.Value, UriKind.Absolute, out _))
            {
                Add(errors, namespaceUrl, "COGS-VAL-SET-009", "NamespaceUrl must be an absolute URI.", "Settings.NamespaceUrl");
            }

            var prefix = FindSetting(settings, "NamespacePrefix");
            if (prefix != null && (!IsNcName(prefix.Value) || string.Equals(prefix.Value, "xml", StringComparison.OrdinalIgnoreCase) || string.Equals(prefix.Value, "xmlns", StringComparison.OrdinalIgnoreCase)))
            {
                Add(errors, prefix, "COGS-VAL-SET-010", "NamespacePrefix must be an XML NCName other than 'xml' or 'xmlns'.", "Settings.NamespacePrefix");
            }
        }

        private static void ValidateTypeNamespace(CogsDtoModel model, List<CogsError> errors)
        {
            var all = AllTypes(model).ToList();
            foreach (var type in all)
            {
                if (!IsUpperNcName(type.Name))
                {
                    Add(errors, type, "COGS-VAL-NAME-001", $"Type name '{type.Name}' must be an XML NCName beginning with an uppercase Unicode letter.", type.Name);
                }
                if (CogsTypes.SimpleTypeNames.Contains(type.Name, StringComparer.OrdinalIgnoreCase))
                {
                    Add(errors, type, "COGS-VAL-NAME-002", $"Type name '{type.Name}' conflicts with builtin datatype '{CogsTypes.SimpleTypeNames.First(x => string.Equals(x, type.Name, StringComparison.OrdinalIgnoreCase))}'.", type.Name);
                }
                if (RuntimeNames.Contains(type.Name))
                {
                    Add(errors, type, "COGS-VAL-NAME-003", $"Type name '{type.Name}' is reserved by generated runtimes.", type.Name);
                }
                if (type is ItemType && type.IsPrimitive)
                {
                    Add(errors, type, "COGS-VAL-TYPE-001", "The Primitive marker is valid only on composite types.", type.Name);
                }
            }

            ReportCollisions(all, x => x.Name, StringComparer.Ordinal, "COGS-VAL-NAME-004", "duplicate type name", errors);
            ReportCollisions(all, x => x.Name, StringComparer.OrdinalIgnoreCase, "COGS-VAL-NAME-005", "case-insensitive type-name collision", errors);
            ReportCollisions(all, x => (x.Name ?? string.Empty).Normalize(NormalizationForm.FormC), StringComparer.Ordinal, "COGS-VAL-NAME-006", "Unicode-normalized type-name collision", errors);
            ReportCollisions(all, x => NormalizeTargetName(x.Name), StringComparer.Ordinal, "COGS-VAL-NAME-007", "generated-language type-name collision", errors);
        }

        private static void ValidateIdentification(CogsDtoModel model, List<CogsError> errors)
        {
            if (model.Identification == null || model.Identification.Count == 0)
            {
                errors.Add(new CogsError(ErrorLevel.Error, "COGS-VAL-ID-001", "Identification.csv must contain at least one identification field.", model.SourceDirectory, modelPath: "Identification"));
            }

            var identification = (model.Identification ?? new List<Property>()).Concat(model.IdentificationMixin ?? new List<Property>()).ToList();
            foreach (var property in identification)
            {
                var path = $"Identification.{property.Name}";
                if (!IsUpperNcName(property.Name))
                {
                    Add(errors, property, "COGS-VAL-ID-002", $"Identification name '{property.Name}' must be an XML NCName beginning with an uppercase Unicode letter.", path);
                }
                if (property.DataType != "string" && property.DataType != "anyURI")
                {
                    Add(errors, property, "COGS-VAL-ID-003", $"Identification field '{property.Name}' must use string or anyURI.", path);
                }
                if (!CogsConventions.TryParseCardinality(property.MinCardinality, property.MaxCardinality, out var min, out var max, out var cardinalityError) || min != 1 || max != 1)
                {
                    Add(errors, property, "COGS-VAL-ID-004", $"Identification field '{property.Name}' must have cardinality 1..1{(cardinalityError == null ? string.Empty : $": {cardinalityError}")}.", path);
                }
                bool orderedIsValid = CogsConventions.TryParseFlag(property.Ordered, out bool ordered);
                bool allowSubtypesIsValid = CogsConventions.TryParseFlag(property.AllowSubtypes, out bool allowSubtypes);
                if (!orderedIsValid || !allowSubtypesIsValid || ordered || allowSubtypes)
                {
                    Add(errors, property, "COGS-VAL-ID-005",
                        $"Identification field '{property.Name}' must use blank or false for Ordered and AllowSubtypes.", path);
                }
            }

            ReportPropertyCollisions(identification, "Identification", "COGS-VAL-ID-006", errors);
        }

        private static void ValidateProperties(CogsDtoModel model, List<CogsError> errors)
        {
            var types = AllTypes(model).ToList();
            var uniqueTypes = types
                .GroupBy(x => x.Name, StringComparer.Ordinal)
                .Where(x => x.Count() == 1)
                .ToDictionary(x => x.Key, x => x.Single(), StringComparer.Ordinal);
            var exactTypeNames = new HashSet<string>(types.Select(x => x.Name).Concat(CogsTypes.SimpleTypeNames), StringComparer.Ordinal);

            foreach (var owner in types)
            {
                ReportPropertyCollisions(owner.Properties, owner.Name, "COGS-VAL-PROP-001", errors);
                foreach (var property in owner.Properties)
                {
                    var path = $"{owner.Name}.{property.Name}";
                    if (property.Name == "DcTerms" || property.DataType == "dcTerms")
                    {
                        if (!IsExactDcTermsMarker(property))
                        {
                            Add(errors, property, "COGS-VAL-DCTERMS-001", "Dublin Core expansion must be declared by the exact marker DcTerms,dcTerms,0,1 with flags and facets blank.", path);
                        }
                    }
                    if (!IsUpperNcName(property.Name))
                    {
                        Add(errors, property, "COGS-VAL-PROP-002", $"Property name '{property.Name}' must be an XML NCName beginning with an uppercase Unicode letter.", path);
                    }
                    if (string.Equals(property.Name, "TopLevelReference", StringComparison.OrdinalIgnoreCase))
                    {
                        Add(errors, property, "COGS-VAL-PROP-003", $"Property name '{property.Name}' is reserved by the container contract.", path);
                    }
                    if (RuntimeMemberNames.Contains(NormalizeTargetName(property.Name)))
                    {
                        Add(errors, property, "COGS-VAL-PROP-006", $"Property name '{property.Name}' collides with a generated runtime member.", path);
                    }

                    if (!exactTypeNames.Contains(property.DataType))
                    {
                        var canonical = exactTypeNames.FirstOrDefault(x => string.Equals(x, property.DataType, StringComparison.OrdinalIgnoreCase));
                        Add(errors, property, canonical == null ? "COGS-VAL-PROP-004" : "COGS-VAL-PROP-005",
                            canonical == null
                                ? $"Property '{path}' uses undefined datatype '{property.DataType}'."
                                : $"Property '{path}' uses datatype '{property.DataType}' with incorrect casing; expected '{canonical}'.",
                            path);
                    }

                    if (!CogsConventions.TryParseCardinality(property.MinCardinality, property.MaxCardinality, out _, out var maximum, out var cardinalityError))
                    {
                        Add(errors, property, "COGS-VAL-CARD-001", $"Property '{path}' has invalid cardinality: {cardinalityError}.", path);
                    }

                    if (!CogsConventions.TryParseFlag(property.Ordered, out var ordered))
                    {
                        Add(errors, property, "COGS-VAL-FLAG-001", $"Ordered must be blank, true, or false; found '{property.Ordered}'.", path);
                    }
                    bool allowSubtypesIsValid = CogsConventions.TryParseFlag(property.AllowSubtypes, out var allowSubtypes);
                    if (!allowSubtypesIsValid)
                    {
                        Add(errors, property, "COGS-VAL-FLAG-002", $"AllowSubtypes must be blank, true, or false; found '{property.AllowSubtypes}'.", path);
                    }
                    if (ordered && maximum.HasValue && maximum.Value <= 1)
                    {
                        Add(errors, property, "COGS-VAL-CARD-002", "Ordered=true requires a repeated property.", path);
                    }

                    var declared = model.ReusableDataTypes.FirstOrDefault(x => x.Name == property.DataType);
                    var itemDeclared = model.ItemTypes.FirstOrDefault(x => x.Name == property.DataType);
                    var modeledType = declared ?? (DataType)itemDeclared;
                    if (allowSubtypesIsValid && allowSubtypes && modeledType == null)
                    {
                        Add(errors, property, "COGS-VAL-SUB-001", "AllowSubtypes is valid only for item- or composite-valued properties.", path);
                    }
                    if (allowSubtypesIsValid && modeledType?.IsAbstract == true && !allowSubtypes)
                    {
                        AddWarning(errors, property, "COGS-VAL-SUB-002",
                            $"Abstract {(itemDeclared == null ? "composite" : "item")} '{modeledType.Name}' should declare AllowSubtypes=true at this property; it is treated as true.", path);
                    }
                    if (allowSubtypesIsValid && allowSubtypes && modeledType != null &&
                        uniqueTypes.TryGetValue(modeledType.Name, out var uniqueModeledType) &&
                        ReferenceEquals(modeledType, uniqueModeledType))
                    {
                        bool hasDescendant = uniqueTypes.Values.Any(candidate =>
                            !ReferenceEquals(candidate, modeledType) &&
                            (candidate is ItemType) == (modeledType is ItemType) &&
                            IsDescendantOf(candidate, modeledType, uniqueTypes));
                        if (!hasDescendant)
                        {
                            string kind = modeledType is ItemType ? "item" : "composite";
                            AddWarning(errors, property, "COGS-VAL-SUB-003",
                                $"Property '{path}' sets AllowSubtypes=true, but no other {kind} type extends '{modeledType.Name}'; the flag currently permits no additional concrete type.", path);
                        }
                    }

                    ValidateFacets(property, path, errors);
                }
            }

            ValidateReusedPropertyDatatypes(model, types, errors);
            ValidateRdfPropertyNameCollisions(model, types, errors);
        }

        private static void ValidateReusedPropertyDatatypes(
            CogsDtoModel model,
            IReadOnlyCollection<Cogs.Dto.DataType> types,
            List<CogsError> errors)
        {
            List<(string Owner, Property Property)> uses = GetPropertyUses(model, types);

            foreach (var group in uses.GroupBy(use => use.Property.Name, StringComparer.Ordinal))
            {
                (string Owner, Property Property) first = group.First();
                foreach ((string Owner, Property Property) current in group.Skip(1))
                {
                    if (string.Equals(current.Property.DataType, first.Property.DataType, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string currentPath = $"{current.Owner}.{current.Property.Name}";
                    string firstPath = $"{first.Owner}.{first.Property.Name}";
                    Add(errors, current.Property, "COGS-VAL-PROP-007",
                        $"Property name '{group.Key}' is reused with datatype '{current.Property.DataType}' at " +
                        $"'{currentPath}', but its first declaration at '{firstPath}' uses " +
                        $"'{first.Property.DataType}'. Reused property names must use one exact datatype.",
                        currentPath);
                }
            }
        }

        private static void ValidateRdfPropertyNameCollisions(
            CogsDtoModel model,
            IReadOnlyCollection<Cogs.Dto.DataType> types,
            List<CogsError> errors)
        {
            var namedUses = GetPropertyUses(model, types)
                .Select(use => CogsRdfNaming.TryToPropertyLocalName(use.Property.Name, out string rdfName)
                    ? (Use: use, RdfName: rdfName)
                    : (Use: use, RdfName: (string)null))
                .Where(candidate => candidate.RdfName != null)
                .ToList();

            foreach (var group in namedUses.GroupBy(candidate => candidate.RdfName, StringComparer.Ordinal))
            {
                (string Owner, Property Property) first = group.First().Use;
                foreach (var candidate in group.Skip(1))
                {
                    (string Owner, Property Property) current = candidate.Use;
                    if (string.Equals(current.Property.Name, first.Property.Name, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string currentPath = $"{current.Owner}.{current.Property.Name}";
                    string firstPath = $"{first.Owner}.{first.Property.Name}";
                    Add(errors, current.Property, "COGS-VAL-PROP-008",
                        $"Property name '{current.Property.Name}' at '{currentPath}' and " +
                        $"'{first.Property.Name}' at '{firstPath}' both map to RDF property term " +
                        $"'{group.Key}'. Distinct COGS property names must not share an RDF term.",
                        currentPath);
                }
            }
        }

        private static List<(string Owner, Property Property)> GetPropertyUses(
            CogsDtoModel model,
            IEnumerable<Cogs.Dto.DataType> types)
        {
            var uses = new List<(string Owner, Property Property)>();
            uses.AddRange(model.Identification.Select(property => ("Identification", property)));
            uses.AddRange(model.IdentificationMixin.Select(property => ("IdentificationMixin", property)));
            foreach (Cogs.Dto.DataType owner in types.OrderBy(type => type.Name, StringComparer.Ordinal))
            {
                uses.AddRange(owner.Properties.Select(property => (owner.Name, property)));
            }

            return uses;
        }

        private static void ValidateInheritance(CogsDtoModel model, List<CogsError> errors)
        {
            var all = AllTypes(model).ToList();
            var unique = all.GroupBy(x => x.Name, StringComparer.Ordinal).Where(x => x.Count() == 1).ToDictionary(x => x.Key, x => x.Single(), StringComparer.Ordinal);

            foreach (var type in all)
            {
                if (string.IsNullOrWhiteSpace(type.Extends))
                {
                    continue;
                }
                if (CogsTypes.SimpleTypeNames.Contains(type.Extends, StringComparer.Ordinal))
                {
                    Add(errors, type, "COGS-VAL-INH-001", $"Type '{type.Name}' cannot extend builtin datatype '{type.Extends}'.", type.Name);
                    continue;
                }
                if (!unique.TryGetValue(type.Extends, out var parent))
                {
                    var canonical = unique.Keys.FirstOrDefault(x => string.Equals(x, type.Extends, StringComparison.OrdinalIgnoreCase));
                    Add(errors, type, canonical == null ? "COGS-VAL-INH-002" : "COGS-VAL-INH-003",
                        canonical == null ? $"Parent type '{type.Extends}' does not exist." : $"Parent type '{type.Extends}' has incorrect casing; expected '{canonical}'.", type.Name);
                    continue;
                }
                if ((type is ItemType) != (parent is ItemType))
                {
                    Add(errors, type, "COGS-VAL-INH-004", $"Type '{type.Name}' cannot inherit across item/composite kinds from '{parent.Name}'.", type.Name);
                }
            }

            var states = new Dictionary<Cogs.Dto.DataType, int>();
            var stack = new List<Cogs.Dto.DataType>();
            foreach (var type in all)
            {
                DetectCycle(type, unique, states, stack, errors);
            }

            foreach (var type in unique.Values.Where(candidate => candidate.IsAbstract))
            {
                bool hasConcreteDescendant = unique.Values.Any(candidate =>
                    !candidate.IsAbstract &&
                    (candidate is ItemType) == (type is ItemType) &&
                    IsDescendantOf(candidate, type, unique));
                if (!hasConcreteDescendant)
                {
                    string kind = type is ItemType ? "item" : "composite";
                    AddWarning(errors, type, "COGS-VAL-INH-007",
                        $"Abstract {kind} type '{type.Name}' has no concrete descendants; no instance can satisfy this type.",
                        type.Name);
                }
            }
        }

        private static bool IsDescendantOf(
            Cogs.Dto.DataType candidate,
            Cogs.Dto.DataType ancestor,
            IReadOnlyDictionary<string, Cogs.Dto.DataType> types)
        {
            var visited = new HashSet<Cogs.Dto.DataType>();
            Cogs.Dto.DataType current = candidate;
            while (visited.Add(current) &&
                   !string.IsNullOrWhiteSpace(current.Extends) &&
                   types.TryGetValue(current.Extends, out var parent))
            {
                if ((parent is ItemType) != (ancestor is ItemType))
                {
                    return false;
                }
                if (ReferenceEquals(parent, ancestor))
                {
                    return true;
                }
                current = parent;
            }
            return false;
        }

        private static void DetectCycle(Cogs.Dto.DataType type, IReadOnlyDictionary<string, Cogs.Dto.DataType> types, IDictionary<Cogs.Dto.DataType, int> states, IList<Cogs.Dto.DataType> stack, List<CogsError> errors)
        {
            if (states.TryGetValue(type, out var state))
            {
                if (state == 1)
                {
                    var start = stack.IndexOf(type);
                    var cycle = stack.Skip(Math.Max(start, 0)).Select(x => x.Name).Concat(new[] { type.Name });
                    Add(errors, type, "COGS-VAL-INH-005", $"Inheritance cycle detected: {string.Join(" -> ", cycle)}.", type.Name);
                }
                return;
            }
            states[type] = 1;
            stack.Add(type);
            if (!string.IsNullOrWhiteSpace(type.Extends) && types.TryGetValue(type.Extends, out var parent))
            {
                DetectCycle(parent, types, states, stack, errors);
            }
            stack.RemoveAt(stack.Count - 1);
            states[type] = 2;
        }

        private static void ValidateEffectiveProperties(CogsDtoModel model, List<CogsError> errors)
        {
            var all = AllTypes(model).ToList();
            var types = all.GroupBy(x => x.Name, StringComparer.Ordinal).Where(x => x.Count() == 1).ToDictionary(x => x.Key, x => x.Single(), StringComparer.Ordinal);
            var identity = model.Identification.Concat(model.IdentificationMixin).ToList();

            foreach (var type in all)
            {
                var inherited = new List<Property>();
                var seenTypes = new HashSet<string>(StringComparer.Ordinal);
                var parentName = type.Extends;
                while (!string.IsNullOrWhiteSpace(parentName) && seenTypes.Add(parentName) && types.TryGetValue(parentName, out var parent))
                {
                    inherited.AddRange(parent.Properties);
                    parentName = parent.Extends;
                }
                if (type is ItemType)
                {
                    inherited.AddRange(identity);
                }

                var inheritedNames = new HashSet<string>(inherited.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
                var inheritedNormalized = new HashSet<string>(inherited.Select(x => NormalizeTargetName(x.Name)), StringComparer.Ordinal);
                foreach (var property in type.Properties)
                {
                    if (inheritedNames.Contains(property.Name) || inheritedNormalized.Contains(NormalizeTargetName(property.Name)))
                    {
                        Add(errors, property, "COGS-VAL-INH-006", $"Property '{type.Name}.{property.Name}' shadows an inherited or injected identification property.", $"{type.Name}.{property.Name}");
                    }
                }
            }
        }

        private static void ValidateTopics(CogsDtoModel model, List<CogsError> errors)
        {
            var itemNames = new HashSet<string>(model.ItemTypes.Select(x => x.Name), StringComparer.Ordinal);
            foreach (var topic in model.TopicIndices)
            {
                if (!IsSingleRelativeName(topic.Name))
                {
                    errors.Add(new CogsError(ErrorLevel.Error, "COGS-VAL-TOPIC-007",
                        $"Topic name '{topic.Name}' must be a single normalized relative directory name.",
                        topic.IndexSourcePath ?? topic.SourcePath, topic.SourceLine, topic.SourceColumn, modelPath: $"Topics.{topic.Name}"));
                }

                var seenItems = new HashSet<string>(StringComparer.Ordinal);
                for (int itemIndex = 0; itemIndex < topic.ItemTypes.Count; itemIndex++)
                {
                    string item = topic.ItemTypes[itemIndex];
                    SourceTextEntry source = SourceAt(topic.ItemTypeSources, itemIndex);
                    string sourcePath = source?.SourcePath ?? topic.SourcePath;
                    int? sourceLine = source?.SourceLine;
                    int? sourceColumn = source?.SourceColumn;
                    if (string.IsNullOrWhiteSpace(item))
                    {
                        errors.Add(new CogsError(ErrorLevel.Error, "COGS-VAL-TOPIC-004",
                            $"Topic '{topic.Name}' contains a blank item entry.", sourcePath, sourceLine, sourceColumn, modelPath: $"Topics.{topic.Name}.Items"));
                        continue;
                    }
                    if (!string.Equals(item, item.Trim(), StringComparison.Ordinal) || !IsSingleRelativeName(item))
                    {
                        errors.Add(new CogsError(ErrorLevel.Error, "COGS-VAL-TOPIC-005",
                            $"Topic item '{item}' must be a single normalized item type name, not a path.", sourcePath, sourceLine, sourceColumn, modelPath: $"Topics.{topic.Name}.Items"));
                        continue;
                    }
                    if (!seenItems.Add(item))
                    {
                        errors.Add(new CogsError(ErrorLevel.Error, "COGS-VAL-TOPIC-001",
                            $"Topic '{topic.Name}' lists item '{item}' more than once.", sourcePath, sourceLine, sourceColumn, modelPath: $"Topics.{topic.Name}.Items"));
                        continue;
                    }
                    if (!itemNames.Contains(item))
                    {
                        var canonical = itemNames.FirstOrDefault(x => string.Equals(x, item, StringComparison.OrdinalIgnoreCase));
                        errors.Add(new CogsError(ErrorLevel.Error, canonical == null ? "COGS-VAL-TOPIC-002" : "COGS-VAL-TOPIC-003",
                            canonical == null ? $"Topic '{topic.Name}' references unknown item '{item}'." : $"Topic '{topic.Name}' references '{item}' with incorrect casing; expected '{canonical}'.",
                            sourcePath, sourceLine, sourceColumn, modelPath: $"Topics.{topic.Name}.Items"));
                    }
                }

                ValidateArticleEntries(topic.ArticlesPath, topic.ArticleTocEntries, topic.ArticleTocEntrySources,
                    $"Topics.{topic.Name}.Articles", topic.SourcePath, errors);
            }

            ValidateArticleEntries(model.ArticlesPath, model.ArticleTocEntries, model.ArticleTocEntrySources,
                "Articles", model.SourceDirectory, errors);
        }

        private static void ValidateArticleEntries(
            string articleRoot,
            IReadOnlyList<string> entries,
            IReadOnlyList<SourceTextEntry> sources,
            string modelPath,
            string fallbackSourcePath,
            List<CogsError> errors)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var seenDocuments = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                string entry = entries[entryIndex];
                SourceTextEntry source = SourceAt(sources, entryIndex);
                string sourcePath = source?.SourcePath ?? fallbackSourcePath;
                int? sourceLine = source?.SourceLine;
                int? sourceColumn = source?.SourceColumn;
                CogsDocumentationPathStatus status = CogsDocumentationPath.Normalize(entry, out string normalized);
                if (status != CogsDocumentationPathStatus.Valid)
                {
                    AddArticleError(errors, status, entry, sourcePath, sourceLine, sourceColumn, modelPath);
                    continue;
                }
                if (!seen.Add(normalized))
                {
                    errors.Add(new CogsError(ErrorLevel.Error, "COGS-VAL-ARTICLE-003",
                        $"Article TOC entry '{normalized}' appears more than once.", sourcePath, sourceLine, sourceColumn, modelPath: modelPath));
                    continue;
                }

                status = CogsDocumentationPath.Resolve(articleRoot, normalized, out string resolvedPath);
                if (status != CogsDocumentationPathStatus.Valid)
                {
                    AddArticleError(errors, status, normalized, sourcePath, sourceLine, sourceColumn, modelPath);
                }
                else if (!seenDocuments.Add(resolvedPath))
                {
                    errors.Add(new CogsError(ErrorLevel.Error, "COGS-VAL-ARTICLE-003",
                        $"Article TOC entry '{normalized}' resolves to an article that is already listed.", sourcePath, sourceLine, sourceColumn, modelPath: modelPath));
                }
            }
        }

        private static void AddArticleError(
            List<CogsError> errors,
            CogsDocumentationPathStatus status,
            string entry,
            string sourcePath,
            int? sourceLine,
            int? sourceColumn,
            string modelPath)
        {
            string code = status switch
            {
                CogsDocumentationPathStatus.Blank or CogsDocumentationPathStatus.NotNormalized or CogsDocumentationPathStatus.UnsupportedExtension => "COGS-VAL-ARTICLE-001",
                CogsDocumentationPathStatus.DirectiveSyntax => "COGS-VAL-ARTICLE-002",
                CogsDocumentationPathStatus.OutsideRoot or CogsDocumentationPathStatus.RootMissing or CogsDocumentationPathStatus.LinkTraversal => "COGS-VAL-ARTICLE-004",
                CogsDocumentationPathStatus.Missing => "COGS-VAL-ARTICLE-005",
                CogsDocumentationPathStatus.IncorrectCase => "COGS-VAL-ARTICLE-006",
                _ => "COGS-VAL-ARTICLE-007"
            };
            errors.Add(new CogsError(ErrorLevel.Error, code,
                $"Article TOC entry '{entry}' {CogsDocumentationPath.Describe(status)}.", sourcePath, sourceLine, sourceColumn, modelPath: modelPath));
        }

        private static SourceTextEntry SourceAt(IReadOnlyList<SourceTextEntry> sources, int index) =>
            sources != null && index >= 0 && index < sources.Count ? sources[index] : null;

        private static bool IsSingleRelativeName(string value) =>
            CogsDocumentationPath.IsPortableSingleSegment(value);

        private static void ValidateFacets(Property property, string path, List<CogsError> errors)
        {
            var hasLength = property.MinLength.HasValue || property.MaxLength.HasValue;
            if (hasLength && !StringFacetTypes.Contains(property.DataType))
            {
                Add(errors, property, "COGS-VAL-FACET-001", "Length facets apply only to string, anyURI, language, and langString.", path);
            }
            if (property.MinLength < 0 || property.MaxLength < 0 || property.MinLength.HasValue && property.MaxLength.HasValue && property.MinLength > property.MaxLength)
            {
                Add(errors, property, "COGS-VAL-FACET-002", "Length facets must be non-negative and MinLength must not exceed MaxLength.", path);
            }

            if (!string.IsNullOrWhiteSpace(property.Pattern))
            {
                if (!StringFacetTypes.Contains(property.DataType))
                {
                    Add(errors, property, "COGS-VAL-FACET-003", "Pattern applies only to string, anyURI, language, and langString.", path);
                }
                if (!CogsConventions.IsPortablePattern(property.Pattern, out var patternError))
                {
                    Add(errors, property, "COGS-VAL-FACET-004", $"Pattern is outside the portable COGS subset: {patternError}", path);
                }
            }

            var enumeration = CogsConventions.ParseEnumeration(property.Enumeration);
            if (enumeration.Count > 0)
            {
                if (!CogsTypes.SimpleTypeNames.Contains(property.DataType, StringComparer.Ordinal) || property.DataType == "cogsDate" || property.DataType == "dcTerms")
                {
                    Add(errors, property, "COGS-VAL-FACET-006", "Enumeration applies only to scalar builtin datatypes.", path);
                }
                if (enumeration.Distinct(StringComparer.Ordinal).Count() != enumeration.Count)
                {
                    Add(errors, property, "COGS-VAL-FACET-007", "Enumeration contains duplicate lexical values.", path);
                }
                foreach (var value in enumeration)
                {
                    if (!CogsPrimitiveLexical.IsValid(property.DataType, value))
                    {
                        Add(errors, property, "COGS-VAL-FACET-013", $"Enumeration value '{value}' is not a valid {property.DataType} lexical value.", path);
                    }
                }
                for (var left = 0; left < enumeration.Count; left++)
                {
                    for (var right = left + 1; right < enumeration.Count; right++)
                    {
                        if (CogsPrimitiveLexical.Compare(property.DataType, enumeration[left], enumeration[right]) == CogsPrimitiveOrder.Equal)
                        {
                            Add(errors, property, "COGS-VAL-FACET-014", $"Enumeration values '{enumeration[left]}' and '{enumeration[right]}' denote the same primitive value.", path);
                        }
                    }
                }
            }

            var bounds = new[] { property.MinInclusive, property.MinExclusive, property.MaxInclusive, property.MaxExclusive };
            if (bounds.Any(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!NumericTypes.Contains(property.DataType) && !TemporalBoundTypes.Contains(property.DataType))
                {
                    Add(errors, property, "COGS-VAL-FACET-008", "Bounds apply only to numeric, temporal, or duration datatypes (not cogsDate).", path);
                }
                if (!string.IsNullOrWhiteSpace(property.MinInclusive) && !string.IsNullOrWhiteSpace(property.MinExclusive) ||
                    !string.IsNullOrWhiteSpace(property.MaxInclusive) && !string.IsNullOrWhiteSpace(property.MaxExclusive))
                {
                    Add(errors, property, "COGS-VAL-FACET-009", "Inclusive and exclusive bounds cannot both be specified on the same side.", path);
                }

                foreach (var bound in bounds.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    if (!CogsPrimitiveLexical.IsValid(property.DataType, bound))
                    {
                        Add(errors, property, "COGS-VAL-FACET-010", $"Facet value '{bound}' is not a valid {property.DataType} lexical value.", path);
                    }
                }

                var min = !string.IsNullOrWhiteSpace(property.MinInclusive) ? property.MinInclusive : property.MinExclusive;
                var max = !string.IsNullOrWhiteSpace(property.MaxInclusive) ? property.MaxInclusive : property.MaxExclusive;
                if (!string.IsNullOrWhiteSpace(min) && !string.IsNullOrWhiteSpace(max) &&
                    CogsPrimitiveLexical.IsValid(property.DataType, min) && CogsPrimitiveLexical.IsValid(property.DataType, max))
                {
                    var order = CogsPrimitiveLexical.Compare(property.DataType, min, max);
                    var hasExclusiveEnd = !string.IsNullOrWhiteSpace(property.MinExclusive) || !string.IsNullOrWhiteSpace(property.MaxExclusive);
                    if (order is CogsPrimitiveOrder.Greater or CogsPrimitiveOrder.Indeterminate || order == CogsPrimitiveOrder.Equal && hasExclusiveEnd)
                    {
                        Add(errors, property, "COGS-VAL-FACET-011", order == CogsPrimitiveOrder.Indeterminate
                            ? "Minimum and maximum bounds are indeterminate under the XSD partial order."
                            : "Minimum bound does not precede maximum bound.", path);
                    }
                }
            }

            if (enumeration.Count > 0)
            {
                foreach (var value in enumeration.Where(value => CogsPrimitiveLexical.IsValid(property.DataType, value)))
                {
                    if (property.MinLength.HasValue && value.Length < property.MinLength.Value ||
                        property.MaxLength.HasValue && value.Length > property.MaxLength.Value ||
                        !string.IsNullOrWhiteSpace(property.Pattern) && !SafePatternMatches(value, property.Pattern) ||
                        !EnumerationSatisfiesBounds(property, value))
                    {
                        Add(errors, property, "COGS-VAL-FACET-015", $"Enumeration value '{value}' contradicts another facet on this property.", path);
                    }
                }
            }
        }

        private static bool EnumerationSatisfiesBounds(Property property, string value)
        {
            if (!NumericTypes.Contains(property.DataType) && !TemporalBoundTypes.Contains(property.DataType)) return true;
            if (!string.IsNullOrWhiteSpace(property.MinInclusive) && CogsPrimitiveLexical.Compare(property.DataType, value, property.MinInclusive) is not (CogsPrimitiveOrder.Equal or CogsPrimitiveOrder.Greater)) return false;
            if (!string.IsNullOrWhiteSpace(property.MinExclusive) && CogsPrimitiveLexical.Compare(property.DataType, value, property.MinExclusive) != CogsPrimitiveOrder.Greater) return false;
            if (!string.IsNullOrWhiteSpace(property.MaxInclusive) && CogsPrimitiveLexical.Compare(property.DataType, value, property.MaxInclusive) is not (CogsPrimitiveOrder.Equal or CogsPrimitiveOrder.Less)) return false;
            if (!string.IsNullOrWhiteSpace(property.MaxExclusive) && CogsPrimitiveLexical.Compare(property.DataType, value, property.MaxExclusive) != CogsPrimitiveOrder.Less) return false;
            return true;
        }

        private static bool SafePatternMatches(string value, string pattern)
        {
            try
            {
                return Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant);
            }
            catch (ArgumentException)
            {
                // The malformed pattern already receives COGS-VAL-FACET-004.
                return true;
            }
        }

        private static bool IsExactDcTermsMarker(Property property) =>
            property.Name == "DcTerms" && property.DataType == "dcTerms" &&
            property.MinCardinality == "0" && property.MaxCardinality == "1" &&
            string.IsNullOrWhiteSpace(property.Description) &&
            string.IsNullOrWhiteSpace(property.Ordered) && string.IsNullOrWhiteSpace(property.AllowSubtypes) &&
            !property.MinLength.HasValue && !property.MaxLength.HasValue &&
            string.IsNullOrWhiteSpace(property.Enumeration) && string.IsNullOrWhiteSpace(property.Pattern) &&
            string.IsNullOrWhiteSpace(property.MinInclusive) && string.IsNullOrWhiteSpace(property.MinExclusive) &&
            string.IsNullOrWhiteSpace(property.MaxInclusive) && string.IsNullOrWhiteSpace(property.MaxExclusive);

        private static bool HasFacets(Property property) =>
            property.MinLength.HasValue || property.MaxLength.HasValue || !string.IsNullOrWhiteSpace(property.Enumeration) ||
            !string.IsNullOrWhiteSpace(property.Pattern) || !string.IsNullOrWhiteSpace(property.MinInclusive) ||
            !string.IsNullOrWhiteSpace(property.MinExclusive) || !string.IsNullOrWhiteSpace(property.MaxInclusive) ||
            !string.IsNullOrWhiteSpace(property.MaxExclusive);

        private static IEnumerable<Cogs.Dto.DataType> AllTypes(CogsDtoModel model) =>
            model.ItemTypes.Cast<Cogs.Dto.DataType>().Concat(model.ReusableDataTypes);

        private static Setting FindSetting(IEnumerable<Setting> settings, string key) => settings.FirstOrDefault(x => x.Key == key);

        private static bool IsNcName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            try { XmlConvert.VerifyNCName(value); return true; }
            catch (XmlException) { return false; }
        }

        private static bool IsUpperNcName(string value)
        {
            if (!IsNcName(value)) return false;
            return Rune.DecodeFromUtf16(value.AsSpan(), out var rune, out _) == System.Buffers.OperationStatus.Done && Rune.IsUpper(rune);
        }

        private static string NormalizeTargetName(string value) =>
            new string((value ?? string.Empty).Normalize(NormalizationForm.FormC).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        private static void ReportCollisions(IEnumerable<Cogs.Dto.DataType> values, Func<Cogs.Dto.DataType, string> key, IEqualityComparer<string> comparer, string code, string label, List<CogsError> errors)
        {
            foreach (var group in values.GroupBy(key, comparer).Where(x => x.Count() > 1))
            {
                foreach (var type in group.Skip(1))
                {
                    Add(errors, type, code, $"Type '{type.Name}' has a {label} with '{group.First().Name}'.", type.Name);
                }
            }
        }

        private static void ReportPropertyCollisions(IEnumerable<Property> properties, string owner, string code, List<CogsError> errors)
        {
            var list = properties?.ToList() ?? new List<Property>();
            foreach (var group in list.GroupBy(x => NormalizeTargetName(x.Name), StringComparer.Ordinal).Where(x => x.Count() > 1))
            {
                foreach (var property in group.Skip(1))
                {
                    Add(errors, property, code, $"Property '{owner}.{property.Name}' collides with '{group.First().Name}'.", $"{owner}.{property.Name}");
                }
            }
        }

        private static void Add(List<CogsError> errors, Cogs.Dto.DataType source, string code, string message, string modelPath) =>
            errors.Add(new CogsError(ErrorLevel.Error, code, message, source.SourcePath, modelPath: modelPath));

        private static void Add(List<CogsError> errors, Property source, string code, string message, string modelPath) =>
            errors.Add(new CogsError(ErrorLevel.Error, code, message, source.SourcePath, source.SourceLine, modelPath: modelPath));

        private static void Add(List<CogsError> errors, Setting source, string code, string message, string modelPath) =>
            errors.Add(new CogsError(ErrorLevel.Error, code, message, source.SourcePath, source.SourceLine, modelPath: modelPath));

        private static void AddWarning(List<CogsError> errors, Property source, string code, string message, string modelPath) =>
            errors.Add(new CogsError(ErrorLevel.Warning, code, message, source.SourcePath, source.SourceLine, modelPath: modelPath));

        private static void AddWarning(List<CogsError> errors, Cogs.Dto.DataType source, string code, string message, string modelPath) =>
            errors.Add(new CogsError(ErrorLevel.Warning, code, message, source.SourcePath, modelPath: modelPath));

        // Compatibility helpers retained for callers that previously ran individual checks.
        public static List<CogsError> CheckOrderedCollectionsMustHaveCardinalityGreaterThanOne(CogsDtoModel model, List<CogsError> errors = null) => AppendFiltered(model, errors, "COGS-VAL-CARD-002");
        public static List<CogsError> CheckAbstractDataTypePropertiesMustAllowSubtypes(CogsDtoModel model, List<CogsError> errors = null) => AppendFiltered(model, errors, "COGS-VAL-SUB-002");
        public static List<CogsError> CheckDuplicatePropertiesInSameItem(CogsDtoModel model, List<CogsError> errors = null) => AppendFiltered(model, errors, "COGS-VAL-PROP-001");
        public static List<CogsError> CheckDataTypesMustBeDefined(CogsDtoModel model, List<CogsError> errors = null) => AppendFiltered(model, errors, "COGS-VAL-PROP-004");
        public static List<CogsError> CheckDataTypeNamesShouldMatchCase(CogsDtoModel model, List<CogsError> errors = null) => AppendFiltered(model, errors, "COGS-VAL-PROP-005");
        public static List<CogsError> CheckDataTypeNamesShouldNotConflictWithBuiltins(CogsDtoModel model, List<CogsError> errors = null) => AppendFiltered(model, errors, "COGS-VAL-NAME-002");
        public static List<CogsError> CheckDataTypeNamesShouldBePascalCase(CogsDtoModel model, List<CogsError> errors = null) => AppendFiltered(model, errors, "COGS-VAL-NAME-001");
        public static List<CogsError> CheckPropertyNamesShouldBePascalCase(CogsDtoModel model, List<CogsError> errors = null) => AppendFiltered(model, errors, "COGS-VAL-PROP-002");
        public static List<CogsError> CheckSettingsSlugToEnsureNoSpaces(CogsDtoModel model, List<CogsError> errors = null) => AppendFiltered(model, errors, "COGS-VAL-SET-007");
        public static List<CogsError> CheckDerivationOfPrimativeTypesNotAllowed(CogsDtoModel model, List<CogsError> errors = null) => AppendFiltered(model, errors, "COGS-VAL-INH-001");

        public static List<CogsError> CheckReusedPropertyNamesShouldHaveSameDatatype(CogsDtoModel model, List<CogsError> errors = null)
            => AppendFiltered(model, errors, "COGS-VAL-PROP-007");

        public static List<CogsError> NamingDataTypeReferenceTypeNotAllowed(CogsDtoModel model, List<CogsError> errors = null) => AppendFiltered(model, errors, "COGS-VAL-NAME-003");
        public static List<CogsError> NamingPropertyTopLevelReferenceNotAllowed(CogsDtoModel model, List<CogsError> errors = null) => AppendFiltered(model, errors, "COGS-VAL-PROP-003");

        private static List<CogsError> AppendFiltered(CogsDtoModel model, List<CogsError> errors, params string[] codes)
        {
            errors ??= new List<CogsError>();
            var selected = new HashSet<string>(codes, StringComparer.Ordinal);
            errors.AddRange(Validate(model).Where(x => selected.Contains(x.Code)));
            return errors;
        }
    }
}
