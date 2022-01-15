namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    [Display("Run Times Count", "A test step that counts how many time it has been run during a single test plan run.", "Tests")]
    public class RunTimeStep : TestStep
    {
        [Output] public int RunCount { get; set; }
        public override void PrePlanRun() => RunCount = 0;
        public override void Run() => RunCount += 1;
    }
}