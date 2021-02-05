//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace OpenTap
{
    class FinishPlanStage : TestPlanExecutionStageBase
    {
        internal static readonly TraceSource Log = OpenTap.Log.CreateSource("Executor(FP)");
        public StepOverrideStage StepOverrideStage { get; set; }
        public CreateLogStage CreateLogStage { get; set; }

        public CreateRunStage CreateRunStage { get; set; }
        [ExecutionStageReference(executeIfReferenceFails: true, executeIfReferenceSkipped: true)]
        public ExecutePlanStage ExecutePlanStage { get; set; }
        [ExecutionStageReference(executeIfReferenceFails: true)]
        public OpenResourcesStage OpenResourcesStage { get; set; }
        protected override void Execute(TestPlanExecutionContext context)
        {
            TestPlanRun execStage = CreateRunStage.execStage;

            Exception e = context.GetExceptionFromFailedStage(OpenResourcesStage);
            if(e == null)
                e = context.GetExceptionFromFailedStage(ExecutePlanStage);
            if (e != null)
            {
                if (e is OperationCanceledException && execStage.MainThread.AbortToken.IsCancellationRequested)
                {

                    Log.Warning(String.Format("TestPlan aborted. ({0})", e.Message));
                    execStage.UpgradeVerdict(Verdict.Aborted);
                }
                else if (e is ThreadAbortException)
                {
                    // It seems this actually never happens.
                    Log.Warning("TestPlan aborted.");
                    execStage.UpgradeVerdict(Verdict.Aborted);
                    //Avoid entering the finally clause.
                    return;
                }
                else if (e is System.ComponentModel.LicenseException)
                {
                    Log.Error(e.Message);
                    execStage.UpgradeVerdict(Verdict.Error);
                }
                else
                {
                    Log.Warning("TestPlan aborted.");
                    Log.Error(e.Message);
                    Log.Debug(e);
                    execStage.UpgradeVerdict(Verdict.Error);
                }
                execStage.FailedToStart = true;
            }
            //finally
            {
                try
                {
                    if (execStage != null)
                    {
                        if (execStage.FailedToStart)
                        {
                            if (context.PrintTestPlanRunSummary)
                                TestPlanStagedExecutor.summaryListener.OnTestPlanRunStart(execStage); // Call this to ensure that the correct planrun is being summarized

                            if (execStage.Verdict < Verdict.Aborted)
                                execStage.Verdict = Verdict.Error;
                        }

                        for (int i = execStage.StepsWithPrePlanRun.Count - 1; i >= 0; i--)
                        {
                            Stopwatch postTimer = Stopwatch.StartNew();
                            String stepPath = string.Empty;
                            try
                            {
                                ITestStep step = execStage.StepsWithPrePlanRun[i];

                                if ((step as TestStep)?.PrePostPlanRunUsed ?? true)
                                {
                                    stepPath = step.GetStepPath();
                                    execStage.AddTestStepStateUpdate(step.Id, null, StepState.PostPlanRun);
                                    try
                                    {
                                        execStage.ResourceManager.BeginStep(execStage, step, TestPlanExecutionStage.PostPlanRun, TapThread.Current.AbortToken);
                                        step.PlanRun = execStage;
                                        try
                                        {
                                            step.PostPlanRun();
                                        }
                                        finally
                                        {
                                            execStage.ResourceManager.EndStep(step, TestPlanExecutionStage.PostPlanRun);
                                            step.PlanRun = null;
                                        }
                                    }
                                    finally
                                    {
                                        execStage.AddTestStepStateUpdate(step.Id, null, StepState.Idle);
                                    }
                                    Log.Debug(postTimer, "{0} PostPlanRun completed.", stepPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Warning("Error during post plan run of {0}.", stepPath);
                                Log.Debug(ex);
                            }
                        }
                        execStage.Duration = CreateRunStage.preRun_Run_PostRunTimer.Elapsed;
                    }

                    if (execStage != null)
                    {

                        try
                        {
                            execStage.ResourceManager.WaitUntilAllResourcesOpened(TapThread.Current.AbortToken);
                        }
                        catch (OperationCanceledException)
                        {
                            execStage.UpgradeVerdict(Verdict.Aborted);
                        }
                        catch (AggregateException ae)
                        {
                            if (ae.InnerExceptions.Count == 1)
                            {
                                Log.Error("Failed to open resource ({0})", ae.GetInnerMostExceptionMessage());
                                Log.Debug(ae);
                            }
                            else
                            {
                                Log.Error("Errors while opening resources:", ae.GetInnerMostExceptionMessage());
                                foreach (Exception ie in ae.InnerExceptions)
                                {
                                    Log.Error("    {0}", ie.GetInnerMostExceptionMessage());
                                    Log.Debug(ie);
                                }
                            }
                        }

                        if (context.PrintTestPlanRunSummary)
                        {
                            // wait for the summaryListener so the summary appears in the log file.
                            execStage.WaitForResultListener(TestPlanStagedExecutor.summaryListener);
                            TestPlanStagedExecutor.summaryListener.PrintSummary();
                        }

                        OpenTap.Log.Flush();
                        CreateLogStage.planRunLog.Flush();
                        CreateLogStage.logStream.Flush();
                        execStage.AddTestPlanCompleted(CreateLogStage.logStream, !execStage.FailedToStart);
                        execStage.ResourceManager.EndStep(context.Plan, TestPlanExecutionStage.Execute);

                        if (!execStage.IsCompositeRun)
                            execStage.ResourceManager.EndStep(context.Plan, TestPlanExecutionStage.Open);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Error while finishing TestPlan.");
                    Log.Debug(ex);
                }
                finally
                {
                    if (OpenResourcesStage.monitors != null)
                        foreach (var item in OpenResourcesStage.monitors)
                            item.ExitTestPlanRun(execStage);
                }

                OpenTap.Log.RemoveListener(CreateLogStage.planRunLog);
                CreateLogStage.planRunLog.Dispose();

                CreateLogStage.logStream.Dispose();
                File.Delete(CreateLogStage.fileStreamFile);

                // Clean all test steps StepRun, otherwise the next test plan execution will be stuck at TestStep.DoRun at steps that does not have a cleared StepRun.
                foreach (var step in StepOverrideStage.allSteps)
                    step.StepRun = null;
                
                context.Run = null;
            }
        }
    }
}
