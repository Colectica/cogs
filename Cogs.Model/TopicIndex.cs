// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using System.Collections.Generic;

namespace Cogs.Model
{
    public class TopicIndex : CogsModelNode
    {
        private string name;
        private string description;
        private IList<string> itemTypeNames = new List<string>();
        private IList<DataType> itemTypes = new List<DataType>();
        private string articlesPath;
        private IList<string> articleTocEntries = new List<string>();

        public string Name
        {
            get => name;
            set => SetValue(ref name, value);
        }

        public string Description
        {
            get => description;
            set => SetValue(ref description, value);
        }

        public IList<string> ItemTypeNames => itemTypeNames;
        public IList<DataType> ItemTypes => itemTypes;

        public string ArticlesPath
        {
            get => articlesPath;
            set => SetValue(ref articlesPath, value);
        }

        public IList<string> ArticleTocEntries => articleTocEntries;

        protected sealed override void MakeReadOnlyCore()
        {
            foreach (DataType itemType in itemTypes)
            {
                itemType?.MakeReadOnly();
            }

            itemTypeNames = ReadOnlyCopy(itemTypeNames);
            itemTypes = ReadOnlyCopy(itemTypes);
            articleTocEntries = ReadOnlyCopy(articleTocEntries);
        }
    }
}
