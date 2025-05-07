using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTap.Translation;

/// <summary>
/// A string localizer can define localizable strings by calling the Translate() method from a public property.
/// A localizable string defaults to the neutral string, unless, OpenTAP is configured to display in a different
/// language in which a translation is available.
/// </summary>
public abstract class StringLocalizer : ITapPlugin
{
    public bool RecordKeys { get; set; } = false;
    public Dictionary<string, string> RecordedKeys { get; } = [];
    protected CultureInfo Language { get; }
    private EngineSettings Engine { get; }

    protected StringLocalizer()
    {
        Engine = EngineSettings.Current;
        Language = Engine.Language;
    }

    protected StringLocalizer(CultureInfo language) : this()
    {
        Language = language;
    }

    /// <summary>
    /// Look for a translated version of the input member. If no translation is found, return the neutral string.
    /// </summary>
    /// <param name="neutral">The language-neutral string</param>
    /// <param name="member">The member name</param>
    /// <returns></returns>
    protected string Translate(string neutral, [CallerMemberName] string member = null)
    {
        if (string.IsNullOrWhiteSpace(member))
            return neutral;
        var key = GetType().FullName + $".{member}";
        if (RecordKeys) RecordedKeys.Add(key, neutral);
        if (Equals(Language, EngineSettings.DefaultLanguage))
            return neutral;
        return Engine.TranslateKey(key, Language) ?? neutral;
    }

    /// <summary>
    /// Look for a translated version of the input member and validate the format parameters.
    /// If no translation is found, or the parameter count does not match, return the neutral string.
    /// </summary>
    /// <param name="neutral">The language-neutral string</param>
    /// <param name="member">The member name</param>
    /// <returns></returns>
    protected string TranslateFormat(string neutral, [CallerMemberName] string member = null)
    {
        var translated = Translate(neutral, member);
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
                return neutral;
            }
            return translated;
        }
        return neutral;
    }

    private static readonly TraceSource log = Log.CreateSource("String Localizer");
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

}

