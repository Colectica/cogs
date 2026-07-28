// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cogs.Model
{
    public class CogsModelBuilder
    {
        public List<CogsError> Errors { get; } = new List<CogsError>();

        private Cogs.Dto.CogsDtoModel dto;
        private CogsModel model;
        private Dictionary<string, DataType> types;

        /// <summary>Builds a connected model or returns diagnostics without throwing.</summary>
        public CogsBuildResult BuildResult(Cogs.Dto.CogsDtoModel cogsDtoModel)
        {
            Errors.Clear();
            if (cogsDtoModel == null)
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "COGS-BUILD-001", "A DTO model is required."));
                return new CogsBuildResult(null, Errors);
            }

            dto = cogsDtoModel;
            model = new CogsModel
            {
                SourceDirectory = dto.SourceDirectory,
                ArticlesPath = dto.ArticlesPath,
                HeaderInclude = dto.HeaderInclude,
                Settings = MapSettings(dto.Settings)
            };
            AddRange(model.ArticleTocEntries, dto.ArticleTocEntries);

            MapIdentification();
            CreateTypeStubs();
            CreateTypeIndex();
            ResolveInheritance();
            InjectIdentification();
            ResolvePropertyTypes();
            ApplyAbstractSubtypeDefaults();
            MapTopics();
            BuildRelationships();
            MarkCompositeSubstitutes();

            var ordered = Errors
                .OrderBy(x => x.SourcePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(x => x.Line ?? 0)
                .ThenBy(x => x.Code, StringComparer.Ordinal)
                .ToArray();
            return new CogsBuildResult(ordered.Any(x => x.Level == ErrorLevel.Error) ? null : model, ordered);
        }

        /// <summary>
        /// Compatibility adapter. Prefer <see cref="BuildResult"/> and inspect diagnostics.
        /// Invalid input returns null rather than a partial or fabricated model.
        /// </summary>
        [Obsolete("Use BuildResult and inspect its diagnostics. This compatibility adapter will be removed in COGS 3.0.")]
        public CogsModel Build(Cogs.Dto.CogsDtoModel cogsDtoModel) => BuildResult(cogsDtoModel).Model;

        private void MapIdentification()
        {
            foreach (var source in dto.Identification)
            {
                model.Identification.Add(MapProperty(source));
            }
            foreach (var source in dto.IdentificationMixin)
            {
                var property = MapProperty(source);
                property.FromMixin = true;
                model.Identification.Add(property);
            }
        }

        private void CreateTypeStubs()
        {
            foreach (var source in dto.ItemTypes)
            {
                var target = new ItemType();
                MapDataType(source, target, true);
                model.ItemTypes.Add(target);
            }
            foreach (var source in dto.ReusableDataTypes)
            {
                var target = new DataType();
                MapDataType(source, target, false);
                model.ReusableDataTypes.Add(target);
            }
        }

        private void CreateTypeIndex()
        {
            types = new Dictionary<string, DataType>(StringComparer.Ordinal);
            foreach (var type in model.AllDataTypes)
            {
                if (!types.TryAdd(type.Name, type))
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, "COGS-BUILD-002", $"Duplicate type '{type.Name}' cannot be connected.", modelPath: type.Name));
                }
            }
            foreach (var primitiveName in CogsTypes.SimpleTypeNames.Distinct(StringComparer.Ordinal))
            {
                if (!types.TryAdd(primitiveName, new DataType
                {
                    Name = primitiveName,
                    IsXmlPrimitive = true,
                    IsPrimitive = true,
                    Properties = new List<Property>()
                }))
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, "COGS-BUILD-003", $"Model type '{primitiveName}' conflicts with a builtin datatype.", modelPath: primitiveName));
                }
            }
        }

        private void ResolveInheritance()
        {
            foreach (var type in model.AllDataTypes)
            {
                if (string.IsNullOrWhiteSpace(type.ExtendsTypeName))
                {
                    continue;
                }
                if (!types.TryGetValue(type.ExtendsTypeName, out var parent) || parent.IsXmlPrimitive)
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, "COGS-BUILD-010", $"Parent type '{type.ExtendsTypeName}' for '{type.Name}' is undefined.", modelPath: type.Name));
                    continue;
                }
                if ((type is ItemType) != (parent is ItemType))
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, "COGS-BUILD-011", $"Type '{type.Name}' cannot inherit across item/composite kinds from '{parent.Name}'.", modelPath: type.Name));
                    continue;
                }
                parent.ChildTypes.Add(type);
            }

            foreach (var type in model.AllDataTypes)
            {
                var chain = new List<DataType>();
                var seen = new HashSet<string>(StringComparer.Ordinal) { type.Name };
                var parentName = type.ExtendsTypeName;
                while (!string.IsNullOrWhiteSpace(parentName))
                {
                    if (!types.TryGetValue(parentName, out var parent) || parent.IsXmlPrimitive)
                    {
                        break;
                    }
                    if (!seen.Add(parent.Name))
                    {
                        Errors.Add(new CogsError(ErrorLevel.Error, "COGS-BUILD-012", $"Inheritance cycle encountered while connecting '{type.Name}'.", modelPath: type.Name));
                        chain.Clear();
                        break;
                    }
                    chain.Insert(0, parent);
                    parentName = parent.ExtendsTypeName;
                }
                AddRange(type.ParentTypes, chain);
            }
        }

        private void InjectIdentification()
        {
            foreach (var item in model.ItemTypes.Where(x => string.IsNullOrWhiteSpace(x.ExtendsTypeName)))
            {
                var identification = model.Identification.Select(CloneProperty).ToArray();
                for (int index = identification.Length - 1; index >= 0; index--)
                {
                    item.Properties.Insert(0, identification[index]);
                }
            }
        }

        private void ResolvePropertyTypes()
        {
            foreach (var type in model.AllDataTypes)
            {
                foreach (var property in type.Properties)
                {
                    if (!types.TryGetValue(property.DataTypeName, out var propertyType))
                    {
                        Errors.Add(new CogsError(ErrorLevel.Error, "COGS-BUILD-020", $"Property '{type.Name}.{property.Name}' uses undefined datatype '{property.DataTypeName}'.", modelPath: $"{type.Name}.{property.Name}"));
                        continue;
                    }
                    property.DataType = propertyType;
                }
            }
            foreach (var property in model.Identification)
            {
                if (types.TryGetValue(property.DataTypeName, out var propertyType))
                {
                    property.DataType = propertyType;
                }
                else
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, "COGS-BUILD-021", $"Identification property '{property.Name}' uses undefined datatype '{property.DataTypeName}'.", modelPath: $"Identification.{property.Name}"));
                }
            }
        }

        private void ApplyAbstractSubtypeDefaults()
        {
            foreach (var property in model.AllDataTypes.SelectMany(type => type.Properties))
            {
                if (property.DataType?.IsAbstract == true)
                {
                    property.AllowSubtypes = true;
                }
            }
        }

        private void MapTopics()
        {
            foreach (var source in dto.TopicIndices)
            {
                var target = new TopicIndex
                {
                    Name = source.Name,
                    Description = source.Description,
                    ArticlesPath = source.ArticlesPath
                };
                AddRange(target.ItemTypeNames, source.ItemTypes);
                AddRange(target.ArticleTocEntries, source.ArticleTocEntries);
                for (int itemIndex = 0; itemIndex < source.ItemTypes.Count; itemIndex++)
                {
                    string itemName = source.ItemTypes[itemIndex];
                    if (types.TryGetValue(itemName, out var type) && type is ItemType)
                    {
                        target.ItemTypes.Add(type);
                    }
                    else
                    {
                        Cogs.Dto.SourceTextEntry location = itemIndex < source.ItemTypeSources.Count ? source.ItemTypeSources[itemIndex] : null;
                        Errors.Add(new CogsError(ErrorLevel.Error, "COGS-BUILD-030", $"Topic '{source.Name}' references unknown item type '{itemName}'.",
                            location?.SourcePath ?? source.SourcePath, location?.SourceLine, location?.SourceColumn, modelPath: $"Topics.{source.Name}.Items"));
                    }
                }
                model.TopicIndices.Add(target);
            }
        }

        private void BuildRelationships()
        {
            foreach (var item in model.ItemTypes)
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                TraverseProperties(CogsTypeSystem.EffectiveProperties(item), item.Relationships, new HashSet<DataType>(), string.Empty, keys);
            }
        }

        private void TraverseProperties(IEnumerable<Property> properties, IList<Relationship> relationships, HashSet<DataType> recursionStack, string prefix, HashSet<string> keys)
        {
            foreach (var property in properties)
            {
                if (property.DataType == null || property.DataType.IsXmlPrimitive)
                {
                    continue;
                }
                var path = string.IsNullOrEmpty(prefix) ? property.Name : prefix + "/" + property.Name;
                if (property.DataType is ItemType item)
                {
                    var key = path + "\u001f" + item.Name;
                    if (keys.Add(key))
                    {
                        relationships.Add(new Relationship { PropertyName = path, TargetItemType = item });
                    }
                    continue;
                }

                if (!recursionStack.Add(property.DataType))
                {
                    continue;
                }
                TraverseProperties(CogsTypeSystem.EffectiveProperties(property.DataType), relationships, recursionStack, path, keys);
                recursionStack.Remove(property.DataType);
            }
        }

        private void MarkCompositeSubstitutes()
        {
            foreach (var property in model.AllDataTypes.SelectMany(x => x.Properties))
            {
                if (!property.AllowSubtypes || property.DataType == null || property.DataType is ItemType || property.DataType.IsXmlPrimitive)
                {
                    continue;
                }
                MarkSubstitute(property.DataType, new HashSet<DataType>());
            }
        }

        private static void MarkSubstitute(DataType dataType, HashSet<DataType> seen)
        {
            if (!seen.Add(dataType)) return;
            dataType.IsSubstitute = true;
            foreach (var child in dataType.ChildTypes)
            {
                MarkSubstitute(child, seen);
            }
        }

        private void MapDataType(Cogs.Dto.DataType source, DataType target, bool isItemType)
        {
            target.Name = source.Name;
            target.Description = source.Description;
            target.IsAbstract = source.IsAbstract;
            target.IsPrimitive = source.IsPrimitive;
            target.ExtendsTypeName = source.Extends;
            target.DeprecatedNamespace = source.DeprecatedNamespace;
            target.IsDeprecated = source.IsDeprecated;
            AddRange(target.AdditionalText, source.AdditionalText.Select(CloneAdditionalText));
            foreach (var property in source.Properties)
            {
                target.Properties.Add(MapProperty(property));
            }
            target.Path = isItemType ? $"/item-types/{target.Name}/index" : $"/composite-types/{target.Name}/index";
        }

        private Property MapProperty(Cogs.Dto.Property source)
        {
            if (!CogsConventions.TryParseCardinality(source.MinCardinality, source.MaxCardinality, out var minimum, out var maximum, out var cardinalityError))
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "COGS-BUILD-040", $"Property '{source.Name}' has invalid cardinality: {cardinalityError}.", source.SourcePath, source.SourceLine, modelPath: source.Name));
            }
            CogsConventions.TryParseFlag(source.Ordered, out var ordered);
            CogsConventions.TryParseFlag(source.AllowSubtypes, out var allowSubtypes);
            var enumeration = CogsConventions.ParseEnumeration(source.Enumeration);

            return new Property
            {
                Name = source.Name,
                DataTypeName = source.DataType,
                MinCardinality = minimum.ToString(System.Globalization.CultureInfo.InvariantCulture),
                MaxCardinality = maximum?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n",
                Description = source.Description,
                Ordered = ordered,
                AllowSubtypes = allowSubtypes,
                MinLength = source.MinLength,
                MaxLength = source.MaxLength,
                Enumeration = enumeration.ToList(),
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

        private static Property CloneProperty(Property source) => new Property
        {
            Name = source.Name,
            DataTypeName = source.DataTypeName,
            DataType = source.DataType,
            MinCardinality = source.MinCardinality,
            MaxCardinality = source.MaxCardinality,
            Description = source.Description,
            DeprecatedNamespace = source.DeprecatedNamespace,
            DeprecatedElementOrAttribute = source.DeprecatedElementOrAttribute,
            DeprecatedChoiceGroup = source.DeprecatedChoiceGroup,
            Ordered = source.Ordered,
            AllowSubtypes = source.AllowSubtypes,
            MinLength = source.MinLength,
            MaxLength = source.MaxLength,
            Enumeration = new List<string>(source.Enumeration),
            Pattern = source.Pattern,
            MinInclusive = source.MinInclusive,
            MinExclusive = source.MinExclusive,
            MaxInclusive = source.MaxInclusive,
            MaxExclusive = source.MaxExclusive,
            FromMixin = source.FromMixin
        };

        private static Cogs.Dto.AdditionalText CloneAdditionalText(Cogs.Dto.AdditionalText source) =>
            new Cogs.Dto.AdditionalText
            {
                FilePath = source.FilePath,
                Format = source.Format,
                Name = source.Name,
                Content = source.Content
            };

        private static void AddRange<T>(ICollection<T> target, IEnumerable<T> values)
        {
            foreach (T value in values)
            {
                target.Add(value);
            }
        }

        private static Settings MapSettings(IEnumerable<Cogs.Dto.Setting> settings)
        {
            var target = new Settings();
            foreach (var setting in settings)
            {
                switch (setting.Key)
                {
                    case "CogsVersion": target.CogsVersion = setting.Value; break;
                    case "Title": target.Title = setting.Value; break;
                    case "ShortTitle": target.ShortTitle = setting.Value; break;
                    case "Slug": target.Slug = setting.Value; break;
                    case "Description": target.Description = setting.Value; break;
                    case "Version": target.Version = setting.Value; break;
                    case "Author": target.Author = setting.Value; break;
                    case "Copyright": target.Copyright = setting.Value; break;
                    case "NamespaceUrl": target.NamespaceUrl = setting.Value; break;
                    case "NamespacePrefix": target.NamespacePrefix = setting.Value; break;
                    case "CSharpNamespace": target.CSharpNamespace = setting.Value; break;
                    default:
                        if (!target.ExtraSettings.ContainsKey(setting.Key ?? string.Empty))
                        {
                            target.ExtraSettings.Add(setting.Key ?? string.Empty, setting.Value);
                        }
                        break;
                }
            }
            return target;
        }
    }
}
