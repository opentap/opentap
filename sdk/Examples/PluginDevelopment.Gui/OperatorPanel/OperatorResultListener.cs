using System;
using OpenTap;

namespace PluginDevelopment.Gui.OperatorPanel
{
    /// <summary>
    /// This result listener is used for giving callback to the user interface about
    /// status changes in the test plan.
    /// Note, it is private, which means it cannot be added by the user manually.
    /// </summary>
    class OperatorResultListener : ResultListener
    {
        
        public event EventHandler<TestPlanRun> TestPlanRunStarted;
        public event EventHandler<TestStepRun> TestStepRunCompleted;
        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            TestPlanRunStarted?.Invoke(this, planRun);
            base.OnTestPlanRunStart(planRun);
        }

        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        {
            TestStepRunCompleted?.Invoke(this, stepRun);
            base.OnTestStepRunStart(stepRun);
        }
    }
}