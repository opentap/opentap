namespace OpenTap.Translation;

/// <summary>
/// Language-aware container of user-facing strings.
/// </summary>
public abstract class StringLocalizer : ITapPlugin
{
}

/// <summary>
/// Language-aware container of user-facing strings.
/// Inherit from this class to create a new language-aware container of strings for use in log messages, dialogs,
/// and other user-facing strings.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class StringLocalizer<T> : StringLocalizer where T : StringLocalizer, new()
{ 
    /// <summary>
    /// Get the a StringLocalizer instance for the currently selected language.
    /// </summary>
    public static T Current => EngineSettings.Current.GetLocalizer<T>();
} 