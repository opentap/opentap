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
using System.Threading;
using System.Xml.Linq;

namespace OpenTap.Translation;

internal interface ITranslationProvider
{
    DisplayAttribute GetDisplayAttribute(IReflectionData mem);
    DisplayAttribute GetDisplayAttribute(Enum e, string name);
    string GetString(string key);
}

internal class ResXTranslationProvider : ITranslationProvider, IDisposable
{
    private TapThread _updateThread = null; 
    private readonly object StartUpdateThreadLock = new();
    void MaybeStartUpdateThread()
    {
        if (_updateThread != null) return;
        lock (StartUpdateThreadLock)
        {
            // If we got the lock after the update thread was initialized, we shouldn't initialie it again
            if (_updateThread != null) return;
            
            // If we are starting the update thread for the first time, we should block while initializing translations.
            UpdateInvalidatedMappings();
            
            TapThread.WithNewContext(() =>
            {
                _updateThread = TapThread.Start(() =>
                {
                    while (!TapThread.Current.AbortToken.IsCancellationRequested)
                    {
                        try
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(5));
                            UpdateInvalidatedMappings();
                        }
                        catch
                        {
                            // we don't need to handle exceptions here, but we should ensure the update thread doesn't die
                        }
                    }
                });
            });
        }
    }

    public void Dispose()
    {
        _updateThread?.Abort();
    }

    public CultureInfo Culture { get; }
    private readonly Dictionary<string, string> CacheFileInvalidationTable = [];
    public ResXTranslationProvider(CultureInfo culture, IEnumerable<string> files)
    {
        Culture = culture;
        foreach (var file in files)
        {
            // Set the initial invalidation key for all files
            CacheFileInvalidationTable[file] = string.Empty;
        }
    }

    static string AttributeValue(XElement x, string name) => x.Attributes(name).FirstOrDefault()?.Value;
    static string ElementValue(XElement x, string name) => x.Elements(name).FirstOrDefault()?.Value;

    DisplayAttribute ComputeDisplayAttribute(string key, DisplayAttribute neutral)
    {
        var name = GetString($"{key}.Name") ?? neutral.Name;
        var description = GetString($"{key}.Description") ?? neutral.Description;
        var group = GetString($"{key}.Group") ?? string.Join(" \\ ", neutral.Group);
        double order = neutral.Order;
        if (GetString($"{key}.Order") is { } ord)
            if (double.TryParse(ord, out var o))
                order = o;
        return new DisplayAttribute(Culture, name, description, group, order, neutral.Collapsed) { NeutralDisplayAttribute = neutral };
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

    private ImmutableDictionary<IReflectionData, DisplayAttribute> _displayLookup = ImmutableDictionary<IReflectionData, DisplayAttribute>.Empty;
    private ImmutableDictionary<Enum, DisplayAttribute> _enumDisplayLookup = ImmutableDictionary<Enum, DisplayAttribute>.Empty;
    private ImmutableDictionary<string, string> _stringLookup = ImmutableDictionary<string, string>.Empty;

    private static readonly TraceSource log = Log.CreateSource("Translation");
    void UpdateInvalidatedMappings()
    {
        var newDict = new Dictionary<string, string>();
        foreach (var file in CacheFileInvalidationTable.Keys.ToArray())
        {
            var invalidationKey = GetCacheMarker(file);
            if (CacheFileInvalidationTable[file] != invalidationKey)
            {
                CacheFileInvalidationTable[file] = invalidationKey;
                try
                {
                    var resxDoc = XDocument.Load(file);

                    if (resxDoc.Root == null)
                        throw new Exception("Invalid XML");

                    foreach (var ele in resxDoc.Root.Elements("data"))
                    {
                        if (AttributeValue(ele, "name") is { } translationKey
                                && ElementValue(ele, "value") is { } translatedValue)
                        {
                            newDict[translationKey] = translatedValue;
                        }
                    }
                    log.Debug($"Reloaded translation file '{file}'.");
                }
                catch(Exception e)
                {
                    log.Error($"Unable to load translation resource file {file}.");
                    log.Debug(e);
                }
                _stringLookup = _stringLookup.SetItems(newDict);
                _displayLookup = ImmutableDictionary<IReflectionData, DisplayAttribute>.Empty;
                _enumDisplayLookup = ImmutableDictionary<Enum, DisplayAttribute>.Empty;
            }
        }

        static string GetCacheMarker(string file)
        {
            string key = string.Empty;
            if (File.Exists(file))
            {
                var fi = new FileInfo(file);
                key = $"{fi.LastWriteTimeUtc}.{fi.Length}";
            }
            return key;
        }
    }

    public DisplayAttribute GetDisplayAttribute(IReflectionData mem)
    {
        if (_displayLookup.TryGetValue(mem, out var disp)) return disp;
        disp = ComputeDisplayAttribute(mem);
        _displayLookup = _displayLookup.SetItem(mem, disp);
        return disp;
    }

    public DisplayAttribute GetDisplayAttribute(Enum e, string name)
    {
        if (_enumDisplayLookup.TryGetValue(e, out var disp)) return disp;
        disp = ComputeDisplayAttribute(e, name);
        _enumDisplayLookup = _enumDisplayLookup.SetItem(e, disp);
        return disp;
    }

    public string GetString(string key)
    {
        MaybeStartUpdateThread();
        return _stringLookup.TryGetValue(key, out var val) ? val : null;
    }
}
