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
        Type Type;

        public CogsDate(DateTimeOffset item, bool isDate = false) : this()
        {
            if (isDate) { Date = item; }
            else { DateTime = item; }
            Type = item.GetType();
        }

        public CogsDate(Tuple<int, int> item) : this()
        {
            GYearMonth = item;
            Type = item.GetType();
        }

        public CogsDate(int item) : this()
        {
            GYear = item;
            Type = item.GetType();
        }

        public CogsDate(TimeSpan item) : this()
        {
            Duration = item;
            Type = item.GetType();
        }

        public string GetValue()
        {
            if (Type == DateTime.GetType())
            {
                if (DateTime != null) { return DateTime.DateTime.ToString(); }
                return Date.Date.ToString();
            }
            if (Type == GYearMonth.GetType()) { return GYearMonth.Item1 + "-" + GYearMonth.Item2; }
            if (Type == GYear.GetType()) { return GYear.ToString(); }
            if (Type == Duration.GetType()) { return Duration.Duration().ToString(); }
            throw new InvalidOperationException();
        }
    }
}