namespace OpenTap;

/// <summary>
/// Event args when a pause has been requested.
/// </summary>
public sealed class TestPlanPauseEventArgs
{
    /// <summary>
    ///  the test plan run requesting the pause/
    /// </summary>
    public TestPlanRun PlanRun { get; set; }

    /// <summary>
    /// The test step run (if applicable)
    /// </summary>
    public TestStepRun StepRun { get; set; }
}
