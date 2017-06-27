
#boolean
c#: built-in bool type

#string
c#: built-in string class

#decimal
c#: built-in decimal type

#float
c#: built-in float type

#double
c#: built-in double type

#duration
c#: struct duration : DateSuper{
	int Years;
	int Months;
	int Days;
	char T;
	int Hours;
	int Minutes;
	int Seconds;
	duration MinInclusive;
	duration MinExclusive;
	duration MaxInclusive;
	duration MaxExclusive;
	string[] values;

	public duration(int y, int m, int d, int h, int min, int s)
	{
		Years = y;
		Months = m;
		Days = d;
		Hours = h;
		Minutes = min;
		Seconds = s;
		values = new string[] {y, m, d, h, min, s};
	}

	public string getValue()
	{
		int counter = 0;
			foreach(var item in Values)
			{
				int minIn = 0;
				int minEx = -1;
				int maxEx = int64.MaxInt();
				int maxIn = int64.MaxInt();
				if(MinInclusive != null) { minIn = MinInclusive.Values[counter]; }
				if(MinExclusive != null) { minEx = MinExclusive.Values[counter]; }
				if(MaxInclusive != null) { maxIn = MaxInclusive.Values[counter]; }
				if(MaxExclusive != null) { maxEx = MaxExclusive.Values[counter]; }
				[CheckValueInt(item, minInt, minEx, maxIn, MaxEx)]
				counter++;
			}
		return Years.ToString("0000") + Months.ToString("00") + Days.ToString("00") + T + Hours.ToString("00") + Minutes.ToString("00") + seconds.ToString("00");
	}
}

#dateTime
c#: struct dateTime : DateSuper{
	int C;
	int Y;
	int M;
	int D;
	char T;
	int h;
	int m;
	int s;
	char zone = '';
	char sign = '+';
	dateTime MinInclusive;
	dateTime MinExclusive;
	dateTime MaxInclusive;
	dateTime MaxExclusive;
	string[] values;

	public dateTime(int c, int y, int m, int d, int h, int min, int s)
	{
		C = c;
		Y = y;
		M = m;
		D = d;
		this.h = h;
		this.m = min;
		this.s = s;
		values = new string[] {C, Y, M, D, h, m, s};
	}

	public string getValue()
	{
		int counter = 0;
		foreach(var item in Values)
		{
			int minIn = 0;
			int minEx = 0;
			int maxEx = 0;
			int maxIn = 0;
			if(MinInclusive != null) { minIn = MinInclusive.Values[counter]; }
			if(MinExclusive != null) { minEx = MinExclusive.Values[counter]; }
			if(MaxInclusive != null) { maxIn = MaxInclusive.Values[counter]; }
			if(MaxExclusive != null) { maxEx = MaxExclusive.Values[counter]; }
			[CheckValueInt(item, minInt, minEx, maxIn, MaxEx)]
			counter++;
		}
		return sign + C.ToString("00") + Y.ToString("00") + "-" + M.ToString("00") + "-" + D.ToString("00") + T + h.ToString("00") + ":" + m.ToString("00") + ":" + s.ToString("00") + zone;
	}
}

#time
c#: built-in DateTimeOffset type

#date
c#: built-in DateTimeOffset type wrapped in class inheriting from DateSuper

#gYearMonth
c#: built-in GregorianCalendar class wrapped in class inheriting from DateSuper

#gYear
c#: built-in GregorianCalendar class wrapped in class inheriting from DateSuper

#gYearDay
c#: built-in GregorianCalendar class wrapped in class inheriting from DateSuper

#gDay
c#: built-in GregorianCalendar class wrapped in class inheriting from DateSuper

#gMonth
c#: built-in GregorianCalendar class wrapped in class inheriting from DateSuper

#anyURI
c#: built-in Uri class

#language
c#: built-in string class

#integer
c#: built-in int type

#nonPositiveInteger
c#: built-in int type

#negativeInteger
c#: built-in int type

#long
c#: built-in long type

#int
c#: built-in int type

#nonNegativeInteger
c#: built-in uint type

#unsignedLong
c#: built-in ulong type

#positiveInteger
c#: built-in uint type

#cogsDate
c#: struct CogsDate{
	Type type;
	DateSuper Date;

	public CogsDate(DateSuper date){
		type = Object.GetType();
		Date = date;
	}

	public string GetValue(){
		return Date.GetValue();
	}
}

#Min/Max's for built-in types and classes will be checked with the following attribute:
public class CardinalityAttribute : Attribute
{
	public void CheckValueString(string input, int minLength, int maxLength, enum enumeration, pattern)
	{
		if(input.Length < minLength || input.Length > maxLength || !enumeration.GetNames(typeof(string)).Contains(input) )
		{
			thow new InvalidOperationException();
		}
	}

	public void CheckValueNum(T input, int minInclusive, int minExclusive, int maxInclusive, maxExclusive)
	{
		if(minInclusive != null && input < minInclusive || minExclusive != null && input <= minExclusive)
		{
			throw new InvalidOperationException();
		}
		if(maxInclusive != null && input > maxInclusive || maxExclusive != null && input >= maxExclusive)
		{
			throw new InvalidOperationException();
		}
	}
}
