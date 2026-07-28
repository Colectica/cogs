// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using System;

namespace Cogs.Common
{
    public class CogsError
    {
        public ErrorLevel Level { get; set; }
        /// <summary>A stable, machine-readable diagnostic identifier.</summary>
        public string Code { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
        public string SourcePath { get; set; }
        public int? Line { get; set; }
        public int? Column { get; set; }
        public string ModelPath { get; set; }

        public CogsError(
            ErrorLevel level,
            string code,
            string message,
            string sourcePath = null,
            int? line = null,
            int? column = null,
            string modelPath = null,
            Exception exception = null)
        {
            Level = level;
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("A stable diagnostic code is required.", nameof(code));
            }

            Code = code;
            Message = message;
            SourcePath = sourcePath;
            Line = line;
            Column = column;
            ModelPath = modelPath;
            Exception = exception;
        }

        public override string ToString()
        {
            var location = string.IsNullOrWhiteSpace(SourcePath) ? string.Empty : SourcePath;
            if (Line.HasValue)
            {
                location += $"({Line.Value}{(Column.HasValue ? $",{Column.Value}" : string.Empty)})";
            }

            return string.IsNullOrWhiteSpace(location)
                ? $"{Code}: {Message}"
                : $"{location}: {Code}: {Message}";
        }
    }

    public enum ErrorLevel
    {
        None = 0,
        Message = 1,
        Warning = 2,
        Error = 3
    }
}
