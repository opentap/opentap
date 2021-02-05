//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenTap
{
    class RunStage : TestPlanExecutionStageBase
    {
        internal static readonly TraceSource Log = OpenTap.Log.CreateSource("Executor(EP)");

        public StepOverrideStage StepsStage { get; set; }
        public CreateRunStage CreateRun { get; set; }

        public PrePlanRunStage PrePlanRunStage { get; set; }

        public OnTestPlanRunStartStage OnTestPlanRunStartStage { get; set; }

        protected override bool Execute(TestPlanExecutionContext context)
        {
            var execStage = CreateRun.execStage;
            var steps = StepsStage.steps;


            Stopwatch planRunOnlyTimer = Stopwatch.StartNew();
            var runs = new List<TestStepRun>();

            try
            {
                for (int i = 0; i < StepsStage.steps.Count; i++)
                {
                    var step = StepsStage.steps[i];
                    if (step.Enabled == false) continue;
                    var run = step.DoRun(CreateRun.execStage, CreateRun.execStage);
                    if (!run.Skipped)
                        runs.Add(run);
                    run.CheckBreakCondition();

                    // note: The following is copied inside TestStep.cs
                    if (run.SuggestedNextStep is Guid id)
                    {
                        int nextindex = StepsStage.steps.IndexWhen(x => x.Id == id);
                        if (nextindex >= 0)
                            i = nextindex - 1;
                        // if skip to next step, dont add it to the wait queue.
                    }
                }
            }
            catch (TestStepBreakException breakEx)
            {
                Log.Info("{0}", breakEx.Message);
            }
            finally
            {
                // Now wait for them to actually complete. They might defer internally.
                foreach (var run in runs)
                {
                    run.WaitForCompletion();
                    CreateRun.execStage.UpgradeVerdict(run.Verdict);
                }
            }

            Log.Debug(planRunOnlyTimer, "Test step runs finished.");
            return true;
        }
      
    }
}
