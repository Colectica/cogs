using System;

namespace Cogs.Dto
{
    public class AdditionalText
    {
        private string filePath;
        private string format;
        private string name;
        private string content;

        public string FilePath
        {
            get => filePath;
            set => SetValue(ref filePath, value);
        }

        public string Format
        {
            get => format;
            set => SetValue(ref format, value);
        }

        public string Name
        {
            get => name;
            set => SetValue(ref name, value);
        }

        public string Content
        {
            get => content;
            set => SetValue(ref content, value);
        }

        /// <summary>Gets whether this documentation value is part of a completed model.</summary>
        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// Makes this value read-only. Model builders call this on a private copy so the
        /// source DTO remains mutable.
        /// </summary>
        public void MakeReadOnly() => IsReadOnly = true;

        private void SetValue(ref string storage, string value)
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("The completed COGS model is read-only.");
            }
            storage = value;
        }
    }
}
