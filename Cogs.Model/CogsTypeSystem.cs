using System;
using System.Collections.Generic;
using System.Linq;

namespace Cogs.Model
{
    /// <summary>Shared inheritance and effective-property semantics for all publishers.</summary>
    public static class CogsTypeSystem
    {
        public static bool AllowsSubtypes(Property property) =>
            property?.AllowSubtypes == true || property?.DataType?.IsAbstract == true;

        public static bool IsAssignableFrom(DataType expected, DataType actual)
        {
            if (expected == null || actual == null) return false;
            if (ReferenceEquals(expected, actual) || string.Equals(expected.Name, actual.Name, StringComparison.Ordinal)) return true;
            return actual.ParentTypes.Any(parent => ReferenceEquals(parent, expected) || string.Equals(parent.Name, expected.Name, StringComparison.Ordinal));
        }

        public static IReadOnlyList<DataType> ConcreteAssignableTypes(CogsModel model, DataType declared)
        {
            if (model == null || declared == null) return Array.Empty<DataType>();
            return model.AllDataTypes
                .Where(candidate => !candidate.IsAbstract && IsAssignableFrom(declared, candidate))
                .OrderBy(candidate => candidate.Name, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Returns the concrete types permitted at a property. A false subtype flag
        /// means the exact declared type; abstract declarations are always treated
        /// as subtype-enabled because they cannot be instantiated directly.
        /// </summary>
        public static IReadOnlyList<DataType> ConcreteTypesForProperty(CogsModel model, Property property)
        {
            if (model == null || property?.DataType == null || property.DataType.IsXmlPrimitive)
            {
                return Array.Empty<DataType>();
            }

            DataType declared = property.DataType;
            if (AllowsSubtypes(property))
            {
                return ConcreteAssignableTypes(model, declared);
            }

            return new[] { declared };
        }

        public static IReadOnlyList<Property> EffectiveProperties(DataType type)
        {
            if (type == null) return Array.Empty<Property>();
            return type.ParentTypes
                .SelectMany(parent => parent.Properties)
                .Concat(type.Properties)
                .ToArray();
        }
    }
}
