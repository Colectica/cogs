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

            errors = CheckDuplicatePropertiesInSameItem(model, errors);
            errors = CheckReusedPropertyNamesShouldHaveSameDatatype(model, errors);

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
                        errors.Add(new CogsError(ErrorLevel.Error, $"Duplicate property name found in {item.Name} named {group.Key}"));
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
                    var locations = typeGroupings.Select(x => $"Datatype {x.Key} in {x.Select(y => y.item.Name).Aggregate((i,j) => i + ", " + j)}").Aggregate((i, j) => i + Environment.NewLine + j);
                    errors.Add(new CogsError(ErrorLevel.Error, $"Property name {useage.Key} has different datatypes. Property names may be reused only if the same datatype is used. {Environment.NewLine}{locations}"));
                }
            }

            return errors;
        }
    }
}
