// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Dto;
using System.Collections.Generic;

namespace Cogs.Model
{
    public class DataType : CogsModelNode
    {
        private bool isXmlPrimitive;
        private string path;
        private string name;
        private string description;
        private IList<AdditionalText> additionalText = new List<AdditionalText>();
        private string extendsTypeName;
        private IList<DataType> parentTypes = new List<DataType>();
        private IList<DataType> childTypes = new List<DataType>();
        private bool isSubstitute;
        private IList<Relationship> relationships = new List<Relationship>();
        private bool isAbstract;
        private bool isPrimitive;
        private IList<Property> properties = new List<Property>();
        private string deprecatedNamespace;
        private bool isDeprecated;

        public bool IsXmlPrimitive
        {
            get => isXmlPrimitive;
            set => SetValue(ref isXmlPrimitive, value);
        }

        public string Path
        {
            get => path;
            set => SetValue(ref path, value);
        }

        public string Name
        {
            get => name;
            set => SetValue(ref name, value);
        }

        public string Description
        {
            get => description;
            set => SetValue(ref description, value);
        }

        public IList<AdditionalText> AdditionalText => additionalText;

        public string ExtendsTypeName
        {
            get => extendsTypeName;
            set => SetValue(ref extendsTypeName, value);
        }

        public IList<DataType> ParentTypes => parentTypes;
        public IList<DataType> ChildTypes => childTypes;

        public bool IsSubstitute
        {
            get => isSubstitute;
            set => SetValue(ref isSubstitute, value);
        }

        public IList<Relationship> Relationships => relationships;

        public bool IsAbstract
        {
            get => isAbstract;
            set => SetValue(ref isAbstract, value);
        }

        public bool IsPrimitive
        {
            get => isPrimitive;
            set => SetValue(ref isPrimitive, value);
        }

        public IList<Property> Properties
        {
            get => properties;
            set
            {
                ThrowIfReadOnly(nameof(Properties));
                properties = value ?? new List<Property>();
            }
        }

        public string DeprecatedNamespace
        {
            get => deprecatedNamespace;
            set => SetValue(ref deprecatedNamespace, value);
        }

        public bool IsDeprecated
        {
            get => isDeprecated;
            set => SetValue(ref isDeprecated, value);
        }

        protected sealed override void MakeReadOnlyCore()
        {
            foreach (AdditionalText text in additionalText)
            {
                text?.MakeReadOnly();
            }
            foreach (Property property in properties)
            {
                property?.MakeReadOnly();
            }
            foreach (DataType parent in parentTypes)
            {
                parent?.MakeReadOnly();
            }
            foreach (DataType child in childTypes)
            {
                child?.MakeReadOnly();
            }
            foreach (Relationship relationship in relationships)
            {
                relationship?.MakeReadOnly();
            }

            additionalText = ReadOnlyCopy(additionalText);
            parentTypes = ReadOnlyCopy(parentTypes);
            childTypes = ReadOnlyCopy(childTypes);
            relationships = ReadOnlyCopy(relationships);
            properties = ReadOnlyCopy(properties);
        }
    }
}
