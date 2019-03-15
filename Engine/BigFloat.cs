//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace OpenTap
{
    /// <summary> Arbitrary precision floating point numbers for exact numeric computations for when performance is not an issue. </summary>
    [TypeConverter(typeof(BigFloatConverter))]
    struct BigFloat : IFormattable, IComparable, IComparable<BigFloat>, IEquatable<BigFloat>
    {
        /// <summary> The numerator as an arbitrarily sized integer. </summary>
        BigInteger Numerator;
        /// <summary> The denominator as an arbitrarily sized integer. </summary>
        BigInteger Denominator;

        /// <summary> Creates a new BigFloat from fractional values. </summary>
        /// <param name="nominator"></param>
        /// <param name="denominator"></param>
        public BigFloat(BigInteger nominator, BigInteger denominator)
        {
            Numerator = nominator;
            Denominator = denominator;
            Normalize();
        }

        public BigFloat Invert()
        {
            return new BigFloat { Numerator = Denominator, Denominator = Numerator };
        }

        /// <summary> Normalizes the fraction by dividing by greates common divisor. </summary>
        /// <returns>The normalized fraction.</returns>
        public BigFloat Normalize()
        {
            if(Denominator == 0)
            {
                if(Numerator > 1)
                {
                    Numerator = 1;
                }else if(Numerator < -1)
                {
                    Numerator = -1;
                }
                return this;
            }
            else if (Denominator == Numerator)
            {
                Numerator = 1;
                Denominator = 1;
            }

            if (Denominator < 0)
            {
                Numerator *= -1;
                Denominator *= -1;
            }

            BigInteger gcd = BigInteger.GreatestCommonDivisor(Numerator, Denominator);
            if (Denominator == gcd)
            {
                Numerator = Numerator / gcd;
                Denominator = 1;
                return this;
            }
            Numerator = Numerator / gcd;
            Denominator = Denominator / gcd;
            return this;
        }
        static char getDigitSeparator(IFormatProvider prov)
        {
            if (prov == null)
                prov = CultureInfo.CurrentCulture;
            if(prov is CultureInfo)
            {
                return ((CultureInfo)prov).NumberFormat.NumberDecimalSeparator[0];
            }
            if(prov is NumberFormatInfo)
            {
                return ((NumberFormatInfo)prov).NumberDecimalSeparator[0];
            }
            return '.';
        }
        static string fracToString(BigInteger a, BigInteger b, IFormatProvider prov)
        {
            if (prov == null)
                prov = CultureInfo.CurrentCulture.NumberFormat;
            var numinfo = prov as NumberFormatInfo;
            var digits = (int)Math.Ceiling(BigInteger.Log10(a) - BigInteger.Log10(b));
            StringBuilder sb = new StringBuilder(0);
            
            if (digits < 0)
                sb.Append("0" + getDigitSeparator(prov));

            bool firstDigitFound = false;
            int firstDigit = 0;

            BigInteger ten = 10;

            while (digits >= 0 || firstDigitFound == false || (firstDigit - digits) < 15)
            {
                if (a == 0 && digits < 0)
                    break;
                BigInteger newb = b;
                BigInteger newa = a;
                if (digits >= 0)
                {
                    newb = b * BigInteger.Pow(ten, digits);
                }
                else if (digits < 0)
                {
                    a = a * 10;
                }
                var dig = (a / newb);
                if (dig != 0 && !firstDigitFound)
                {
                    firstDigitFound = true;
                    firstDigit = digits;
                }
                if (dig == 10)
                {
                    sb.Append("10");
                }
                else if (firstDigitFound || digits <= 0)
                {
                    sb.Append((char)('0' + (int)dig));
                }
                a = a - (dig * newb);
                if (digits == 0 && a != 0)
                    sb.Append(getDigitSeparator(prov));

                digits--;
            }

            return sb.ToString();
        }

        public string ToString(IFormatProvider prov)
        {
            if (prov == null)
                prov = CultureInfo.CurrentCulture.NumberFormat;

            var numberFormat = CultureInfo.CurrentCulture.NumberFormat;

            if (prov is CultureInfo) numberFormat = (prov as CultureInfo).NumberFormat;
            else if (prov is NumberFormatInfo) numberFormat = (prov as NumberFormatInfo);
            
            if (Denominator == 0)
            {
                if (Numerator == 0) return numberFormat.NaNSymbol;
                else if (Numerator == 1) return numberFormat.PositiveInfinitySymbol;
                else if (Numerator == -1) return numberFormat.NegativeInfinitySymbol;
            }

            if (Numerator == 0)
                return "0";
            
            if (Denominator < 0)
            {
                Numerator *= -1;
                Denominator *= -1;
            }

            var num = Numerator;
            bool isNegative = false;
            if (num < 0)
            {
                num = -num;
                isNegative = true;
            }

            var stringResult = fracToString(num, Denominator, prov);
            if (isNegative)
                return numberFormat.NegativeSign + stringResult;
            return stringResult;
        }

        /// <summary> Converts the fraction to a decimal string. </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ToString(null);
        }

        /// <summary> Compares two numbers. </summary>
        /// <param name="other"></param>
        /// <returns> True if they are equal. </returns>
        public bool Equals(BigFloat other)
        {
            return CompareTo(other) == 0;
        }

        /// <summary>
        /// Compares this bigfloat with another object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is BigFloat)
                return Equals((BigFloat)obj);
            return base.Equals(obj);
        }

        /// <summary>
        /// Gets the hash code of this value.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Numerator.GetHashCode() ^ Denominator.GetHashCode();
        }

        /// <summary> Compares two numbers. </summary>
        /// <param name="other"></param>
        /// <returns>-1 if other is less, 1 if other is greater and 0 if other is equal to this.</returns>
        public int CompareTo(BigFloat other)
        {
            if (Denominator == 0)
            {
                if (Numerator == 0)
                    return -1;

                if (other.Denominator == 0)
                {
                    if (other.Numerator == 0) // NaN
                        return -1;
                    return Numerator.CompareTo(other.Numerator); //compare negiative/positive infinities.
                }
                return Numerator > 0 ? 1 : -1; // infinity
            }

            var a = Numerator * other.Denominator;
            var b = other.Numerator * Denominator;
            
            return a.CompareTo(b);
        }

        /// <summary>
        /// Converts obj before doing comparison using CompareTo. Throws an exception if obj cannot be compared to a BigFloat.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int CompareTo(object obj)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");
            switch (obj)
            {
                case double d: return CompareTo(new BigFloat(d));
                case short d: return CompareTo(new BigFloat(d));
                case int d: return CompareTo(new BigFloat(d));
                case float d: return CompareTo(new BigFloat(d));
                case decimal d: return CompareTo(new BigFloat(d));
                case uint d: return CompareTo(new BigFloat(d));
                case BigFloat d: return CompareTo(d);
                default:
                    throw new InvalidCastException(string.Format("Unable to compare BigFloat to {0}", obj.GetType()));
            }
        }

        /// <summary> Big float 1. </summary>
        public static readonly BigFloat One = new BigFloat(BigInteger.One, BigInteger.One);

        /// <summary> Supports parsing BigFloat without throwing an exception. Returns an exception in case something went wrong otherwise it will return a BigFloat.</summary>
        /// <param name="value"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        internal static object Parse(string value, IFormatProvider format = null)
        {
            if (string.Equals(value, "infinity", StringComparison.InvariantCultureIgnoreCase))
            {
                return Infinity;
            }
            else if (string.Equals(value, "-infinity", StringComparison.InvariantCultureIgnoreCase))
            {
                return NegativeInfinity;
            }
            else if (string.Equals(value, "nan", StringComparison.InvariantCultureIgnoreCase))
            {
                return NaN;
            }
            else if (value.Contains("/"))
            {
                var splitted = value.Split('/');
                if (splitted.Length != 2)
                {
                    return new FormatException("value contains multiple '/'");
                }
                var numerator = BigInteger.Parse(splitted[0]);
                var denominator = BigInteger.Parse(splitted[1]);
                return new BigFloat(numerator, denominator);
            }
            else
            {
                var sep = getDigitSeparator(format);
                int index = 0;
                bool dotHit = false;
                bool exp = false;
                BigInteger sign = 1;
                BigFloat mantissa = 0;
                BigInteger denom = 1;
                BigInteger nomerator = 0;
                for (; index < value.Length; index++)
                {
                    var chr = value[index];
                    if (chr == '-' && nomerator == 0 && sign == 1)
                    {
                        sign = -1;
                        continue;
                    }
                    if (chr == sep && !dotHit && !exp)
                    {
                        dotHit = true;
                        continue;
                    }
                    if ((chr == 'e' || chr == 'E') && !exp)
                    {
                        exp = true;
                        mantissa = new BigFloat(nomerator * sign, denom);
                        denom = 1;
                        nomerator = 0;
                        dotHit = false;
                        sign = 1;
                        continue;
                    }
                    if (char.IsWhiteSpace(chr)) continue;
                    if ((chr == '+') && exp) continue;
                    if (char.IsDigit(chr) == false)
                        return new FormatException("Format not supported.");
                    if (dotHit)
                        denom *= 10;
                    int v = (chr - '0');
                    nomerator = nomerator * 10 + v;
                }
                
                if (exp)
                {
                    var powerTerm = BigInteger.Pow(10, (int)(nomerator));
                    if (sign < 0)
                    {
                        return mantissa * new BigFloat(1, powerTerm);
                    }
                    else
                    {
                        return mantissa * new BigFloat(powerTerm, 1);
                    }

                }
                return new BigFloat(nomerator * sign, denom);
            }
        }

        /// <summary> Big float 0. </summary>
        public static readonly BigFloat Zero = new BigFloat(BigInteger.Zero, BigInteger.One);
        /// <summary> Big float Infinity. </summary>
        public static readonly BigFloat Infinity = new BigFloat(BigInteger.One, BigInteger.Zero);
        /// <summary> Big float negative infinity. </summary>
        public static readonly BigFloat NegativeInfinity = new BigFloat(BigInteger.MinusOne, BigInteger.Zero);
        /// <summary> Big float not a number. </summary>
        public static readonly BigFloat NaN = new BigFloat(BigInteger.Zero, BigInteger.Zero);

        /// <summary>
        /// Converts this value to a string.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="formatProvider"></param>
        /// <returns></returns>
        public string ToString(string format, IFormatProvider formatProvider)
        {
            return ToString(formatProvider);
        }
        static bool fastButImpreciseEnabled = false;
        /// <summary> Creates a BigFloat. </summary>
        /// <param name="value"></param>
        public BigFloat(double value) : this(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
        {
            if (fastButImpreciseEnabled)
            { 
                if (value == 0)
                {
                    Numerator = 0;
                    Denominator = 1;
                    return;
                }
                double tolerance = 1.0E-9;
                double orig = value;
                var bc = BigFloat.Zero;
                BigInteger frac = BigInteger.One;
                double frac2 = 1.0;
                while (true)
                {
                    if (Math.Abs(value / orig) < tolerance)
                        break;
                    double a = Math.Round(value);
                    value -= a;

                    var fc = new BigFloat((BigInteger)a, frac);
                    bc = bc + fc;
                    frac2 *= 1000;
                    value *= 1000.0;
                    orig *= 1000.0;
                    frac *= 1000;
                }

                this = bc.Normalize();
            }
        }

        public BigFloat(BigInteger v):this(v, BigInteger.One)
        {

        }

        public BigFloat(float value) : this(value.ToString("r", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) { }

        public BigFloat(decimal value) : this(value.ToString("F9", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
        {
        }
        public BigFloat(string value, IFormatProvider format = null)
        {
            var result = Parse(value, format);
            if (result is BigFloat bf)
            {
                this = bf;
            }
            else throw (Exception)result;
        }

        private bool IsNan { get { return (Denominator == 0) && (Numerator == 0); } }
        private bool IsPosInf { get { return (Denominator == 0) && (Numerator == 1); } }
        private bool IsNegInf { get { return (Denominator == 0) && (Numerator == -1); } }

        public BigFloat(int value) : this(value, 1) { }
        public BigFloat(uint value) : this(value, 1) { }
        public BigFloat(short value) : this(value, 1) { }
        public BigFloat(long value) : this(value, 1) { }
        public static implicit operator BigFloat(double d)
        {
            return new BigFloat(d);
        }

        public static implicit operator BigFloat(long d)
        {
            return new BigFloat(d, 1);
        }

        public static implicit operator BigFloat(int d)
        {
            return new BigFloat(d, 1);
        }
        
        public static explicit operator int(BigFloat d)
        {
            return (int)d.Rounded();
        }

        public static explicit operator double(BigFloat d)
        {
            return ((double)d.Numerator) / ((double)d.Denominator);
        }

        public static bool operator ==(BigFloat a, BigFloat b)
        {
            if ((a.Denominator == 0) || (b.Denominator == 0))
            {
                if (a.IsNan || b.IsNan)
                    return false;
                else
                    return (a.Denominator == b.Denominator) && (a.Numerator == b.Numerator);
            }
            else
                return a.Equals(b);
        }

        public static bool operator !=(BigFloat a, BigFloat b)
        {
            if ((a.Denominator == 0) || (b.Denominator == 0))
            {
                if (a.IsNan || b.IsNan)
                    return true;
                else
                    return (a.Denominator != b.Denominator) || (a.Numerator != b.Numerator);
            }
            else
                return a.Equals(b) == false;
        }

        public static bool operator ==(BigFloat a, double b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(BigFloat a, double b)
        {
            return a.Equals(b) == false;
        }

        public static bool operator >=(BigFloat a, BigFloat b)
        {
            if ((a.Denominator == 0) || (b.Denominator == 0))
            {
                if (a.IsNan || b.IsNan) return false;
                else if (a.IsNegInf) return b.IsNegInf;
                else if (b.IsPosInf) return a.IsPosInf;
                else return true;
            }
            else
                return a.CompareTo(b) >= 0;
        }

        public static bool operator <=(BigFloat a, BigFloat b)
        {
            if ((a.Denominator == 0) || (b.Denominator == 0))
            {
                if (a.IsNan || b.IsNan) return false;
                else if (a.IsPosInf) return b.IsPosInf;
                else if (b.IsNegInf) return a.IsNegInf;
                else return true;
            }
            else
                return a.CompareTo(b) <= 0;
        }

        public static bool operator >(BigFloat a, BigFloat b)
        {
            if ((a.Denominator == 0) || (b.Denominator == 0))
            {
                if (a.IsNan || b.IsNan)
                    return false;
                else if (a.IsPosInf) return !b.IsPosInf;
                else if (b.IsNegInf) return !a.IsNegInf;
                else
                    return false;
            }
            else
                return a.CompareTo(b) > 0;
        }

        public static bool operator <(BigFloat a, BigFloat b)
        {
            if ((a.Denominator == 0) || (b.Denominator == 0))
            {
                if (a.IsNan || b.IsNan) return false;
                else if (a.IsNegInf) return !b.IsNegInf;
                else if (b.IsPosInf) return !a.IsPosInf;
                else return false;
            }
            else
                return a.CompareTo(b) < 0;
        }
        
        public BigFloat Sign()
        {
            if (Numerator >= 0)
                return new BigFloat(1, 1);
            return new BigFloat(-1, 1);
        }

        public static BigFloat operator +(BigFloat a, BigFloat b)
        {
            if (a.IsNan || b.IsNan) return NaN;

            if (a.Denominator != b.Denominator)
            {
                var adenum = a.Denominator;

                a.Denominator *= b.Denominator;
                a.Numerator *= b.Denominator;

                b.Denominator *= adenum;
                b.Numerator *= adenum;
            }
            return new BigFloat(a.Numerator + b.Numerator, b.Denominator).Normalize();
        }

        public static BigFloat operator -(BigFloat a, BigFloat b)
        {
            if (a.IsNan || b.IsNan) return NaN;

            if (a.Denominator != b.Denominator)
            {
                var adenum = a.Denominator;

                a.Denominator *= b.Denominator;
                a.Numerator *= b.Denominator;

                b.Denominator *= adenum;
                b.Numerator *= adenum;
            }
            return new BigFloat(a.Numerator - b.Numerator, b.Denominator).Normalize();
        }
        public static BigFloat operator /(BigFloat a, BigFloat b)
        {
            if (a.IsNan || b.IsNan) return NaN;
            else if (a.IsNegInf)
            {
                if (b.IsPosInf || b.IsNegInf) return NaN;
                else if (b < 0) return Infinity;
            }
            else if (a.IsPosInf)
            {
                if (b.IsPosInf || b.IsNegInf) return NaN;
                else if (b < 0) return NegativeInfinity;
            }

            return new BigFloat(a.Numerator * b.Denominator, b.Numerator * a.Denominator).Normalize();
        }

        public static BigFloat operator *(BigFloat a, BigFloat b)
        {
            if (a.IsNan || b.IsNan) return NaN;

            return new BigFloat(a.Numerator * b.Numerator, a.Denominator * b.Denominator).Normalize();
        }

        public BigFloat Abs()
        {
            return new BigFloat(Numerator * Numerator.Sign, Denominator * Denominator.Sign);
        }

        public BigInteger Rounded()
        {
            if (Denominator == 0) return int.MinValue;
            if (Numerator == 0) return 0;

            return Numerator / Denominator;
        }

        public BigFloat Round()
        {
            if (IsNan || IsNegInf || IsPosInf) return this;

            return new BigFloat(Rounded(), 1);
        }
        
        public static BigFloat Convert(object y, IFormatProvider prov = null)
        {
            switch (y)
            {
                case BigFloat x: return x;
                case double x: return new BigFloat(x);
                case float x: return new BigFloat(x);
                case decimal x: return new BigFloat(x);
                case int x: return new BigFloat(x);
                case long x: return new BigFloat(x);
                case string x: return new BigFloat(x, prov);
                default: return new BigFloat((long)System.Convert.ChangeType(y, typeof(long)));
            }
        }

        public object ConvertTo(Type t)
        {
            Normalize();

            if (t == typeof(BigFloat))
                return this;
            if (t == typeof(double))
                return (((double)Numerator) / ((double)Denominator));
            if (t == typeof(float))
                return (float)((double)((double)Numerator) / ((double)Denominator));
            if (t == typeof(decimal))
                return ((decimal)Numerator) / ((decimal)Denominator);
            if (t == typeof(int))
                return (int)(Numerator / Denominator);
            if (t == typeof(long))
                return (long)(Numerator / Denominator);
            if (t == typeof(string))
                return ToString();
            if (typeof(BigFloat).DescendsTo(t))
                return this;
            return System.Convert.ChangeType(ConvertTo(typeof(long)), t);
        }

        public static BigFloat operator -(BigFloat a)
        {
            return new BigFloat(-a.Numerator, a.Denominator);
        }
    }

    /// <summary> Converter for OpenTAP arbitrary precision number types. </summary>
    class BigFloatConverter : TypeConverter
    {
        /// <summary> returns  true if it can convert from </summary>
        /// <param name="context"></param>
        /// <param name="sourceType"></param>
        /// <returns></returns>
        public override bool CanConvertFrom(ITypeDescriptorContext context,
           Type sourceType)
        {
            if (sourceType.IsNumeric() || sourceType == typeof(string) || sourceType == typeof(BigFloat))
                return true;
            return base.CanConvertFrom(context, sourceType);
        }

        /// <summary>
        /// Converts from an object to a number.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="culture"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public override object ConvertFrom(ITypeDescriptorContext context,
           CultureInfo culture, object value)
        {
            return BigFloat.Convert(value);
        }

        /// <summary>
        /// Converts to a number.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="culture"></param>
        /// <param name="value"></param>
        /// <param name="destinationType"></param>
        /// <returns></returns>
        public override object ConvertTo(ITypeDescriptorContext context,
           CultureInfo culture, object value, Type destinationType)
        {
            return ((BigFloat)value).ConvertTo(destinationType);
        }
    }
}
