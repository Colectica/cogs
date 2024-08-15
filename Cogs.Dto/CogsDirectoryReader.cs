// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Common;
using Cogs.Dto;
using CsvHelper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Cogs.Dto
{
    public class CogsDirectoryReader
    {
        public string SettingsDirectoryName { get; set; }
        public List<CogsError> Errors { get; } = new List<CogsError>();

        private List<Property> dcTerms;

        public CogsDtoModel Load(string directory)
        {
            var model = new CogsDtoModel();


            // Add mixins (Dublin Core Terms)
            using (Stream dcStream = this.GetType().GetTypeInfo().Assembly.GetManifestResourceStream("Cogs.Dto.DcTerms.csv"))
            using (StreamReader dcReader = new StreamReader(dcStream))
            {
                try
                {
                    var csvReader = new CsvReader(dcReader, CultureInfo.InvariantCulture);
                    dcTerms = csvReader.GetRecords<Property>().ToList();
                }
                catch (Exception e)
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, e.Message + " on internal dc terms file ", e));
                }
            }
            
            // create built in identification and reference types
            SettingsDirectoryName = Path.Combine(directory, "Settings");
            string identificationFile = Path.Combine(SettingsDirectoryName, "Identification.csv");

            if (!File.Exists(identificationFile))
            {
                Errors.Add(new CogsError(ErrorLevel.Error, "identification information is not present in the Settings directory."));
                return model;
            }


            string idCsvStr = File.ReadAllText(identificationFile, Encoding.UTF8);
            using (var textReader = new StringReader(idCsvStr))
            {
                try
                {
                    var csvReader = new CsvReader(textReader, CultureInfo.InvariantCulture);
                    var records = csvReader.GetRecords<Property>();
                    model.Identification.AddRange(records);
                }
                catch(Exception e)
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, e.Message + " " + identificationFile, e));
                    return model;
                }
            }

            // Load settings
            string settingsFileName = Path.Combine(SettingsDirectoryName, "Settings.csv");
            string settingsCsvStr = File.ReadAllText(settingsFileName, Encoding.UTF8);
            using (var textReader = new StringReader(settingsCsvStr))
            {
                try
                {
                    var csvReader = new CsvReader(textReader, CultureInfo.InvariantCulture);
                    var records = csvReader.GetRecords<Setting>();
                    model.Settings.AddRange(records);
                }
                catch(Exception e)
                {
                    Errors.Add(new CogsError(ErrorLevel.Error, e.Message + " " + identificationFile, e));
                    return model;
                }
            }

            // Read the HeaderInclude.txt file.
            string headerIncludeFileName = Path.Combine(SettingsDirectoryName, "HeaderInclude.txt");
            if (File.Exists(headerIncludeFileName))
            {
                model.HeaderInclude = File.ReadAllText(headerIncludeFileName);
            }


            // Load information about articles.
            string articlesPath = Path.Combine(directory, "Articles");
            if (Directory.Exists(articlesPath))
            {
                model.ArticlesPath = articlesPath;
                string articlesIndexFileName = Path.Combine(articlesPath, "toc.txt");
                model.ArticleTocEntries.AddRange(File.ReadAllLines(articlesIndexFileName));
            }

            // Load all item types from the ItemTypes directory.
            LoadDataTypes(directory, "ItemTypes", model, model.ItemTypes);

            // Load all reusable types from the ReusableTypes directory.
            LoadDataTypes(directory, "CompositeTypes", model, model.ReusableDataTypes);

            // Load all topics from the Topics directory.
            string topicsDirectory = Path.Combine(directory, "Topics");
            string topicsListFile = Path.Combine(topicsDirectory, "index.txt");
            string[] viewDirectoryNames = File.ReadAllLines(topicsListFile);
            foreach (string viewDirectoryName in viewDirectoryNames)
            {
                string viewDirectory = Path.Combine(topicsDirectory, viewDirectoryName);

                var view = new TopicIndex();
                view.Name = Path.GetFileName(viewDirectory);

                string viewReadmeFileName = Path.Combine(viewDirectory, "readme.markdown");
                if (File.Exists(viewReadmeFileName))
                {
                    view.Description = File.ReadAllText(viewReadmeFileName);
                }

                string itemsFileName = Path.Combine(viewDirectory, "items.txt");
                string[] itemTypeNames = File.ReadAllLines(itemsFileName);
                foreach (string name in itemTypeNames)
                {
                    view.ItemTypes.Add(name);
                }

                string articlesIndexFileName = Path.Combine(viewDirectory, "toc.txt");
                if (File.Exists(articlesIndexFileName))
                {
                    view.ArticlesPath = Path.Combine(viewDirectory, "Articles");
                    view.ArticleTocEntries.AddRange(File.ReadAllLines(articlesIndexFileName));
                }

                model.TopicIndices.Add(view);
            }         


            return model;
        }

        private void LoadDataTypes(string directory, string subDirectory, CogsDtoModel model, IList list)
        {
            string itemTypesDirectory = Path.Combine(directory, subDirectory);
            foreach (string typeDir in Directory.GetDirectories(itemTypesDirectory))
            {
                string itemTypeName = Path.GetFileName(typeDir);
                string readmePath = Path.Combine(typeDir, "readme.markdown");
                string propertiesFileName = Path.Combine(typeDir, itemTypeName + ".csv");

                var itemType = new ItemType();
                list.Add(itemType);

                itemType.Name = itemTypeName;
                itemType.Description = GetDescription(readmePath);
                itemType.Extends = GetExtendsClass(typeDir);
                
                string abstractFileName = Path.Combine(typeDir, "Abstract");
                if (File.Exists(abstractFileName))
                {
                    itemType.IsAbstract = true;
                }

                string primitiveFileName = Path.Combine(typeDir, "Primitive");
                if (File.Exists(primitiveFileName))
                {
                    itemType.IsPrimitive = true;
                }

                // Read the properties
                if (File.Exists(propertiesFileName))
                {
                    string csvStr = File.ReadAllText(propertiesFileName, Encoding.UTF8);
                    using (var textReader = new StringReader(csvStr))
                    {
                        try
                        {
                            var csvReader = new CsvReader(textReader, CultureInfo.InvariantCulture);
                            var records = csvReader.GetRecords<Property>().ToList();
                            itemType.Properties = records;

                            // process mixins
                            for(int i = 0; i < records.Count; ++i)
                            {
                                if (string.Compare(records[i].Name, "DcTerms", true) == 0)
                                {
                                    records.RemoveAt(i);
                                    records.InsertRange(i, dcTerms);

                                }
                            }

                        }
                        catch (Exception e)
                        {
                            Errors.Add(new CogsError(ErrorLevel.Error, e.Message + " " + propertiesFileName, e));
                        }
                    }
                }

                foreach(var markdown in Directory.EnumerateFiles(typeDir, "*.markdown"))
                {
                    string markdownContent = File.ReadAllText(markdown);
                    string markdownName = Path.GetFileName(markdown).Replace(".markdown","");

                    if(string.IsNullOrWhiteSpace(markdownContent) || string.IsNullOrWhiteSpace(markdownName))
                    {
                        continue;
                    }
                    if(markdownName == "readme") { continue; }

                    var additional = new AdditionalText()
                    {
                        FilePath = markdown,
                        Content = markdownContent,
                        Format = "markdown",
                        Name = markdownName
                    };
                    itemType.AdditionalText.Add(additional);
                }

                // TODO DeprecatedNamespace
                // TODO IsDeprecated
            }
        }

        private string GetDescription(string readmePath)
        {
            if (!File.Exists(readmePath))
            {
                return string.Empty;
            }

            return File.ReadAllText(readmePath);
        }

        private string GetExtendsClass(string typeDir)
        {
            string[] allFiles = Directory.GetFiles(typeDir);
            string extendsFileName = allFiles
                .Select(x => Path.GetFileName(x))
                .FirstOrDefault(x => x.ToLower().StartsWith("extends."));
            if (!string.IsNullOrWhiteSpace(extendsFileName))
            {
                return extendsFileName.Substring(8);
            }

            return string.Empty;
        }
    }
}
