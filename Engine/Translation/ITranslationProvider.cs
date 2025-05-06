//            Copyright Keysight Technologies 2012-2025
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace OpenTap.Translation;

internal interface ITranslationProvider
{
    DisplayAttribute GetDisplayAttribute(IReflectionData mem);
    DisplayAttribute GetDisplayAttribute(Enum e, string name);
    T GetTranslation<T>() where T : StringLocalizer, new();
}

internal class ResXTranslationProvider : ITranslationProvider
{
    public CultureInfo Culture { get; }
    private readonly Dictionary<string, DateTime> UpdateTime = [];
    public ResXTranslationProvider(CultureInfo culture, IEnumerable<string> files)
    {
        Culture = culture;
        foreach (var file in files)
        {
            // Mark all files as 
            UpdateTime[file] = DateTime.MinValue;
        }
    }
    
    static string AttributeValue(XElement x, string name) => x.Attributes(name).FirstOrDefault()?.Value; 
    static string ElementValue(XElement x, string name) => x.Elements(name).FirstOrDefault()?.Value; 

    DisplayAttribute ComputeDisplayAttribute(string key, DisplayAttribute fallback)
    {
        var name = GetValue(_stringLookup, $"{key}.Name") ?? fallback.Name;
        var description = GetValue(_stringLookup, $"{key}.Description") ?? fallback.Description;
        var group = GetValue(_stringLookup, $"{key}.Group") ?? string.Join(" \\ ", fallback.Group);
        double order = fallback.Order;
        if (GetValue(_stringLookup, $"{key}.Order") is {} ord)
            if (double.TryParse(ord, out var o))
                order = o;
        return new DisplayAttribute(Culture, name, description, group, order, fallback.Collapsed); 
    }

    DisplayAttribute ComputeDisplayAttribute(Enum e, string name)
    {
        var enumType = e.GetType();
        var key = enumType.FullName + $".{name}";
        var fallback = enumType.GetMember(name).FirstOrDefault().GetDisplayAttribute();
        return ComputeDisplayAttribute(key, fallback);
    }

    DisplayAttribute ComputeDisplayAttribute(IReflectionData mem)
    {
        var key = mem switch
        {
            IMemberData imem => $"{imem.DeclaringType.Name}.{imem.Name}",
            ITypeData tmem => tmem.Name,
            _ => mem.Name
        };
        
        var fallback = DefaultDisplayAttribute.GetUntranslatedDisplayAttribute(mem);
        return ComputeDisplayAttribute(key, fallback);
    }

    private object updateLock = new();

    private ImmutableDictionary<Type, StringLocalizer> _localizerLookup = ImmutableDictionary<Type, StringLocalizer>.Empty;
    private ImmutableDictionary<IReflectionData, DisplayAttribute> _displayLookup = ImmutableDictionary<IReflectionData, DisplayAttribute>.Empty;
    private ImmutableDictionary<Enum, DisplayAttribute> _enumDisplayLookup = ImmutableDictionary<Enum, DisplayAttribute>.Empty;
    private ImmutableDictionary<string, string> _stringLookup = ImmutableDictionary<string, string>.Empty;

    bool NeedsUpdate(string key)
    {
        if (!File.Exists(key)) return false;
        if (!UpdateTime.TryGetValue(key, out var lastUpdate)) return false;
        if (DateTime.Now - lastUpdate < TimeSpan.FromSeconds(1)) return false;
        if (lastUpdate == DateTime.MinValue) return true;
        var lastWrite = new FileInfo(key).LastWriteTime;
        if (lastWrite > lastUpdate) return true;
        return false;
    }
    void MaybeUpdateMappings()
    {
        lock (updateLock)
        {
            var updates = new Dictionary<string, DateTime>();
            var newDict = new Dictionary<string, string>();
            foreach (var kvp in UpdateTime)
            {
                if (NeedsUpdate(kvp.Key))
                {
                    updates[kvp.Key] = new FileInfo(kvp.Key).LastWriteTime;
                    try
                    {
                        var doc = XDocument.Load(kvp.Key);
                        foreach (var ele in doc?.Root?.Elements("data")?.ToArray() ?? [])
                        {
                            if (AttributeValue(ele, "name") is {} key
                                && ElementValue(ele, "value") is { } value)
                            {
                                newDict[key] = value;
                            }
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                } 

                _stringLookup = _stringLookup.AddRange(newDict);
                _displayLookup = ImmutableDictionary<IReflectionData, DisplayAttribute>.Empty;
                _enumDisplayLookup = ImmutableDictionary<Enum, DisplayAttribute>.Empty;
                _localizerLookup = ImmutableDictionary<Type, StringLocalizer>.Empty;
            }
            foreach (var u in updates)
            {
                UpdateTime[u.Key] = u.Value;
            } 
        }
    }

    public DisplayAttribute GetDisplayAttribute(IReflectionData mem)
    {
        MaybeUpdateMappings();
        if (_displayLookup.TryGetValue(mem, out var disp)) return disp;
        disp = ComputeDisplayAttribute(mem);
        _displayLookup = _displayLookup.SetItem(mem, disp);
        return disp;
    }

    public DisplayAttribute GetDisplayAttribute(Enum e, string name)
    {
        MaybeUpdateMappings();
        if (_enumDisplayLookup.TryGetValue(e, out var disp)) return disp;
        disp = ComputeDisplayAttribute(e, name);
        _enumDisplayLookup = _enumDisplayLookup.SetItem(e, disp);
        return disp;
    }

    static string GetValue(IDictionary<string, string> d, string key)
    {
        if (d.TryGetValue(key, out var value)) return value;
        return null;
    }

    private readonly static TraceSource log = Log.CreateSource("Translation");
    StringLocalizer ComputeStringLocalizer<T>() where T : StringLocalizer, new()
    {
        var t = Activator.CreateInstance<T>();
        foreach (var fld in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (fld.FieldType != typeof(string)) 
                continue;
            
            if (_stringLookup.TryGetValue($"{typeof(T).FullName}.{fld.Name}", out var newValue))
            {
                var sourceValue = (string)fld.GetValue(t);
                var sourceCount = CountTemplates(sourceValue);
                var newCount = CountTemplates(newValue);
                // Count the number of template parameters in a format string.
                // If there is a parameter count mismatch, a runtime error will occur
                // when the string is used in a string.Format call.
                // Of course, this assumes that all strings are format strings,
                // which is likely going to be the exception rather than the norm,
                // but strings containing balanced curly braces is probably also going to be rare,
                // and in cases where they are, the braces are likely also going to match in translated strings,
                // so I consider this an acceptable trade-off since the alternative would be treating changes to
                // format strings as breaking changes. With this check, it becomes a missing translation instead,
                // which is probably preferable.
                if (newCount != sourceCount)
                {
                    log.ErrorOnce(fld, $"Template parameter count mismatch detected.\n" +
                                       $"'{sourceValue}' has {sourceCount} template parameters.\n" +
                                       $"'{newValue}' has {newCount} template parameters.\n" +
                                       $"The first string will be preferred over the second string.");
                    continue;
                }
                fld.SetValue(t, newValue);
            }
        }

        return t;

        static int CountTemplates(string s)
        {
            var chars = s.ToArray();
            int n = 0;
            bool bracketOpen = false;
            for (int i = 0; i < chars.Length; i++)
            {
                if (!bracketOpen && chars[i] == '{')
                {
                    bracketOpen = true;
                }
                else if (bracketOpen && chars[i] == '{')
                {
                    /* double bracket escapes */ 
                    bracketOpen = false;
                }
                else if (bracketOpen && chars[i] == '}')
                {
                    bracketOpen = false;
                    n++;
                }
            }
            return n;
        }
    }
    public T GetTranslation<T>() where T : StringLocalizer, new()
    {
        MaybeUpdateMappings();
        if (_localizerLookup.TryGetValue(typeof(T), out StringLocalizer t)) return (T)t;
        t = ComputeStringLocalizer<T>();
        _localizerLookup = _localizerLookup.SetItem(typeof(T), t);
        return (T)t;
    }
}