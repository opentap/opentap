namespace OpenTap;

/// <summary>
/// Marks a test step as a step that loops. Infinite loops can return null in place of MaxIterations.
/// </summary>
public interface ILoopStep : ITestStep
{
    /// <summary> Gets or sets the current iteration. This can be used to skip iterations.</summary>
    int CurrentIteration { get; set; }
    /// <summary> Gets the max number of iterations. </summary>
    int? MaxIterations { get; }
}