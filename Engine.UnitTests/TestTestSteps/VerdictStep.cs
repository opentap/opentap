using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.Engine.UnitTests.TestTestSteps
{
    [Display("Set Verdict", Group: "Basic Steps", Description: "Reports a verdict for the step.")]
    public class VerdictStep : TestStep
    {
        [Display("Resulting Verdict", Order: -1, Description: "The verdict the step should produce when run.")]
        public Verdict VerdictOutput { get; set; }

        [Display("Request Abort", Description: "Setting this property to true will cause the test plan to abort as quickly as possible.")]
        public bool RequestAbort { get; set; }

        public VerdictStep()
        {
            VerdictOutput = Verdict.Pass;
            RequestAbort = false;
        }

        public override void Run()
        {
            UpgradeVerdict(VerdictOutput);
            if (RequestAbort)
                PlanRun.MainThread.Abort("Verdict Step requested to abort the test plan execution.");
        }
    }
}
