//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenTap
{
    class PrePlanRunStage : TestPlanExecutionStageBase
    {
        internal static readonly TraceSource Log = OpenTap.Log.CreateSource("Executor(PP)");
        public CreateRunStage CreateRun { get; set; }
        public StepOverrideStage StepsStage { get; set; }

        protected override bool Execute(TestPlanExecutionContext context)
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
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.Error(e.GetInnerMostExceptionMessage());
                Log.Debug(e);
                execStage.FailedToStart = true;
                return false;
            }
            finally
            {
                {
                    Log.Debug(sw, "PrePlanRun Methods completed");
                }
            }
            return true;
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
