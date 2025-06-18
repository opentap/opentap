namespace OpenTap;

/// <summary>
/// A test step implements custom name formatting.
/// </summary>
public interface IFormatName : ITestStep
{
    /// <summary>
    /// Returns the formatted name, this is called before it is given to the logic of TestStep.GetFormattedName, so it include macros ("{}") in the output.
    /// </summary>
    string GetFormattedName();
}
