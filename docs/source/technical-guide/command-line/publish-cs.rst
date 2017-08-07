publish-cs
~~~~~~~~~~
Generates c# classes for all ItemTypes and ReusableTypes in the model. The generated classes can be serialized to json, populated from json, and serialized to XML.

Command Line Arguments
----------------------
Required inputs for publish-cs command (must be specified in order).

* ``[CogsLocation]`` 

    The location of folder containing model.

* ``[TargetLocation]`` 

    The location of folder where the output will be created.

Command Line Flags
----------------------
Optional inputs for publish-cs command in no particular order.

* ``-?|-h|--help``

    Displays all command arguments and flags are for the publish-cs command.

* ``-o|--overwrite``

    If the ``[TargetLocation]`` is not empty, erase all files in the folder before generation.

* ``-n|--namespace``

    Allows user to specify the XMI of desired XML namespace used in XML creation from generated c# classes.

* ``-p|--prefix``

    Allows user to specify the prefix for XML namespace used in XML creation from generated c# classes.

Example Command Line Usage
--------------------------
A few examples of how the command line arguments and flags are used together.

.. code-block:: console

    publish-cs -h
    publish-cs C:\Users\kevin\Documents\GitHub\cogs\cogsburger C:\Users\kevin\Documents\GitHub\cogs\Cogs.Console\out
    publish-cs -o C:\Users\kevin\Documents\GitHub\cogs\cogsburger C:\Users\kevin\Documents\GitHub\cogs\Cogs.Console\out
    publish-cs -n http://example.org/cogsburger -p cogs -o C:\Users\kevin\Documents\GitHub\cogs\cogsburger C:\Users\kevin\Documents\GitHub\cogs\Cogs.Console\out

Simple Type Mappings to c#
--------------------------
============    =================
Simple Type     c# representation
============    =================
Duration        `Timespan <https://msdn.microsoft.com/en-us/library/system.timespan(v=vs.110).aspx>`_
DateTime        `DateTimeOffset <https://msdn.microsoft.com/en-us/library/system.datetimeoffset(v=vs.110).aspx>`_
Time            `DateTimeOffset <https://msdn.microsoft.com/en-us/library/system.datetimeoffset(v=vs.110).aspx>`_
Date            `DateTimeOffset <https://msdn.microsoft.com/en-us/library/system.datetimeoffset(v=vs.110).aspx>`_
GYearMonth      Custom GYearMonth_ class
GMonthDay       Custom GMonthDay_ class
GYear           Custom GYear_ class
GMonth          Custom GMonth_ class
GDay            Custom GDay_ class
AnyURI          `Uri <https://msdn.microsoft.com/en-us/library/system.uri(v=vs.110).aspx?>`_
Language        `String <https://msdn.microsoft.com/en-us/library/system.string(v=vs.110).aspx>`_
CogsDate        Custom CogsDate_ class 
============    =================

Custom Simple Types in c#
-------------------------
Source code for custom simple types.

.. _GYearMonth:

**GYearMonth** ::

    public class GYearMonth : IComparable
    {
        int Y;
        int M;
        string Timezone;

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
    }

.. _GMonthDay:

**GMonthDay** ::

    public class GMonthDay : IComparable
    {
        int M;
        int D;
        string Timezone;

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
    }

.. _GYear:

**GYear** ::

    public class GYear : IComparable
	{
		int Value;
		string Timezone;

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
    }

.. _GMonth:

**GMonth** ::

    public class GMonth : IComparable
    {
        int Value;
        string Timezone;

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
    }

.. _GDay:

**GDay** ::

    public class GDay : IComparable
    {
        int Value;
        string Timezone;

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
    }

.. _CogsDate: 

**CogsDate** ::

    public struct CogsDate
    {
        public DateTimeOffset DateTime { get; set; }
        public DateTimeOffset Date { get; set; }
        public GYearMonth GYearMonth { get; set; }
        public GYear GYear { get; set; }
        public TimeSpan Duration { get; set; }
        public enum CogsDateType { DateTime, Date, GYearMonth, GYear, Duration } 
        public CogsDateType UsedType { get; private set; }

        public CogsDate(DateTimeOffset item, bool isDate = false) : this()
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

        public CogsDate(GYearMonth item) : this()
        {
            GYearMonth = item;
            UsedType = CogsDateType.GYearMonth;
        }

        public CogsDate(GYear item) : this()
        {
            GYear = item;
            UsedType = CogsDateType.GYear;
        }

        public CogsDate(TimeSpan item) : this()
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
                        return DateTime.ToString("yyyy-MM-dd\\THH:mm:ss.FFFFFFFK");
                    }
                case CogsDateType.Date:
                    {
						if (Date == default(DateTimeOffset)) { return null; }
                        return Date.ToString("u").Split(' ')[0];
                    }
                case CogsDateType.GYearMonth:
                    {
                        return GYearMonth.ToJson();
                    }
                case CogsDateType.GYear:
                    {
                        return GYear.ToJson();
                    }
                case CogsDateType.Duration:
                    {
						if (Duration == default(TimeSpan)) { return null; }
                        return Duration.Ticks;
                    }
            }
            return null;
        }
    }

