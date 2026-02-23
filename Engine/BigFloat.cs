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
        /// <summary> Big float 0. </summary>
        public static readonly BigFloat Zero = new(BigInteger.Zero, BigInteger.One);
        /// <summary> Big float Infinity. </summary>
        public static readonly BigFloat Infinity = new(BigInteger.One, BigInteger.Zero);
        /// <summary> Big float negative infinity. </summary>
        public static readonly BigFloat NegativeInfinity = new(BigInteger.MinusOne, BigInteger.Zero);
        /// <summary> Big float not a number. </summary>
        public static readonly BigFloat NaN = new(BigInteger.Zero, BigInteger.Zero);
        /// <summary> Big float 1. </summary>
        public static readonly BigFloat One = new(BigInteger.One, BigInteger.One);
        /// <summary> Big float -1. </summary>
        public static readonly BigFloat NegativeOne = new(BigInteger.MinusOne, BigInteger.One);
        /// <summary> Big float 0.5. </summary>
        public static readonly BigFloat Half = new(BigInteger.One, 2);
        
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

        
        public BigFloat(long nominator, long denominator)
        {
            if (denominator == 1)
            {
                Numerator = nominator;
                Denominator = BigInteger.One;
            }
            else
            {
                Numerator = nominator;
                Denominator = denominator;
                Normalize();
            }
        }

        public BigFloat Invert()
        {
            return new BigFloat { Numerator = Denominator, Denominator = Numerator };
        }

        /// <summary> Normalizes the fraction by dividing by greates common divisor. </summary>
        /// <returns>The normalized fraction.</returns>
        public BigFloat Normalize()
        {
            if (Denominator.IsOne)
                return this;
            if(Denominator == 0)
            {
                if(Numerator > 1)
                    return Infinity;
                if(Numerator < -1)
                    return NegativeInfinity;
                return NaN;
            }
            if (Denominator == Numerator)
                return One;

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
            switch (prov)
            {
                case null:
                    return CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
                case CultureInfo info:
                    return info.NumberFormat.NumberDecimalSeparator[0];
                case NumberFormatInfo formatInfo:
                    return formatInfo.NumberDecimalSeparator[0];
            }
            return '.';
        }

        static string fracToString(BigInteger a, BigInteger b, IFormatProvider prov)
        {
            var sb = new StringBuilder();
            fracToString(sb, a, b, prov);
            return sb.ToString();
        }
        
        static void fracToString(StringBuilder sb, BigInteger a, BigInteger b, IFormatProvider prov)
        {
            if (prov == null)
                prov = CultureInfo.CurrentCulture.NumberFormat;
            var digits = (int)Math.Ceiling(BigInteger.Log10(a) - BigInteger.Log10(b));
            
            if (digits < 0)
                sb.Append("0" + getDigitSeparator(prov));

            bool firstDigitFound = false;
            int firstDigit = 0;

            BigInteger ten = 10;
            
            // the most precise number we know of is a decimal, which uses G29 for roundtrip conversion. 
            const int maxSignificantDigits = 29;
            
            while (digits >= 0 || firstDigitFound == false || (firstDigit - digits) < maxSignificantDigits)
            {
                if (a == 0 && digits < 0)
                    break;
                BigInteger newb = b;
                
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
        }

        public void AppendTo(StringBuilder sb, IFormatProvider prov)
        {
            if (prov == null)
                prov = CultureInfo.CurrentCulture.NumberFormat;

            var numberFormat = CultureInfo.CurrentCulture.NumberFormat;

            if (prov is CultureInfo) numberFormat = (prov as CultureInfo).NumberFormat;
            else if (prov is NumberFormatInfo) numberFormat = (prov as NumberFormatInfo);
            
            if (Denominator == 0)
            {
                if (Numerator == 0){ 
                    sb.Append(numberFormat.NaNSymbol);
                    return;
                }
                else if (Numerator == 1)
                {
                    sb.Append(numberFormat.PositiveInfinitySymbol);
                    return;
                }
                else if (Numerator == -1)
                {
                    sb.Append(numberFormat.NegativeInfinitySymbol);
                    return;
                }
            }

            if (Numerator == 0)
            {
                sb.Append("0");
                return;
            }

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

            if (isNegative)
                sb.Append(numberFormat.NegativeSign);

            fracToString(sb, num, Denominator, prov);
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


        internal static BigFloat Parse(string value, IFormatProvider format, out Exception outEx) => Parse(value.AsSpan(), format, out outEx);

        /// <summary> Supports parsing BigFloat without throwing an exception. Returns an exception in case something went wrong otherwise it will return a BigFloat.</summary>
        internal static BigFloat Parse(ReadOnlySpan<char> value, IFormatProvider format, out Exception outEx)
        {
            outEx = null;
            try
            {
                // the only exception ParseWithLongChecked will throw is an overflow exception.
                // in that case, we use ParseWithBigInt since that can handle arbitrary number sizes values.
                var result = ParseWithLongChecked(value, format, out outEx);
                if (outEx is FormatException)
                {
                    if (value.Equals("infinity", StringComparison.OrdinalIgnoreCase))
                    {
                        outEx = null;
                        return Infinity;
                    }
                    if (value.Equals("-infinity", StringComparison.OrdinalIgnoreCase))
                    {
                        outEx = null;
                        return NegativeInfinity;
                    }
                    if (value.Equals("nan", StringComparison.OrdinalIgnoreCase))
                    {
                        outEx = null;
                        return NaN;
                    }
                }
                return result;
            }
            catch (OverflowException)
            {
                // only parse the slow way if we get an overflow exception parsing the fast way.
                return ParseWithBigInt(value, format, out outEx);
            }
        }
        
        static BigFloat ParseWithLongChecked(ReadOnlySpan<char> value, IFormatProvider format, out Exception outEx)
        {
            var sep = getDigitSeparator(format);
            
            bool dotHit = false;
            bool exp = false;
            long sign = 1;
            BigFloat mantissa = Zero;
            long denom = 1;
            long numerator = 0;
            
            
            // we use overflow checking to figure out if we can parse the fast way
            // using longs or parse the slow way using BigInteger. 
            // if an OverflowException gets thrown we default to using BigInteger for parsing.
            checked
            {
                foreach (var chr in value)
                {
                    if (chr is >= '0' and <= '9')
                    {
                        if (dotHit)
                            denom *= 10;
                        int v = chr - '0';
                        numerator = numerator * 10 + v;
                        continue;
                    }
                    
                    if (chr == '-' && numerator == 0 && sign == 1)
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
                        mantissa = new BigFloat(numerator * sign, denom);
                        denom = 1;
                        numerator = 0;
                        dotHit = false;
                        sign = 1;
                        continue;
                    }

                    if (char.IsWhiteSpace(chr)) continue;
                    if ((chr == '+') && exp) continue;
                    
                    // all likely cases exhausted. only invalid options left.
                    outEx = new FormatException("Format not supported.");
                    return NaN;
                }

                outEx = null;
                if (exp)
                {
                    //The scientific format "xEy" was detected, e.g 1.5e6
                    if (numerator > 1000)
                    {   // Extremely large numbers will cause the application to stall
                        // the biggest number in .NET is double.Infinty: 1.7976931348623157E+308
                        // so 1E+1000 is probably an ok max limit.
                        outEx = new FormatException($"The value {value.ToString()} is too huge or too precise to be presented as a number.");
                        return NaN;
                    }

                    var powerTerm = BigInteger.Pow(10, (int)numerator);
                    if (sign < 0)
                        return mantissa * new BigFloat(1, powerTerm);
                    return mantissa * new BigFloat(powerTerm);
                }
                
                return new BigFloat(numerator * sign, denom);
            }
        }
        
        static BigFloat ParseWithBigInt(ReadOnlySpan<char> value, IFormatProvider format, out Exception outEx)
        {
            outEx = null;
            var sep = getDigitSeparator(format);
            int index = 0;
            bool dotHit = false;
            bool exp = false;
            long sign = 1;
            BigFloat mantissa = Zero;
            BigInteger denom = 1;
            BigInteger nomerator = 0;
            checked
            {

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
                    {
                        outEx = new FormatException("Format not supported.");
                        return NaN;
                    }

                    if (dotHit)
                        denom *= 10;
                    int v = (chr - '0');
                    nomerator = nomerator * 10 + v;
                }
            

            if (exp)
            {
                //The scientific format "xEy" was detected, e.g 1.5e6
                if (nomerator > 1000)
                {   // Extremely large numbers will cause the application to stall
                    // the biggest number in .NET is double.Infinty: 1.7976931348623157E+308
                    // so 1E+1000 is probably an ok max limit.
                    outEx = new FormatException($"The value {value.ToString()} is too huge or too precise to be presented as a number.");
                    return NaN;
                }

                var powerTerm = BigInteger.Pow(10, (int)nomerator);
                if (sign < 0)
                    return mantissa * new BigFloat(1, powerTerm);
                return mantissa * new BigFloat(powerTerm);
            }
            
            return new BigFloat(nomerator * sign, denom);
            }
        }

        
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
        
        /// <summary> Creates a BigFloat. </summary>
        /// <param name="value"></param>
        public BigFloat(double value) 
        {
            if (value == 1.0)
            {
                this = One;
            }
            else if (value == 0.0)
            {
                this = Zero;
            }
            else
            {
                this = new BigFloat(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            }
        }

        public BigFloat(BigInteger v)
        {
            Numerator = v;
            Denominator = BigInteger.One;
        }

        public BigFloat(float value) : this(value.ToString("r", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) { }

        public BigFloat(decimal value) : this(value.ToString("G29", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
        {
        }

        internal BigFloat(string value, IFormatProvider format = null) : this(value.AsSpan(), format)
        {
        }

        public BigFloat(ReadOnlySpan<char> value, IFormatProvider format = null)
        {
            var result = Parse(value, format, out var ex);
            if (ex != null)
                throw ex;
            this = result;
        }

        private bool IsNan => (Denominator == 0) && (Numerator == 0);
        private bool IsPosInf => (Denominator == 0) && (Numerator == 1);
        private bool IsNegInf => (Denominator == 0) && (Numerator == -1);

        public BigFloat(int value) : this((BigInteger)value) { }
        public BigFloat(uint value) : this((BigInteger)value) { }
        public BigFloat(short value) : this((BigInteger)value) { }
        public BigFloat(long value) : this((BigInteger)value) { }
        public static implicit operator BigFloat(double d)
        {
            return new BigFloat(d);
        }

        public static implicit operator BigFloat(long d)
        {
            return new BigFloat(d);
        }

        public static implicit operator BigFloat(int d)
        {
            if (d == 0) return Zero;
            return new BigFloat(d);
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
            bool eq = a.Numerator == b.Numerator && a.Denominator == b.Denominator;
            if (eq)
                return !a.IsNan;
            return false;
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
                if (a.IsNegInf) return b.IsNegInf;
                if (b.IsPosInf) return a.IsPosInf;
                return true;
            }
            else
                return a.CompareTo(b) >= 0;
        }

        public static bool operator <=(BigFloat a, BigFloat b)
        {
            if ((a.Denominator == 0) || (b.Denominator == 0))
            {
                if (a.IsNan || b.IsNan) return false;
                if (a.IsPosInf) return b.IsPosInf;
                if (b.IsNegInf) return a.IsNegInf;
                return true;
            }
            return a.CompareTo(b) <= 0;
        }

        public static bool operator >(BigFloat a, BigFloat b)
        {
            if ((a.Denominator == 0) || (b.Denominator == 0))
            {
                if (a.IsNan || b.IsNan)
                    return false;
                if (a.IsPosInf) return !b.IsPosInf;
                if (b.IsNegInf) return !a.IsNegInf;
                return false;
            }
            return a.CompareTo(b) > 0;
        }

        public static bool operator <(BigFloat a, BigFloat b)
        {
            if ((a.Denominator == 0) || (b.Denominator == 0))
            {
                if (a.IsNan || b.IsNan) return false;
                if (a.IsNegInf) return !b.IsNegInf;
                if (b.IsPosInf) return !a.IsPosInf;
                return false;
            }
            return a.CompareTo(b) < 0;
        }
        
        public BigFloat Sign()
        {
            if (Numerator >= 0)
                return One;
            return NegativeOne;
        }

        public static BigFloat operator +(BigFloat a, BigFloat b)
        {
            if (a.IsNan || b.IsNan) return NaN;
            if (a.IsZero) return b;
            if (b.IsZero) return a;
            
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

        public bool IsZero => Numerator.IsZero;

        public static BigFloat operator -(BigFloat a, BigFloat b)
        {
            if (a.Numerator == 0)
            {
                if (a.Denominator == 0) return NaN;
                if(a.IsZero) return new BigFloat(-b.Numerator, b.Denominator);
            }
            if (b.Numerator == 0)
            {
                if (b.Denominator == 0) return NaN;
                if (b.IsZero) return a;
            }

            if (a.Denominator != b.Denominator)
            {
                var adenum = a.Denominator;

                a.Denominator *= b.Denominator;
                a.Numerator *= b.Denominator;

                b.Denominator *= adenum;
                b.Numerator *= adenum;
            }

            if (a.Numerator == b.Numerator)
            {
                if (a.Denominator == 0 || b.Denominator == 0) return NaN;
                return Zero;
            }

            return new BigFloat(a.Numerator - b.Numerator, b.Denominator);
        }

        public static BigFloat operator /(BigFloat a, BigFloat b)
        {
            if (b.Numerator == 1)
            {
                if (b == One) return a;
            }else if (b.IsNan)
                return NaN;

            if (a.Numerator == 0)
            {
                if (a.Denominator == 0) 
                    return NaN;
                if (b.IsZero) return NaN;
                return Zero;
            }
            if (a.Denominator == 0)
            {
                if (a.IsNegInf)
                {
                    if (b.IsPosInf || b.IsNegInf) return NaN;
                    else if (b < 0) return Infinity;
                    return a;
                }
                else if (a.IsPosInf)
                {
                    if (b.IsPosInf || b.IsNegInf) return NaN;
                    if (b < 0) return NegativeInfinity;
                    return a;
                }    
            }
            else if (b.Numerator == 0)
            {
                if (b.Denominator == 0) 
                    return NaN;
                return a.Numerator > 0 ? Infinity : NegativeInfinity;
            }

            return new BigFloat(a.Numerator * b.Denominator, b.Numerator * a.Denominator);
        }

        public static BigFloat operator *(BigFloat a, BigFloat b)
        {
            if (b.Denominator == 1)
            {
                if (b.Numerator == 1)
                    return a; // this is a very common case in parsing.
                if (b.Numerator == -1)
                    return new BigFloat(-a.Numerator, a.Denominator);
                return new BigFloat(a.Numerator * b.Numerator, a.Denominator);
            }
            
            if (a.IsNan || b.IsNan) return NaN;
            if (a.IsZero)
            {
                if (b.IsPosInf || b.IsNegInf) return NaN;
                return Zero;
            }

            if (b.IsZero)
            {
                if (a.IsPosInf || a.IsNegInf) return NaN;
                return Zero;
            }
            
            return new BigFloat(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
        }

        public BigFloat Abs()
        {
            return new BigFloat(Numerator * Numerator.Sign, Denominator * Denominator.Sign);
        }

        public BigInteger Rounded()
        {
            if (Denominator == 0) return int.MinValue;
            if (Numerator == 0) return 0;
            if (Denominator == 1) return Numerator;

            return Numerator / Denominator;
        }

        public BigFloat Round()
        {
            if (IsNan || IsNegInf || IsPosInf) return this;

            return new BigFloat(Rounded());
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
