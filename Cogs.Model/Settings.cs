using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Cogs.Model
{
    public class Settings : CogsModelNode
    {
        private string cogsVersion;
        private string title;
        private string shortTitle;
        private string slug;
        private string description;
        private string version;
        private string author;
        private string copyright;
        private string namespaceUrl;
        private string namespacePrefix;
        private string cSharpNamespace;
        private IDictionary<string, string> extraSettings = new Dictionary<string, string>();

        public string CogsVersion
        {
            get => cogsVersion;
            set => SetValue(ref cogsVersion, value);
        }

        public string Title
        {
            get => title;
            set => SetValue(ref title, value);
        }

        public string ShortTitle
        {
            get => shortTitle;
            set => SetValue(ref shortTitle, value);
        }

        public string Slug
        {
            get => slug;
            set => SetValue(ref slug, value);
        }

        public string Description
        {
            get => description;
            set => SetValue(ref description, value);
        }

        public string Version
        {
            get => version;
            set => SetValue(ref version, value);
        }

        public string Author
        {
            get => author;
            set => SetValue(ref author, value);
        }

        public string Copyright
        {
            get => copyright;
            set => SetValue(ref copyright, value);
        }

        public string NamespaceUrl
        {
            get => namespaceUrl;
            set => SetValue(ref namespaceUrl, value);
        }

        public string NamespacePrefix
        {
            get => namespacePrefix;
            set => SetValue(ref namespacePrefix, value);
        }

        public string CSharpNamespace
        {
            get => cSharpNamespace;
            set => SetValue(ref cSharpNamespace, value);
        }

        public IDictionary<string, string> ExtraSettings => extraSettings;

        protected sealed override void MakeReadOnlyCore()
        {
            extraSettings = new ReadOnlyDictionary<string, string>(
                extraSettings.ToDictionary(pair => pair.Key, pair => pair.Value));
        }
    }
}
