using System;

namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    [Display("Run Times Count", "A test step that counts how many time it has been run during a single test plan run.", "Tests")]
    public class RunTimeStep : TestStep
    {
        [Output] public int RunCount { get; set; }
        public ITestStep RestartMarker { get; set; }

        Guid lastRestartMarker;
        public override void Run()
        {
            var nowMarker = RestartMarker.StepRun?.Id ?? RestartMarker?.Id ?? Guid.Empty;
            if (nowMarker != lastRestartMarker)
            {
                RunCount = 0;
                lastRestartMarker = nowMarker;
            }
            RunCount += 1;
        }
    }
}