/* -----------------------------------------------------
 *
 * File: NumberUnitSplitter.cs
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
    /// <summary>
    /// A number unit splitter can split a text into numbers and units using whitespaces and changes
    /// from digits to characters as splitting points.
    /// </summary>
    public static class NumberUnitSplitter
    {
        /// <summary>
        /// Splits a string into pairs of numbers and units. The units are not evaluated in any way but
        /// just returned as strings. Separation points are whitespaces, any switch from digits to
        /// characters and vice versa. Dots, commas etc. are considered to belong to numbers, also the
        /// character 'e' when it is followed by a sign or digit. You can provide parsing parameters
        /// though that may render such a number invalid.
        /// For empty input strings, an empty list is returned.
        /// In case of invalid numbers, an FormatException is thrown.
        /// </summary>
        ///
        /// <param name="text">                 The text. </param>
        /// <param name="numberParseStyles">    The accepted styles for number parsing. </param>
        /// <param name="numberParseCulture">   The culture for number parsing. If omitted, DefaultThreadCurrentCulture is chosen.</param>
        ///
        /// <returns>   A List&lt;Tuple&lt;double,string&gt;&gt; </returns>
        public static List<Tuple<double, string>> Split(string text, NumberStyles numberParseStyles = NumberStyles.Any, CultureInfo numberParseCulture = null)
        {
            List<Tuple<double, string>> result = new List<Tuple<double, string>>();

            while (!string.IsNullOrWhiteSpace(text))
            {
                // Extract number
                string number = GetLeadingDigits(text, numberParseStyles);
                if(string.IsNullOrWhiteSpace(number))
                    throw new FormatException("Missing number");

                // Skip whitespace between number and unit
                int i = number.Length;
                while ((i < text.Length) && char.IsWhiteSpace(text[i]))
                    i++;
                int istart = i;
                // Extract unit
                // Everything below A is not considered a unit character
                // Everything from a-z, A-Z and everything with codes > 127 is considered a regular char
                // Remember that a char in .Net is actually a 16 bit unicode character and the unit could 
                // have something like a leading "µ", a "£" or "°C"
                int unitStartIndex = i;
                while ((i < text.Length)
                        &&
                        (
                            ((text[i] >= 'A') && (text[i] <= 'Z'))
                            ||
                            ((text[i] >= 'a') && (text[i] <= 'z'))
                            ||
                            (text[i] == '+' && i >= istart)
                            ||
                            (text[i] == '-' && i >= istart)
                            ||
                            (text[i] >= 128)
                        )
                       )
                    i++;

                string unit = "";
                if (i != unitStartIndex)
                    unit = text.Substring(unitStartIndex, i - unitStartIndex);
                result.Add(new Tuple<double, string>(double.Parse(number, numberParseStyles, numberParseCulture ?? CultureInfo.DefaultThreadCurrentCulture), unit));

                if (i >= text.Length)
                    break;

                text = text.Substring(i);
            }

            return result;
        }
        
        /// <summary>
        /// Retrieves the leading part of the string that contains digits only and an optional set of
        /// formatting signs. Note that the returned string may even consist of formatting signs only
        /// like ",.-+e+" so you should use the .NET type parser on the result determine if it is an
        /// actual number.
        /// </summary>
        ///
        /// <param name="text">                     The string to parse. </param>
        /// <param name="numberParseStyles">        This can be configured to accept the features decimal
        ///                                         delimiter, leading sign and exponent. </param>
        /// <param name="includeLeadingWhiteSpace">    true to include leading white space characters. </param>
        ///
        /// <returns>   The leading digits. </returns>
        private static string GetLeadingDigits(string text, NumberStyles numberParseStyles = NumberStyles.Any, bool includeLeadingWhiteSpace = true)
        {
            bool acceptDecimalDelimiter = (numberParseStyles & (NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands)) > 0;
            bool acceptLeadingSign = (numberParseStyles & NumberStyles.AllowLeadingSign) > 0;
            bool acceptExponent = (numberParseStyles & NumberStyles.AllowExponent) > 0;

            string digits = "";
            bool nextCharAllowsSign = true;

            foreach (char c in text)
            {
                bool acceptedChar = false;

                if ((c < '0') || (c > '9'))
                {
                    if (nextCharAllowsSign)
                    {
                        if ((c == '+') || (c == '-'))
                        {
                            if (acceptLeadingSign)
                                acceptedChar = true;
                        }
                    }
                    if ((c == '.') || (c == ','))
                    {
                        if (acceptDecimalDelimiter)
                            acceptedChar = true;
                    }
                    else if ((c == 'e') || (c == 'E'))
                    {
                        if (acceptExponent)
                        {
                            acceptedChar = true;
                            nextCharAllowsSign = true;
                        }
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        if (includeLeadingWhiteSpace && string.IsNullOrWhiteSpace(digits))
                            acceptedChar = true;
                    }
                }
                else
                {
                    acceptedChar = true;
                    nextCharAllowsSign = false;
                }

                if (!acceptedChar)
                    break;

                digits += c;
            }

            return digits;
        }
    }
}