//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests.UnitTestUtils
{
    /// <summary>
    /// This class can test INotifyPropertyChanged types and make sure that the setters and getters behave correspondingly.
    /// PropertyChanged events should always refer to an EXISTING property.
    /// The event should NOT be invoked if the value is set to the current value.
    /// </summary>
    [TestFixture]
    public class ValidatingObjectConformance
    {

        static bool validProp(PropertyInfo prop)
        {
            if (!(prop.CanRead && prop.CanWrite))
            {
                return false;
            }
            if (prop.HasAttribute<XmlIgnoreAttribute>())
            {
                return false;
            }

            if (prop.HasAttribute<BrowsableAttribute>())
            {
                if (prop.GetAttribute<BrowsableAttribute>().Browsable == false)
                {
                    return false;
                }
            }

            return true;
        }

        static object GetUniqueValue(Type genType)
        {
            try
            {
                Type finalType = genType;
                if (genType.IsPrimitive)
                {
                    if (genType.IsEnum)
                    {
                        var vals = Enum.GetValues(genType);
                        if (vals.Length > 1)
                        {
                            return vals.GetValue(1);
                        }
                        return vals.GetValue(0);
                    }
                    if (genType == typeof(string))
                    {
                        return "..";
                    }
                    //126 random value. The works with most types.
                    return Convert.ChangeType(126, genType);
                }
                else if (genType.IsInterface || genType.IsAbstract)
                {
                    var iface = PluginManager.GetPlugins(genType)
                        .FirstOrDefault(item => !item.IsInterface);
                    finalType = iface;
                }
                else if (genType == typeof(string))
                {
                    return new Guid().ToString();
                }
                else if (genType.IsArray)
                {
                    var elemType = genType.GetElementType();
                    var outarr = Array.CreateInstance(elemType, 1);
                    outarr.SetValue(GetUniqueValue(elemType), 0);
                    return outarr;

                }


                return Activator.CreateInstance(finalType);
            }
            catch (Exception)
            {
                return null;
            }
        }


        static bool propertyChangedOk(Type holder, PropertyChangedEventArgs args)
        {
            string propStr = args.PropertyName;
            if (String.IsNullOrEmpty(propStr))
            {
                return true;
            }
            foreach (string subStr in propStr.Split(','))
            {
                var prop = holder.GetProperty(subStr);
                if (null == prop || !prop.CanRead)
                {
                    return false;
                }
            }
            return true;

        }

        public class ErrorPropException : Exception
        {
            public ErrorPropException(string msg)
                : base(msg)
            {

            }
        }

        public class WarningPropException : ErrorPropException
        {
            public WarningPropException(string msg) : base(msg)
            {

            }
        }

        static public List<Exception> testTypeProperties(Type tp)
        {
            List<Exception> ex = new List<Exception>();
            if (!tp.IsClass || !tp.HasInterface<INotifyPropertyChanged>())
            {
                return ex;
            }

            var properties = tp.GetProperties(BindingFlags.Instance
                | BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(validProp).ToArray();

            if (properties.Length == 0)
            {
                return ex;
            }
            INotifyPropertyChanged instance;
            try
            {
                instance = (INotifyPropertyChanged)Activator.CreateInstance(tp);
            }
            catch (Exception)
            {
                return ex;
            }
            instance.PropertyChanged += (s, e) =>
            {
                if (!propertyChangedOk(tp, e))
                {
                    throw new ErrorPropException(String.Format("Property '{0}' does not exist on type '{1}'.", e.PropertyName, tp.FullName));
                }
            };

            bool shouldNotChange = false;

            instance.PropertyChanged += (s, e) =>
            {

                if (shouldNotChange)
                {
                    throw new WarningPropException(String.Format("Event invoked even when value is the same. Property: '{0}' Type: '{1}'", e.PropertyName, tp.FullName));
                }
            };

            foreach (var prop in properties)
            {
                try
                {
                    prop.SetValue(instance, GetUniqueValue(prop.PropertyType), null);

                    //Test set/get same value
                    shouldNotChange = true;
                    prop.SetValue(instance, prop.GetValue(instance, null), null);
                    shouldNotChange = false;
                }
                catch (Exception _ex)
                {
                    if (_ex.InnerException is ErrorPropException)
                    {
                        ex.Add(_ex.InnerException);
                    }
                }
            }

            return ex;
        }

        [Test, Ignore("This will generate false positives.")]
        public void TestINotifyPropertyChanged()
        {
            List<Exception> exceptions = new List<Exception>();
            foreach (var type in PluginManager.GetPlugins<INotifyPropertyChanged>())
            {
                exceptions.AddRange(testTypeProperties(type));
            }
            if (exceptions.Any())
            {
                var warnings = exceptions.Where(ex => ex is WarningPropException);
                warnings.Evaluate(ex =>
                {
                    Debug.Print("Warning: " + ex.Message);
                });
                var errors = exceptions.Where(ex => !(ex is WarningPropException)).ToList();
                if (errors.Count > 0)
                {
                    throw new AggregateException(errors);
                }
            }
        }

        public static void TestAssembly(Assembly asm)
        {
            List<Exception> exceptions = new List<Exception>();

            foreach (var type in asm.GetTypes())
            {
                exceptions.AddRange(testTypeProperties(type));
            }
            if (exceptions.Any())
            {
                var warnings = exceptions.Where(ex => ex is WarningPropException);
                warnings.Evaluate(ex =>
                {
                    Debug.Print("Warning: " + ex.Message);
                });
                var errors = exceptions.Where(ex => !(ex is WarningPropException)).ToList();
                if (errors.Count > 0)
                {
                    throw new AggregateException(errors);
                }
            }
        }
    }
}
