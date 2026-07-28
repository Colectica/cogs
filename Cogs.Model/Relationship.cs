// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.

namespace Cogs.Model
{
    public class Relationship : CogsModelNode
    {
        private string propertyName;
        private DataType targetItemType;

        public string PropertyName
        {
            get => propertyName;
            set => SetValue(ref propertyName, value);
        }

        public DataType TargetItemType
        {
            get => targetItemType;
            set => SetValue(ref targetItemType, value);
        }

        protected sealed override void MakeReadOnlyCore() => targetItemType?.MakeReadOnly();
    }
}
