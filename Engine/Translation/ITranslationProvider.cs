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
using System.Xml.Linq;

namespace OpenTap.Translation;

internal interface ITranslationProvider
{
    DisplayAttribute GetDisplayAttribute(IReflectionData mem);
    DisplayAttribute GetDisplayAttribute(Enum e, string name);
    string GetString(string key);
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
        if (GetValue(_stringLookup, $"{key}.Order") is { } ord)
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
            // If the type is generic, the lookup key should not include the generic type parameter
            ITypeData td2 when td2.AsTypeData()?.Type is { } tp && tp.IsGenericType
                => TypeData.FromType(tp.GetGenericTypeDefinition()).Name,
            ITypeData tmem => tmem.Name,
            _ => mem.Name
        };

        var fallback = DefaultDisplayAttribute.GetUntranslatedDisplayAttribute(mem);
        return ComputeDisplayAttribute(key, fallback);
    }

    private readonly object updateLock = new();

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
                            if (AttributeValue(ele, "name") is { } key
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

                _stringLookup = _stringLookup.SetItems(newDict);
                _displayLookup = ImmutableDictionary<IReflectionData, DisplayAttribute>.Empty;
                _enumDisplayLookup = ImmutableDictionary<Enum, DisplayAttribute>.Empty;
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

    public string GetString(string key)
    {
        MaybeUpdateMappings();
        return _stringLookup.TryGetValue(key, out var val) ? val : null;
    }
}
