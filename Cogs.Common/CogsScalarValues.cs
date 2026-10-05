using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace Cogs.Common
{
    /// <summary>Native scalar domains shared by model validation and generated C# codecs.</summary>
    public static class CogsScalarValues
    {
        public const long SafeInteger = 9007199254740991;
        public const long MaximumDurationMilliseconds = 922337203685477;
        private static readonly BigInteger DecimalCoefficient = BigInteger.Parse("79228162514264337593543950335", CultureInfo.InvariantCulture);
        private static readonly Regex Number = new Regex(@"\A(?<sign>-?)(?<whole>0|[1-9][0-9]*)(?:\.(?<fraction>[0-9]+))?(?:[eE](?<exponent>[+-]?[0-9]+))?\z", RegexOptions.CultureInvariant);
        private static readonly Regex XmlNumber = new Regex(@"\A[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?\z", RegexOptions.CultureInvariant);
        private static readonly Regex Instant = new Regex(@"\A(?<date>[0-9]{4}-[0-9]{2}-[0-9]{2})T(?<clock>[0-9]{2}:[0-9]{2}:[0-9]{2})(?:\.(?<fraction>[0-9]+))?(?<zone>Z|[+-](?:0[0-9]|1[0-3]):[0-5][0-9]|[+-]14:00)\z", RegexOptions.CultureInvariant);
        private static readonly Regex Clock = new Regex(@"\A(?<clock>[0-9]{2}:[0-9]{2}:[0-9]{2})(?:\.(?<fraction>[0-9]+))?\z", RegexOptions.CultureInvariant);
        private static readonly Regex Elapsed = new Regex(@"\A(?<negative>-)?P(?=[0-9]|T[0-9.])(?:(?<days>[0-9]+)D)?(?:T(?=[0-9.])(?:(?<hours>[0-9]+)H)?(?:(?<minutes>[0-9]+)M)?(?:(?<seconds>[0-9]+(?:\.[0-9]*)?|\.[0-9]+)S)?)?\z", RegexOptions.CultureInvariant);

        public static bool IsText(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsHighSurrogate(character))
                {
                    if (++index >= value.Length || !char.IsLowSurrogate(value[index]))
                    {
                        return false;
                    }
                }
                else if (char.IsLowSurrogate(character) || character is '\uFFFE' or '\uFFFF' ||
                    character < ' ' && character is not '\t' and not '\n' and not '\r')
                {
                    return false;
                }
            }
            return true;
        }

        public static string Text(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!IsText(value))
            {
                throw new FormatException("Text must contain only XML 1.0 Unicode characters.");
            }
            return value;
        }

        public static int TextLength(string value)
        {
            Text(value);
            int count = 0;
            foreach (Rune rune in value.EnumerateRunes())
            {
                count++;
            }
            return count;
        }

        private static bool TryParts(string lexical, out BigInteger coefficient, out int scale)
        {
            coefficient = BigInteger.Zero;
            scale = 0;
            Match match = Number.Match(lexical);
            if (!match.Success)
            {
                return false;
            }
            string digits = (match.Groups["whole"].Value + match.Groups["fraction"].Value).TrimStart('0');
            if (digits.Length == 0)
            {
                return true;
            }
            int trailing = digits.Length - digits.TrimEnd('0').Length;
            digits = digits.TrimEnd('0');
            if (digits.Length > 29)
            {
                return false;
            }
            string exponentText = match.Groups["exponent"].Value;
            if (exponentText.Length > 0 && !int.TryParse(exponentText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }
            long exponent = exponentText.Length == 0 ? 0 : int.Parse(exponentText, CultureInfo.InvariantCulture);
            long exactScale = (long)match.Groups["fraction"].Length - trailing - exponent;
            if (exactScale > 28 || exactScale < -29 || digits.Length - Math.Min(0, exactScale) > 29)
            {
                return false;
            }
            coefficient = BigInteger.Parse(match.Groups["sign"].Value + digits, CultureInfo.InvariantCulture);
            if (exactScale < 0)
            {
                coefficient *= BigInteger.Pow(10, (int)-exactScale);
            }
            scale = (int)Math.Max(0, exactScale);
            return true;
        }

        public static bool TryInteger(string lexical, string datatype, out long value)
        {
            value = 0;
            if (!TryParts(lexical, out BigInteger coefficient, out int scale) || scale != 0 ||
                coefficient < -SafeInteger || coefficient > SafeInteger)
            {
                return false;
            }
            value = (long)coefficient;
            return datatype switch
            {
                "int" => value >= int.MinValue && value <= int.MaxValue,
                "long" => true,
                "unsignedLong" or "nonNegativeInteger" => value >= 0,
                "positiveInteger" => value > 0,
                "nonPositiveInteger" => value <= 0,
                "negativeInteger" => value < 0,
                _ => false
            };
        }

        public static long Integer(string lexical, string datatype)
        {
            if (!TryInteger(lexical, datatype, out long value))
            {
                throw new FormatException($"Value is outside the {datatype} safe-integer domain.");
            }
            return value;
        }

        public static bool TryDecimal(string lexical, out decimal value)
        {
            value = 0;
            if (!TryParts(lexical, out BigInteger coefficient, out int scale) || BigInteger.Abs(coefficient) > DecimalCoefficient)
            {
                return false;
            }
            BigInteger absolute = BigInteger.Abs(coefficient);
            value = new decimal((int)(uint)(absolute & uint.MaxValue), (int)(uint)((absolute >> 32) & uint.MaxValue),
                (int)(uint)(absolute >> 64), coefficient.Sign < 0, (byte)scale);
            string native = double.Parse(lexical, NumberStyles.Float, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture);
            return TryParts(native, out BigInteger roundTrip, out int roundTripScale) &&
                coefficient * BigInteger.Pow(10, roundTripScale) == roundTrip * BigInteger.Pow(10, scale);
        }

        public static decimal Decimal(string lexical)
        {
            if (!TryDecimal(lexical, out decimal value))
            {
                throw new FormatException("Decimal must be exactly representable by System.Decimal and survive native JavaScript JSON interchange.");
            }
            return value;
        }

        public static float JsonFloat(string lexical)
        {
            if (!float.TryParse(lexical, NumberStyles.Float, CultureInfo.InvariantCulture, out float direct) ||
                !float.IsFinite(direct) ||
                !double.TryParse(lexical, NumberStyles.Float, CultureInfo.InvariantCulture, out double intermediate) ||
                direct != (float)intermediate)
            {
                throw new FormatException("JSON float must preserve its binary32 value through native JavaScript number parsing; use a stable binary32 spelling.");
            }
            return direct == 0 ? 0 : direct;
        }

        public static string DecimalText(decimal value)
        {
            string lexical = value.ToString(CultureInfo.InvariantCulture);
            Decimal(lexical);
            return value == 0 ? "0" : lexical;
        }

        public static string XmlNumeric(string lexical, string datatype)
        {
            string text = lexical.Trim(' ', '\t', '\r', '\n');
            string pattern = datatype is "int" or "long" or "unsignedLong" or "positiveInteger" or "negativeInteger" or "nonPositiveInteger" or "nonNegativeInteger"
                ? @"\A[+-]?[0-9]+\z"
                : datatype == "decimal" ? @"\A[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)\z" : XmlNumber.ToString();
            if (!Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant))
            {
                throw new FormatException($"Invalid XML {datatype} lexical value.");
            }
            bool negative = text.StartsWith('-');
            text = text.TrimStart('+', '-');
            int exponentIndex = text.IndexOfAny(['e', 'E']);
            string exponent = exponentIndex < 0 ? string.Empty : text[exponentIndex..];
            string mantissa = exponentIndex < 0 ? text : text[..exponentIndex];
            string[] parts = mantissa.Split('.');
            string whole = parts[0].TrimStart('0');
            string fraction = parts.Length > 1 && parts[1].Length > 0 ? "." + parts[1] : string.Empty;
            return (negative ? "-" : string.Empty) + (whole.Length == 0 ? "0" : whole) + fraction + exponent;
        }

        public static DateOnly Date(string lexical)
        {
            if (!DateOnly.TryParseExact(lexical, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly value))
            {
                throw new FormatException("date requires a local date in years 0001 through 9999 without a timezone.");
            }
            return value;
        }

        private static string Fraction(Match match, int precision)
        {
            string fraction = match.Groups["fraction"].Value.TrimEnd('0');
            if (fraction.Length > precision)
            {
                throw new FormatException($"Fraction exceeds the supported {precision}-digit resolution.");
            }
            return fraction.Length == 0 ? string.Empty : "." + fraction;
        }

        public static DateTimeOffset DateTime(string lexical)
        {
            Match match = Instant.Match(lexical);
            if (!match.Success)
            {
                throw new FormatException("dateTime requires a timezone and years 0001 through 9999.");
            }
            string fraction = Fraction(match, 3);
            string clock = match.Groups["clock"].Value;
            bool endOfDay = clock == "24:00:00" && fraction.Length == 0;
            string normalized = match.Groups["date"].Value + "T" + (endOfDay ? "23:59:59" : clock) + fraction + match.Groups["zone"].Value;
            if (!DateTimeOffset.TryParseExact(normalized, ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFK"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset value))
            {
                throw new FormatException("Invalid native dateTime value.");
            }
            try
            {
                return endOfDay ? value.ToUniversalTime().AddSeconds(1) : value.ToUniversalTime();
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new FormatException("UTC dateTime is outside years 0001 through 9999.", exception);
            }
        }

        public static string DateTimeText(DateTimeOffset value)
        {
            if (value.Ticks % TimeSpan.TicksPerMillisecond != 0)
            {
                throw new FormatException("dateTime requires whole milliseconds.");
            }
            return value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.FFF'Z'", CultureInfo.InvariantCulture);
        }

        public static TimeOnly Time(string lexical)
        {
            Match match = Clock.Match(lexical);
            if (!match.Success)
            {
                throw new FormatException("time requires a local clock without a timezone.");
            }
            string fraction = Fraction(match, 6);
            string clock = match.Groups["clock"].Value;
            if (clock == "24:00:00" && fraction.Length == 0)
            {
                return TimeOnly.MinValue;
            }
            if (!TimeOnly.TryParseExact(clock + fraction, ["HH:mm:ss", "HH:mm:ss.FFFFFF"], CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly value))
            {
                throw new FormatException("Invalid local time.");
            }
            return value;
        }

        public static string TimeText(TimeOnly value)
        {
            if (value.Ticks % 10 != 0)
            {
                throw new FormatException("time requires whole microseconds.");
            }
            return value.ToString("HH:mm:ss.FFFFFF", CultureInfo.InvariantCulture);
        }

        public static TimeSpan Duration(string lexical)
        {
            Match match = Elapsed.Match(lexical);
            if (!match.Success)
            {
                throw new FormatException("duration requires an elapsed duration without year/month components.");
            }
            BigInteger milliseconds = BigInteger.Zero;
            foreach ((string name, long factor) in new[] { ("days", 86400000L), ("hours", 3600000L), ("minutes", 60000L) })
            {
                if (match.Groups[name].Success)
                {
                    milliseconds += BigInteger.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture) * factor;
                }
            }
            if (match.Groups["seconds"].Success)
            {
                string seconds = match.Groups["seconds"].Value;
                string[] parts = seconds.Split('.');
                string fraction = parts.Length == 2 ? parts[1].TrimEnd('0') : string.Empty;
                if (fraction.Length > 3)
                {
                    throw new FormatException("duration requires whole milliseconds.");
                }
                milliseconds += BigInteger.Parse(parts[0].Length == 0 ? "0" : parts[0], CultureInfo.InvariantCulture) * 1000;
                milliseconds += int.Parse(fraction.PadRight(3, '0'), CultureInfo.InvariantCulture);
            }
            if (milliseconds > MaximumDurationMilliseconds)
            {
                throw new FormatException("duration exceeds the common native duration range.");
            }
            long ticks = (long)milliseconds * TimeSpan.TicksPerMillisecond;
            return new TimeSpan(match.Groups["negative"].Success ? -ticks : ticks);
        }

        public static string DurationText(TimeSpan value)
        {
            if (value.Ticks % TimeSpan.TicksPerMillisecond != 0 ||
                value.Ticks / TimeSpan.TicksPerMillisecond is < -MaximumDurationMilliseconds or > MaximumDurationMilliseconds)
            {
                throw new FormatException("duration requires whole milliseconds in the common native duration range.");
            }
            return System.Xml.XmlConvert.ToString(value);
        }
    }
}
