//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace OpenTap
{
    interface ITestPlanExecutor
    {
        TestPlanRun CurrentRun { get; }
        bool PrintTestPlanRunSummary { get; set; }
        TestPlanRun State { get; set; }

        TestPlanRun Execute(IEnumerable<IResultListener> resultListeners, IEnumerable<ResultParameter> metaDataParameters, HashSet<ITestStep> stepsOverride);

        void Open(IEnumerable<IResultListener> listeners);

        void Close();
    }

    class TestPlanExecutor : ITestPlanExecutor
    {
        #region Nested Types
        enum failState
        {
            Ok,
            StartFail,
            ExecFail
        }
        #endregion

        internal static readonly TraceSource Log = OpenTap.Log.CreateSource("Executor");

        private ITestPlanRunMonitor[] monitors; // For locking/unlocking or generally monitoring test plan start/stop.

        private bool IsRunning => CurrentRun != null;
        private TestPlanRun currentExecutionState = null;
        private TestPlan plan;

        public TestPlanRun State
        {
            get => currentExecutionState;
            set => currentExecutionState = value;
        }

        TestPlanRun _currentRun;
        public TestPlanRun CurrentRun
        {
            set
            {
                _currentRun = value;
                //OnPropertyChanged(nameof(IsRunning)); // TODO: is this needed?
            }
            get => _currentRun;
        }

        public bool PrintTestPlanRunSummary { get; set; }

        public TestPlanExecutor(TestPlan plan)
        {
            this.plan = plan;
        }

        readonly TestPlanRunSummaryListener summaryListener = new TestPlanRunSummaryListener();
        internal static ThreadHierarchyLocal<TestPlanRun> executingPlanRun = new ThreadHierarchyLocal<TestPlanRun>();

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

        failState execTestPlan(TestPlanRun execStage, IList<ITestStep> steps)
        {
            //WaitHandle.WaitAny(new[] { execStage.PromptWaitHandle, TapThread.Current.AbortToken.WaitHandle });
            bool resultListenerError = false;
            execStage.ScheduleInResultProcessingThread<IResultListener>(resultListener =>
            {
                try
                {
                    using (TimeoutOperation.Create(() => TestPlanExecutonHelpers.PrintWaitingMessage(new List<IResource>() { resultListener })))
                        execStage.ResourceManager.WaitUntilResourcesOpened(TapThread.Current.AbortToken, resultListener);
                    try
                    {
                        // some resources might set metadata in the Open methods.
                        // this information needs to be propagated to result listeners as well.
                        // this returns quickly if its a lazy resource manager.
                        using (TimeoutOperation.Create(
                            () => TestPlanExecutonHelpers.PrintWaitingMessage(new List<IResource>() { resultListener })))
                            execStage.ResourceManager.WaitUntilAllResourcesOpened(TapThread.Current.AbortToken);
                    }
                    catch // this error will also be handled somewhere else.
                    {

                    }

                    //execStage.WaitForSerialization();
                    foreach (var res in execStage.PromptedResources)
                        execStage.Parameters.AddRange(ResultParameters.GetMetadataFromObject(res));
                    resultListener.OnTestPlanRunStart(execStage);
                }
                catch (OperationCanceledException) when (execStage.MainThread.AbortToken.IsCancellationRequested)
                {
                    // test plan thread was aborted, this is OK.
                }
                catch (Exception ex)
                {
                    Log.Error("Error in OnTestPlanRunStart for '{0}': '{1}'", resultListener, ex.Message);
                    Log.Debug(ex);
                    resultListenerError = true;
                }

            }, true);

            if (resultListenerError)
                return failState.StartFail;

            var sw = Stopwatch.StartNew();
            try
            {
                execStage.StepsWithPrePlanRun.Clear();
                if (!runPrePlanRunMethods(steps, execStage))
                {
                    return failState.StartFail;
                }
            }
            catch (Exception e)
            {
                Log.Error(e.GetInnerMostExceptionMessage());
                Log.Debug(e);
                return failState.StartFail;
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
                for (int i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    if (step.Enabled == false) continue;
                    var run = step.DoRun(execStage, execStage);
                    if (!run.Skipped)
                        runs.Add(run);
                    run.CheckBreakCondition();

                    // note: The following is copied inside TestStep.cs
                    if (run.SuggestedNextStep is Guid id)
                    {
                        int nextindex = steps.IndexWhen(x => x.Id == id);
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
                    execStage.UpgradeVerdict(run.Verdict);
                }
            }



            Log.Debug(planRunOnlyTimer, "Test step runs finished.");

            return failState.Ok;
        }

        void finishTestPlanRun(TestPlanRun run, Stopwatch testPlanTimer, failState runWentOk, TraceListener Logger, HybridStream logStream)
        {
            try
            {
                if (run != null)
                {
                    if (runWentOk == failState.StartFail)
                    {
                        if (PrintTestPlanRunSummary)
                            summaryListener.OnTestPlanRunStart(run); // Call this to ensure that the correct planrun is being summarized

                        if (run.Verdict < Verdict.Aborted)
                            run.Verdict = Verdict.Error;
                    }

                    for (int i = run.StepsWithPrePlanRun.Count - 1; i >= 0; i--)
                    {
                        Stopwatch postTimer = Stopwatch.StartNew();
                        String stepPath = string.Empty;
                        try
                        {
                            ITestStep step = run.StepsWithPrePlanRun[i];

                            if ((step as TestStep)?.PrePostPlanRunUsed ?? true)
                            {
                                stepPath = step.GetStepPath();
                                run.AddTestStepStateUpdate(step.Id, null, StepState.PostPlanRun);
                                try
                                {
                                    run.ResourceManager.BeginStep(run, step, TestPlanExecutionStage.PostPlanRun, TapThread.Current.AbortToken);
                                    step.PlanRun = run;
                                    try
                                    {
                                        step.PostPlanRun();
                                    }
                                    finally
                                    {
                                        run.ResourceManager.EndStep(step, TestPlanExecutionStage.PostPlanRun);
                                        step.PlanRun = null;
                                    }
                                }
                                finally
                                {
                                    run.AddTestStepStateUpdate(step.Id, null, StepState.Idle);
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
                    run.Duration = testPlanTimer.Elapsed;
                }

                if (run != null)
                {

                    try
                    {
                        // The open resource threads might throw exceptions. If they do we must
                        // Wait() for them to catch the exception.
                        // If the run was aborted after the open resource threads were started but 
                        // before we wait for them (e.g. by an error in PrePlanRun), then we do it
                        // here.
                        run.ResourceManager.WaitUntilAllResourcesOpened(TapThread.Current.AbortToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore this because this typically means that the wait was cancelled before.
                        // Just to be sure also upgrade verdict to aborted.
                        run.UpgradeVerdict(Verdict.Aborted);
                    }
                    catch (AggregateException e)
                    {
                        if (e.InnerExceptions.Count == 1)
                        {
                            Log.Error("Failed to open resource ({0})", e.GetInnerMostExceptionMessage());
                            Log.Debug(e);
                        }
                        else
                        {
                            Log.Error("Errors while opening resources:", e.GetInnerMostExceptionMessage());
                            foreach (Exception ie in e.InnerExceptions)
                            {
                                Log.Error("    {0}", ie.GetInnerMostExceptionMessage());
                                Log.Debug(ie);
                            }
                        }
                    }

                    if (PrintTestPlanRunSummary)
                    {
                        // wait for the summaryListener so the summary appears in the log file.
                        run.WaitForResultListener(summaryListener);
                        summaryListener.PrintSummary();
                    }

                    OpenTap.Log.Flush();
                    Logger.Flush();
                    logStream.Flush();

                    run.AddTestPlanCompleted(logStream, runWentOk != failState.StartFail);

                    run.ResourceManager.EndStep(plan, TestPlanExecutionStage.Execute);

                    if (!run.IsCompositeRun)
                        run.ResourceManager.EndStep(plan, TestPlanExecutionStage.Open);
                }
            }
            finally
            {
                if (monitors != null)
                    foreach (var item in monitors)
                        item.ExitTestPlanRun(run);
            }
        }

        /// <summary>
        /// Execute the TestPlan as specified. Blocking.
        /// </summary>
        /// <param name="resultListeners">ResultListeners for result outputs.</param>
        /// <param name="metaDataParameters">Optional metadata parameters.</param>
        /// <param name="stepsOverride">Sub-section of test plan to be executed. Note this might include child steps of disabled parent steps.</param>
        /// <returns>TestPlanRun results, no StepResults.</returns>
        private TestPlanRun DoExecute(IEnumerable<IResultListener> resultListeners, IEnumerable<ResultParameter> metaDataParameters, HashSet<ITestStep> stepsOverride)
        {
            if (resultListeners == null)
                throw new ArgumentNullException("resultListeners");

            if (PrintTestPlanRunSummary && !resultListeners.Contains(summaryListener))
                resultListeners = resultListeners.Concat(new IResultListener[] { summaryListener });
            resultListeners = resultListeners.Where(r => r is IEnabledResource ? ((IEnabledResource)r).IsEnabled : true);
            IList<ITestStep> steps;
            if (stepsOverride == null)
                steps = plan.Steps;
            else
            {
                // Remove steps that are already included via their parent steps.
                foreach (var step in stepsOverride)
                {
                    if (step == null)
                        throw new ArgumentException("stepsOverride may not contain null", "stepsOverride");

                    var p = step.GetParent<ITestStep>();
                    while (p != null)
                    {
                        if (stepsOverride.Contains(p))
                            throw new ArgumentException("stepsOverride may not contain steps and their parents.", "stepsOverride");
                        p = p.GetParent<ITestStep>();
                    }
                }
                steps = Utils.FlattenHeirarchy(plan.Steps, step => step.ChildTestSteps).Where(stepsOverride.Contains).ToList();
            }

            long initTimeStamp = Stopwatch.GetTimestamp();
            var initTime = DateTime.Now;

            Log.Info("-----------------------------------------------------------------");

            var fileStreamFile = FileSystemHelper.CreateTempFile();
            var logStream = new HybridStream(fileStreamFile, 1024 * 1024);

            var planRunLog = new FileTraceListener(logStream) { IsRelative = true };
            OpenTap.Log.AddListener(planRunLog);

            var allSteps = Utils.FlattenHeirarchy(steps, step => step.ChildTestSteps);
            var allEnabledSteps = Utils.FlattenHeirarchy(steps.Where(x => x.Enabled), step => step.GetEnabledChildSteps());

            var enabledSinks = new HashSet<IResultSink>();
            TestStepExtensions.GetObjectSettings<IResultSink, ITestStep, IResultSink>(allEnabledSteps, true, null, enabledSinks);
            if (enabledSinks.Count > 0)
            {
                var sinkListener = new ResultSinkListener(enabledSinks);
                resultListeners = resultListeners.Append(sinkListener);
            }

            Log.Info("Starting TestPlan '{0}' on {1}, {2} of {3} TestSteps enabled.", plan.Name, initTime, allEnabledSteps.Count, allSteps.Count);

            // Reset step verdict.
            foreach (var step in allSteps)
            {
                if (step.Verdict != Verdict.NotSet)
                {
                    step.Verdict = Verdict.NotSet;
                    step.OnPropertyChanged("Verdict");
                }
            }

            if (currentExecutionState != null)
            {
                // load result listeners that are _not_ used in the previous runs.
                // otherwise they wont get opened later.
                foreach (var rl in resultListeners)
                {
                    if (!currentExecutionState.ResultListeners.Contains(rl))
                        currentExecutionState.ResultListeners.Add(rl);
                }
            }

            var currentListeners = currentExecutionState != null ? currentExecutionState.ResultListeners : resultListeners;

            TestPlanRun execStage;
            bool continuedExecutionState = false;
            if (currentExecutionState != null)
            {
                execStage = new TestPlanRun(currentExecutionState, initTime, initTimeStamp);
                continuedExecutionState = true;
            }
            else
            {
                execStage = new TestPlanRun(plan, resultListeners.ToList(), initTime, initTimeStamp);
                execStage.Start();

                execStage.Parameters.AddRange(PluginManager.GetPluginVersions(allEnabledSteps));
                execStage.ResourceManager.ResourceOpened += r =>
                {
                    execStage.Parameters.AddRange(PluginManager.GetPluginVersions(new List<object> { r }));
                };
            }


            if (metaDataParameters != null)
                execStage.Parameters.AddRange(metaDataParameters);

            var prevExecutingPlanRun = executingPlanRun.LocalValue;
            executingPlanRun.LocalValue = execStage;
            CurrentRun = execStage;

            failState runWentOk = failState.StartFail;

            // ReSharper disable once InconsistentNaming
            var preRun_Run_PostRunTimer = Stopwatch.StartNew();
            try
            {
                execStage.FailedToStart = true; // Set it here in case OpenInternal throws an exception. Could happen if a step is missing an instrument

                OpenInternal(execStage, continuedExecutionState, currentListeners.Cast<IResource>().ToList(), allEnabledSteps);

                //execStage.WaitForSerialization();
                execStage.ResourceManager.BeginStep(execStage, plan, TestPlanExecutionStage.Execute, TapThread.Current.AbortToken);

                if (continuedExecutionState)
                {  // Since resources are not opened, getting metadata cannot be done in the wait for resources continuation
                   // like shown in TestPlanRun. Instead we do it here.
                    foreach (var res in execStage.ResourceManager.Resources)
                        execStage.Parameters.AddRange(ResultParameters.GetMetadataFromObject(res));
                }

                runWentOk = failState.ExecFail; //important if test plan is aborted and runWentOk is never returned.
                runWentOk = execTestPlan(execStage, steps);
            }
            catch (Exception e)
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
                    Thread.Sleep(500);
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
            }
            finally
            {
                execStage.FailedToStart = (runWentOk == failState.StartFail);

                try
                {
                    finishTestPlanRun(execStage, preRun_Run_PostRunTimer, runWentOk, planRunLog, logStream);
                }
                catch (Exception ex)
                {
                    Log.Error("Error while finishing TestPlan.");
                    Log.Debug(ex);
                }

                OpenTap.Log.RemoveListener(planRunLog);
                planRunLog.Dispose();

                logStream.Dispose();
                File.Delete(fileStreamFile);

                // Clean all test steps StepRun, otherwise the next test plan execution will be stuck at TestStep.DoRun at steps that does not have a cleared StepRun.
                foreach (var step in allSteps)
                    step.StepRun = null;

                executingPlanRun.LocalValue = prevExecutingPlanRun;
                CurrentRun = prevExecutingPlanRun;
            }
            return execStage;
        }

        public TestPlanRun Execute(IEnumerable<IResultListener> resultListeners, IEnumerable<ResultParameter> metaDataParameters, HashSet<ITestStep> stepsOverride)
        {
            return DoExecute(resultListeners, metaDataParameters, stepsOverride);
        }

        public void Close()
        {
            if (IsRunning)
                throw new InvalidOperationException("Cannot close TestPlan while it is running.");
            if (currentExecutionState == null)
                throw new InvalidOperationException("Call open first.");

            Stopwatch timer = Stopwatch.StartNew();
            currentExecutionState.ResourceManager.EndStep(plan, TestPlanExecutionStage.Open);

            // If we locked the setup earlier, unlock it now that all recourses has been closed:
            foreach (var item in monitors)
                item.ExitTestPlanRun(currentExecutionState);

            currentExecutionState = null;
            Log.Debug(timer, "TestPlan closed.");
        }

        public void Open(IEnumerable<IResultListener> listeners)
        {
            if (listeners == null)
                throw new ArgumentNullException(nameof(listeners));
            if (PrintTestPlanRunSummary)
                listeners = listeners.Concat(new IResultListener[] { summaryListener });

            if (currentExecutionState != null)
                throw new InvalidOperationException("Open has already been called.");
            if (IsRunning)
                throw new InvalidOperationException("This TestPlan is already running.");

            try
            {
                var allSteps = Utils.FlattenHeirarchy(plan.Steps.Where(x => x.Enabled), step => step.GetEnabledChildSteps()).ToList();

                Stopwatch timer = Stopwatch.StartNew();
                currentExecutionState = new TestPlanRun(plan, listeners.ToList(), DateTime.Now, Stopwatch.GetTimestamp(), true);
                currentExecutionState.Start();
                OpenInternal(currentExecutionState, false, listeners.Cast<IResource>().ToList(), allSteps);
                try
                {
                    currentExecutionState.ResourceManager.WaitUntilAllResourcesOpened(TapThread.Current.AbortToken);
                }
                catch
                {
                    Log.Warning("Caught error while opening resources! See error message for details.");
                    throw;
                }

                Log.Debug(timer, "TestPlan opened.");
            }
            catch
            {
                // If there is an error, reset the state to allow calling open again later 
                // when the user has fixed the error.
                if (currentExecutionState != null)
                    currentExecutionState.ResourceManager.EndStep(plan, TestPlanExecutionStage.Open);

                if (monitors != null)
                    foreach (var item in monitors)
                        item.ExitTestPlanRun(currentExecutionState);

                currentExecutionState = null;
                throw;
            }
        }

        private void OpenInternal(TestPlanRun run, bool isOpen, List<IResource> resources, List<ITestStep> steps)
        {
            monitors = TestPlanRunMonitors.GetCurrent();
            try
            {
                // Enter monitors
                foreach (var item in monitors)
                    item.EnterTestPlanRun(run);
            }
            finally   // We need to make sure OpenAllAsync is always called (even when CheckResources throws an exception). 
            {         // Otherwise we risk that e.g. ResourceManager.WaitUntilAllResourcesOpened() will hang forever.
                run.ResourceManager.EnabledSteps = steps;
                run.ResourceManager.StaticResources = resources;

                if (!isOpen)
                    run.ResourceManager.BeginStep(run, plan, TestPlanExecutionStage.Open, TapThread.Current.AbortToken);
            }
        }
    }
}
