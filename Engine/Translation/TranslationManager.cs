using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace OpenTap.Translation;

/// <summary>
/// Functions for working with translations
/// </summary>
public static class TranslationManager
{
    private static readonly ITranslator Translator = new Translator();

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
    internal static CultureInfo NeutralLanguage => CultureInfo.InvariantCulture;

    /// <summary>
    /// The list of languages supported by installed resource files.
    /// </summary>
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

    /// <summary>
    /// Look for a translated version of the input member and validate the format parameters.
    /// If no translation is found, or the parameter count does not match, return the neutral string.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer which owns the translated string.</param>
    /// <param name="neutral">The neutral (non-translated) string.</param>
    /// <param name="key">Lookup key for finding the translation.</param>
    /// <param name="language">The desired translation language</param>
    /// <returns>The translated string, if a translation exists, and the string format parameters in the translated string matches the source string. Otherwise, returns the neutral string.</returns>
    public static FormatString TranslateFormat(this IStringLocalizer stringLocalizer, string neutral,
        [CallerMemberName] string key = null,
        CultureInfo language = null)
    {
        var translated = Translate(stringLocalizer, neutral, key, language);
        if (!ReferenceEquals(neutral, translated))
        {
            var c1 = CountTemplates(neutral);
            var c2 = CountTemplates(translated);
            if (c1 != c2)
            {
                log.ErrorOnce(neutral, $"Template parameter count mismatch detected.\n" +
                                       $"'{neutral}' has {c1} template parameters.\n" +
                                       $"'{translated}' has {c2} template parameters.\n" +
                                       $"The first string will be preferred over the second string.");
                // Use neutral string instead
                return new FormatString(neutral);
            }
        }

        return new FormatString(translated);
    }

    /// <summary>
    /// Look for a translated version of the input member. If no translation is found, return the neutral string.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer which owns the translated string.</param>
    /// <param name="neutral">The neutral (non-translated) string.</param>
    /// <param name="key">Lookup key for finding the translation.</param>
    /// <param name="language">The desired translation language</param>
    /// <returns>The translated string, if a translation exists. Otherwise, returns the neutral string.</returns>
    public static string Translate(this IStringLocalizer stringLocalizer, string neutral,
        [CallerMemberName] string key = null, CultureInfo language = null)
    {
        return TranslateFunction(stringLocalizer, neutral, key, language);
    }

    // We add this small level of indirection in order to inject a hook to automatically
    // populate a list of lookup keys and strings for creating translations.
    // Do not rename this field or change the delegate!!
    private static Func<IStringLocalizer, string, string, CultureInfo, string> TranslateFunction = _translate;

    private static string _translate(this IStringLocalizer stringLocalizer, string neutral,
        [CallerMemberName] string key = null, CultureInfo language = null)
    {
        language ??= EngineSettings.Current.Language;
        if (language.Equals(NeutralLanguage) || string.IsNullOrWhiteSpace(key))
            return neutral;
        var fullKey = stringLocalizer.GetType().FullName + $".{key}";
        return TranslateKey(fullKey, language) ?? neutral;
    }

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

    private static readonly TraceSource log = Log.CreateSource("Translation Manager");
}

/// <summary>
/// An IStringLocalizer can define localizable strings by calling the Translate() or TranslateFormat() method from a public computed property.
/// Example: public string MyString => Translate("Neutral string") // returns "Translated string" if available
/// A localizable string defaults to the neutral string, unless, OpenTAP is configured to display in a different
/// language in which a translation is available.
/// </summary>
public interface IStringLocalizer : ITapPlugin
{
}

/// <summary>
/// A FormatString is a string which requires format parameters.
/// </summary>
public class FormatString(string format)
{

    /// <summary>
    /// Creates a formatted string based on this instance and the provided format arguments.
    /// </summary>
    /// <param name="args">The format arguments</param>
    /// <returns>The formatted string</returns>
    public string Format(params object[] args) => string.Format(format, args);
}
