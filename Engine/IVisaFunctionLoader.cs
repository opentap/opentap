namespace OpenTap;
/// <summary>
/// Interface to load visa functions.
/// </summary>
public interface IVisaFunctionLoader : ITapPlugin
{
    /// <summary>
    /// The order in which IVisaProviders will be tested. Lower numbers go first
    /// </summary>
    public double Order { get; }
        
    /// <summary> Load all visa functions. </summary>
    VisaFunctions? Functions { get; }
}
