// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
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
            "nonPositiveInteger",
            "negativeInteger",
            "long",
            "int",
            "nonNegativeInteger",
            "unsignedLong",
            "positiveInteger",
            "cogsDate",
            "dcTerms",
            "langString"
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
