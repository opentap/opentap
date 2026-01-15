//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

namespace OpenTap
{
    class Range : IEnumerable<BigFloat>
    {
        public readonly BigFloat Start;
        public readonly BigFloat Stop;
        public readonly BigFloat Step;

        public IEnumerator<BigFloat> GetEnumerator()
        {
            var start = Start;
            var stop = Stop;
            var step = Step;

            if (Start == Stop)
            { // A a range of 0 elements will be created otherwise. So return Start.
                yield return Start;
                yield break;
            }
            // This calculation is based on linear extrapolation that includes the end point 
            // on the condition that it's within double.epsilon
            var approximate_steps = ((stop - start) / step).Abs();

            // Cancel the last step if it's the calculated spot is more than double.epsilon further away.
            BigInteger steps = approximate_steps.Rounded();
            for (BigInteger i = 0; i <= steps; i++)
            {
                yield return new BigFloat(i) * step + start;
            }
        }

        public Range(BigFloat start, BigFloat stop)
        {
            Start = start;
            Stop = stop;
            Step = (Stop - Start).Sign();
            CheckRange();
        }

        public Range(BigFloat start, BigFloat stop, BigFloat step)
        {
            Start = start;
            Stop = stop;
            Step = step;
            CheckRange();
        }

        public void CheckRange()
        {
            if (Stop == Start) return; // Math.Sign(0) == 0.
            if (Step == 0 || (Stop - Start).Sign() != Step.Sign())
                throw new Exception("Infinite range not supported.");
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return ToString(null);
        }

        public string ToString(Func<object, string> conv)
        {
            conv ??= ((v) => v.ToString());

            if (Step == BigFloat.One || Step == BigFloat.NegativeOne)
                return $"{conv(Start)} : {conv(Stop)}";
            return $"{conv(Start)} : {conv(Step)} : {conv(Stop)}";
        }
    }

    class UnitFormatter
    {
        static BigFloat peta = 1000000000000000L;
        static BigFloat tera = 1000000000000L;
        static BigFloat giga = 1000000000L;
        static BigFloat mega = 1000000;
        static BigFloat kilo = 1000;
        static BigFloat one = 1;
        static BigFloat mili = kilo.Invert();
        static BigFloat micro = mega.Invert();
        static BigFloat nano = giga.Invert();
        static BigFloat pico = tera.Invert();
        static BigFloat femto = peta.Invert();


        static BigFloat engineeringPrefixLevel(char l)
        {
            switch (l)
            {
                case 'T': return tera;
                case 'G': return giga;
                case 'M': return mega;
                case 'k': return kilo;
                case ' ': return one;
                case 'm': return mili;
                case 'u': return micro;
                case 'n': return nano;
                case 'p': return pico;
                case 'f': return femto;
                default: throw new Exception("Invalid engineering prefix");
            }
        }
        static char[] levels = { 'T', 'G', 'M', 'k', ' ', 'm', 'u', 'n', 'p', 'f' };

        static char findLevel(BigFloat value)
        {
            foreach (char level in levels)
            {
                var eng = engineeringPrefixLevel(level);
                if (value >= eng || value <= -eng)
                {
                    return level;
                }
            }
            return ' ';
        }

        public static void Format(StringBuilder sb, BigFloat value, bool prefix, string unit, string format, CultureInfo culture,
            bool compact = false)
        {
            char level = prefix ? findLevel(value) : ' ';
            BigFloat scaling = engineeringPrefixLevel(level);
            BigFloat post_scale = value / scaling;

            if (string.IsNullOrEmpty(format))
                post_scale.AppendTo(sb, culture);
            else
            {
                if (format.StartsWith("x", StringComparison.OrdinalIgnoreCase))
                    sb.Append(((long)post_scale.Rounded()).ToString(format, culture));
                else if (format.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    sb.Append("0x" + ((long)post_scale.Rounded()).ToString(format.Substring(1), culture));
                else
                {
                    try
                    {
                        sb.Append(((decimal)post_scale.ConvertTo(typeof(decimal))).ToString(format, culture));
                    }
                    catch
                    {
                        post_scale.AppendTo(sb, culture);
                    }
                }
            }

            if (level == ' ' && string.IsNullOrEmpty(unit))
            {
                return;
            }

            string space = compact ? "" : " ";
            sb.Append(space);
            if (level != ' ')
                sb.Append(level);
            if(unit != null)
                sb.Append(unit);
        }

        public static string Format(BigFloat value, bool prefix, string unit, string format, CultureInfo culture, bool compact = false)
        {
            char level = prefix ? findLevel(value) : ' ';
            BigFloat scaling = engineeringPrefixLevel(level);
            BigFloat post_scale = value / scaling;
            string final_string;

            if (string.IsNullOrEmpty(format))
                final_string = post_scale.ToString("R", culture);
            else
            {
                if (format.StartsWith("x", StringComparison.OrdinalIgnoreCase))
                    final_string = ((long)post_scale.Rounded()).ToString(format, culture);
                else if (format.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    final_string = "0x" + ((long)post_scale.Rounded()).ToString(format.Substring(1), culture);
                else
                {
                    try
                    {
                        final_string = ((decimal)post_scale.ConvertTo(typeof(decimal))).ToString(format, culture);
                    }
                    catch
                    {
                        final_string = post_scale.ToString(format, culture);
                    }
                }
            }

            if (level == ' ' && string.IsNullOrEmpty(unit))
            {
                return final_string;
            }

            string space = compact ? "" : " ";

            if (level == ' ')
                return string.Format("{0}{2}{1}", final_string, unit, space);
            else
                return string.Format("{0}{3}{1}{2}", final_string, level, unit ?? "", space);
        }
        static BigFloat ParseInternal(ReadOnlySpan<char> str, string unit, string format, CultureInfo culture, out Exception ex)
        {
            ReadOnlySpan<char> strSpan = str.Trim();
            if (strSpan.Length == 0)
            {
                ex = new FormatException("Invalid format");
                return BigFloat.NaN;
            }
            
            
            int hexSkip = 0;
            ex = null;

            BigFloat multiplier;
            // handle the minus sign.
            if (strSpan[0] == '-')
            {
                multiplier = BigFloat.NegativeOne;
                strSpan = strSpan.Slice(1);
            }
            else
            {
                multiplier = BigFloat.One;
            }
            
            var isHex = strSpan.StartsWith("0x", StringComparison.OrdinalIgnoreCase);

            if (isHex)
            {
                hexSkip = 2;
            }
            else if (format.StartsWith("x", StringComparison.OrdinalIgnoreCase))
            {
                isHex = true;
                hexSkip = 0;
            }
            

            if (unit.Length > 0 && strSpan.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
                strSpan = strSpan.Slice(0, strSpan.Length - unit.Length);
            strSpan = strSpan.TrimEnd();
            if (strSpan.Length == 0)
            {
                ex = new FormatException("Invalid format");
                return BigFloat.NaN;
            }

            char siPrefix = strSpan[strSpan.Length - 1];
            
            if (char.IsLetter(siPrefix) && levels.Contains(siPrefix))
            {
                // Handle case of "femto" unit
                if (!(isHex && (siPrefix == 'f') && !strSpan.EndsWith(" f", StringComparison.Ordinal)))
                {
                    multiplier *= engineeringPrefixLevel(siPrefix);
                    if (strSpan[strSpan.Length - 1] == siPrefix)
                        strSpan = strSpan.Slice(0, strSpan.Length - 1).TrimEnd();
                }
            }

            if (isHex)
            {
                return new BigFloat(BigInteger.Parse("0" + strSpan.Slice(hexSkip).ToString(), NumberStyles.HexNumber,
                    culture)) * multiplier;
            }

            if (strSpan.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            {
                ReadOnlySpan<char> bits = strSpan.Slice(2);
                
                BigInteger v = 0;
                foreach (var bit in bits)
                {
                    if (bit == '1')
                    {
                        v = v << 1 | 1;
                    }
                    else if (bit == '0')
                    {
                        v = v << 1;
                    }
                    else if (char.IsWhiteSpace(bit))
                        continue;
                    else
                    {
                        ex = new FormatException("Invalid binary format.");
                        return BigFloat.NaN;
                    }
                }
                return new BigFloat(v) * multiplier;
            }
            var r = BigFloat.Parse(strSpan, culture, out ex);
            return r * multiplier;
        }
        
        public static BigFloat Parse(string str, string unit, string format, CultureInfo culture)
        {
            var result = ParseInternal(str, unit, format, culture, out var ex);
            if (ex != null)
                throw ex;
            return result;
        }
        public static BigFloat Parse(ReadOnlySpan<char> str, string unit, string format, CultureInfo culture)
        {
            var result = ParseInternal(str, unit, format, culture, out var ex);
            if (ex != null)
                throw ex;
            return result;
        }

        public static bool TryParse(string str, string unit, string format, CultureInfo culture, out BigFloat bf)
        {
            bf = ParseInternal(str, unit, format, culture, out var ex);
            return ex == null;
        }
    }

    /// <summary>
    /// A number of combined number sequences.
    /// </summary>
    public interface ICombinedNumberSequence : IEnumerable
    {
        /// <summary>
        /// Inner values representing the sequence.
        /// </summary>
        List<IEnumerable<double>> Sequences { get; }

        /// <summary>
        /// Casts the number sequence to a specific type. Should be a numeric type, e.g. typeof(float).
        /// </summary>
        /// <param name="elementType"></param>
        /// <returns></returns>
        ICombinedNumberSequence CastTo(Type elementType);
    }

    /// <summary>
    /// Generic number sequence type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ICombinedNumberSequence<T> : IEnumerable<T>, ICombinedNumberSequence
    {
        /// <summary>
        /// Casts this to a new number sequence type.
        /// </summary>
        /// <typeparam name="T2"></typeparam>
        /// <returns></returns>
        ICombinedNumberSequence<T2> CastTo<T2>();
    }

    /// <summary>
    /// Extensions for ICombinedNumberSequence.
    /// </summary>
    public static class ICombinedNumberSequenceExtension
    {
        /// <summary> Casts one type ICombinedNumberSequence to another. </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="seq"></param>
        /// <returns></returns>
        public static ICombinedNumberSequence<T> CastTo<T>(this ICombinedNumberSequence seq)
        {
            return (ICombinedNumberSequence<T>)seq.CastTo(typeof(T));
        }
    }

    interface _ICombinedNumberSequence
    {
        List<IEnumerable<BigFloat>> Sequences { get; set; }
    }

    class CombinedNumberSequences<T> : IEnumerable<T>, ICombinedNumberSequence<T>, _ICombinedNumberSequence
    {
        public List<IEnumerable<BigFloat>> Sequences { get; set; }

        List<IEnumerable<double>> ICombinedNumberSequence.Sequences { get {return Sequences.Select(x => x.Select(y => (double)y.ConvertTo(typeof(double)))).ToList(); } }

        public CombinedNumberSequences(List<IEnumerable<BigFloat>> sequences)
        {
            this.Sequences = sequences;
        }

        IEnumerable<T> getValues()
        {
            foreach (var seq in Sequences)
            {
                foreach (BigFloat val in seq)
                {
                    yield return (T)val.ConvertTo(typeof(T));
                }
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return getValues().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return getValues().GetEnumerator();
        }

        public ICombinedNumberSequence<T1> CastTo<T1>()
        {
            return new CombinedNumberSequences<T1>(Sequences);
        }

        public ICombinedNumberSequence CastTo(Type elemType)
        {
            return (ICombinedNumberSequence)Activator.CreateInstance(typeof(CombinedNumberSequences<>).MakeGenericType(elemType), Sequences);
        }
    }

    /// <summary>
    /// Parser / back parser for numbers and sequences of numbers.
    /// </summary>
    public class NumberFormatter
    {
        CultureInfo culture;
        string separator;
        /// <summary>
        /// Argument to string.Format.
        /// </summary>
        public string Format = "";
        /// <summary>
        /// Unit of the numbers (e.g. 'Hz').
        /// </summary>
        public string Unit = "";
        /// <summary>
        /// Boolean setting. When true, parse number into prefixes. For example, '10000 Hz' becomes '10 kHz'. 
        /// </summary>
        public bool UsePrefix = false;
        /// <summary>
        /// Pre-scales numbers before converting.
        /// </summary>
        public double PreScaling = 1.0;
        /// <summary>
        /// Boolean setting. When true, numbers are parsed back into ranges. When false, separate values as used as their raw representation. 
        /// </summary>
        public bool UseRanges = true;

        /// <summary>
        /// Print using compact representation.
        /// </summary>
        public bool IsCompact = false;

        /// <summary> </summary>
        /// <param name="culture"> The culture used to parse/write numbers.</param>
        public NumberFormatter(CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException("culture");
            this.culture = culture;
            separator = culture.NumberFormat.NumberGroupSeparator + " ";
        }

        /// <summary>
        /// Creates a number parser based on a UnitAttribute.
        /// </summary>
        /// <param name="culture"></param>
        /// <param name="unit"></param>
        public NumberFormatter(CultureInfo culture, UnitAttribute unit) : this(culture)
        {
            if (unit != null)
            {
                Format = unit.StringFormat;
                Unit = unit.Unit;
                UsePrefix = unit.UseEngineeringPrefix;
                PreScaling = unit.PreScaling;
                UseRanges = unit.UseRanges;
            }
        }

        BigFloat parseNumber(ReadOnlySpan<char> trimmed)
        {
            return UnitFormatter.Parse(trimmed, Unit ?? "", Format, culture) * PreScaling;
        }

        bool tryParseNumber(string trimmed, out BigFloat val)
        {
            return UnitFormatter.TryParse(trimmed, Unit ?? "", Format, culture, out val);
        }
        
        void parseBackNumber(BigFloat number, StringBuilder sb)
        {
            if (PreScaling != 1.0)
                number = number / PreScaling;
            UnitFormatter.Format(sb, number , UsePrefix, Unit ?? "", Format, culture, IsCompact);
        }

        string parseBackNumber(BigFloat number)
        {
            if (PreScaling != 1.0)
                number /= PreScaling;
            return UnitFormatter.Format(number, UsePrefix, Unit ?? "", Format, culture, IsCompact);
        }

        void parseBackRange(BigFloat Start, BigFloat Step, BigFloat Stop, StringBuilder sb)
        {
            parseBackNumber(Start, sb);
            if (Step != PreScaling)
            {
                sb.Append(" : ");
                parseBackNumber(Step, sb);
            }
            sb.Append(" : ");
            parseBackNumber(Stop, sb);
        }
        
        string parseBackRange(Range rng)
        {
            if (rng.Step == PreScaling)
            {
                return string.Format("{0} : {1}", parseBackNumber(rng.Start), parseBackNumber(rng.Stop));
            }
            return string.Format("{0} : {1} : {2}", parseBackNumber(rng.Start), parseBackNumber(rng.Step), parseBackNumber(rng.Stop));
        }

        Range parseRange(ReadOnlySpan<char> formatted)
        {
            var parts = formatted.ToString().Split(':').Select(s => s.Trim());
            var rangeitems = parts.Select(str => parseNumber(str)).ToArray();
            Range result = null;
            if (rangeitems.Length == 3)
            {
                result = new Range(rangeitems[0], rangeitems[2], rangeitems[1]);
            }
            else
            if (rangeitems.Length == 2)
            {
                result = new Range(rangeitems[0], rangeitems[1], PreScaling * (rangeitems[1] - rangeitems[0]).Sign());
            }
            else
            {
                throw new FormatException($"Unable to parse Range from {formatted.ToString()}");
            }
            result.CheckRange();
            return result;
        }

        /// <summary>
        /// Parses a string to a sequence of doubles.
        /// supports ranges, sequences, units and prefixes.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public ICombinedNumberSequence<double> Parse(ReadOnlySpan<char> value)
        {
            string separator = culture.NumberFormat.NumberGroupSeparator;
            
            List<IEnumerable<BigFloat>> parts = new List<IEnumerable<BigFloat>>();
            List<BigFloat> endPart = null;
            List<BigFloat> endPartBacking = new(value.Length/2);
            int indexer = 0;
            while(value.Length > 0)
            {
                var separatorIndex = value.IndexOf(separator);
                if (separatorIndex == -1)
                {
                    separatorIndex = value.Length - 1;
                }
                var trimmed = value.Slice(0, separatorIndex).Trim();
                value = value.Slice(separatorIndex + 1);
                
                if (trimmed.IndexOf(':') != -1)
                {
                    Range rng = parseRange(trimmed);
                    if (endPart != null)
                    {
                        parts[parts.Count - 1] = endPart.ToArray();
                        endPart = null;
                        endPartBacking.Clear();
                    }
                    parts.Add(rng);
                }
                else
                {
                    var result = parseNumber(trimmed);
                    if (endPart == null)
                    {
                        endPart = endPartBacking;
                        parts.Add(endPart);
                    }
                    else
                    {
                        endPart.Add(result);
                    }
                }
            }
            List<IEnumerable<BigFloat>> parts2 = new List<IEnumerable<BigFloat>>();

            if (UseRanges)
            {
                // this for loop 'compresses' the parts by trying to reuse and create Range objects.
                for (int i = 0; i < parts.Count; i++)
                {
                    var item = parts[i];
                    if (item is Range)
                    {
                        parts2.Add(item);
                        continue;
                    }
                    
                    var vals = item as ICollection<BigFloat>;
                    bool reuse_last = false;
                    BigFloat start = 0, stop = 0, step = 0;
                    int nitems = 0;
                    var last = parts2.LastOrDefault() as Range;
                    if (last != null)
                    {
                        start = last.Start;
                        stop = last.Stop;
                        step = last.Step;
                        nitems = ((int)((((stop - start) / step) + BigFloat.Half).Rounded())) + 1;
                        reuse_last = true;
                    }

                    Action submit = () =>
                    {
                        if (nitems == 0)
                            return;
                        if (reuse_last)
                        {
                            parts2[parts2.Count - 1] = new Range(start, stop, step);
                        }
                        else
                        {
                            if (nitems > 2)
                            {
                                if (step == 0)
                                {
                                    BigFloat[] values = new BigFloat[nitems];
                                    for (int j = 0; j < nitems; j++)
                                        values[j] = start;
                                    parts2.Add(values);
                                }
                                else
                                {
                                    parts2.Add(new Range(start, stop, step));
                                }
                            }
                            else if (nitems == 2)
                            {
                                parts2.Add([start, stop]);
                            }
                            else
                            {
                                parts2.Add([start]);
                            }
                        }
                        nitems = 0;
                        step = 0;
                    };

                    foreach (var val in vals)
                    {
                        start:
                        if (nitems == 0)
                        {
                            start = val;
                            nitems = 1;
                            continue;
                        }
                        if (nitems == 1)
                        {
                            stop = val;
                            step = stop - start;
                            nitems = 2;
                            continue;
                        }

                        BigFloat nextval;
                        if (step == 0)
                            nextval = stop; // Avoid NaN nextval.
                        else
                            nextval = start + (BigFloat.One + ((stop - start) / step).Round()) * step;

                        if ((val == nextval))
                        {
                            stop = val;
                            nitems += 1;
                        }
                        else
                        {
                            submit();
                            reuse_last = false;
                            goto start; // run again with val as start.
                        }
                    }
                    submit();

                }
            }
            else
                parts2 = parts.Select(x => x.ToArray() as IEnumerable<BigFloat>).ToList();
            return new CombinedNumberSequences<BigFloat>(parts2).CastTo<double>();

        }

        void pushSeq(StringBuilder sb, BigFloat val)
        {
            if (sb.Length != 0)
                sb.Append(separator);
            parseBackNumber(val, sb);
        }
        
        void pushSeq(StringBuilder sb, IList<BigFloat> seq, BigFloat step)
        {
            if (seq.Count > 2 && step.IsZero == false)
            {
                if (sb.Length != 0)
                    sb.Append(separator);
                parseBackRange(seq[0], step, seq[seq.Count - 1], sb);
            }
            else
            {
                foreach (var val in seq)
                {
                    if (sb.Length != 0)
                        sb.Append(separator);
                    parseBackNumber(val, sb);
                }
            }
        }

        /// <summary>
        /// Parses a number back to a string.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public string FormatNumber(object value)
        {
            if (value == null)
                return "";
            return parseBackNumber(BigFloat.Convert(value));
        }

        /// <summary>
        /// Parses a single number from a string.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public object ParseNumber(string str, Type t)
        {
            if (str == null) throw new ArgumentNullException("str");
            if (t == null) throw new ArgumentNullException("t");
            try
            {
                
                return parseNumber(str).ConvertTo(t);
            }
            catch (OverflowException)
            {
                throw new FormatException(string.Format("Unable to parse '{0}' to a {1}", str, t.Name));
            }
        }

        /// <summary>
        /// Try to parse a single number from a string.
        /// </summary>
        /// <param name="str">the string to parse.</param>
        /// <param name="t">the return type of value. must be numeric. </param>
        /// <param name="val">resulting value. Null if parsing failed.</param>
        /// <returns></returns>
        public bool TryParseNumber(string str, Type t, out object val)
        {
            if(tryParseNumber(str, out BigFloat val2))
            {
                val = val2.ConvertTo(t);
                return true;
            }
            val = null;
            return false;
        }

        /// <summary>
        /// Parses a sequence of numbers back into a string.
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public string FormatRange(IEnumerable values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            { // Check if values is a ICombinedNumberSequence. 
              // If so, it can be parsed back faster. (without iterating through all the ranges).
              if (values is _ICombinedNumberSequence seqs)
                {
                    StringBuilder sb = StringBuilderCache.GetStringBuilder();
                    foreach (var subseq in seqs.Sequences)
                    {
                        if (subseq is Range range)
                        {
                            if (sb.Length != 0)
                                sb.Append(separator);
                            sb.Append(parseBackRange(range));
                        }
                        else
                        {
                            foreach (var val in (BigFloat[])subseq)
                            {
                                if (sb.Length != 0)
                                    sb.Append(separator);
                                sb.Append(parseBackNumber(val));
                            }
                        }
                    }
                    return sb.ToString();
                }
            }
            if (UseRanges)
            { // Parse the slow way.
                StringBuilder sb = StringBuilderCache.GetStringBuilder();
                List<BigFloat> sequence = new List<BigFloat>();
                BigFloat seq_step = 0;
                foreach (var _val in values)
                {
                    var val = BigFloat.Convert(_val);
                    if (sequence.Count < 2)
                    {
                        sequence.Add(val);
                    }
                    else
                    {
                        seq_step = sequence[1] - sequence[0];
                        var nextVal = sequence[0] + seq_step * (BigFloat.One + ((sequence.Last() - sequence[0]) / seq_step).Round());
                        if (nextVal == val)
                        {
                            sequence.Add(val);
                        }
                        else
                        {
                            if (sequence.Count == 2)
                            {
                                pushSeq(sb, sequence[0]);
                                sequence.RemoveAt(0);
                            }
                            else
                            {
                                pushSeq(sb, sequence, seq_step);
                                sequence.Clear();
                            }

                            sequence.Add(val);
                        }
                    }
                }
                pushSeq(sb, sequence, seq_step);
                return sb.ToString();
            }
            if (!UsePrefix)
            {
                // this ca be done really fast since we dont have to use BigFloat.
                var sb = StringBuilderCache.GetStringBuilder();
                foreach (var _val in values)
                {
                    if (sb.Length != 0)
                        sb.Append(separator);
                    switch (_val)
                    {
                        case float i:
                            sb.Append(i.ToString("R", culture));
                            break;
                        case decimal i: 
                            sb.Append(i.ToString("G", culture));
                            break;
                        case double i: 
                            sb.Append(i.ToString("R17", culture));
                            break;
                        default:
                            sb.Append(_val);
                            break;
                    }

                    if (string.IsNullOrEmpty(Unit) == false)
                    {
                        sb.Append(" ");
                        sb.Append(Unit);
                    }
                }

                return sb.ToString();
            }
            
            {
                StringBuilder sb = StringBuilderCache.GetStringBuilder();
                foreach (var _val in values)
                {
                    var val = BigFloat.Convert(_val);

                    if (sb.Length != 0)
                        sb.Append(separator);
                    parseBackNumber(val, sb);
                }
                return sb.ToString();
            }
        }
    }
}
