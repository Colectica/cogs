using Cogs.Common;
using Cogs.Model;
using Cogs.SimpleTypes;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Cogs.Publishers
{
    public class DcTapPublisher
    {
        public required string TargetDirectory { get; set; }
        public bool Overwrite { get; set; }

        public required CogsModel CogsModel { get; set; }

        private HashSet<string> LowerCaseSimpleTypes { get; set; } = new HashSet<string>();
        private string NamespacePrefix { get; set; } = ":";
        public void Publish()
        {
            LowerCaseSimpleTypes = CogsTypes.SimpleTypeNames.Select(x => x.ToLower()).ToHashSet();

            if (!string.IsNullOrWhiteSpace(CogsModel.Settings.NamespacePrefix))
            {
                NamespacePrefix = CogsModel.Settings.NamespacePrefix + ":";
            }

            //if (CogsLocation == null)
            //{
            //    throw new InvalidOperationException("Cogs location must be specified");
            //}
            if (TargetDirectory == null)
            {
                throw new InvalidOperationException("Target directory must be specified");
            }

            if (Overwrite && Directory.Exists(TargetDirectory))
            {
                Directory.Delete(TargetDirectory, true);
            }

            Directory.CreateDirectory(TargetDirectory);

            var entries = new List<DcTapEntry>();
            var lastShapeId = string.Empty;

            // create all the datatype shapes
            foreach (var item in CogsModel.ReusableDataTypes)
            {
                if (item.IsAbstract) { continue; } // DCTAP doesn't do inheritance

                var currentEntries = new List<DcTapEntry>();
                // Add an entry for the shape definition line

                var entry = new DcTapEntry();
                entry.ShapeId = item.Name;
                entry.ShapeLabel = GetLabel(item.Name);
                entry.Note = item.Description;
                currentEntries.Add(entry);

                foreach (var parent in item.ParentTypes)
                {
                    var parentEntries = GetPropertyEntries(parent);
                    currentEntries.AddRange(parentEntries);
                }
                var itemEntries = GetPropertyEntries(item);
                currentEntries.AddRange(itemEntries);

                entries.AddRange(currentEntries);

                // it seems a blank line between shapes is a convention
                entries.Add(new DcTapEntry());
            }

            // create all the item type shapes
            foreach(var item in CogsModel.ItemTypes)
            {
                if (item.IsAbstract) { continue; } // DCTAP doesn't do inheritance

                var currentEntries = new List<DcTapEntry>();
                // Add an entry for the shape definition line

                var entry = new DcTapEntry();
                entry.ShapeId = item.Name;
                entry.ShapeLabel = GetLabel(item.Name);
                entry.Note = item.Description;
                currentEntries.Add(entry);

                foreach (var parent in item.ParentTypes)
                {
                    var parentEntries = GetPropertyEntries(parent);
                    currentEntries.AddRange(parentEntries);
                }
                var itemEntries = GetPropertyEntries(item);
                currentEntries.AddRange(itemEntries);

                entries.AddRange(currentEntries);
                // it seems a blank line between shapes is a convention
                entries.Add(new DcTapEntry());
            }


            // remove the final blank line
            if (entries.Count > 0)
            {
                entries.RemoveAt(entries.Count - 1);
            }

            // write out the cdtap profile as a csv
            var fileName = Path.Combine(TargetDirectory, "dctap.csv");
            using (var writer = new StreamWriter(fileName))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(entries);
            }
        }

        private string GetLabel(string name)
        {
            return string.Join(" ", SplitCamelCase(name));
        }
        public IEnumerable<string> SplitCamelCase(string source)
        {
            if (source == "ID" || source == "URN") { yield return source; yield break; }//TODO don't break apart acronyms

            const string pattern = @"[A-Z][a-z]*|[a-z]+|\d+";
            var matches = Regex.Matches(source, pattern);
            foreach (Match match in matches)
            {
                yield return match.Value;
            }
        }
        public List<DcTapEntry> GetPropertyEntries(DataType dataType)
        {
            var results = new List<DcTapEntry>();
            foreach (var property in dataType.Properties)
            {
                var entry = new DcTapEntry();
                entry.PropertyId = property.Name;
                entry.PropertyLabel = GetLabel(property.Name);

                // Change embedded dcterms to dublin core predicates
                if (property.Name.StartsWith("DublinCore"))
                {
                    var term = property.Name.Remove(0, 10);
                    term = term[0].ToString().ToLower() + term.Substring(1);
                    entry.PropertyId = "dcterms:" + term;
                }

                if(property.MinCardinality != "0")
                {
                    entry.Mandatory = true;
                }
                else
                {
                    entry.Mandatory = false;
                }
                if (property.MaxCardinality == "n")
                {
                    entry.Repeatable = true;
                }
                else
                {
                    entry.Repeatable = false;
                }
                if (LowerCaseSimpleTypes.Contains(property.DataTypeName.ToLower()))
                {
                    entry.ValueNodeType = "Literal";
                    entry.ValueDataType = GetValueDataType(property.DataTypeName);
                }
                else
                {
                    if (CogsModel.ReusableDataTypes.Contains(property.DataType))
                    {
                        entry.ValueNodeType = "IRI BNODE";
                    }
                    else
                    {
                        entry.ValueNodeType = "IRI";
                    }

                    if (property.AllowSubtypes)
                    {
                        var subclasses = property.DataType.ChildTypes.Where(x => x.IsAbstract = false).ToList();
                        if (! property.DataType.IsAbstract)
                        {
                            subclasses.Add(property.DataType);
                        }
                        entry.ValueShape = string.Join(" ", subclasses.Select(x => x.Name));
                    }
                    else
                    {
                        entry.ValueShape = property.DataTypeName;
                    }
                }
                if (!string.IsNullOrWhiteSpace(property.Pattern))
                {
                    entry.ValueConstraint = property.Pattern;
                    entry.ValueConstraintType = "pattern";
                }
                if (property.Enumeration.Count > 0)
                {
                    entry.ValueConstraint = string.Join(" ", property.Enumeration);
                    entry.ValueConstraintType = "picklist";
                }

                entry.Note = property.Description;

                results.Add(entry);
            }
            return results;
        }

        private string GetValueDataType(string cogsType)
        {
            var lower = cogsType.ToLower();
            if(lower == "cogsdate")
            {
                return "xsd:date xsd:dateTime xsd:duration xsd:gYear xsd:gYearMonth";
            }
            else if(lower == "langString")
            {
                return "rdf:langString";
            }
            else if(lower == "dcterms")
            {

            }
            return "xsd:" + cogsType;
            //TODO implement
        }
    }

    

    public class DcTapEntry
    {

        [Name("shapeID")]
        public string? ShapeId {  get; set; }

        [Name("shapeLabel")] 
        public string? ShapeLabel { get; set; }

        [Name("propertyID")]
        public string? PropertyId {  get; set; }

        [Name("propertyLabel")]
        public string? PropertyLabel { get; set; }

        [BooleanTrueValues("TRUE")]
        [BooleanFalseValues("FALSE")]
        [Name("mandatory")] 
        public bool? Mandatory { get; set; }

        [BooleanTrueValues("TRUE")]
        [BooleanFalseValues("FALSE")]
        [Name("repeatable")]
        public bool? Repeatable { get; set; }

        [Name("valueNodeType")]
        public string? ValueNodeType { get;set; }

        [Name("valueDataType")]
        public string? ValueDataType { get; set; }

        [Name("valueShape")]
        public string? ValueShape {  get; set; }

        [Name("valueConstraint")]
        public string? ValueConstraint { get; set; }

        [Name("valueConstraintType")]
        public string? ValueConstraintType { get; set; }

        [Name("note")]
        public string? Note { get; set; }

    }
}
