namespace OpenTap.Translation;

public abstract class StringLocalizer : ITapPlugin
{
}

public abstract class StringLocalizer<T> : StringLocalizer where T : StringLocalizer, new()
{ 
    public static T Current => EngineSettings.Current.GetLocalizer<T>();
} 