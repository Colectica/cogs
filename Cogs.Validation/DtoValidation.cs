using Cogs.Common;
using Cogs.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Cogs.Validation
{
    public class DtoValidation
    {

        public static List<CogsError> Validate(CogsDtoModel model)
        {
            List<CogsError> errors = new List<CogsError>();

            errors = CheckSettingsSlugToEnsureNoSpaces(model, errors);

            errors = CheckDataTypesMustBeDefined(model, errors);
            errors = CheckDataTypeNamesShouldMatchCase(model, errors);
            errors = CheckDataTypeNamesShouldNotConflictWithBuiltins(model, errors);
            errors = CheckDataTypeNamesShouldBePascalCase(model, errors);
            
            errors = CheckDuplicatePropertiesInSameItem(model, errors);
            errors = CheckReusedPropertyNamesShouldHaveSameDatatype(model, errors);
            errors = CheckPropertyNamesShouldBePascalCase(model, errors);
            errors = CheckAbstractDataTypePropertiesMustAllowSubtypes(model, errors);
            errors = CheckDerivationOfPrimativeTypesNotAllowed(model, errors);

            errors = CheckOrderedCollectionsMustHaveCardinalityGreaterThanOne(model, errors);

            errors = NamingDataTypeReferenceTypeNotAllowed(model, errors);
            errors = NamingPropertyTopLevelReferenceNotAllowed(model, errors);
            

            return errors;
        }
        public static List<CogsError> CheckOrderedCollectionsMustHaveCardinalityGreaterThanOne(CogsDtoModel model, List<CogsError> errors = null)
        {
            errors = errors ?? new List<CogsError>();
            
            foreach (var item in model.ItemTypes.Union(model.ReusableDataTypes))
            {
                foreach (var property in item.Properties)
                {                    
                    var ordered = !string.IsNullOrWhiteSpace(property.Ordered);
                    var cardinalityNotOverOne = property.MaxCardinality == "0" || property.MaxCardinality == "1";
                    if (ordered && cardinalityNotOverOne)
                    {
                        errors.Add(new CogsError(ErrorLevel.Error, $"Ordered property constraint: Property '{property.Name}' in '{item.Name}' has max cardinality '{property.MaxCardinality}', which is not 2 or greater"));
                    }
                    
                }
            }
            return errors;
        }

        public static List<CogsError> CheckAbstractDataTypePropertiesMustAllowSubtypes(CogsDtoModel model, List<CogsError> errors = null)
        {
            errors = errors ?? new List<CogsError>();

            var typeNames = model.ItemTypes.Union(model.ReusableDataTypes).ToDictionary(x => x.Name);

            foreach (var item in model.ItemTypes.Union(model.ReusableDataTypes))
            {
                foreach (var property in item.Properties)
                {
                    DataType dataType = null;
                    if(typeNames.TryGetValue(property.DataType, out dataType))
                    {
                        if(dataType.IsAbstract && string.IsNullOrWhiteSpace(property.AllowSubtypes))
                        {
                            errors.Add(new CogsError(ErrorLevel.Error, $"Abstract needs to allow subtypes: Property '{property.Name}' in '{item.Name}' uses datatype '{property.DataType}', which is abstract and does not allow subtypes"));
                        }
                    }
                }
            }
            return errors;
        }


        public static List<CogsError> CheckDuplicatePropertiesInSameItem(CogsDtoModel model, List<CogsError> errors = null)
        {
            errors = errors ?? new List<CogsError>();

            foreach(var item in model.ItemTypes.Union(model.ReusableDataTypes))
            {
                var groupings = item.Properties.GroupBy(x => x.Name).ToList();
                foreach(var group in groupings)
                {
                    if(group.Count() > 1)
                    {
                        errors.Add(new CogsError(ErrorLevel.Error, $"Duplicate property: in '{item.Name}' named '{group.Key}'"));
                    }
                }
            }
            return errors;
        }

        public static List<CogsError> CheckReusedPropertyNamesShouldHaveSameDatatype(CogsDtoModel model, List<CogsError> errors = null)
        {
            errors = errors ?? new List<CogsError>();


            List<(DataType item, string Property, string DataType)> uses = new List<(DataType item, string Property, string DataType)>();
            foreach (var item in model.ItemTypes.Union(model.ReusableDataTypes))
            {
                foreach(var property in item.Properties)
                {
                    uses.Add((item, property.Name, property.DataType));
                }
            }

            var usageGroups = uses.GroupBy(x => x.Property);
            foreach(var useage in usageGroups)
            {
                var typeGroupings = useage.GroupBy(x => x.DataType).ToList();
                if (typeGroupings.Count() > 1)
                {
                    var locations = typeGroupings.Select(x => $"Datatype '{x.Key}' in {x.Select(y => y.item.Name).Aggregate((i,j) => i + ", " + j)}").Aggregate((i, j) => i + Environment.NewLine + j);
                    errors.Add(new CogsError(ErrorLevel.Error, $"Property name '{useage.Key}' has different datatypes. Property names may be reused only if the same datatype is used. {Environment.NewLine}{locations}"));
                }
            }

            return errors;
        }

        public static List<CogsError> CheckDataTypesMustBeDefined(CogsDtoModel model, List<CogsError> errors = null)
        {
            errors = errors ?? new List<CogsError>();

            List<string> typeNames = model.ItemTypes.Union(model.ReusableDataTypes).Select(x => x.Name).ToList();
            List<string> allTypeNames = typeNames.Union(CogsTypes.SimpleTypeNames).Union(CogsTypes.BuiltinTypeNames).ToList();
            
            foreach (var item in model.ItemTypes.Union(model.ReusableDataTypes))
            {
                foreach (var property in item.Properties)
                {
                    if (!allTypeNames.Contains(property.DataType) && !allTypeNames.Contains(property.DataType, StringComparer.OrdinalIgnoreCase))
                    {
                        errors.Add(new CogsError(ErrorLevel.Error, $"Undefined datatype: Property '{property.Name}' in '{item.Name}' uses datatype '{property.DataType}', which is undefined"));
                    }
                }
            }
            return errors;
        }

        public static List<CogsError> CheckDataTypeNamesShouldMatchCase(CogsDtoModel model, List<CogsError> errors = null)
        {
            errors = errors ?? new List<CogsError>();

            List<string> typeNames = model.ItemTypes.Union(model.ReusableDataTypes).Select(x => x.Name).ToList();
            List<string> allTypeNames = typeNames.Union(CogsTypes.SimpleTypeNames).Union(CogsTypes.BuiltinTypeNames).ToList();

            foreach (var item in model.ItemTypes.Union(model.ReusableDataTypes))
            {
                foreach (var property in item.Properties)
                {
                    if (!allTypeNames.Contains(property.DataType) && allTypeNames.Contains(property.DataType, StringComparer.OrdinalIgnoreCase))
                    {
                        errors.Add(new CogsError(ErrorLevel.Warning, $"Improper casing: Property '{property.Name}' in '{item.Name}' uses datatype '{property.DataType}', but should be '{allTypeNames.First(x => x.Equals(property.DataType, StringComparison.OrdinalIgnoreCase))}'"));
                    }
                }
            }
            return errors;
        }


        public static List<CogsError> CheckDataTypeNamesShouldNotConflictWithBuiltins(CogsDtoModel model, List<CogsError> errors = null)
        {
            errors = errors ?? new List<CogsError>();

            List<string> typeNames = model.ItemTypes.Union(model.ReusableDataTypes).Select(x => x.Name).ToList();
            List<string> cogsTypes = CogsTypes.SimpleTypeNames.Union(CogsTypes.BuiltinTypeNames).ToList();

            var conflicts = typeNames.Intersect(cogsTypes, StringComparer.OrdinalIgnoreCase);
            foreach (var conflict in conflicts)
            {
                errors.Add(new CogsError(ErrorLevel.Warning, $"Datatype name '{conflict}' conflicts with a built in type."));
            }
            return errors;
        }

        public static List<CogsError> CheckDataTypeNamesShouldBePascalCase(CogsDtoModel model, List<CogsError> errors = null)
        {
            errors = errors ?? new List<CogsError>();

            List<string> typeNames = model.ItemTypes.Union(model.ReusableDataTypes).Select(x => x.Name).ToList();
            
            foreach (var typeName in typeNames)
            {
                if (typeName.Length > 0 && char.IsLower(typeName[0]))
                {
                    errors.Add(new CogsError(ErrorLevel.Warning, $"Datatype name '{typeName}' should be PascalCase, and start with an upper case character."));
                }
            }
            return errors;
        }

        public static List<CogsError> CheckPropertyNamesShouldBePascalCase(CogsDtoModel model, List<CogsError> errors = null)
        {
            errors = errors ?? new List<CogsError>();
            
            foreach (var item in model.ItemTypes.Union(model.ReusableDataTypes))
            {
                foreach (var property in item.Properties)
                {
                    if (property.Name.Length > 0 && char.IsLower(property.Name[0]))
                    {
                        errors.Add(new CogsError(ErrorLevel.Warning, $"Property name '{property.Name}' in '{item.Name}' should be PascalCase, and start with an upper case character."));
                    }
                }
            }
            List<string> typeNames = model.ItemTypes.Union(model.ReusableDataTypes).Select(x => x.Name).ToList();

            foreach (var typeName in typeNames)
            {
                if (typeName.Length > 0 && char.IsLower(typeName[0]))
                {
                    errors.Add(new CogsError(ErrorLevel.Warning, $"Datatype name '{typeName}' should be PascalCase, and start with an upper case character."));
                }
            }
            return errors;
        }

        public static List<CogsError> CheckSettingsSlugToEnsureNoSpaces(CogsDtoModel model, List<CogsError> errors)
        {
            errors = errors ?? new List<CogsError>();

            // If a slug is set, it must not contain spaces.
            // TODO check for other characters that would be invalid in URLs, C#/Java namespaces, etc.
            var slugSetting = model.Settings.FirstOrDefault(x => x.Key == "Slug");
            if (slugSetting.Value.Contains(" "))
            {
                errors.Add(new CogsError(ErrorLevel.Error, "The slug cannot contain spaces"));
            }

            return errors;
        }

        public static List<CogsError> CheckDerivationOfPrimativeTypesNotAllowed(CogsDtoModel model, List<CogsError> errors)
        {
            errors = errors ?? new List<CogsError>();

            var primatives = CogsTypes.SimpleTypeNames.ToHashSet();

            foreach (var item in model.ItemTypes.Union(model.ReusableDataTypes))
            {
                if (primatives.Contains(item.Extends))
                {
                    errors.Add(new CogsError(ErrorLevel.Error, $"Datatype name '{item.Name}' can not extend primative type {item.Extends}."));
                }
            }


            return errors;
        }

        public static List<CogsError> NamingDataTypeReferenceTypeNotAllowed(CogsDtoModel model, List<CogsError> errors = null)
        {
            errors = errors ?? new List<CogsError>();

            List<string> typeNames = model.ItemTypes.Union(model.ReusableDataTypes).Select(x => x.Name).ToList();
            List<string> allTypeNames = typeNames.Union(CogsTypes.SimpleTypeNames).Union(CogsTypes.BuiltinTypeNames).ToList();

            foreach (var item in model.ItemTypes.Union(model.ReusableDataTypes))
            {
                if (string.Compare(item.Name, "ReferenceType", true) == 0)
                {
                    errors.Add(new CogsError(ErrorLevel.Warning, $"Naming: '{item.Name}' is a reserved datatype name."));
                }
            }
            return errors;
        }

        public static List<CogsError> NamingPropertyTopLevelReferenceNotAllowed(CogsDtoModel model, List<CogsError> errors = null)
        {
            errors = errors ?? new List<CogsError>();

            List<string> typeNames = model.ItemTypes.Union(model.ReusableDataTypes).Select(x => x.Name).ToList();
            List<string> allTypeNames = typeNames.Union(CogsTypes.SimpleTypeNames).Union(CogsTypes.BuiltinTypeNames).ToList();

            foreach (var item in model.ItemTypes.Union(model.ReusableDataTypes))
            {
                foreach(var property in item.Properties)
                {
                    if (string.Compare(property.Name, "TopLevelReference", true) == 0)
                    {
                        errors.Add(new CogsError(ErrorLevel.Warning, $"Naming: '{property.Name}' in '{item.Name}' is a reserved property name."));
                    }
                }

            }
            return errors;
        }

    }
}
