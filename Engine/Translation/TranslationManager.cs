using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;

namespace OpenTap.Translation;

public static class TranslationManager
{
    private static ITranslator translator;
    private static ITranslator Translator => translator ??= new Translator();
    
    internal static string CultureAsString(CultureInfo culture)
    {
        if (CultureInfo.InvariantCulture.Equals(culture)) return "Neutral";
        return $"{culture.NativeName} ({culture.EnglishName})";
    }

    /// <summary>
    /// The default language.
    /// </summary>
    [Browsable(false)]
    [XmlIgnore]
    internal static CultureInfo NeutralLanguage { get; } = CultureInfo.InvariantCulture;

    public static IEnumerable<CultureInfo> SupportedLanguages => Translator.SupportedLanguages;
    /// <summary>
    /// Get an appropriate DisplayAttribute for the specified reflection data in the requested language.
    /// </summary>
    /// <param name="mem"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    public static DisplayAttribute TranslateMember(IReflectionData mem, CultureInfo language = null)
    {
        return Translator.TranslateMember(mem, language ?? EngineSettings.Current.Language);
    }
    /// <summary>
    /// Get an appropriate DisplayAttribute for the specified enum in the requested language.
    /// </summary>
    /// <param name="e"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    public static DisplayAttribute TranslateEnum(Enum e, CultureInfo language = null)
    {
        return Translator.TranslateEnum(e, language ?? EngineSettings.Current.Language);
    }

    /// <summary>
    /// The directory containing translation resource files.
    /// </summary>
    public static string TranslationDirectory => Path.Combine(ExecutorClient.ExeDir, "Languages");

    /// <summary>
    /// Get an appropriate DisplayAttribute for the specified enum in the requested language.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    public static string TranslateKey(string key, CultureInfo language = null)
    {
        return Translator.TranslateString(key, language ?? EngineSettings.Current.Language);
    }
}