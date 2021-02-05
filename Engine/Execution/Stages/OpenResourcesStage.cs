//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

namespace OpenTap
{
    class OpenResourcesStage : TestPlanExecutionStageBase
    {
        public CreateRunStage CreateRun { get; set; }
        public StepOverrideStage StepsStage { get; set; }

        public ITestPlanRunMonitor[] monitors { get; private set; }

        protected override void Execute(TestPlanExecutionContext context)
        {
            var currentListeners = context.currentExecutionState != null ? context.currentExecutionState.ResultListeners : context.resultListeners;

            TestPlanRun run = CreateRun.execStage;
            monitors = TestPlanRunMonitors.GetCurrent();
            try
            {
                // Enter monitors
                foreach (var item in monitors)
                    item.EnterTestPlanRun(run);
            }
            finally   // We need to make sure OpenAllAsync is always called (even when CheckResources throws an exception). 
            {         // Otherwise we risk that e.g. ResourceManager.WaitUntilAllResourcesOpened() will hang forever.
                run.ResourceManager.EnabledSteps = StepsStage.allEnabledSteps;
                run.ResourceManager.StaticResources = currentListeners;

                if (!CreateRun.continuedExecutionState)
                    run.ResourceManager.BeginStep(run, context.Plan, TestPlanExecutionStage.Open, TapThread.Current.AbortToken);
            }

            //run.WaitForSerialization();
            run.ResourceManager.BeginStep(run, context.Plan, TestPlanExecutionStage.Execute, TapThread.Current.AbortToken);

            if (CreateRun.continuedExecutionState)
            {  // Since resources are not opened, getting metadata cannot be done in the wait for resources continuation
               // like shown in TestPlanRun. Instead we do it here.
                foreach (var res in run.ResourceManager.Resources)
                    run.Parameters.AddRange(ResultParameters.GetMetadataFromObject(res));
            }
        }
    }
}
