using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;
using Cogs.SimpleTypes;
using System.Globalization;
using Cogs.Converters;

/// <summary>
/// Data Annotations
/// </summary>
namespace Cogs.DataAnnotations
{
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
            if (value is string s && String.IsNullOrEmpty(s))
            {
                return true;
            }
            dynamic val = value;
            dynamic min = Minimum;
            dynamic max = Maximum;

            if (val <= min) { return false; }
            if (val >= max) { return false; }
            return true;
        }
    }


    [AttributeUsage(AttributeTargets.All)]
    public class StringValidationAttribute : ValidationAttribute
    {
        Regex Rgx;
        List<string> Enumerations;

        public StringValidationAttribute(string[] enumerations, string pattern = null)
        {
            if (pattern != null) { this.Rgx = new Regex(pattern); }
            if (enumerations != null) { this.Enumerations = new List<string>(enumerations); }
        }

        public override bool IsValid(object value)
        {
            if(value == null)
            {
                return true;
            }
            if (Enumerations != null && !Enumerations.Contains(value.ToString())) { return false; }
            // check regex Pattern
            if (Rgx != null && !this.Rgx.IsMatch(value.ToString())) { return false; }
            return true;
        }
    }
        
}

namespace Cogs.SimpleTypes
{


    public enum CogsDateType
    {
        None = 0,
        DateTime = 1,
        Date = 2,
        GYearMonth = 3,
        GYear = 4,
        Duration = 5
    }
    public class CogsDate
    {
        private DateTimeOffset dateTimeOffset;
        private GYearMonth gYearMonth;
        private GYear gYear;
        private TimeSpan timespan;

        private void Clear()
        {
            dateTimeOffset = default(DateTimeOffset);
            gYearMonth = null;
            gYear = null;
            timespan = default(TimeSpan);
            UsedType = CogsDateType.None;
        }

        [JsonConverter(typeof(DateTimeConverter))]
        public DateTimeOffset DateTime
        {
            get
            {
                if(this.UsedType == CogsDateType.DateTime) { return dateTimeOffset; }
                return default(DateTimeOffset);
            }
            set
            {
                Clear();
                dateTimeOffset = value;
                this.UsedType = CogsDateType.DateTime;
            }
        }

        [JsonConverter(typeof(DateConverter))]
        public DateTimeOffset Date
        {
            get
            {
                if (this.UsedType == CogsDateType.Date) { return dateTimeOffset; }
                return default(DateTimeOffset);
            }
            set
            {
                Clear();
                dateTimeOffset = value;
                this.UsedType = CogsDateType.Date;
            }
        }

        [JsonConverter(typeof(GYearMonthConverter))]
        public GYearMonth GYearMonth
        {
            get
            {
                return gYearMonth;
            }
            set
            {
                Clear();
                gYearMonth = value;
                this.UsedType = CogsDateType.GYearMonth;
            }
        }

        [JsonConverter(typeof(GYearConverter))]
        public GYear GYear
        {
            get
            {
                return gYear;
            }
            set
            {
                Clear();
                gYear = value;
                this.UsedType = CogsDateType.GYear;
            }
        }

        public TimeSpan Duration
        {
            get
            {
                return timespan;
            }
            set
            {
                Clear();
                timespan = value;
                this.UsedType = CogsDateType.Duration;
            }
        }

        [JsonIgnore]
        public CogsDateType UsedType { get; private set; }

        public CogsDate() { }

        public CogsDate(DateTimeOffset item, bool isDate = false)
        {
            if (isDate)
            {
                Date = item;
                UsedType = CogsDateType.Date;
            }
            else
            {
                DateTime = item;
                UsedType = CogsDateType.DateTime;
            }
        }

        public CogsDate(GYearMonth item)
        {
            GYearMonth = item;
            UsedType = CogsDateType.GYearMonth;
        }

        public CogsDate(GYear item)
        {
            GYear = item;
            UsedType = CogsDateType.GYear;
        }

        public CogsDate(TimeSpan item)
        {
            Duration = item;
            UsedType = CogsDateType.Duration;
        }

        public string GetUsedType()
        {
            switch (UsedType)
            {
                case CogsDateType.Date: { return "date"; }
                case CogsDateType.DateTime: { return "datetime"; }
                case CogsDateType.Duration: { return "duration"; }
                case CogsDateType.GYear: { return "year"; }
                case CogsDateType.GYearMonth: { return "YearMonth"; }
            }
            return null;
        }

        public override string ToString()
        {
            switch (UsedType)
            {
                case CogsDateType.Date: { return Date.ToString("u").Split(' ')[0]; }
                case CogsDateType.DateTime: { return DateTime.ToString("yyyy-MM-dd\\THH:mm:ss.FFFFFFFK"); }
                case CogsDateType.Duration:
                    {
                        return string.Format("P{00}DT{00}H{00}M{00}S", Duration.ToString("%d"), Duration.ToString("%h"),
                            Duration.ToString("%m"), Duration.ToString("%s"));
                    }
                case CogsDateType.GYear: { return GYear.ToString(); }
                case CogsDateType.GYearMonth: { return GYearMonth.ToString(); }
            }
            return base.ToString();
        }
        
        public object GetValue()
        {
            switch (UsedType)
            {
                case CogsDateType.DateTime:
                    {
                        if (DateTime == default(DateTimeOffset)) { return null; }
                        return DateTime;
                    }
                case CogsDateType.Date:
                    {
                        if (Date == default(DateTimeOffset)) { return null; }
                        return Date;
                    }
                case CogsDateType.GYearMonth:
                    {
                        return GYearMonth;
                    }
                case CogsDateType.GYear:
                    {
                        return GYear;
                    }
                case CogsDateType.Duration:
                    {
                        if (Duration == default(TimeSpan)) { return null; }
                        return Duration;
                    }
            }
            return null;
        }
    }

    public class GYear : IComparable, IEquatable<GYear>
	{
        public int Value;
        public string Timezone;

		public GYear(int year)
		{
			Value = year;
		}

		public GYear(int year, string zone)
		{
			Value = year;
			Timezone = zone;
		}

		public override string ToString()
		{
			if (Timezone != null) 
			{
				if (char.IsDigit(Timezone[0])) { return Value.ToString().PadLeft(4, '0') + "+" + Timezone; }
				return Value.ToString().PadLeft(4, '0') + Timezone; 
			}
			return Value.ToString().PadLeft(4, '0');
		}

		public JObject ToJson()
		{
            if (Timezone != null) { return new JObject(new JProperty("year", Value), new JProperty("timezone", Timezone)); }
            return new JObject(new JProperty("year", Value));
        }

        public int CompareTo(object obj)
        {
            if (obj == null || obj.GetType() != typeof(GYear)) { return -1; }
            var other = (GYear)obj;
            if (other.Value < Value) { return -1; }
            if (other.Value == Value)
            {
                if (other.Timezone == null && Timezone == null) { return 0; }
                if (other.Timezone == null) { return -1; }
                if (Timezone == null) { return 1; }
                if (other.Timezone.Equals(Timezone)) { return 0; }
                return -1;
            }
            return 1;
        }

        public bool Equals(GYear other)
        {
            if (CompareTo(other) == 0) { return true; }
            return false;
        }
    }

    public class GMonth : IComparable, IEquatable<GMonth>
    {
        public int Value;
        public string Timezone;

        public GMonth(int month)
        {
            Value = month;
        }

        public GMonth(int month, string zone)
        {
            Value = month;
            Timezone = zone;
        }

        public override string ToString()
        {
            if (Timezone != null)
            {
                if (char.IsDigit(Timezone[0])) { return "--" + Value.ToString().PadLeft(2, '0') + "+" + Timezone; }
                return "--" + Value.ToString().PadLeft(2, '0') + Timezone;
            }
            return "--" + Value.ToString().PadLeft(2, '0');
        }

        public JObject ToJson()
        {
            if (Timezone != null) { return new JObject(new JProperty("month", Value), new JProperty("timezone", Timezone)); }
            return new JObject(new JProperty("month", Value));
        }

        public int CompareTo(object obj)
        {
            if (obj == null || obj.GetType() != typeof(GMonth)) { return -1; }
            var other = (GMonth)obj;
            if (other.Value < Value) { return -1; }
            if (other.Value == Value)
            {
                if (other.Timezone == null && Timezone == null) { return 0; }
                if (other.Timezone == null) { return -1; }
                if (Timezone == null) { return 1; }
                if (other.Timezone.Equals(Timezone)) { return 0; }
                return -1;
            }
            return 1;
        }

        public bool Equals(GMonth other)
        {
            if (CompareTo(other) == 0) { return true; }
            return false;
        }
    }

    public class GDay : IComparable, IEquatable<GDay>
    {
        public int Value;
        public string Timezone;

        public GDay(int day)
        {
            Value = day;
        }

        public GDay(int day, string zone)
        {
            Value = day;
            Timezone = zone;
        }

        public override string ToString()
        {
            if (Timezone != null)
            {
                if (char.IsDigit(Timezone[0])) { return "---" + Value.ToString().PadLeft(2, '0') + "+" + Timezone; }
                return "---" + Value.ToString().PadLeft(2, '0') + Timezone;
            }
            return "---" + Value.ToString().PadLeft(2, '0');
        }

        public JObject ToJson()
        {
            if (Timezone != null) { return new JObject(new JProperty("day", Value), new JProperty("timezone", Timezone)); }
            return new JObject(new JProperty("day", Value));
        }

        public int CompareTo(object obj)
        {
            if (obj == null || obj.GetType() != typeof(GDay)) { return -1; }
            var other = (GDay)obj;
            if (other.Value < Value) { return -1; }
            if (other.Value == Value)
            {
                if (other.Timezone == null && Timezone == null) { return 0; }
                if (other.Timezone == null) { return -1; }
                if (Timezone == null) { return 1; }
                if (other.Timezone.Equals(Timezone)) { return 0; }
                return -1;
            }
            return 1;
        }

        public bool Equals(GDay other)
        {
            if (CompareTo(other) == 0) { return true; }
            return false;
        }
    }

    public class GYearMonth : IComparable, IEquatable<GYearMonth>
    {
        public int Y;
        public int M;
        public string Timezone;

        public GYearMonth(int year, int month)
        {
            Y = year;
            M = month;
        }

        public GYearMonth(int year, int month, string zone)
        {
            Y = year;
            M = month;
            Timezone = zone;
        }

        public override string ToString()
        {
            if (Timezone != null)
            {
                if (char.IsDigit(Timezone[0])) { return Y.ToString().PadLeft(4, '0') + "-" + M.ToString().PadLeft(2, '0') + "+" + Timezone; }
                return Y.ToString().PadLeft(4, '0') + "-" + M.ToString().PadLeft(2, '0') + Timezone;
            }
            return Y.ToString().PadLeft(4, '0') + "-" + M.ToString().PadLeft(2, '0');
        }

        public JObject ToJson()
        {
            if (Timezone != null) { return new JObject(new JProperty("year", Y), new JProperty("month", M), new JProperty("timezone", Timezone)); }
            return new JObject(new JProperty("year", Y), new JProperty("month", M));
        }

        public int CompareTo(object obj)
        {
            if (obj == null || obj.GetType() != typeof(GYearMonth)) { return -1; }
            var other = (GYearMonth)obj;
            if (other.Y < Y) { return -1; }
            if (other.Y == Y)
            {
                if (other.M < M) { return -1; }
                if (other.M == M)
                {
                    if (other.Timezone == null && Timezone == null) { return 0; }
                    if (other.Timezone == null) { return -1; }
                    if (Timezone == null) { return 1; }
                    if (other.Timezone.Equals(Timezone)) { return 0; }
                    return -1;
                }
                if (other.M > M) { return 1; }
            }
            return 1;
        }

        public bool Equals(GYearMonth other)
        {
            if (CompareTo(other) == 0) { return true; }
            return false;
        }
    }

    public class GMonthDay : IComparable, IEquatable<GMonthDay>
    {
        public int M;
        public int D;
        public string Timezone;

        public GMonthDay(int month, int day)
        {
            M = month;
            D = day;
        }

        public GMonthDay(int month, int day, string zone)
        {
            M = month;
            D = day;
            Timezone = zone;
        }

        public override string ToString()
        {
            if (Timezone != null)
            {
                if (char.IsDigit(Timezone[0])) { return "--" + M.ToString().PadLeft(2, '0') + "-" + D.ToString().PadLeft(2, '0') + "+" + Timezone; }
                return "--" + M.ToString().PadLeft(2, '0') + "-" + D.ToString().PadLeft(2, '0') + Timezone;
            }
            return "--" + M.ToString().PadLeft(2, '0') + "-" + D.ToString().PadLeft(2, '0');
        }

        public JObject ToJson()
        {
            if (Timezone != null) { return new JObject(new JProperty("month", M), new JProperty("day", D), new JProperty("timezone", Timezone)); }
            return new JObject(new JProperty("month", M), new JProperty("day", D));
        }

        public int CompareTo(object obj)
        {
            if (obj == null || obj.GetType() != typeof(GMonthDay)) { return -1; }
            var other = (GMonthDay)obj;
            if (other.M < M) { return -1; }
            if (other.M == M)
            {
                if (other.D < D) { return -1; }
                if (other.D == D)
                {
                    if (other.Timezone == null && Timezone == null) { return 0; }
                    if (other.Timezone == null) { return -1; }
                    if (Timezone == null) { return 1; }
                    if (other.Timezone.Equals(Timezone)) { return 0; }
                    return -1;
                }
                if (other.D > D) { return 1; }
            }
            return 1;
        }

        public bool Equals(GMonthDay other)
        {
            if (CompareTo(other) == 0) { return true; }
            return false;
        }
    }
}

namespace Cogs.Converters
{
    public class DateConverter : BaseDateTimeConverter
    {
        public override string DateTimeFormat { get; } = "yyyy-MM-dd";   
    }

    public class TimeConverter : BaseDateTimeConverter
    {
        public override string DateTimeFormat { get; } = "HH:mm:ss.FFFFFFFK";
    }

    public class DateTimeConverter : BaseDateTimeConverter
    {
        public override string DateTimeFormat { get; } = "yyyy-MM-dd\\THH:mm:ss.FFFFFFFK";        
    }

    public abstract class BaseDateTimeConverter : JsonConverter
    {
        public abstract string DateTimeFormat { get; } 

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTimeOffset) || objectType == typeof(List<DateTimeOffset>);
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                var results = new List<DateTimeOffset>();
                var array = JArray.Load(reader);
                foreach (var item in array.Children())
                {
                    var itemValue = item.ToString();
                    if (DateTimeOffset.TryParseExact(itemValue.ToString(), DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset itemResult))
                    {
                        results.Add(itemResult);
                    }
                }
                return results;
            }

            var token = (string)reader.Value;
            if (DateTimeOffset.TryParseExact(token.ToString(), DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset result))
            {
                return result;
            }
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is DateTimeOffset offset)
            {
                writer.WriteValue(offset.ToString(DateTimeFormat));
            }
            else if (value is List<DateTimeOffset> offsets)
            {
                writer.WriteStartArray();
                foreach (var off in offsets)
                {
                    writer.WriteValue(off.ToString(DateTimeFormat));
                }
                writer.WriteEndArray();
            }
        }
    }


    public class DurationConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TimeSpan) || objectType == typeof(List<TimeSpan>);
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                var results = new List<TimeSpan>();
                var array = JArray.Load(reader);
                foreach (var item in array.Children())
                {
                    var itemValue = item.ToString();
                    if (int.TryParse(itemValue, out int milliseconds))
                    {
                        results.Add(new TimeSpan(0, 0, 0, 0, milliseconds));
                    }
                }
                return results;
            }

            if (reader.Value is Int64 largeMilli)
            {
                return new TimeSpan(0, 0, 0, 0, (int)largeMilli);
            }
            if (reader.Value is int milli)
            {
                return new TimeSpan(0, 0, 0, 0, milli);
            }
            var token = reader.Value.ToString();
            if (int.TryParse(token, out int mill))
            {
                return new TimeSpan(0, 0, 0, 0, mill);
            }
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is TimeSpan span)
            {
                writer.WriteValue(span.TotalMilliseconds);
            }
            else if (value is List<TimeSpan> offsets)
            {
                writer.WriteStartArray();
                foreach (var off in offsets)
                {
                    writer.WriteValue(span.TotalMilliseconds);
                }
                writer.WriteEndArray();
            }
        }
    }

    public abstract class BaseGConverter<T> : JsonConverter
    {

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(T) || objectType == typeof(List<T>);
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                var results = new List<T>();
                var array = JArray.Load(reader);
                foreach (var item in array.Children())
                {
                    var jsonObject = (JObject)item;
                    results.Add(FromObject(jsonObject));
                }
                return results;
            }

            var single = JObject.Load(reader);
            return FromObject(single);
        }

        internal abstract T FromObject(JObject jsonObject);

        internal abstract void Write(JsonWriter writer, T item);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is T item)
            {
                Write(writer, item);
            }
            else if (value is List<T> items)
            {
                writer.WriteStartArray();
                foreach (var i in items)
                {
                    Write(writer, i);
                }
                writer.WriteEndArray();
            }
        }
    }

    public class GDayConverter : BaseGConverter<GDay>
    {
        internal override GDay FromObject(JObject jsonObject)
        {
            int day = (int)jsonObject["day"];
            string timezone = (string)jsonObject["timezone"];
            return new GDay(day, timezone);
        }

        internal override void Write(JsonWriter writer, GDay item)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("day");
            writer.WriteValue(item.Value);
            if (!string.IsNullOrWhiteSpace(item.Timezone))
            {
                writer.WritePropertyName("timezone");
                writer.WriteValue(item.Timezone);
            }
            writer.WriteEndObject();
        }
    }


    public class GMonthConverter : BaseGConverter<GMonth>
    {
        internal override GMonth FromObject(JObject jsonObject)
        {
            int month = (int)jsonObject["month"];
            string timezone = (string)jsonObject["timezone"];
            return new GMonth(month, timezone);
        }

        internal override void Write(JsonWriter writer, GMonth item)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("month");
            writer.WriteValue(item.Value);
            if (!string.IsNullOrWhiteSpace(item.Timezone))
            {
                writer.WritePropertyName("timezone");
                writer.WriteValue(item.Timezone);
            }
            writer.WriteEndObject();
        }
    }


    public class GMonthDayConverter : BaseGConverter<GMonthDay>
    {
        internal override GMonthDay FromObject(JObject jsonObject)
        {
            int month = (int)jsonObject["month"];
            int day = (int)jsonObject["day"];
            string timezone = (string)jsonObject["timezone"];
            return new GMonthDay(month, day, timezone);
        }

        internal override void Write(JsonWriter writer, GMonthDay item)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("month");
            writer.WriteValue(item.M);
            writer.WritePropertyName("day");
            writer.WriteValue(item.D);
            if (!string.IsNullOrWhiteSpace(item.Timezone))
            {
                writer.WritePropertyName("timezone");
                writer.WriteValue(item.Timezone);
            }
            writer.WriteEndObject();
        }
    }

    public class GYearConverter : BaseGConverter<GYear>
    {      
        internal override GYear FromObject(JObject jsonObject)
        {
            int year = (int)jsonObject["year"];
            string timezone = (string)jsonObject["timezone"];
            return new GYear(year, timezone);
        }

        internal override void Write(JsonWriter writer, GYear item)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("year");
            writer.WriteValue(item.Value);

            if (!string.IsNullOrWhiteSpace(item.Timezone))
            {
                writer.WritePropertyName("timezone");
                writer.WriteValue(item.Timezone);
            }

            writer.WriteEndObject();
        }
    }

    public class GYearMonthConverter : BaseGConverter<GYearMonth>
    {
        internal override GYearMonth FromObject(JObject jsonObject)
        {
            int year = (int)jsonObject["year"];
            int month = (int)jsonObject["month"];
            string timezone = (string)jsonObject["timezone"];
            return new GYearMonth(year, month, timezone);
        }

        internal override void Write(JsonWriter writer, GYearMonth item)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("year");
            writer.WriteValue(item.Y);
            writer.WritePropertyName("month");
            writer.WriteValue(item.M);

            if (!string.IsNullOrWhiteSpace(item.Timezone))
            {
                writer.WritePropertyName("timezone");
                writer.WriteValue(item.Timezone);
            }

            writer.WriteEndObject();
        }
    }
}