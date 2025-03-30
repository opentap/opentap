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
namespace OpenTap.Package.Translation;

internal interface ITranslationProvider
{
    public IEnumerable<CultureInfo> SupportedLanguages();
    public DisplayAttribute GetDisplayAttribute(IMemberData mem, CultureInfo culture);
    public DisplayAttribute GetDisplayAttribute(ITypeData mem, CultureInfo culture);
    public string Name { get; }
}

class TranslationFile : ITranslationProvider
{
    private string packageName;
    private string PackageName =>
        packageName ??= _root.Attribute(TranslationHelpers.PackageNameAttribute).Value;

    public string Name => Path.GetFileName(File);
    private TranslationFile(string file, XElement elem, CultureInfo culture)
    {
        File = file;
        _root = elem;
        _culture = culture;
    }

    public string File { get; }
    private readonly XElement _root;
    private readonly CultureInfo _culture;

    public static TranslationFile CreateFromFile(string file)
    {
        if (System.IO.File.Exists(file))
        {
            var xml = XDocument.Load(file, LoadOptions.None);
            if (xml.Element(TranslationHelpers.RootElementName) is XElement translationElement)
            {
                if (translationElement.Attribute(TranslationHelpers.IsoLanguageAttributename)?.Value is string iso)
                {
                    var culture = new CultureInfo(iso);
                    if (culture.CultureTypes.HasFlag(CultureTypes.UserCustomCulture) == false)
                        return new TranslationFile(file, translationElement, culture);
                }
            }
        }
        return null;
    }

    private ImmutableDictionary<IMemberData, DisplayAttribute> _memberLookup = ImmutableDictionary<IMemberData, DisplayAttribute>.Empty;
    private readonly object _memLock = new();

    private DisplayAttribute ComputeDisplayAttribute(IMemberData mem)
    {
        var classElement = GetElementForType(mem.DeclaringType);
        if (classElement == null) return null;

        var propertyElement = classElement.Elements(TranslationHelpers.MemberElementName)
            .FirstOrDefault(elem => elem.Attribute(TranslationHelpers.PropertyIdAttributeName)?.Value == mem.Name);

        if (propertyElement == null) return null;

        var defaultDisplay = mem.GetDisplayAttribute();
        return MergeAttributes(propertyElement, defaultDisplay);
    }
    public DisplayAttribute GetDisplayAttribute(IMemberData mem, CultureInfo culture)
    {
        if (!culture.Equals(_culture)) return null;
        if (_memberLookup.TryGetValue(mem, out var disp))
            return disp;
        disp = ComputeDisplayAttribute(mem);
        lock (_memLock)
            _memberLookup = _memberLookup.SetItem(mem, disp);
        return disp;
    }

    public IEnumerable<CultureInfo> SupportedLanguages()
    {
        yield return _culture;
    }

    private ImmutableDictionary<ITypeData, DisplayAttribute> _typeLookup = ImmutableDictionary<ITypeData, DisplayAttribute>.Empty;
    private readonly object _typeLock = new();

    private XElement GetElementForType(ITypeData type)
    {
        var pkg = Installation.Current.FindPackageContainingType(type);
        if (pkg?.Name != PackageName) return null;
        var src = TypeData.GetTypeDataSource(type);
        if (src == null) return null;
        var location = TranslationHelpers.GetRelativeFilePathNormalized(Installation.Current, type);
        var fileElement = _root.Elements(TranslationHelpers.FileElementName)
            .FirstOrDefault(file =>
                    file.Attribute(TranslationHelpers.SourceAttributeName)?.Value == location);
        if (fileElement == null) return null;

        var classElement = fileElement.Elements(TranslationHelpers.TypeElementName)
            .FirstOrDefault(elem => elem.Attribute(TranslationHelpers.TypeIdPropertyName)?.Value == type.Name);
        return classElement;
    }

    private DisplayAttribute MergeAttributes(XElement elem, DisplayAttribute defaultDisplay)
    {
        var name = elem.Attribute("Name")?.Value ?? defaultDisplay.Name;
        var description = elem.Attribute("Description").Value ?? defaultDisplay.Description;
        var disp = new DisplayAttribute(_culture, name, description, null, defaultDisplay.Order, defaultDisplay.Collapsed, defaultDisplay.Group);
        return disp;
    }

    private DisplayAttribute ComputeDisplayAttribute(ITypeData type)
    {
        var classElement = GetElementForType(type);
        if (classElement == null) return null;

        // Found! Return a new display attribute 
        var defaultDisplay = type.GetDisplayAttribute();
        return MergeAttributes(classElement, defaultDisplay);
    }

    public DisplayAttribute GetDisplayAttribute(ITypeData type, CultureInfo culture)
    {
        if (!culture.Equals(_culture)) return null;
        if (_typeLookup.TryGetValue(type, out var disp))
            return disp;
        disp = ComputeDisplayAttribute(type);
        lock (_typeLock)
            _typeLookup = _typeLookup.SetItem(type, disp);
        return disp;
    }
}
