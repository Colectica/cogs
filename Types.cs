using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace cogsBurger
{
    /*
    [AttributeUsage(AttributeTargets.All)]
    public class ExclusiveRangeAttribute : RangeAttribute
    {

        public ExclusiveRangeAttribute(int minimum, int maximum) : base(minimum, maximum) { }

        public override bool IsValid(object value)
        {
            // Automatically pass if value is null or empty. RequiredAttribute should be used to assert a value is not empty.
            if (value == null)
            {
                return true;
            }
            string s = value as string;
            if (s != null && String.IsNullOrEmpty(s))
            {
                return true;
            }
            dynamic val = value;
            dynamic min = Minimum;
            dynamic max = Maximum;

            if (MinExclusive && val <= min) { return false; }
            if (MaxExclusive && val >= max) { return false; }
            return true;
        }
    }


    [AttributeUsage(AttributeTargets.All)]
    public class StringValidationAttribute : ValidationAttribute
    {
        Regex Rgx;
        Enum Enumerations;

        public StringValidationAttribute(Enum enumerations, string pattern)
        {
            this.Rgx = new Regex(pattern);
            this.Enumerations = enumerations;
        }

        public override bool IsValid(object value)
        {
            if (!System.Enum.IsDefined(Enumerations.GetType(), value)) { return false; }
            // check regex Pattern
            return true;
        }
    }
    */


    public struct CogsDate
    {
        DateTimeOffset DateTime;
        DateTimeOffset Date;
        Tuple<int, int> GYearMonth;
        int GYear;
        TimeSpan Duration;
        enum CogsDateType {DateTime, Date, GYearMonth, GYear, Duration};

        public CogsDate(DateTimeOffset item, bool isDate = false) : this()
        {
            if (isDate) { Date = item; }
            else { DateTime = item; }
        }

        public CogsDate(Tuple<int, int> item) : this()
        {
            GYearMonth = item;
        }

        public CogsDate(int item) : this()
        {
            GYear = item;
        }

        public CogsDate(TimeSpan item) : this()
        {
            Duration = item;
        }

        public string GetValue()
        {
            switch(CogsDateType)
            {
                case CogsDateType.DateTime:
                    {
                        return DateTime.DateTime.ToString();
                    }
                case CogsDateType.Date:
                    {
                        return Date.Date.ToString();
                    }
                case CogsDateType.GYearMonth:
                    {
                        return GYearMonth.Item1 + "-" + GYearMonth.Item2;
                    }
                case CogsDateType.GYear:
                    {
                        return GYear.ToString();
                    }
                case CogsDateType.Duration:
                    {
                        return Duration.Duration().ToString();
                    }
            }
     //       throw new InvalidOperationException();
        }
    }
}