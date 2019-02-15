//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;

namespace OpenTap
{
    /// <summary>
    /// Plugin interface for string convert providers.
    /// </summary>
    /// <remarks>
    /// FromString(GetString(value), value.GetType()) shall always return a non-null value, if GetString(value) returns a non-null value.
    /// </remarks>
    [Display("String Converter")]
    public interface IStringConvertProvider : ITapPlugin
    {
        /// <summary>
        /// Returns a string when the implementation supports converting the value. Otherwise, returns null.
        /// </summary>
        /// <param name="value">Cannot be null.</param>
        /// <param name="culture">The culture used for the conversion.</param>
        /// <returns></returns>
        string GetString(object value, CultureInfo culture);
        /// <summary>
        /// Creates an object from stringdata. The returned object should be of type 'type'. Returns null if it cannot convert stringdata to type.
        /// </summary>
        /// <param name="stringdata"></param>
        /// <param name="type"></param>
        /// <param name="contextObject">The object on which the value is set.</param>
        /// <param name="culture">The culture used for the conversion. This value can will default to InvariantCulture if nothing else is selected.</param>
        /// <returns></returns>
        object FromString(string stringdata, Type type, object contextObject, CultureInfo culture);
    }
    
    /// <summary>
    /// Helper methods for converting to/from strings.
    /// </summary>
    public static class StringConvertProvider
    {
        static class PluginTypeFetcher<T>
        {
            static readonly Dictionary<Type, T> saveTypes = new Dictionary<Type, T>();
            static ImmutableList<T> things = ImmutableList<T>.Empty;
            public static IEnumerable<T> GetPlugins()
            {
                var types = PluginManager.GetPlugins<T>();

                if (types.Count != saveTypes.Count)
                {
                    lock (saveTypes)
                    {
                        if (types.Count != saveTypes.Count)
                        {
                            var builder = things.ToBuilder();

                            foreach (var type in types)
                            {
                                if (saveTypes.ContainsKey(type) == false)
                                {
                                    try
                                    {
                                        var newthing = (T)Activator.CreateInstance(type);
                                        saveTypes.Add(type, newthing);
                                        builder.Add(newthing);
                                    }
                                    catch
                                    {
                                        saveTypes[type] = default(T);

                                    } // Ignore erros here.
                                }
                            }
                            things = builder.ToImmutable();
                        }
                    }
                }

                return things;
            }
        }

        static IEnumerable<IStringConvertProvider> Providers
        {
            get { return PluginTypeFetcher<IStringConvertProvider>.GetPlugins(); }
        }

        /// <summary>
        /// Turn value to a string if an IStringConvertProvider plugin supports the value.
        /// </summary>
        /// <param name="value">The value to be converted to a string.</param>
        /// <param name="culture">If null, invariant culture is used.</param>
        /// <returns></returns>
        public static string GetString(object value, CultureInfo culture = null)
        {
            if (value == null) return null;
            culture = culture ?? CultureInfo.InvariantCulture;
            foreach (var provider in Providers)
            {
                try { 
                    var str = provider.GetString(value, culture);
                    if (str != null) return str;
                }
                catch { } // ignore errors. Assume unsupported value.
            }
            return value.ToString();
        }

        /// <summary>
        /// Turn stringdata back to an object of type 'type', if an IStringConvertProvider plugin supports the string/type.
        /// </summary>
        /// <param name="stringdata"></param>
        /// <param name="type"></param>
        /// <param name="contextObject"></param>
        /// <param name="culture">If null, invariant culture is used.</param>
        /// <returns></returns>
        public static object FromString(string stringdata, Type type, object contextObject, CultureInfo culture = null)
        {
            if (stringdata == null)
                return null;
            if (type == null)
                throw new ArgumentNullException("type");
            culture = culture ?? CultureInfo.InvariantCulture;
            foreach (var provider in Providers)
            {
                try
                {
                    var value = provider.FromString(stringdata, type, contextObject, culture);
                    if (value != null) return value;
                }
                catch
                {
                } // ignore errors. Assume unsupported value.
            }

            throw new FormatException(string.Format("Unable to parse '{0}' as a {1}.", stringdata, type));
        }

        /// <summary>
        /// Turn stringdata back to an object of type 'type', if an IStringConvertProvider plugin supports the string/type. returns true if the parsing was successful.
        /// </summary>
        /// <param name="stringdata"></param>
        /// <param name="type"></param>
        /// <param name="contextObject"></param>
        /// <param name="result">The result of the operation.</param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public static bool TryFromString(string stringdata, Type type, object contextObject, out object result, CultureInfo culture = null)
        {
            if (type == null)
                throw new ArgumentNullException("type");

            result = null;
            if (stringdata == null)
                return false;
            
            culture = culture ?? CultureInfo.InvariantCulture;
            foreach (var provider in Providers)
            {
                try
                {
                    var value = provider.FromString(stringdata, type, contextObject, culture);
                    if (value != null)
                    {
                        result = value;
                        return true;
                    }
                }
                catch
                {
                } // ignore errors. Assume unsupported value.
            }
            return false;
        }

    }

    // All the IStringConvertProvider default implementations.
    namespace StringConvertPlugins
    {
        /// <summary> String Convert probider for IConvertible. </summary>
        public class ConvertibleStringConvertProvider : IStringConvertProvider
        {
            /// <summary> Returns an IConvertible if applicable. </summary>
            public object FromString(string stringdata, Type type, object contextObject, CultureInfo culture)
            {
                if (type == typeof(DateTime))
                {
                    return DateTime.ParseExact(stringdata, "yyyy-MM-dd HH-mm-ss", culture);
                }
                if (type.DescendsTo(typeof(IConvertible)) && type.IsEnum == false)
                    return Convert.ChangeType(stringdata, type, culture);

                return null;
            }

            /// <summary> Turns an IConvertible into a string. </summary>
            public string GetString(object value, CultureInfo culture)
            {
                if (value is Enum)
                    return null;
                if (value is DateTime)
                    return ((DateTime)value).ToString("yyyy-MM-dd HH-mm-ss");
                if (value is IConvertible)
                    return (string)Convert.ChangeType(value, typeof(string), culture);
                
                return null;
            }
        }

        /// <summary> String Convert for Enabled of T. </summary>
        public class EnabledStringConvertProvider : IStringConvertProvider
        {
            static Type GetEnabledElementType(Type enumType)
            {
                if (enumType.IsArray)
                    return enumType.GetElementType();

                var ienumInterface = enumType.GetInterface("Enabled`1") ?? enumType;
                if (ienumInterface != null)
                    return ienumInterface.GetGenericArguments().FirstOrDefault();

                return typeof(object);
            }

            /// <summary> Creates an Enabled from a string. </summary>
            public object FromString(string stringdata, Type type, object contextObject, CultureInfo culture)
            {
                if (type.DescendsTo(typeof(Enabled<>)) == false)
                    return null;
                stringdata = stringdata.Trim();
                var disabled = "(disabled)";
                dynamic enabled = Activator.CreateInstance(type);
                var elemType = GetEnabledElementType(type);
                if (stringdata.StartsWith(disabled))
                {
                    enabled.Value = (dynamic)StringConvertProvider.FromString(stringdata.Substring(disabled.Length).Trim(), elemType, culture);
                    enabled.IsEnabled = false;
                }
                else
                {
                    enabled.Value = (dynamic)StringConvertProvider.FromString(stringdata, elemType, culture);
                    enabled.IsEnabled = true;
                }
                return enabled;

            }

            /// <summary> Turns Enabled into a string. </summary>
            public string GetString(object value, CultureInfo culture)
            {
                if (value is IEnabled && value.GetType().DescendsTo(typeof(Enabled<>)))
                {

                    dynamic val = value;
                    var inner = StringConvertProvider.GetString(val.Value, culture);
                    if (inner == null) return null;
                    if (val.IsEnabled)
                        return inner;
                    return "(disabled)" + inner;
                }
                return null;
            }
        }

        /// <summary> String Convert for enums. </summary>
        public class EnumStringConvertProvider : IStringConvertProvider
        {
            static bool tryParseEnumString(string str, Type type, out Enum result)
            {
                if (str == "")
                {
                    result = (Enum)Enum.ToObject(type, 0);
                    return true;
                }
                if (str.Contains('|'))
                {
                    var flagStrings = str.Split('|');
                    int flags = 0;
                    foreach (var flagString in flagStrings)
                    {
                        Enum result2;
                        if (tryParseEnumString(flagString, type, out result2))
                        {

                            flags += (int)Convert.ChangeType(result2, typeof(int));
                        }
                    }
                    result = (Enum)Enum.ToObject(type, flags);
                    return true;


                }
                try
                {   // Look for an exact match.
                    result = (Enum)Enum.Parse(type, str);
                    var values = Enum.GetValues(type);
                    if (Array.IndexOf(values, result) == -1)
                        return false;
                    return true;
                }
                catch (ArgumentException)
                {
                    // try a more robust parse method. (tolower, trim, '_'=' ')
                    str = str.Trim().ToLower();
                    var fixedNames = Enum.GetNames(type).Select(name => name.Trim().ToLower()).ToArray();
                    for (int i = 0; i < fixedNames.Length; i++)
                    {
                        if (fixedNames[i] == str || fixedNames[i].Replace('_', ' ') == str)
                        {
                            result = (Enum)Enum.GetValues(type).GetValue(i);
                            return true;
                        }
                    }
                }
                result = null;
                return false;
            }

            /// <summary> Creates an enum from a string. </summary>
            public object FromString(string stringdata, Type type, object contextObject, CultureInfo culture)
            {
                if (false == type.IsEnum) return null;
                Enum result;
                if (tryParseEnumString(stringdata, type, out result) == false)
                    return null;
                return result;
            }

            /// <summary> Turns an enum into a string. Usually just ToString unless flags. </summary>
            public string GetString(object value, CultureInfo culture)
            {
                if (false == value is Enum) return null;
                var type = value.GetType();
                if (type.HasAttribute<FlagsAttribute>())
                {
                    var values = Enum.GetValues(type);
                    StringBuilder sb = new StringBuilder();
                    Enum val = (Enum)value;
                    bool first = true;
                    foreach (Enum flag in values)
                    {
                        if (val.HasFlag(flag))
                        {
                            if (first)
                                first = false;
                            else
                                sb.Append('|');
                            sb.Append(flag);
                        }
                    }
                    return sb.ToString();
                }
                else
                {
                    return value.ToString();
                }

            }
        }

        /// <summary> String convert for list/IEnumerable types. </summary>
        public class ListStringConvertProvider : IStringConvertProvider
        {
            /// <summary> Creates a sequence from string. </summary>
            public object FromString(string stringdata, Type type, object contextObject, CultureInfo culture)
            {
                var elemType = type.GetEnumerableElementType();
                if (elemType == null) return null;

                if (elemType.IsNumeric() || elemType.IsEnum)
                {

                    Array seq = null;
                    if (elemType.IsNumeric())
                    {
                        var fmt = new NumberFormatter(culture);
                        var data = fmt.Parse(stringdata);
                        if (data.GetType().DescendsTo(type))
                            return data;
                        seq = data.CastTo(elemType).Cast<object>().ToArray();   
                    }
                    else if (elemType.IsEnum)
                    {
                        var lst = stringdata.Split(',');
                        List<object> items = new List<object>();
                        foreach (var item in lst)
                        {
                            var e = StringConvertProvider.FromString(item.Trim(), elemType, null);
                            if (e == null)
                                return null; // error parsing.
                            items.Add(e);
                        }
                        seq = items.ToArray();
                    }
                    if (type.IsArray)
                    {
                        var array = Array.CreateInstance(elemType, seq.Length);
                        int idx = 0;
                        foreach (var item in seq)
                            array.SetValue(item, idx++);
                        return array;
                    }
                    else if (type.GetConstructor(new Type[0]) != null)
                    {
                        dynamic lst = (dynamic)Activator.CreateInstance(type);
                        foreach (dynamic item in seq)
                            lst.Add(item);
                        return lst;
                    }
                    else
                    {
                        return null;
                    }
                }

                return null;
            }

            /// <summary> Turns a value into a string, </summary>
            public string GetString(object value, CultureInfo culture)
            {
                if (value is IEnumerable == false || value is string)
                    return null;
                var elemType = value.GetType().GetEnumerableElementType();
                if (elemType == null) return null;
                if (elemType.IsNumeric())
                {
                    var fmt = new NumberFormatter(culture);
                    return fmt.FormatRange((IEnumerable)value);
                }
                else if (elemType.IsEnum)
                {
                    return string.Join(", ", ((IEnumerable)value).Cast<object>().Select(x => StringConvertProvider.GetString(x, culture)));
                }
                return null;
            }
        }

        /// <summary>
        /// String convert for IResource types.
        /// </summary>
        public class ResourceStringConvertProvider : IStringConvertProvider
        {
            /// <summary> Finds a IResource based on strings. Only works on things loaded in Component Settings. </summary>
            public object FromString(string stringdata, Type type, object contextObject, CultureInfo culture)
            {
                if (type.DescendsTo(typeof(IResource)) == false) return null;
                return ComponentSettingsList.GetContainer(type).Cast<IResource>().FirstOrDefault(x => x.Name == stringdata);
            }

            /// <summary> Turns a resource into a string. </summary>
            public string GetString(object value, CultureInfo culture)
            {
                if (value is IResource)
                    return ((IResource)value).Name;
                return null;
            }
        }

        /// <summary> String convert provider for MacroString types.</summary>
        public class MacroStringConvertProvider : IStringConvertProvider
        {
            /// <summary> Creates a new MacroString, using the contextObject if its a ITestStep.</summary>
            public object FromString(string stringData, Type type, object contextObject, CultureInfo culture)
            {
                if (type.DescendsTo(typeof(MacroString)) == false) return null;
                return new MacroString(contextObject as ITestStepParent) { Text = stringData };
            }
            /// <summary> Extracts the text component of a macro string. </summary>
            public string GetString(object value, CultureInfo culture)
            {
                if (value is MacroString == false) return null;
                return ((MacroString)value).Text;
            }
        }

        /// <summary> String convert provider for SecureString </summary>
        public class SecureStringConvertProvider : IStringConvertProvider
        {
            /// <summary> Creates a new SecureString</summary>
            public object FromString(string stringData, Type targetType, object contextObject, CultureInfo culture)
            {
                if (targetType != typeof(SecureString)) return null;
                return stringData.ToSecureString();
            }
            /// <summary> Extracts the text component of a SecureString. </summary>
            public string GetString(object obj, CultureInfo culture)
            {
                if (obj is SecureString == false) return null;
                return ((SecureString)obj).ConvertToUnsecureString();
                
            }
        }

        /// <summary> Supports converting Inputs to a string and back. Requires the context to be an ITestStep.</summary>
        public class InputStringConvertProvider : IStringConvertProvider
        {
            /// <summary> Creates an Input from a string. contextObject must be an ITestStep.</summary>
            /// <param name="stringdata"></param>
            /// <param name="type"></param>
            /// <param name="contextObject"></param>
            /// <param name="culture"></param>
            /// <returns></returns>
            public object FromString(string stringdata, Type type, object contextObject, CultureInfo culture)
            {

                var s = stringdata.Split(':');
                var step = contextObject as ITestStep;
                if (step == null) return null;
                if (s.Length != 2) return null;
                Guid id;
                if (!Guid.TryParse(s[0], out id))
                    return null;
                var prop = s[1];
                
                var plan = step.GetParent<TestPlan>();
                var step2 = Utils.FlattenHeirarchy(plan.ChildTestSteps, x => x.ChildTestSteps).FirstOrDefault(x => x.Id == id);
                if (step2 == null) return null;
                var inp = (IInput)Activator.CreateInstance(type);
                inp.Step = step2;
                inp.Property = step2.GetType().GetProperty(s[1]);

                return inp;
            }

            /// <summary> Turns an IInput into a string. </summary>
            /// <param name="value"></param>
            /// <param name="culture"></param>
            /// <returns></returns>
            public string GetString(object value, CultureInfo culture)
            {
                var val = value as IInput;
                if (val == null) return null;
                if (val.Step == null || val.Property == null) return null;
                return string.Format("{0}:{1}", val.Step.Id, val.Property.Name);
            }
        }

        /// <summary> Supports converting Inputs to a string and back.</summary>
        public class BoolConverter : IStringConvertProvider
        {
            /// <summary> Creates a bool from a string. </summary>
            public object FromString(string stringdata, Type type, object ctx, CultureInfo culture)
            {
                if (type == typeof(bool))
                {
                    var sd = stringdata.ToLower();

                    switch (sd)
                    {
                        case "true":
                        case "y":
                        case "yes":
                            return true;
                        case "false":
                        case "n":
                        case "no":
                            return false;
                    }
                }

                return null;
            }

            /// <summary> Gets the string representation of a bool. </summary>
            public string GetString(object value, CultureInfo culture)
            {
                if (value is bool)
                    return value.ToString();
                else
                    return null;
            }

        }

    }
}
