using System;
using System.Collections.Generic;
using System.Text;

namespace Cogs.Common
{
    public static class CogsTypes
    {

        public static readonly string[] SimpleTypeNames =
        {
            "boolean",
            "string",
            "boolean",
            "decimal",
            "float",
            "double",
            "duration",
            "dateTime",
            "time",
            "date",
            "gYearMonth",
            "gYear",
            "gMonthDay",
            "gDay",
            "gMonth",
            "anyURI",
            "language",
            "int",
            "nonPositiveInteger",
            "negativeInteger",
            "long",
            "int",
            "nonNegativeInteger",
            "unsignedLong",
            "positiveInteger",
            "cogsDate"
        };

        public static readonly string CogsDate = "cogsDate";

        public static readonly string[] BuiltinTypeNames =
        {
            "This",
            "Any"
        };

        public static readonly string[] BuiltinPropertyNames =
        {
            "Language",
            "DcTerms"
        };
    }
}
