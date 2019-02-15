//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace OpenTap
{
    /// <summary>
    /// Utility class for SCPI communication.
    /// </summary>
    public static class Scpi
    {
        /// <summary>
        /// Enum converter for converting Enums to SCPI strings. use the GetEnumConv to get a converter.
        /// </summary>
        class ScpiEnum
        {
            // Remembers how to convert types.
            readonly static Dictionary<Type, ScpiEnum> converters = new Dictionary<Type, ScpiEnum>();

            /// <summary>
            /// Get an enum converter for a specific enum type.
            /// </summary>
            /// <param name="t">Must be a enum type!</param>
            /// <returns></returns>
            public static ScpiEnum GetEnumConv(Type t)
            {
                if (converters.ContainsKey(t) == false)
                {
                    converters[t] = new ScpiEnum(t);
                }
                return converters[t];
            }

            readonly Dictionary<string, Enum> convertBack = new Dictionary<string, Enum>();

            readonly Dictionary<Enum, string> convert = new Dictionary<Enum, string>();

            /// <summary>
            /// String to Enum.
            /// </summary>
            /// <param name="input"></param>
            /// <returns></returns>
            public Enum FromString(string input)
            {
                return convertBack[input];
            }

            /// <summary>
            /// Enum to string.
            /// </summary>
            /// <param name="item"></param>
            /// <returns></returns>
            public string ToString(Enum item)
            {
                return convert[item];
            }

            ScpiEnum(Type type)
            {
                // collect enum values and string conversions.
                var enumValues = Enum.GetValues(type);
                foreach (Enum val in enumValues)
                {
                    var member = type.GetMember(val.ToString());
                    var attr = member[0].GetCustomAttributes(typeof(ScpiAttribute), false);
                    string scpiName = null;
                    if (attr.Length != 1)
                    {
                        scpiName = val.ToString();
                    }
                    else
                    {
                        scpiName = ((ScpiAttribute)attr[0]).ScpiString;
                    }
                    convert[val] = scpiName;
                    convertBack[scpiName] = val;
                }
            }
        }

        static string valueToScpiString(object propertyValue)
        {
            var type = propertyValue.GetType();
            string value;
            if (type == typeof(bool))
            {
                value = (bool)propertyValue ? "ON" : "OFF";
            }
            else if (type.IsEnum)
            {
                value = ScpiEnum.GetEnumConv(type).ToString((Enum)propertyValue);
            }
            else if (type.IsArray)
            {
                var Props = (Array)propertyValue;

                string[] PropStrings = new string[Props.Length];
                for (int i = 0; i < Props.Length; i++)
                    PropStrings[i] = valueToScpiString(Props.GetValue(i));

                value = string.Join(",", PropStrings);
            }
            else
            {
                value = Convert.ToString(propertyValue, CultureInfo.InvariantCulture);
            }

            return value;
        }

        /// <summary>
        /// Similar to <see cref="string.Format(string,object[])"/>, but makes args SCPI compatible. Bools are ON/OFF formatted. Enum values uses <see cref="ScpiAttribute.ScpiString"/>.
        /// Arrays will have their elements formatted and separated by commas if available; if not they are converted using <see cref="string.ToString()"/>.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string Format(string command, params object[] args)
        {
            if (command == null)
                throw new ArgumentNullException("command");
            if (args == null)
                throw new ArgumentNullException("args");
            string[] stringArgs = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                stringArgs[i] = args[i] == null ? "" : valueToScpiString(args[i]);
            }
            return string.Format(command, stringArgs);
        }

        /// <summary>
        /// Extension method that checks whether a given char is a IEEE488.2 whitespace (7.4.1.2).
        /// </summary>
        private static bool IsScpiWhitespace(this char c)
        {
            return (
                ((((int)c) >= 0) && (((int)c) <= 9)) ||
                ((((int)c) >= 11) && (((int)c) <= 32))
            );
        }

        private static int FindStringEnd(string Resp, int Start)
        {
            int stop = Resp.IndexOf("\"", Start + 1);

            if (stop < 0)
                return -1;
            // Skip inserted quotes
            while ((stop < (Resp.Length - 1)) && (Resp.Substring(stop, 2) == "\"\""))
            {
                stop = Resp.IndexOf("\"", stop + 2);
                if (stop < 0)
                    return -1;
            }

            return stop;
        }

        /// <summary>
        /// Splits a string into a list of valid separated SCPI response data strings. Needed because String.Split(resp, ',') is not tolerant to " characters.
        /// </summary>
        private static List<string> SplitScpiArray(string resp, bool ThrowIfInvalid = false)
        {
            List<string> result = new List<string>();

            int start = 0;
            int stop = 0;

            while (start < (resp.Length - 1))
            {
                // Trim whitespace from the start of the string
                char c = resp[start];
                while (c.IsScpiWhitespace())
                    c = resp[++start];
                stop = start;

                // Test if the next character is a string quote
                if (c == '"')
                {
                    stop = FindStringEnd(resp, stop);

                    if (stop < 0)
                    {
                        if (ThrowIfInvalid) throw new Exception(string.Format("Unterminated SCPI response: '{0}'", resp));
                        stop = resp.Length;
                    }
                    else
                        stop++;

                    result.Add(resp.Substring(start, stop - start));

                    stop = resp.IndexOf(",", stop);
                    if (stop < 0)
                        stop = resp.Length;
                    start = stop + 1;
                }
                else
                {
                    stop = resp.IndexOf(",", start);

                    if (stop < 0)
                        stop = resp.Length;

                    result.Add(resp.Substring(start, stop - start));
                    start = stop + 1;
                }
            }
            if (start < resp.Length)
                result.Add(resp.Substring(start));
            return result;
        }

        /// <summary>
        /// Overloaded.  Parses the result of a SCPI query back to T, with special parsing for enums, bools and arrays. Bools support 1/0 and ON/OFF formats. 
        /// If Enums are tagged with <see cref="ScpiAttribute"/>, <see cref="ScpiAttribute.ScpiString"/> will be used instead of <see cref="string.ToString()"/> .  
        /// </summary>
        /// <param name="scpiString"></param>
        /// <param name="T"></param>
        /// <returns></returns>
        public static object Parse(string scpiString, Type T)
        {
            if (scpiString == null)
                throw new ArgumentNullException("scpiString");
            if (T == null)
                throw new ArgumentNullException("T");
            scpiString = scpiString.Trim(); // Ensure no garbage.
            if (T == typeof(bool))
            {
                return (object)(scpiString == "ON" || scpiString == "1");
            }
            else if (T.IsEnum)
            {
                return (object)ScpiEnum
                    .GetEnumConv(T)
                    .FromString(scpiString);
            }
            else if (T.IsArray)
            {
                List<string> elements = SplitScpiArray(scpiString);
                Array result = Array.CreateInstance(T.GetElementType(), elements.Count);

                for (int i = 0; i < elements.Count; i++)
                    result.SetValue(Parse(elements[i], T.GetElementType()), i);

                return result;
            }

            return Convert.ChangeType(scpiString, T, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the result of a SCPI query back to T. Special parsing for enums, bools and arrays. bools supports 1/0 and ON/OFF formats. 
        /// If Enums are tagged with <see cref="ScpiAttribute"/> <see cref="ScpiAttribute.ScpiString"/> will be used instead of <see cref="string.ToString()"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="scpiString"></param>
        /// <returns></returns>
        public static T Parse<T>(string scpiString)
        {
            return (T)Parse(scpiString, typeof(T));
        }

        /// <summary>
        /// <see cref="GetUnescapedScpi"/>.
        /// </summary>
        /// <param name="src"></param>
        /// <param name="ScpiString"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        static string getUnescapedScpi(object src, string ScpiString, PropertyInfo property)
        {

            if (ScpiString.Contains("%"))
            {
                string value;
                object propertyValue = property.GetValue(src, null);
                if (propertyValue == null)
                    return ScpiString;
                value = valueToScpiString(propertyValue);

                return ScpiString.Replace("%", value);
            }

            if (property.PropertyType == typeof(bool) && ScpiString.Contains("|"))
            {
                Regex rx = new Regex(".* (?<true>[^|]+)\\|(?<false>[^\\s]+)");
                Match m = rx.Match(ScpiString);
                if (m.Success)
                {
                    bool value = (bool)property.GetValue(src, null);
                    if (value == true)
                        return ScpiString.Replace("|" + m.Groups["false"], "");
                    else
                        return ScpiString.Replace(m.Groups["true"] + "|", "");
                }
            }
            return ScpiString;
        }
        /// <summary>
        /// Parses one or more items of <see cref="ScpiAttribute.ScpiString"/> 'property', replacing '%' with the value of the property given after formatting. 
        /// Note that 'property' must be a property with the <see cref="ScpiAttribute"/>, and 'src' is the object containing 'property', not the value of the property. 
        /// If property.PropertyType is bool, then from the <see cref="ScpiAttribute.ScpiString"/> value 'A|B' A is selected if true, and B is selected if false.  
        /// </summary>
        static public string[] GetUnescapedScpi(object src, PropertyInfo property)
        {
            if (property == null)
                throw new ArgumentNullException("property");
            var scpiStrings = property.GetCustomAttributes<ScpiAttribute>().Select(scpiAttr => scpiAttr.ScpiString);
            return scpiStrings.Select(str => getUnescapedScpi(src, str, property)).ToArray();
        }
    }
}
