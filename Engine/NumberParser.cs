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
            var sign = step.Sign();

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
            conv = conv ?? ((v) => v.ToString());

            if (Step == new BigFloat(1.0) || Step == new BigFloat(-1.0))
            {
                return string.Format("{0} : {1}", conv(Start), conv(Stop));
            }
            return string.Format("{0} : {1} : {2}", conv(Start), conv(Step), conv(Stop));
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
                if (format.StartsWith("x", StringComparison.InvariantCultureIgnoreCase))
                    final_string = ((long)post_scale.Rounded()).ToString(format, culture);
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
        static object parse(string str, string unit, string format, CultureInfo culture)
        {
            if (str == null)
                return new ArgumentNullException("str");
            if (unit == null)
                return new ArgumentNullException("unit");
            if (format == null)
                return new ArgumentNullException("format");
            if (culture == null)
                return new ArgumentNullException("culture");

            str = str.Trim();

            bool IsHex =
                (str.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase) ||
                str.StartsWith("-0x", StringComparison.InvariantCultureIgnoreCase));
            int HexSkip = 2;

            if ((!IsHex) &&
               (format.StartsWith("x", StringComparison.InvariantCultureIgnoreCase)))
            {
                IsHex = true;
                HexSkip = 0;
            }

            if (unit.Length > 0 && str.EndsWith(unit, StringComparison.InvariantCultureIgnoreCase))
                str = str.Remove(str.Length - unit.Length);
            str = str.TrimEnd();
            if (str.Length == 0)
            {
                return new FormatException("Invalid format");
            }

            char prefix = str[str.Length - 1];
            BigFloat multiplier = 1.0;
            if (levels.Contains(prefix))
            {
                // Handle case of "femto" unit
                if (!(IsHex && (prefix == 'f') && !str.EndsWith(" f", StringComparison.InvariantCulture)))
                {
                    multiplier = engineeringPrefixLevel(prefix);
                    if (str[str.Length - 1] == prefix)
                        str = str.Substring(0, str.Length - 1).TrimEnd();
                }
            }

            if (IsHex && str.StartsWith("-", StringComparison.InvariantCultureIgnoreCase))
                return new BigFloat(-Int64.Parse(str.Substring(HexSkip + 1).ToUpper(), NumberStyles.HexNumber, culture)) * multiplier;
            else if (IsHex)
                return new BigFloat(BigInteger.Parse("0" + str.Substring(HexSkip).ToUpper(), NumberStyles.HexNumber, culture)) * multiplier;

            if (str.StartsWith("0b", StringComparison.InvariantCultureIgnoreCase) || str.StartsWith("-0b", StringComparison.InvariantCultureIgnoreCase))
            {
                string bits = "";
                if (str.StartsWith("-0b", StringComparison.InvariantCultureIgnoreCase))
                {
                    multiplier = -multiplier;
                    bits = str.Substring(2);
                }
                else
                {
                    bits = str.Substring(2);
                }

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
                        return new FormatException("Invalid binary format.");
                    }
                }
                return new BigFloat(v) * multiplier;
            }
            var r = BigFloat.Parse(str, culture);
            if (r is BigFloat bf)
                return bf * multiplier;
            return r;
        }
        public static BigFloat Parse(string str, string unit, string format, CultureInfo culture)
        {
            var result = parse(str, unit, format, culture);
            if (result is BigFloat bf)
                return bf;
            else throw (Exception)result;
        }

        public static bool TryParse(string str, string unit, string format, CultureInfo culture, out BigFloat bf)
        {
            var result = parse(str, unit, format, culture);
            if (result is BigFloat _bf)
            {
                bf = _bf;
                return true;
            }

            return false;

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

        const bool enableFeatureFractions = false;
        BigFloat parseNumber(string trimmed)
        {
            // support fractions e.g 1/3 disabled because its hard to convert back from 0.333333... to 1/3, so this gives problems with formatting.
            if (enableFeatureFractions && trimmed.Contains('/'))
                return trimmed.Split('/').Select(part => parseNumber(part.Trim())).Aggregate((x, y) => x * PreScaling / y);
            return UnitFormatter.Parse(trimmed, Unit ?? "", Format, culture) * PreScaling;
        }

        bool tryParseNumber(string trimmed, out BigFloat val)
        {
            return UnitFormatter.TryParse(trimmed, Unit ?? "", Format, culture, out val);
        }

        string parseBackNumber(BigFloat number)
        {
            return UnitFormatter.Format(number / PreScaling, UsePrefix, Unit ?? "", Format, culture, IsCompact);
        }

        string parseBackRange(Range rng)
        {
            if (rng.Step == PreScaling)
            {
                return string.Format("{0} : {1}", parseBackNumber(rng.Start), parseBackNumber(rng.Stop));
            }
            return string.Format("{0} : {1} : {2}", parseBackNumber(rng.Start), parseBackNumber(rng.Step), parseBackNumber(rng.Stop));
        }

        Range parseRange(string formatted)
        {
            var parts = formatted.Split(':').Select(s => s.Trim());
            var rangeitems = parts.Select(parseNumber).ToArray();
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
                throw new FormatException(string.Format("Unable to parse Range from {0}", formatted));
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
        public ICombinedNumberSequence<double> Parse(string value)
        {
            if (value == null)
                throw new ArgumentNullException("value");
            string separator = culture.NumberFormat.NumberGroupSeparator;
            var splits = value.Split(new string[] { separator }, StringSplitOptions.RemoveEmptyEntries);
            List<IEnumerable<BigFloat>> parts = new List<IEnumerable<BigFloat>>();
            foreach (var split in splits)
            {
                var trimmed = split.Trim();
                if (trimmed.Contains(':'))
                {
                    Range rng = parseRange(trimmed);
                    parts.Add(rng);
                }
                else
                {
                    var result = parseNumber(trimmed);
                    if (parts.Count == 0 || parts[parts.Count - 1] is Range)
                    {
                        parts.Add(new List<BigFloat> { result });
                    }
                    else
                    {
                        ((IList<BigFloat>)parts[parts.Count - 1]).Add(result);
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
                    var vals = item as IList<BigFloat>;
                    bool reuse_last = false;
                    BigFloat start = 0, stop = 0, step = 0;
                    int nitems = 0;
                    var last = parts2.LastOrDefault() as Range;
                    if (last != null)
                    {
                        start = last.Start;
                        stop = last.Stop;
                        step = last.Step;
                        nitems = (int)((((stop - start) / step) + 0.5).Rounded() + 1);
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
                                parts2.Add(new BigFloat[] { start, stop });
                            }
                            else
                            {
                                parts2.Add(new BigFloat[] { start });
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
                            nextval = start + (step * (1 + ((stop - start) / step).Round()));

                        if ((val - nextval) == 0)
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
                parts2 = parts.Select(x => x.ToList() as IEnumerable<BigFloat>).ToList();
            return new CombinedNumberSequences<BigFloat>(parts2).CastTo<double>();

        }

        void pushSeq(StringBuilder sb, List<BigFloat> seq, BigFloat step)
        {
            if (seq.Count > 2 && step != 0.0)
            {
                if (sb.Length != 0)
                    sb.Append(separator);
                sb.Append(parseBackRange(new Range(seq[0], seq[seq.Count - 1], step)));
            }
            else
            {
                foreach (var val in seq)
                {
                    if (sb.Length != 0)
                        sb.Append(separator);
                    sb.Append(parseBackNumber(val));
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
                throw new ArgumentNullException("values");
            { // Check if values is a ICombinedNumberSequence. 
              // If so, it can be parsed back faster. (without iterating through all the ranges).
                var seqs = values as _ICombinedNumberSequence;
                if (seqs != null)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var subseq in seqs.Sequences)
                    {
                        var range = subseq as Range;
                        if (range != null)
                        {
                            if (sb.Length != 0)
                                sb.Append(separator);
                            sb.Append(parseBackRange(range));
                        }
                        else
                        {
                            foreach (var val in subseq)
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
                StringBuilder sb = new StringBuilder();
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
                        var nextVal = sequence[0] + seq_step * (1 + ((sequence.Last() - sequence[0]) / seq_step).Round());
                        if ((nextVal - val) == BigFloat.Zero)
                        {
                            sequence.Add(val);
                        }
                        else
                        {
                            if (sequence.Count == 2)
                            {
                                pushSeq(sb, new List<BigFloat> { sequence[0] }, seq_step);
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
            else
            {
                StringBuilder sb = new StringBuilder();
                foreach (var _val in values)
                {
                    var val = BigFloat.Convert(_val);

                    if (sb.Length != 0)
                        sb.Append(separator);
                    sb.Append(parseBackNumber(val));
                }
                return sb.ToString();
            }
        }
    }
}
