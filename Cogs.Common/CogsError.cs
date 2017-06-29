// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using System;

namespace Cogs.Common
{
    public class CogsError
    {
        public ErrorLevel Level { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }

        public CogsError(ErrorLevel level, string message, Exception exception = null)
        {
            Level = level;
            Message = message;
            Exception = exception;
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
