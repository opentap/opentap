/* -----------------------------------------------------
 *
 * File: TimeSpanParser.cs
 * Author: sven.kopacz@keysight.com
 * Created: 21.10.2016
 *
 * -----------------------------------------------------
 * 
 * Description: See class summary
 *
 * -----------------------------------------------------
 */

using OpenTap;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenTap
{
    /// <summary>
    /// A time span parser can parse a text input (which would typically be a user input) for
    /// timespan information like "1 hour 2 minutes" or "1 week 250ms" and sum them all up into a
    /// TimeSpan. Currently only English units are supported.
    /// </summary>
    public static class TimeSpanParser
    {
        /// <summary>
        /// Parses a string for TimeSpan Information like "1 week 2 days" or "2h12m3s". Currently only
        /// English units are accepted. Note that "month" is not a valid unit, since it does not
        /// correspond to a determined number of days and so is "year" because of leap years. However you
        /// can have both units parsed to hardwired values using the <cref name="acceptIndeterminate"/> parameter.
        /// Note that Microseconds are accepted, but the .NET TimeSpan only resolves milliseconds. Thus only
        /// multiples of thousands of Microseconds will affect the result, e.g. "2000µs", when reformatted, will
        /// evaluate to "2ms" whereas "200µs" will simply return TimeSpan.Zero.
        /// </summary>
        ///
        /// <exception cref="ArgumentException">    Thrown when one or more arguments have unsupported or
        ///                                         illegal values. </exception>
        /// <exception cref="FormatException">      Thrown when a unit is unknown. </exception>
        ///
        /// <param name="input">                The input string. </param>
        /// <param name="numberParseStyles">    The acceptable number styles. </param>
        /// <param name="numberParseCulture">   The culture for number parsing. If omitted, DefaultThreadCurrentCulture is chosen. </param>
        /// <param name="acceptIndeterminate">  true to accept months (as 30 days) and years (as 356
        ///                                     days). </param>
        ///
        /// <returns>   A TimeSpan. </returns>
        public static TimeSpan Parse(string input, NumberStyles numberParseStyles = NumberStyles.Any, CultureInfo numberParseCulture = null, bool acceptIndeterminate = false)
        {
            TimeSpan result = TimeSpan.Zero;
            var csplit = input.Split(':');
            if (csplit.Length > 1)
            {  // Parse superbrief format: (dd:)(HH:)mm:ss(.fff)
               // TimeSpan.Parse does not work, because it misinterprets 24:0:0 as 24 days.
                if (csplit.Length > 4)
                    throw new FormatException("Invalid TimeSpan format.");
                double[] multipliers = new double[] { 24 * 60 * 60, 60 * 60, 60, 1 };
                var last = csplit[csplit.Length - 1];
                double frac = 0.0;
                if (last.Contains('.'))
                {
                    var fsplit = last.Split('.');
                    if (fsplit.Length == 2)
                    {
                        csplit[csplit.Length - 1] = fsplit[0];
                        fsplit[1] = fsplit[1].Trim();
                        int decimals = fsplit[1].Length;
                        frac = double.Parse(fsplit[1]) / Math.Pow(10, decimals);
                    }
                }


                int j = multipliers.Length - 1;
                for(int i = csplit.Length - 1; i >= 0; i--)
                {
                    frac += multipliers[j] * int.Parse(csplit[i]);
                    j--;
                }
                return Time.FromSeconds(frac);
            }
            List<Tuple<double, string>> entries = NumberUnitSplitter.Split(input, numberParseStyles, numberParseCulture);
            
            foreach (Tuple<double, string> valueUnitPair in entries)
            {
                switch (valueUnitPair.Item2.ToLower())
                {
                    case "µs":
                    case "µsec":
                    case "µsecond":
                    case "µseconds":
                    case "microsec":
                    case "microsecond":
                    case "microseconds":
                        result += Time.FromSeconds(valueUnitPair.Item1 / 1E6);
                        break;

                    case "ms":
                    case "millisec":
                    case "millisecond":
                    case "milliseconds":
                        result += Time.FromSeconds(valueUnitPair.Item1 / 1e3);
                        break;
                    case "":
                    case "s":
                    case "ss":
                    case "sec":
                    case "second":
                    case "seconds":
                        result += Time.FromSeconds(valueUnitPair.Item1);
                        break;

                    case "m":
                    case "mm":
                    case "min":
                    case "minute":
                    case "minutes":
                        result += Time.FromSeconds(valueUnitPair.Item1 * 60);
                        break;

                    case "h":
                    case "hh":
                    case "hour":
                    case "hours":
                        result += Time.FromSeconds(valueUnitPair.Item1 * 60 * 60);
                        break;

                    case "d":
                    case "dd":
                    case "day":
                    case "days":
                        result += Time.FromSeconds(valueUnitPair.Item1 * 60 * 60 * 24);
                        break;

                    case "w":
                    case "week":
                    case "weeks":
                        result += Time.FromSeconds(valueUnitPair.Item1 * 60 * 60 * 24 * 7);
                        break;

                    case "month":
                    case "months":
                        if (acceptIndeterminate)
                            result += Time.FromSeconds(valueUnitPair.Item1 * 60 * 60 * 24 * 30);
                        else
                            throw new ArgumentException("Unit 'month' is indeterminate");
                        break;

                    case "y":
                    case "year":
                    case "years":
                        if (acceptIndeterminate)
                            result += Time.FromSeconds(valueUnitPair.Item1 * 60 * 60 * 24 * 365);
                        else
                            throw new ArgumentException("Unit 'year' is indeterminate");
                        break;

                    default:
                        throw new FormatException("Unknown Unit: '" + valueUnitPair.Item2 + "'");
                }
            }
            return result;
        }
    }
}
