using Cogs.Common;
using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Cogs.Dto
{
    public class RewriteCsvFormat
    {

        public List<CogsError> Errors { get; } = new List<CogsError>();

        public void Rewrite(string directory)
        {
            // create built in identification and reference types
            var settingsDirectoryName = Path.Combine(directory, "Settings");
            string identificationFile = Path.Combine(settingsDirectoryName, "Identification.csv");

            string idCsv = File.ReadAllText(identificationFile, Encoding.UTF8);
            List<Property> rows = new List<Property>();
            using (var textReader = new StringReader(idCsv))
            {
                try
                {
                    var config = new CsvConfiguration(CultureInfo.InvariantCulture);
                    config.HeaderValidated = null;
                    config.MissingFieldFound = null;
                    var csvReader = new CsvReader(textReader, config);

                    var records = csvReader.GetRecords<Property>();
                    rows.AddRange(records);
                }
                catch (Exception e)
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, e.Message + " " + identificationFile, e));
                }
            }

            using (TextWriter textWriter = File.CreateText(identificationFile))
            {
                CsvWriter csvWriter = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
                csvWriter.WriteRecords(rows);
            }

            // settings
            string settingsFileName = Path.Combine(settingsDirectoryName, "Settings.csv");
            string settingsCsvStr = File.ReadAllText(settingsFileName, Encoding.UTF8);
            List<Setting> settings = new List<Setting>();
            using (var textReader = new StringReader(settingsCsvStr))
            {
                try
                {
                    var config = new CsvConfiguration(CultureInfo.InvariantCulture);
                    config.HeaderValidated = null;
                    config.MissingFieldFound = null;
                    var csvReader = new CsvReader(textReader, config);

                    var records = csvReader.GetRecords<Setting>();
                    settings.AddRange(records);
                }
                catch (Exception e)
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, e.Message + " " + identificationFile, e));
                }
            }

            using (TextWriter textWriter = File.CreateText(settingsFileName))
            {
                CsvWriter csvWriter = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
                csvWriter.WriteRecords(settings);
            }

            // item types from the ItemTypes directory.
            LoadDataTypes(directory, "ItemTypes");

            //reusable types from the ReusableTypes directory.
            LoadDataTypes(directory, "CompositeTypes");

        }

        private void LoadDataTypes(string directory, string subDirectory)
        {
            string itemTypesDirectory = Path.Combine(directory, subDirectory);
            foreach (string typeDir in Directory.GetDirectories(itemTypesDirectory))
            {
                string itemTypeName = Path.GetFileName(typeDir);
                string propertiesFileName = Path.Combine(typeDir, itemTypeName + ".csv");

                var rows = new List<Property>();

                // Read the properties
                if (File.Exists(propertiesFileName))
                {
                    string csvStr = File.ReadAllText(propertiesFileName, Encoding.UTF8);
                    using (var textReader = new StringReader(csvStr))
                    {
                        try
                        {
                            var config = new CsvConfiguration(CultureInfo.InvariantCulture);
                            config.HeaderValidated = null;
                            config.MissingFieldFound = null;
                            var csvReader = new CsvReader(textReader, config);

                            rows = csvReader.GetRecords<Property>().ToList();
                        }
                        catch (Exception e)
                        {
                            Errors.Add(new CogsError(ErrorLevel.Error, e.Message + " " + propertiesFileName, e));
                        }
                    }
                }
                else
                {
                    continue;
                }

                using (TextWriter textWriter = File.CreateText(propertiesFileName))
                {
                    CsvWriter csvWriter = new CsvWriter(textWriter, CultureInfo.InvariantCulture);
                    csvWriter.WriteRecords(rows);
                }

            }
        }
    }


}
