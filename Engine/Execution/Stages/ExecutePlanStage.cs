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
    class ExecutePlanStage : TestPlanExecutionStageBase
    {
        internal static readonly TraceSource Log = OpenTap.Log.CreateSource("Executor(EP)");

        public StepOverrideStage StepsStage { get; set; }
        public CreateRunStage CreateRun { get; set; }

        public OnTestPlanRunStartStage OnTestPlanRunStartStage { get; set; }

        protected override void Execute(TestPlanExecutionContext context)
        {
            var execStage = CreateRun.execStage;
            var steps = StepsStage.steps;


            var sw = Stopwatch.StartNew();
            try
            {
                execStage.StepsWithPrePlanRun.Clear();
                if (!runPrePlanRunMethods(steps, execStage))
                {
                    execStage.FailedToStart = true;
                    return;
                }
            }
            catch (Exception e)
            {
                Log.Error(e.GetInnerMostExceptionMessage());
                Log.Debug(e);
                execStage.FailedToStart = true;
                return;
            }
            finally
            {
                {
                    Log.Debug(sw, "PrePlanRun Methods completed");
                }
            }

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

        }
        bool runPrePlanRunMethods(IEnumerable<ITestStep> steps, TestPlanRun planRun)
        {
            Stopwatch preTimer = Stopwatch.StartNew(); // try to avoid calling Stopwatch.StartNew too often.
            TimeSpan elaps = preTimer.Elapsed;
            foreach (ITestStep step in steps)
            {
                if (step.Enabled == false)
                    continue;
                bool runPre = true;
                if (step is TestStep s)
                {
                    runPre = s.PrePostPlanRunUsed;
                }
                planRun.StepsWithPrePlanRun.Add(step);
                try
                {
                    if (runPre)
                    {
                        planRun.AddTestStepStateUpdate(step.Id, null, StepState.PrePlanRun);
                        try
                        {
                            step.PlanRun = planRun;
                            planRun.ResourceManager.BeginStep(planRun, step, TestPlanExecutionStage.PrePlanRun, TapThread.Current.AbortToken);
                            try
                            {
                                step.PrePlanRun();
                            }
                            finally
                            {
                                planRun.ResourceManager.EndStep(step, TestPlanExecutionStage.PrePlanRun);
                                step.PlanRun = null;
                            }
                        }
                        finally
                        {
                            planRun.AddTestStepStateUpdate(step.Id, null, StepState.Idle);
                        }
                    }

                    if (!runPrePlanRunMethods(step.ChildTestSteps, planRun))
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(String.Format("PrePlanRun of '{0}' failed with message '{1}'.",
                        step.Name, ex.Message));
                    Log.Debug(ex);
                    Log.Error("Aborting TestPlan.");
                    return false;
                }
                finally
                {
                    if (runPre)
                    {
                        var newelaps = preTimer.Elapsed;

                        Log.Debug(newelaps - elaps, "{0} PrePlanRun completed.", step.GetStepPath());
                        elaps = newelaps;

                    }
                }
            }
            return true;
        }

    }
}
