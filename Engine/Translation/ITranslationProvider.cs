//            Copyright Keysight Technologies 2012-2025
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using System.IO;
using System.Collections.Immutable;
using System.Linq;
using System;
namespace OpenTap.Package.Translation;

internal interface ITranslationProvider
{
    public IEnumerable<CultureInfo> SupportedLanguages();
    public DisplayAttribute GetDisplayAttribute(IReflectionData mem, CultureInfo culture);
    public string Name { get; }
}

class TranslationFile : ITranslationProvider
{
    static string AttributeValue(XElement x, string name) => x.Attributes(name).FirstOrDefault()?.Value;
    private string[] packageNames; 
    private string[] PackageNames =>
        packageNames ??= _root.Elements(TranslationHelpers.PackageElementName)
            .Select(x => AttributeValue(x, TranslationHelpers.PackageNameAttributeName))
            .Where(x => x != null)
            .ToArray();

    public string Name => Path.GetFileName(File);
    private DateTime lastWrite; 
    private DateTime lastReloadCheck;

    internal void ReloadIfFileChanged()
    {
        if (DateTime.Now - lastReloadCheck < TimeSpan.FromSeconds(1))
            return;
        try
        {
            if (!System.IO.File.Exists(File))
                return;
            var write = new FileInfo(File).LastWriteTime;
            if (write != lastWrite)
            {
                var doc = XDocument.Load(File, LoadOptions.None);
                if (doc.Element(TranslationHelpers.TranslationElementName) is XElement translationElement)
                {
                    lock (_lookupLock)
                    {
                        lastWrite = write;
                        _lookup = ImmutableDictionary<IReflectionData, DisplayAttribute>.Empty;
                        packageNames = null;
                        _root = translationElement;
                    }
                }
            }
        }
        catch (Exception)
        {
            // ignore -- the file could be missing. Just try to reload it later, but keep caches.
        }
        finally
        {
            lastReloadCheck = DateTime.Now; 
        }
    }

    private TranslationFile(string file, XElement elem, CultureInfo culture)
    {
        File = file;
        _root = elem;
        _culture = culture;
        lastWrite = new FileInfo(File).LastWriteTime; 
        lastReloadCheck = DateTime.Now;
    }

    public string File { get; }
    private XElement _root;
    private readonly CultureInfo _culture;

    public static TranslationFile CreateFromFile(string file)
    {
        if (System.IO.File.Exists(file))
        {
            var xml = XDocument.Load(file, LoadOptions.None);
            if (xml.Element(TranslationHelpers.TranslationElementName) is XElement translationElement)
            {
                
                if (AttributeValue(translationElement, TranslationHelpers.IsoLanguageAttributename) is string iso)
                {
                    var culture = new CultureInfo(iso);
                    if (culture.CultureTypes.HasFlag(CultureTypes.UserCustomCulture) == false)
                        return new TranslationFile(file, translationElement, culture);
                }
            }
        }
        return null;
    }

    private IEnumerable<XElement> GetElementsRecursive(XElement classElement, XName name)
    {
        if (classElement == null) yield break;
        var queue = new Queue<XElement>();
        queue.Enqueue(classElement);
        while (queue.Any())
        {
            var elem = queue.Dequeue();
            foreach (var prop in elem.Elements(name))
            {
                yield return prop;
            }

            foreach (var grp in elem.Elements(TranslationHelpers.DisplayGroupElementName))
            { 
                queue.Enqueue(grp);
            }
        }
    }

    private DisplayAttribute ComputeDisplayAttribute(IMemberData mem)
    {
        var classElement = GetElementForType(mem.DeclaringType);
        if (classElement == null) return null;
        
        var propertyElement = GetElementsRecursive(classElement, TranslationHelpers.MemberElementName)
            .FirstOrDefault(elem => AttributeValue(elem, TranslationHelpers.PropertyIdAttributeName) == mem.Name);

        if (propertyElement == null) return null;

        var defaultDisplay = DefaultDisplayAttribute.GetUntranslatedDisplayAttribute(mem);
        return MergeAttributes(propertyElement, defaultDisplay);
    }

    private DisplayAttribute GetDisplayAttribute(IMemberData mem, CultureInfo culture)
    {
        if (!culture.Equals(_culture)) return null;
        ReloadIfFileChanged();
        if (_lookup.TryGetValue(mem, out var disp))
            return disp;
        disp = ComputeDisplayAttribute(mem);
        lock (_lookupLock)
            _lookup = _lookup.SetItem(mem, disp);
        return disp;
    }

    public IEnumerable<CultureInfo> SupportedLanguages()
    {
        yield return _culture;
    }

    private ImmutableDictionary<IReflectionData, DisplayAttribute> _lookup = ImmutableDictionary<IReflectionData, DisplayAttribute>.Empty;
    private readonly object _lookupLock = new();

    private XElement GetElementForType(ITypeData type)
    {
        var location = TranslationHelpers.GetRelativeFilePathNormalized(ExecutorClient.ExeDir, type);
        var pkgElements = _root.Elements(TranslationHelpers.PackageElementName);
        
        var fileElement = pkgElements.SelectMany(x => x.Elements(TranslationHelpers.FileElementName))
            .FirstOrDefault(file =>
                AttributeValue(file, TranslationHelpers.SourceAttributeName) == location);

        var typeElement = GetElementsRecursive(fileElement, TranslationHelpers.TypeElementName)
            .FirstOrDefault(elem => AttributeValue(elem, TranslationHelpers.TypeIdPropertyName) == type.Name);
        
        return typeElement;
    }

    private DisplayAttribute MergeAttributes(XElement element, DisplayAttribute defaultDisplay)
    {
        var groups2 = new List<string>();
        {
            var grp = element.Parent;
            while (grp?.Name == TranslationHelpers.DisplayGroupElementName)
            {
                groups2.Insert(0, AttributeValue(grp, TranslationHelpers.DisplayNameAttributeName));
                grp = grp.Parent;
            }
        }
        var name = AttributeValue(element, TranslationHelpers.DisplayNameAttributeName) ?? defaultDisplay.Name;
        var description = AttributeValue(element, TranslationHelpers.DisplayDescriptionAttributeName) ?? defaultDisplay.Description;
        var orderString = AttributeValue(element, TranslationHelpers.DisplayOrderAttributeName);
        var order = orderString == null ? defaultDisplay.Order : double.Parse(orderString);
        var disp = new DisplayAttribute(_culture, name, description, null, order, defaultDisplay.Collapsed, groups2.ToArray());
        return disp;
    }

    private DisplayAttribute ComputeDisplayAttribute(ITypeData type)
    {
        var classElement = GetElementForType(type);
        if (classElement == null) return null;

        // Found! Return a new display attribute 
        var defaultDisplay = DefaultDisplayAttribute.GetUntranslatedDisplayAttribute(type);
        return MergeAttributes(classElement, defaultDisplay);
    }

    private DisplayAttribute GetDisplayAttribute(ITypeData type, CultureInfo culture)
    {
        if (!culture.Equals(_culture)) return null;
        if (_lookup.TryGetValue(type, out var disp))
            return disp;
        disp = ComputeDisplayAttribute(type);
        lock (_lookupLock)
            _lookup = _lookup.SetItem(type, disp);
        return disp;
    } 
    
    public DisplayAttribute GetDisplayAttribute(IReflectionData i, CultureInfo culture)
    {
        if (i is ITypeData td) return GetDisplayAttribute(td, culture);
        if (i is IMemberData mem) return GetDisplayAttribute(mem, culture);
        return null;
    }
}  