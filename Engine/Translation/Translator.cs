using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenTap.Translation;

internal interface ITranslator
{
    public IEnumerable<CultureInfo> SupportedLanguages { get; }
    public DisplayAttribute TranslateMember(IReflectionData i, CultureInfo language);
    public string TranslateString(string key, CultureInfo language);
    public DisplayAttribute TranslateEnum(Enum e, CultureInfo language);
}

internal class Translator : ITranslator
{
    internal static string CultureAsString(CultureInfo culture)
    {
        if (CultureInfo.InvariantCulture.Equals(culture)) return "Neutral";
        return $"{culture.NativeName} ({culture.EnglishName})";
    }

    public IEnumerable<CultureInfo> SupportedLanguages => _cultures;

    /// <summary>
    /// Get a display attribute for the provided member in the requested language.
    /// <param name="i">The type or property a translated display attribute for.</param>
    /// <param name="language">The desired language of the output attribute. Defaults to the currently selected language
    /// if not specified.</param>
    /// <returns>A display attribute in the requested language. If no translation could be provided,
    /// the default DisplayAttribute is returned instead. Check the Language property of the returned attribute.</returns>
    /// </summary>
    public DisplayAttribute TranslateMember(IReflectionData i, CultureInfo language)
    {
        if (i == null) return null;
        if (_lookup.TryGetValue(language, out var prov))
        {
            if (prov.GetDisplayAttribute(i) is { } result)
                return result;
        }
        return DefaultDisplayAttribute.GetUntranslatedDisplayAttribute(i);
    }

    public DisplayAttribute TranslateEnum(Enum e, CultureInfo language)
    {
        var enumType = e.GetType();
        var name = enumType.GetEnumName(e);
        // This happens when the enum value is out of range, or if a flags enum is passed with multiple flags set
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Enum value out of range.");

        if (_lookup.TryGetValue(language, out var prov))
            if (prov.GetDisplayAttribute(e, name) is { } disp)
                return disp;

        // fallback
        var memberInfo = enumType.GetMember(name).FirstOrDefault();
        return memberInfo.GetDisplayAttribute();
    }

    public string TranslateString(string key, CultureInfo language)
    {
        if (_lookup.TryGetValue(language, out var prov))
            return prov.GetString(key);
        return null;
    }

    private readonly ImmutableArray<CultureInfo> _cultures;
    private ImmutableDictionary<CultureInfo, ITranslationProvider> _lookup;

    public void AddTranslationProvider(CultureInfo language, ITranslationProvider provider) => _lookup = _lookup.SetItem(language, provider);
    public Translator()
    {
        var translationDir = Path.Combine(ExecutorClient.ExeDir, "Resources");
        var translationFiles = Directory.Exists(translationDir)
            ? Directory.GetFiles(translationDir, "*.resx", SearchOption.AllDirectories)
            : [];

        var lut = new Dictionary<CultureInfo, List<string>>();
        var neutralLanguage = EngineSettings.NeutralLanguage;

        foreach (var f in translationFiles)
        {
            // strip .resx
            var localname = Path.GetFileNameWithoutExtension(f);
            var cultureString = Path.GetExtension(localname);
            CultureInfo culture;
            try
            {
                if (cultureString.StartsWith(".")) cultureString = cultureString.Substring(1);
                culture = new CultureInfo(cultureString);
                // If the culture is a custom culture, this likely means that no culture was specified at all.
                // This is a bit hard to determine unambiguously because the pattern used by resx files will be e.g:
                // foo.resx -> neutral
                // foo.de.resx -> german
                // But the OpenTAP resx generator will name the resource file based on the package name,
                // so in the edge case where a package is named "MyPackage.de", the english resource file would be named
                // MyPackage.de.resx, and the german resource file would be named MyPackage.de.de.resx
                // But let's ignore this edge case for now.
                if (culture.CultureTypes.HasFlag(CultureTypes.UserCustomCulture) || string.IsNullOrWhiteSpace(cultureString))
                    culture = neutralLanguage;
            }
            catch
            {
                // If there was a parser error, also assume english
                // This normally happens when the culture contains illegal characters, but not if the culture 
                // is not recognized. E.g. the culture 'asdkjasheoiqwje' would be parsed successfully as a UserCustomCulture
                culture = neutralLanguage;
            }

            // Ignore neutral language files. Assume the source code is authoritative.
            if (culture.Equals(neutralLanguage))
                continue;

            if (!lut.TryGetValue(culture, out var lst)) lst = [];
            lst.Add(f);
            lut[culture] = lst;
        }

        _cultures = lut.Keys
            .Concat([neutralLanguage])
            .Distinct()
            .OrderBy(CultureAsString)
            .ToImmutableArray();
        _lookup = lut.ToImmutableDictionary(x => x.Key,
            ITranslationProvider (x) => new ResXTranslationProvider(x.Key, x.Value));
    }
}
