// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using System.Collections.Generic;
using System.Linq;

namespace Cogs.Model
{
    public class CogsModel : CogsModelNode
    {
        private string sourceDirectory;
        private IList<ItemType> itemTypes = new List<ItemType>();
        private IList<DataType> reusableDataTypes = new List<DataType>();
        private IList<TopicIndex> topicIndices = new List<TopicIndex>();
        private IList<Property> identification = new List<Property>();
        private Settings settings;
        private string headerInclude;
        private string articlesPath;
        private IList<string> articleTocEntries = new List<string>();

        public string SourceDirectory
        {
            get => sourceDirectory;
            set => SetValue(ref sourceDirectory, value);
        }

        public IList<ItemType> ItemTypes => itemTypes;
        public IList<DataType> ReusableDataTypes => reusableDataTypes;
        public IList<TopicIndex> TopicIndices => topicIndices;
        public IList<Property> Identification => identification;

        public Settings Settings
        {
            get => settings;
            set => SetValue(ref settings, value);
        }

        public string HeaderInclude
        {
            get => headerInclude;
            set => SetValue(ref headerInclude, value);
        }

        public string ArticlesPath
        {
            get => articlesPath;
            set => SetValue(ref articlesPath, value);
        }

        public IList<string> ArticleTocEntries => articleTocEntries;

        public IEnumerable<DataType> AllDataTypes => ItemTypes.Cast<DataType>().Concat(ReusableDataTypes);

        protected sealed override void MakeReadOnlyCore()
        {
            settings?.MakeReadOnly();
            foreach (Property property in identification)
            {
                property?.MakeReadOnly();
            }
            foreach (DataType dataType in AllDataTypes.ToArray())
            {
                dataType?.MakeReadOnly();
            }
            foreach (TopicIndex topic in topicIndices)
            {
                topic?.MakeReadOnly();
            }

            itemTypes = ReadOnlyCopy(itemTypes);
            reusableDataTypes = ReadOnlyCopy(reusableDataTypes);
            topicIndices = ReadOnlyCopy(topicIndices);
            identification = ReadOnlyCopy(identification);
            articleTocEntries = ReadOnlyCopy(articleTocEntries);
        }
    }
}
