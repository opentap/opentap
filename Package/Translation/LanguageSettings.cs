//            Copyright Keysight Technologies 2012-2025
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap.Package.Translation;

/// <summary>
/// Settings class containing language settings and methods for looking up translations.
/// </summary>
public class LanguageSettings : ComponentSettings<LanguageSettings>
{
    private static readonly TraceSource log = Log.CreateSource("Language");

    private static string CultureAsString(CultureInfo culture)
    {
        return $"{culture.NativeName} ({culture.EnglishName})";
    }

    /// <summary>
    /// The list of available languages. This is based on the currently installed language packs.
    /// </summary>
    [Browsable(false)]
    [XmlIgnore]
    public IEnumerable<string> AvailableLanguages => _cultures.Select(CultureAsString);

    /// <summary>
    /// The currently selected language. Defaults to English.
    /// </summary>
    [Display("Language", "The currently selected language.")]
    [AvailableValues(nameof(AvailableLanguages))]
    public string LanguageString
    {
        get => CultureAsString(Language);
        set
        {
            if (_cultures.FirstOrDefault(x => CultureAsString(x) == value) is { } newLanguage)
                Language = newLanguage;
        }
    }

    /// <summary>
    /// Get a display attribute for the provided member in the requested language.
    /// <param name="i">The type or property a translated display attribute for.</param>
    /// <param name="language">The desired language of the output attribute. Defaults to the currently selected language
    /// if not specified.</param>
    /// <returns>A display attribute in the requested language. If no translation could be provided,
    /// the default DisplayAttribute is returned instead. Check the Language property of the returned attribute.</returns>
    /// </summary>
    public DisplayAttribute GetTranslatedDisplayAttribute(IReflectionData i, CultureInfo language = null)
    {
        if (i == null) return null;
        language ??= Language;
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

    /// <summary>
    /// The currently selected language. Defaults to English.
    /// </summary>
    [Browsable(false)]
    [XmlIgnore]
    public CultureInfo Language { get; private set; }

    private readonly ImmutableArray<CultureInfo> _cultures;

    private readonly ImmutableDictionary<CultureInfo, ImmutableArray<ITranslationProvider>> _lookup;

    /// <inheritdoc/>
    public LanguageSettings()
    {
        var translationFiles = Installation.Current.GetPackages().SelectMany(x => x.Files).Where(x =>
                Path.GetExtension(x.FileName).Equals(".xml", StringComparison.OrdinalIgnoreCase))
            .Select(x => Path.Combine(ExecutorClient.ExeDir, x.FileName));

        {
            var translationDir = Path.Combine(ExecutorClient.ExeDir, "translations");
            if (Directory.Exists(translationDir))
            {
                translationFiles = translationFiles.Concat(Directory.GetFiles(translationDir, "*.xml", SearchOption.AllDirectories));
            }
        }

        _translationProviders = translationFiles.Select(TranslationFile.CreateFromFile)
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

        Language = english;
        _cultures = supportedCultures
            .SelectMany(x => x)
            .Concat([english])
            .Distinct()
            .OrderBy(x => CultureAsString(x))
            .ToImmutableArray();
        _lookup = dict;
    }

    private readonly ImmutableArray<ITranslationProvider> _translationProviders;
}


