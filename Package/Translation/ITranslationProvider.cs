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
    private string packageName;
    private string PackageName =>
        packageName ??= _root.Attribute(TranslationHelpers.PackageNameAttribute).Value;

    public string Name => Path.GetFileName(File);
    private DateTime lastWrite = DateTime.MinValue;

    internal void ReloadIfFileChanged()
    {
        try
        {
            var write = new FileInfo(File).LastWriteTime;
            if (write != lastWrite)
            {
                var doc = XDocument.Load(File, LoadOptions.None);
                if (doc.Element(TranslationHelpers.RootElementName) is XElement translationElement)
                {
                    lock (_lookupLock)
                    {
                        lastWrite = write;
                        _lookup = ImmutableDictionary<IReflectionData, DisplayAttribute>.Empty;
                        _root = translationElement;
                    }
                }
            }
        }
        catch (Exception)
        {
            // ignore -- the file could be missing. Just try to reload it later, but keep caches.
        }
    }

    private TranslationFile(string file, XElement elem, CultureInfo culture)
    {
        File = file;
        _root = elem;
        _culture = culture;
        lastWrite = new FileInfo(File).LastWriteTime;
    }

    public string File { get; }
    private XElement _root;
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

    private DisplayAttribute ComputeDisplayAttribute(IMemberData mem)
    {
        var classElement = GetElementForType(mem.DeclaringType);
        if (classElement == null) return null;

        var propertyElement = classElement.Elements(TranslationHelpers.MemberElementName)
            .FirstOrDefault(elem => elem.Attribute(TranslationHelpers.PropertyIdAttributeName)?.Value == mem.Name);

        if (propertyElement == null) return null;

        var defaultDisplay = DefaultDisplayAttribute.GetUntranslatedDisplayAttribute(mem);
        return MergeAttributes(propertyElement, defaultDisplay);
    }

    private DisplayAttribute GetDisplayAttribute(IMemberData mem, CultureInfo culture)
    {
        if (!culture.Equals(_culture)) return null;
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
        var name = elem.Attributes("Name")?.FirstOrDefault()?.Value ?? defaultDisplay.Name;
        var description = elem.Attributes("Description")?.FirstOrDefault()?.Value ?? defaultDisplay.Description;
        var disp = new DisplayAttribute(_culture, name, description, null, defaultDisplay.Order, defaultDisplay.Collapsed, defaultDisplay.Group);
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

class DefaultDisplayAttribute : DisplayAttribute
{
    internal static DisplayAttribute GetUntranslatedDisplayAttribute(IReflectionData mem)
    {
        DisplayAttribute attr;
        if (mem is TypeData td)
            attr = td.Display;
        else
            attr = mem.GetAttribute<DisplayAttribute>();
        if (attr != null) return attr;
        // auto-generate a display attribute.
        return new DefaultDisplayAttribute(mem);
    }

    static string GetMemberName(IReflectionData mem)
    {
        // mem.Name has to be something fully qualifiable, but the display attribute name should be something more human friendly.
        var name = mem.Name;
        if (name.EndsWith("]]"))
        {
            // This is probably a generic C# type. These have the format Namespace.TypeName`N[[assemblyQualifiedNameOfFirstGenericArgument][assemblyQualifiedNameOfSecondGenericArgument]...]
            var idx = name.LastIndexOf("[[");
            if (idx >= 0)
                name = name.Substring(0, idx);
        }
        return name.Split('.').Last().Split('+').Last();
    }

    /// <summary> Always true for this class. </summary>
    public override bool IsDefaultAttribute() => true;

    public DefaultDisplayAttribute(IReflectionData mem) : base(GetMemberName(mem))
    {
    }
}
