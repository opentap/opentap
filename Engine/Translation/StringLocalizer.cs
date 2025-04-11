namespace OpenTap.Translation;

public abstract class StringLocalizer : ITapPlugin
{
}

public abstract class StringLocalizer<T> : StringLocalizer where T : StringLocalizer, new()
{
    private static T getCurrent()
    {
        return EngineSettings.Current.GetLocalizer<T>();
    }

    public static T Current => getCurrent();
} 