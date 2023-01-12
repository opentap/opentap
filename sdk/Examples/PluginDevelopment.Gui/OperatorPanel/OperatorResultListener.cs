using System;
using OpenTap;

namespace PluginDevelopment.Gui.OperatorPanel
{
    class OperatorResultListener : ResultListener
    {
        public event EventHandler<TestPlanRun> TestPlanRunStarted;
        public event EventHandler<TestStepRun> TestStepRunStart;
        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            TestPlanRunStarted?.Invoke(this, planRun);
            base.OnTestPlanRunStart(planRun);
        }

        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        {
            TestStepRunStart?.Invoke(this, stepRun);
            base.OnTestStepRunStart(stepRun);
        }
    }
}