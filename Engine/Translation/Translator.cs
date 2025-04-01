using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenTap.Package.Translation;

namespace OpenTap.Translation;

internal interface ITranslator
{ 
    public IEnumerable<CultureInfo> SupportedLanguages { get; }
    public DisplayAttribute Translate(IReflectionData i, CultureInfo language);
}

internal class Translator : ITranslator
{
    internal static string CultureAsString(CultureInfo culture)
    {
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
    public DisplayAttribute Translate(IReflectionData i, CultureInfo language)
    {
        if (i == null) return null;
        if (_lookup.TryGetValue(language, out var providers))
        {
            foreach (var prov in providers)
            {
                if (prov.GetDisplayAttribute(i, language) is { } result)
                    return result;
            }
        }
        return DefaultDisplayAttribute.GetUntranslatedDisplayAttribute(i);
    } 

    private readonly ImmutableArray<CultureInfo> _cultures; 
    private readonly ImmutableDictionary<CultureInfo, ImmutableArray<ITranslationProvider>> _lookup;

    public Translator()
    {
        var translationDir = Path.Combine(ExecutorClient.ExeDir, "translations");
        var translationFiles = Directory.Exists(translationDir)
            ? Directory.GetFiles(translationDir, "*.xml", SearchOption.AllDirectories)
            : [];

        var _translationProviders = translationFiles.Select(TranslationFile.CreateFromFile)
            .Where(x => x != null).ToImmutableArray<ITranslationProvider>();

        // English is assumed to be the default when no culture is specified. (e.g. all strings defined in source code)
        var english = new CultureInfo("en");
        var supportedCultures = _translationProviders.Select(x => x.SupportedLanguages()).ToArray();

        var dict = ImmutableDictionary<CultureInfo, ImmutableArray<ITranslationProvider>>.Empty;
        for (int i = 0; i < _translationProviders.Length; i++)
        {
            var provider = _translationProviders[i];
            var cs = supportedCultures[i];
            foreach (var c in cs)
            {
                dict.TryGetValue(c, out var lst);
                if (lst == null)
                    lst = new[] { provider }.ToImmutableArray();
                else
                    lst = lst.Add(provider);
                dict = dict.SetItem(c, lst);
            }
        }

        _cultures = supportedCultures
            .SelectMany(x => x)
            .Concat([english])
            .Distinct()
            .OrderBy(CultureAsString)
            .ToImmutableArray();
        _lookup = dict;
    } 
}
