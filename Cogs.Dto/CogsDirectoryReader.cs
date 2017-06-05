// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using Cogs.Dto;
using CsvHelper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cogs.Dto
{
    public class CogsDirectoryReader
    {
        public CogsDtoModel Load(string directory)
        {
            var model = new CogsDtoModel();

            // Load all item types from the ItemTypes directory.
            LoadDataTypes(directory, "ItemTypes", model, model.ItemTypes);

            // Load all reusable types from the ReusableTypes directory.
            LoadDataTypes(directory, "ReusableTypes", model, model.ReusableDataTypes);

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

                // TODO IsAbstract

                // Read the properties
                string csvStr = File.ReadAllText(propertiesFileName);
                using (var textReader = new StringReader(csvStr))
                {
                    var csvReader = new CsvReader(textReader);
                    var records = csvReader.GetRecords<Property>().ToList();
                    itemType.Properties = records;
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
                .FirstOrDefault(x => x.StartsWith("Extends."));
            if (!string.IsNullOrWhiteSpace(extendsFileName))
            {
                return extendsFileName.Substring(8);
            }

            return string.Empty;
        }
    }
}
