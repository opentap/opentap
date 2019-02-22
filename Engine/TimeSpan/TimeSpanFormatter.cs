/* -----------------------------------------------------
 *
 * File: TimeSpanFormatter.cs
 * Author: sven.kopacz@keysight.com
 * Created: 21.10.2016
 *
 * -----------------------------------------------------
 * 
 * Description: See class summary
 *
 * -----------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenTap
{
    /// <summary>   Contains helper functions to format a time span into a human readable string. </summary>
    public static class TimeSpanFormatter
    {
        // Each dictionary contains a pair of string per verbosity level
        // The first string is the unit for the values 1 or -1, the second one is
        // the unit for values != 1 / -1 (plural)

        private static readonly Dictionary<FormatVerbosities, Tuple<string, string>> C_DAY_UNITS = new Dictionary<FormatVerbosities, Tuple<string, string>>()
        {
            { FormatVerbosities.SuperBrief, new Tuple<string, string>(".", ".") },
            { FormatVerbosities.Brief, new Tuple<string, string>("d", "d") },
            { FormatVerbosities.Normal, new Tuple<string, string>("day", "days") },
            { FormatVerbosities.Verbose, new Tuple<string, string>("day", "days") },
        };

        private static readonly Dictionary<FormatVerbosities, Tuple<string, string>> C_HOUR_UNITS = new Dictionary<FormatVerbosities, Tuple<string, string>>()
        {
            { FormatVerbosities.SuperBrief, new Tuple<string, string>(":", ":") },
            { FormatVerbosities.Brief, new Tuple<string, string>("h", "h") },
            { FormatVerbosities.Normal, new Tuple<string, string>("hour", "hours") },
            { FormatVerbosities.Verbose, new Tuple<string, string>("hour", "hours") },
        };

        private static readonly Dictionary<FormatVerbosities, Tuple<string, string>> C_MINUTE_UNITS = new Dictionary<FormatVerbosities, Tuple<string, string>>()
        {
            { FormatVerbosities.SuperBrief, new Tuple<string, string>(":", ":") },
            { FormatVerbosities.Brief, new Tuple<string, string>("m", "m") },
            { FormatVerbosities.Normal, new Tuple<string, string>("min", "min") },
            { FormatVerbosities.Verbose, new Tuple<string, string>("minute", "minutes") },
        };

        private static readonly Dictionary<FormatVerbosities, Tuple<string, string>> C_SECOND_UNITS = new Dictionary<FormatVerbosities, Tuple<string, string>>()
        {
            { FormatVerbosities.SuperBrief, new Tuple<string, string>("", "") },
            { FormatVerbosities.Brief, new Tuple<string, string>("s", "s") },
            { FormatVerbosities.Normal, new Tuple<string, string>("sec", "sec") },
            { FormatVerbosities.Verbose, new Tuple<string, string>("second", "seconds") }
        };

        /// <summary>   Formats a timespan to a string like "3 min 4 sec". </summary>
        ///
        /// <param name="timespan">             The timespan. </param>
        /// <param name="verbosity">            The verbosity. </param>
        /// <param name="unitSpacer">           true to insert a space before each unit. </param>
        /// <param name="includeZeros">         true to include those parts of the timespan that are zero,
        ///                                     starting with hours, e.g. "0h 2m 0s" instead of just "2m". </param>
        /// <param name="includeSplitSeconds">  true to include, false to exclude the fractions of the second portion. </param>
        /// <param name="numberFormatCulture">  The Culture to use for number formatting. Since
        ///                                     milliseconds are displayed as fractions of seconds, this
        ///                                     effects the decimal delimiter. By default, the
        ///                                     DefaultThreadCurrentCulture is used. </param>
        ///
        /// <returns>   The formatted value. </returns>
        public static string Format(TimeSpan timespan, FormatVerbosities verbosity = FormatVerbosities.Normal, bool unitSpacer = true, bool includeZeros = true, bool includeSplitSeconds = true, CultureInfo numberFormatCulture = null)
        {
            string result = "";

            if (verbosity == FormatVerbosities.SuperBrief)
            {
                result = timespan.ToString();
                // The system's ToString() will add a fixed number of 0 for the millisecond fraction (e.g. 12:30:05.1200000)
                // but we want to return only the significant digits
                if (timespan.Milliseconds != 0)
                    result = result.TrimEnd('0');
            }
            else
            {
                string unitInsert = unitSpacer ? " " : "";
                result += CreateUnitString(timespan.Days, C_DAY_UNITS[verbosity], unitInsert, createIfZero: false, numberFormatCulture: numberFormatCulture);
                result += CreateUnitString(timespan.Hours, C_HOUR_UNITS[verbosity], unitInsert, includeZeros, numberFormatCulture: numberFormatCulture);
                result += CreateUnitString(timespan.Minutes, C_MINUTE_UNITS[verbosity], unitInsert, includeZeros, numberFormatCulture: numberFormatCulture);
                result += CreateUnitString(timespan.Seconds + (includeSplitSeconds ? timespan.Milliseconds / 1000.0 : 0), C_SECOND_UNITS[verbosity], unitInsert, includeZeros, numberFormatCulture: numberFormatCulture);
            }

            return result.Trim(); ;
        }

        private static string CreateUnitString(double value, Tuple<string, string> unit, string unitInsert, bool createIfZero, CultureInfo numberFormatCulture = null)
        {
            if (createIfZero || (value != 0))
                return value.ToString(numberFormatCulture ?? CultureInfo.DefaultThreadCurrentCulture) + unitInsert + ((Math.Abs(value) == 1) ? unit.Item1 : unit.Item2) + unitInsert;

            return "";
        }
    }
}
