// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using System.Collections.Generic;

namespace Cogs.Model
{
    public class Property : CogsModelNode
    {
        private string name;
        private string dataTypeName;
        private DataType dataType;
        private string minCardinality;
        private string maxCardinality;
        private string description;
        private string deprecatedNamespace;
        private string deprecatedElementOrAttribute;
        private string deprecatedChoiceGroup;
        private bool ordered;
        private bool allowSubtypes;
        private int? minLength;
        private int? maxLength;
        private IList<string> enumeration = new List<string>();
        private string pattern;
        private string minInclusive;
        private string minExclusive;
        private string maxInclusive;
        private string maxExclusive;
        private bool fromMixin;

        public string Name
        {
            get => name;
            set => SetValue(ref name, value);
        }

        public string DataTypeName
        {
            get => dataTypeName;
            set => SetValue(ref dataTypeName, value);
        }

        public DataType DataType
        {
            get => dataType;
            set => SetValue(ref dataType, value);
        }

        public string MinCardinality
        {
            get => minCardinality;
            set => SetValue(ref minCardinality, value);
        }

        public string MaxCardinality
        {
            get => maxCardinality;
            set => SetValue(ref maxCardinality, value);
        }

        public string Description
        {
            get => description;
            set => SetValue(ref description, value);
        }

        public string DeprecatedNamespace
        {
            get => deprecatedNamespace;
            set => SetValue(ref deprecatedNamespace, value);
        }

        public string DeprecatedElementOrAttribute
        {
            get => deprecatedElementOrAttribute;
            set => SetValue(ref deprecatedElementOrAttribute, value);
        }

        public string DeprecatedChoiceGroup
        {
            get => deprecatedChoiceGroup;
            set => SetValue(ref deprecatedChoiceGroup, value);
        }

        public bool IsPrimitive => DataType == null || DataType.IsPrimitive;

        public bool Ordered
        {
            get => ordered;
            set => SetValue(ref ordered, value);
        }

        public bool AllowSubtypes
        {
            get => allowSubtypes;
            set => SetValue(ref allowSubtypes, value);
        }

        public int? MinLength
        {
            get => minLength;
            set => SetValue(ref minLength, value);
        }

        public int? MaxLength
        {
            get => maxLength;
            set => SetValue(ref maxLength, value);
        }

        public IList<string> Enumeration
        {
            get => enumeration;
            set
            {
                ThrowIfReadOnly(nameof(Enumeration));
                enumeration = value ?? new List<string>();
            }
        }

        public string Pattern
        {
            get => pattern;
            set => SetValue(ref pattern, value);
        }

        public string MinInclusive
        {
            get => minInclusive;
            set => SetValue(ref minInclusive, value);
        }

        public string MinExclusive
        {
            get => minExclusive;
            set => SetValue(ref minExclusive, value);
        }

        public string MaxInclusive
        {
            get => maxInclusive;
            set => SetValue(ref maxInclusive, value);
        }

        public string MaxExclusive
        {
            get => maxExclusive;
            set => SetValue(ref maxExclusive, value);
        }

        public bool FromMixin
        {
            get => fromMixin;
            set => SetValue(ref fromMixin, value);
        }

        protected sealed override void MakeReadOnlyCore()
        {
            dataType?.MakeReadOnly();
            enumeration = ReadOnlyCopy(enumeration);
        }

        public override string ToString() =>
            $"{Name} - {DataType} - {MinCardinality}..{MaxCardinality}";
    }
}
