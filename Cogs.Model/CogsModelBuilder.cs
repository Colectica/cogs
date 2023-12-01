// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cogs.Model
{
    public class CogsModelBuilder
    {
        public List<CogsError> Errors { get; } = new List<CogsError>();

        private Cogs.Dto.CogsDtoModel dto;
        private CogsModel model;

        public CogsModel Build(Cogs.Dto.CogsDtoModel cogsDtoModel)
        {
            this.dto = cogsDtoModel;
            this.model = new CogsModel();

            // Copy information about articles.
            model.ArticlesPath = dto.ArticlesPath;
            model.ArticleTocEntries.AddRange(dto.ArticleTocEntries);

            // Identification
            foreach (var id in dto.Identification)
            {
                var property = new Property();
                MapProperty(id, property);
                model.Identification.Add(property);
            }

            // Settings
            model.HeaderInclude = dto.HeaderInclude;
            model.Settings = new Settings();
            foreach (var setting in dto.Settings)
            {
                switch (setting.Key)
                {
                    case "Title":
                        model.Settings.Title = setting.Value;
                        break;
                    case "ShortTitle":
                        model.Settings.ShortTitle = setting.Value;
                        break;
                    case "Slug":
                        model.Settings.Slug = setting.Value;
                        break;
                    case "Description":
                        model.Settings.Description = setting.Value;
                        break;
                    case "Version":
                        model.Settings.Version = setting.Value;
                        break;
                    case "Author":
                        model.Settings.Author = setting.Value;
                        break;
                    case "Copyright":
                        model.Settings.Copyright = setting.Value;
                        break;
                    case "NamespaceUrl":
                        model.Settings.NamespaceUrl = setting.Value;
                        break;
                    case "NamespacePrefix":
                        model.Settings.NamespacePrefix = setting.Value;
                        break;
                    case "CSharpNamespace":
                        model.Settings.CSharpNamespace = setting.Value;
                        break;
                    default:
                        model.Settings.ExtraSettings.Add(setting.Key, setting.Value);
                        break;
                }
            }

            // Set defaults for well-known settings, if they are blank.
            if (string.IsNullOrWhiteSpace(model.Settings.Title))
            {
                model.Settings.Title = "Model Title";
            }
            if (string.IsNullOrWhiteSpace(model.Settings.ShortTitle))
            {
                model.Settings.ShortTitle = "Model";
            }
            if (string.IsNullOrWhiteSpace(model.Settings.Slug))
            {
                model.Settings.Slug = "model";
            }

            if (string.IsNullOrWhiteSpace(model.Settings.Description))
            {
                model.Settings.Description = "TODO";
            }

            if (string.IsNullOrWhiteSpace(model.Settings.Version))
            {
                model.Settings.Version = "0.1";
            }

            if (string.IsNullOrWhiteSpace(model.Settings.Author))
            {
                model.Settings.Author = "TODO";
            }

            if (string.IsNullOrWhiteSpace(model.Settings.Copyright))
            {
                model.Settings.Copyright = "TODO";
            }

            if (string.IsNullOrWhiteSpace(model.Settings.NamespaceUrl))
            {
                model.Settings.NamespaceUrl = "http://example.org/todo";
            }

            if (string.IsNullOrWhiteSpace(model.Settings.NamespacePrefix))
            {
                model.Settings.NamespacePrefix = "todo";
            }


            // First pass: create object stubs.
            string[] itemNames = dto.ItemTypes.Select(x => x.Name).ToArray();

            foreach (var itemTypeDto in dto.ItemTypes)
            {
                var itemType = new ItemType();
                MapDataType(itemTypeDto, itemType, true);
                model.ItemTypes.Add(itemType);
                
                // add identification to all base types in itemtypes
                if (string.IsNullOrEmpty(itemType.ExtendsTypeName))
                {
                    itemType.Properties.InsertRange(0, model.Identification);
                }
                else
                {
                    if (!itemNames.Contains(itemType.ExtendsTypeName))
                    {
                        string errorMessage = $"Item {itemType.Name} can not extend {itemType.ExtendsTypeName} because it is not an item type.";
                        throw new InvalidOperationException(errorMessage);
                    }
                }
            }

            foreach (var reusableTypeDto in dto.ReusableDataTypes)
            {
                var reusableType = new DataType();
                MapDataType(reusableTypeDto, reusableType, false);
                model.ReusableDataTypes.Add(reusableType);
            }

            foreach (var topicIndexDto in dto.TopicIndices)
            {
                var index = new TopicIndex();
                MapTopicIndex(topicIndexDto, index);
                model.TopicIndices.Add(index);
            }
            

            // Second pass: add references between items.
            foreach (var itemType in model.ItemTypes)
            {
                CreateRelationships(itemType);
            }

            foreach (var type in model.ReusableDataTypes)
            {
                CreateRelationships(type);
            }

            foreach (var index in model.TopicIndices)
            {
                foreach (var itemTypeName in index.ItemTypeNames)
                {
                    var includedType = GetTypeByName(itemTypeName);
                    index.ItemTypes.Add(includedType);
                }
            }

            // Third pass: look for relationships among items.
            // Related item types, based on following the properties' data types.
            foreach (var itemType in model.ItemTypes)
            {
                ProcessProperties(itemType.Properties, itemType.Relationships, new HashSet<string>());
            }

            // find reusable types which can have a subclass used in their place
            foreach(var dataType in model.ReusableDataTypes.Union(model.ItemTypes))
            {
                foreach(var property in dataType.Properties)
                {
                    if (property.AllowSubtypes)
                    {
                        MarkSubstitute(property.DataType);
                    }
                }
            }

            return model;
        }

        private void MarkSubstitute(DataType dataType)
        {
            dataType.IsSubstitute = true;
            foreach(var child in dataType.ChildTypes)
            {
                MarkSubstitute(child);
            }
        }

        private void CreateRelationships(DataType type)
        {
            // Property types
            foreach (var property in type.Properties)
            {
                property.DataType = GetTypeByName(property.DataTypeName);
            }

            // Parents
            string extendsTypeName = type.ExtendsTypeName;
            while (!string.IsNullOrWhiteSpace(extendsTypeName))
            {
                var parent = GetTypeByName(extendsTypeName);
                type.ParentTypes.Insert(0, parent);
                extendsTypeName = parent.ExtendsTypeName;
            }

            // Look through all other types to determine which types extend this one.
            foreach (var otherType in model.ItemTypes.Where(x => x.ExtendsTypeName == type.Name))
            {
                type.ChildTypes.Add(otherType);
            }
            foreach (var otherType in model.ReusableDataTypes.Where(x => x.ExtendsTypeName == type.Name))
            {
                type.ChildTypes.Add(otherType);
            }

        }

        private void ProcessProperties(List<Property> properties, List<Relationship> relationships, HashSet<string> seenTypeNames, string prefixTypeStr = "")
        {
            foreach (var property in properties)
            {
                if (seenTypeNames.Contains(property.DataType?.Name))
                {
                    continue;
                }
                seenTypeNames.Add(property.DataType?.Name);

                // If the type of this property is an ItemType, consider it related.
                if (property.DataType is ItemType it)
                {
                    string nameStr = property.Name;
                    if (!string.IsNullOrWhiteSpace(prefixTypeStr))
                    {
                        nameStr = prefixTypeStr + "/" + nameStr;
                    }
                    var relationship = new Relationship
                    {
                        PropertyName = nameStr,
                        TargetItemType = it
                    };
                    relationships.Add(relationship);
                }

                // If the type is not an item type, dive deeper to see if
                // the regular-type might reference an ItemType.
                else
                {
                    string nameStr = property.Name;
                    if (!string.IsNullOrWhiteSpace(prefixTypeStr))
                    {
                        nameStr = prefixTypeStr + "/" + nameStr;
                    }
                    ProcessProperties(property.DataType.Properties, relationships, seenTypeNames, nameStr);
                }

            }

        }

        private DataType GetTypeByName(string dataTypeName)
        {
            // Try Item Type.
            var itemType = model.ItemTypes.FirstOrDefault(x => x.Name == dataTypeName);
            if (itemType != null)
            {
                return itemType;
            }

            // Try Reusable Type.
            var reusableType = model.ReusableDataTypes.FirstOrDefault(x => x.Name == dataTypeName);
            if (reusableType != null)
            {
                return reusableType;
            }

            // Must be a primitive, or something from outside the system.
            var primitiveType = new DataType();
            primitiveType.Name = dataTypeName;
            primitiveType.IsXmlPrimitive = true;
            return primitiveType;
        }

        private void MapDataType(Cogs.Dto.DataType dto, DataType dataType, bool isItemType)
        {
            dataType.Name = dto.Name;
            dataType.Description = dto.Description;
            dataType.IsAbstract = dto.IsAbstract;
            dataType.IsPrimitive = dto.IsPrimitive;
            dataType.ExtendsTypeName = dto.Extends;
            dataType.DeprecatedNamespace = dto.DeprecatedNamespace;
            dataType.IsDeprecated = dto.IsDeprecated;
            dataType.AdditionalText = dto.AdditionalText;

            foreach (var dtoProperty in dto.Properties)
            {
                var property = new Property();
                MapProperty(dtoProperty, property);
                dataType.Properties.Add(property);
            }

            if (isItemType)
            {
                dataType.Path = $"/item-types/{dataType.Name}/index";
            }
            else
            {
                dataType.Path = $"/composite-types/{dataType.Name}/index";
            }
        }

        private void MapProperty(Cogs.Dto.Property dto, Property property)
        {
            property.Name = dto.Name;
            property.DataTypeName = dto.DataType;

            property.MinCardinality = dto.MinCardinality;
            if (string.IsNullOrWhiteSpace(property.MinCardinality))
            {
                property.MinCardinality = "0";
            }

            property.MaxCardinality = dto.MaxCardinality;
            property.Description = dto.Description;

            property.Ordered = !string.IsNullOrWhiteSpace(dto.Ordered);
            property.AllowSubtypes = !string.IsNullOrWhiteSpace(dto.AllowSubtypes);

            // simple string restrictions
            property.MinLength = dto.MinLength;
            property.MaxLength = dto.MaxLength;
            if (!string.IsNullOrWhiteSpace(dto.Enumeration))
            {                
                string[] parts = dto.Enumeration.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                property.Enumeration = new List<string>(parts);
            }           
            property.Pattern = dto.Pattern;
            // numeric restrictions
            property.MinInclusive = dto.MinInclusive;
            property.MinExclusive = dto.MinExclusive;
            property.MaxInclusive = dto.MaxInclusive;
            property.MaxExclusive = dto.MaxExclusive;

            property.DeprecatedNamespace = dto.DeprecatedNamespace;
            property.DeprecatedElementOrAttribute = dto.DeprecatedElementOrAttribute;
            property.DeprecatedChoiceGroup = dto.DeprecatedChoiceGroup;
        }

        private void MapTopicIndex(Cogs.Dto.TopicIndex dto, TopicIndex topicIndex)
        {
            topicIndex.Name = dto.Name;
            topicIndex.Description = dto.Description;

            topicIndex.ItemTypeNames.AddRange(dto.ItemTypes);
            topicIndex.ArticlesPath = dto.ArticlesPath;
            topicIndex.ArticleTocEntries.AddRange(dto.ArticleTocEntries);
        }

    }
}
