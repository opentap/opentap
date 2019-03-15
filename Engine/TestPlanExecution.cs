//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenTap
{
    partial class TestPlan
    {
        bool runPrePlanRunMethods(IEnumerable<ITestStep> steps, TestPlanRun planRun, String parentPath)
        {
            Stopwatch preTimer = Stopwatch.StartNew(); // try to avoid calling Stopwatch.StartNew too often.
            TimeSpan elaps = preTimer.Elapsed;
            foreach (ITestStep step in steps)
            {
                if (step.Enabled == false)
                    continue;
                bool runPre = true;
                if(step is TestStep s)
                {
                    runPre = s.PrePostPlanRunUsed;
                }
                planRun.StepsWithPrePlanRun.Add(step);

                string stepPath;
                if (string.IsNullOrEmpty(parentPath) == false)
                    stepPath = parentPath + " \\ " + step.GetFormattedName();
                else
                    stepPath = step.GetFormattedName();
                try
                {
                    if (runPre)
                    {
                        planRun.AddTestStepStateUpdate(step.Id, null, StepState.PrePlanRun);
                        try
                        {
                            planRun.ResourceManager.BeginStep(planRun, step, TestPlanExecutionStage.PrePlanRun, TapThread.Current.AbortToken);
                            try
                            {
                                step.PrePlanRun();
                            }
                            finally
                            {
                                planRun.ResourceManager.EndStep(step, TestPlanExecutionStage.PrePlanRun);
                            }
                        }
                        finally
                        {
                            planRun.AddTestStepStateUpdate(step.Id, null, StepState.Idle);
                        }
                    }

                    if (!runPrePlanRunMethods(step.ChildTestSteps, planRun, stepPath))
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
                        Log.Debug(newelaps - elaps, "{0} PrePlanRun completed.", stepPath);
                        elaps = newelaps;
                    }
                }
            }
            return true;
        }
        #region Nested Types

        enum failState
        {
            Ok,
            StartFail,
            ExecFail
        }

        #endregion

        internal static void PrintWaitingMessage(IEnumerable<IResource> resources)
        {
            Log.Info("Waiting for resources to open:");
            foreach (var resource in resources)
            {
                if (resource.IsConnected) continue;
                Log.Info(" - {0}", resource);
            }
        }

        failState execTestPlan(TestPlanRun execStage, IList<ITestStep> steps)
        {
            WaitHandle.WaitAny(new[] { execStage.PromptWaitHandle, TapThread.Current.AbortToken.WaitHandle });
            bool resultListenerError = false;
            execStage.ScheduleInResultProcessingThread<IResultListener>(resultListener =>
            {
                try
                {
                    using (TimeoutOperation.Create(() => PrintWaitingMessage(new List<IResource>() { resultListener })))
                        execStage.ResourceManager.WaitUntilResourcesOpened(TapThread.Current.AbortToken, resultListener);
                    resultListener.OnTestPlanRunStart(execStage);
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
            
            try
            {
                execStage.StepsWithPrePlanRun.Clear();
                if (!runPrePlanRunMethods(steps, execStage, null))
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
            
            Stopwatch planRunOnlyTimer = Stopwatch.StartNew();
            var runs = new List<TestStepRun>();
            
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step.Enabled == false) continue;
                var run = step.DoRun(execStage, null);

                // note: The following is copied inside TestStep.cs
                if (run.SuggestedNextStep != null)
                {
                    int nextindex = steps.IndexWhen(x => x.Id == run.SuggestedNextStep);
                    if (nextindex >= 0)
                        i = nextindex - 1;
                    // if skip to next step, dont add it to the wait queue.
                }
                else
                {
                    runs.Add(run);
                }
            }

            // Now wait for them to actually complete. They might defer internally.
            foreach (var run in runs)
            {
                run.WaitForCompletion();
                execStage.UpgradeVerdict(run.Verdict);
            }

            if (TapThread.Current.AbortToken.IsCancellationRequested)
            {
                execStage.Verdict = Verdict.Aborted;
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

                        run.UpgradeVerdict(Verdict.Error);
                    }

                    for (int i = run.StepsWithPrePlanRun.Count - 1; i >= 0; i--)
                    {
                        Stopwatch postTimer = Stopwatch.StartNew();
                        String stepPath = string.Empty;
                        try
                        {
                            ITestStep step = run.StepsWithPrePlanRun[i];
                            stepPath = step.GetStepPath();
                            if ((step as TestStep)?.PrePostPlanRunUsed ?? true)
                            {
                                run.AddTestStepStateUpdate(step.Id, null, StepState.PostPlanRun);
                                try
                                {
                                    run.ResourceManager.BeginStep(run, step, TestPlanExecutionStage.PostPlanRun, TapThread.Current.AbortToken);
                                    try
                                    {
                                        step.PostPlanRun();
                                    }
                                    finally
                                    {
                                        run.ResourceManager.EndStep(step, TestPlanExecutionStage.PostPlanRun);
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

                    run.ResourceManager.EndStep(this, TestPlanExecutionStage.Execute);

                    if (!run.IsCompositeRun)
                        run.ResourceManager.EndStep(this, TestPlanExecutionStage.Open);
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
        /// When true, prints the test plan run summary at the end of a run.  
        /// </summary>
        [XmlIgnore]
        public bool PrintTestPlanRunSummary { get; set; }
            
        /// <summary>
        /// Calls the PromptForDutMetadata delegate for all referenced DUTs.
        /// </summary>
        internal void StartResourcePromptAsync(TestPlanRun planRun, IEnumerable<IResource> resources)
        {
            bool AnyMetaData = false;
            planRun.PromptWaitHandle.Reset();
            try
            {
                foreach (var resource in resources)
                {
                    var name = resource.ToString().Trim();
                    var type = TypeData.GetTypeData(resource);
                    foreach (var __prop in type.GetMembers())
                    {
                    IMemberData prop = __prop;
                        var attr = prop.GetAttribute<MetaDataAttribute>();
                        if (attr == null || attr.PromptUser == false) continue;
                    AnyMetaData = true;
                    }
                }
            }
            catch
            {
                // this is just a defensive catch to make sure that the waithandle is not left unset (and we risk waiting for it indefinitely)
                planRun.PromptWaitHandle.Set();
                throw;
            }

            if (AnyMetaData && EngineSettings.Current.PromptForMetaData)
            {
                TapThread.Start(() =>
                    {
                        try
                        {
                            var obj = new MetadataPromptObject { Resources = resources };
                            UserInput.Request(obj, false);
                        }
                        catch(Exception e)
                        {
                            Log.Error("Error occured while executing platform requests");
                            Log.Debug(e);
                        }
                        finally
                        {
                            planRun.PromptWaitHandle.Set();
                        }
                    }, name: "Request Metadata");
                    
                
                planRun.ResourcePromptReset = new System.Collections.Concurrent.ConcurrentStack<Action>();
            }
            else
            {
                planRun.PromptWaitHandle.Set();
            }
        }

        /// <summary>
        /// Blocking Execute TestPlan. Uses ResultListeners from ResultSettings.Current.
        /// </summary>
        /// <returns>Result of test plan run as a TestPlanRun object.</returns>
        public TestPlanRun Execute()
        {
            return Execute(ResultSettings.Current, null);
        }
        TestPlanRunSummaryListener summaryListener = new TestPlanRunSummaryListener();
        /// <summary>
        /// Execute the TestPlan as specified.
        /// </summary>
        /// <param name="resultListeners">ResultListeners for result outputs.</param>
        /// <param name="metaDataParameters">Optional metadata parameters.</param>
        /// <param name="stepsOverride">Sub-section of test plan to be executed. Note this might include child steps of disabled parent steps.</param>
        /// <param name="cancellationToken">Cancellation token to abort the testplan</param>
        /// <returns>TestPlanRun results, no StepResults.</returns>
        public Task<TestPlanRun> ExecuteAsync(IEnumerable<IResultListener> resultListeners, IEnumerable<ResultParameter> metaDataParameters, HashSet<ITestStep> stepsOverride, CancellationToken cancellationToken)
        {
            Task<TestPlanRun> result = Task.Run(() =>
            {
                var sem = new SemaphoreSlim(0);
                TestPlanRun testPlanRun = null;
                TapThread.Start(() =>
                {
                    try
                    {
                        cancellationToken.Register(TapThread.Current.Abort);
                        testPlanRun = Execute(resultListeners, metaDataParameters, stepsOverride);
                    }
                    finally
                    {
                        sem.Release();
                    }
                }, "Plan Thread");
                sem.Wait();
                
                return testPlanRun;
            });
            
            return result;
        }

        internal static ThreadHierarchyLocal<TestPlanRun> executingPlanRun = new ThreadHierarchyLocal<TestPlanRun>();
        
        /// <summary>
        /// Execute the TestPlan as specified. Blocking.
        /// </summary>
        /// <param name="resultListeners">ResultListeners for result outputs.</param>
        /// <param name="metaDataParameters">Optional metadata parameters.</param>
        /// <param name="stepsOverride">Sub-section of test plan to be executed. Note this might include child steps of disabled parent steps.</param>
        /// <returns>TestPlanRun results, no StepResults.</returns>
        public TestPlanRun Execute(IEnumerable<IResultListener> resultListeners, IEnumerable<ResultParameter> metaDataParameters = null, HashSet<ITestStep> stepsOverride = null)
        {
            var preHooks = PluginManager.GetPlugins<IPreTestPlanExecutionHook>()
                .Select(type => (IPreTestPlanExecutionHook)type.CreateInstance())
                .OrderBy(type => type.GetType().GetDisplayAttribute().Order)
                .ToList();

            var executionHooks = PluginManager.GetPlugins<ITestPlanExecutionHook>()
                .Select(type => (ITestPlanExecutionHook)type.CreateInstance())
                .OrderBy(type => type.GetType().GetDisplayAttribute().Order)
                .ToList();

            var tp = this;

            foreach (var hook in preHooks)
            {
                PreExecutionHookArgs args = new PreExecutionHookArgs { TestPlan = tp, IsSettingsInvalid = false };
                hook.BeforeTestPlanExecute(args);

                Stopwatch sw = Stopwatch.StartNew();

                if (IsOpen && ((this != args.TestPlan) || args.IsSettingsInvalid))
                {
                    Close();

                    Log.Debug(sw, "The testplan was closed by a pre-execution hook ({0}).", hook.ToString());
                    sw.Restart();

                    OnPropertyChanged("IsOpen");
                }

                if (args.IsSettingsInvalid)
                {
                    using (var ms = new MemoryStream())
                    {
                        args.TestPlan.Save(ms);
                        ms.Seek(0, 0);

                        ComponentSettings.InvalidateAllSettings();

                        tp = Load(ms, args.TestPlan.Path);

                        Log.Debug(sw, "Reloaded testplan because settings were invalidate by a pre-execution hook ({0}).", hook.ToString());
                    }
                }
                else
                    tp = args.TestPlan;
            }

            // Hook up the correct events in case testplan was changed.
            if (tp != this)
            {
                tp.BreakOffered += ForwardOfferBreak;
            }

            List<ITestPlanExecutionHook> successfulHooks = new List<ITestPlanExecutionHook>();
            try
            {
                // Send event that testplan changed to `tp`
                // Exceptions before the testplan starts executing are expected to abort, so they will not be caught here
                executionHooks.ForEach(eh =>
                {
                    eh.BeforeTestPlanExecute(tp);
                    successfulHooks.Add(eh);
                });

                return tp.DoExecute(resultListeners, metaDataParameters, stepsOverride, executionHooks);
            }
            finally
            {
                // Send event that testplan changed back to `this`
                successfulHooks.ForEach(eh =>
                {
                    try
                    {
                        eh.AfterTestPlanExecute(tp, this);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Post execution hook '{0}' failed with error: {1}", eh, ex.Message);
                        Log.Debug(ex);
                    }
                });

                if (tp != this)
                {
                    tp.BreakOffered -= ForwardOfferBreak;
                }
            }
        }

        private void ForwardOfferBreak(object sender, BreakOfferedEventArgs e)
        {
            OnBreakOffered(e);
        }

        private TestPlanRun DoExecute(IEnumerable<IResultListener> resultListeners, IEnumerable<ResultParameter> metaDataParameters, HashSet<ITestStep> stepsOverride, List<ITestPlanExecutionHook> executionHooks)
        {
            if (resultListeners == null)
                throw new ArgumentNullException("resultListeners");

            if (PrintTestPlanRunSummary && !resultListeners.Contains(summaryListener))
                resultListeners = resultListeners.Concat(new IResultListener[] { summaryListener });
            resultListeners = resultListeners.Where(r => r is IEnabledResource ? ((IEnabledResource)r).IsEnabled : true);
            IList<ITestStep> steps;
            if (stepsOverride == null)
                steps = Steps;
            else
            {
                // Remove steps that are already included via their parent steps.
                HashSet<ITestStep> foundSteps = new HashSet<ITestStep>();
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
                steps = Utils.FlattenHeirarchy(Steps, step => step.ChildTestSteps).Where(stepsOverride.Contains).ToList();
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

            Log.Info("Starting TestPlan '{0}' on {1}, {2} of {3} TestSteps enabled.", Name, initTime, allEnabledSteps.Count, allSteps.Count);

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
                execStage = new TestPlanRun(this, resultListeners.ToList(), initTime, initTimeStamp);

                execStage.Parameters.AddRange(PluginManager.GetPluginVersions(allEnabledSteps));
                execStage.ResourceManager.ResourceOpened += r =>
                {
                    execStage.Parameters.AddRange(PluginManager.GetPluginVersions(new List<object> { r }));
                };
            }

            execStage.ExecutionHooks = executionHooks;

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

                execStage.ResourceManager.BeginStep(execStage, this, TestPlanExecutionStage.Execute, TapThread.Current.AbortToken);

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
                if (e is OperationCanceledException)
                {
                    Log.Warning(String.Format("TestPlan aborted. ({0})", e.Message));
                    execStage.UpgradeVerdict(Verdict.Aborted);
                }
                else if (e is ThreadAbortException)
                {
                    // It seems this actually never happens.
                    Log.Warning("TestPlan aborted.");
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

                while (execStage.ResourcePromptReset.TryPop(out var action))
                {
                    try
                    {
                        action();
                    }
                    catch (Exception e)
                    {
                        Log.Error("Caught exception while resetting metadata. '{0}'", e.Message);
                        Log.Debug(e);
                    }
                }
                executingPlanRun.LocalValue = prevExecutingPlanRun;
                CurrentRun = prevExecutingPlanRun;
            }
            return execStage;
        }

        /// <summary>
        /// Execute the TestPlan as specified. Blocking.
        /// </summary>
        /// <param name="resultListeners">ResultListeners for result outputs.</param>
        /// <param name="metaDataParameters">Metadata parameters.</param>
        /// <returns>TestPlanRun results, no StepResults.</returns>
        public TestPlanRun Execute(IEnumerable<IResultListener> resultListeners, IEnumerable<ResultParameter> metaDataParameters)
        {
            return Execute(resultListeners, metaDataParameters, null);
        }

        private TestPlanRun currentExecutionState = null;

        /// <summary> true if the plan is in its open state. </summary>
        public bool IsOpen { get { return currentExecutionState != null; } }
        
        /// <summary>
        /// Opens all resources referenced in this TestPlan (Instruments/DUTs/ResultListeners). 
        /// This can be called before <see cref="TestPlan.Execute()"/> to manually control the opening/closing of the resources.
        /// </summary>
        public void Open()
        {
            Open(ResultSettings.Current);
        }

        /// <summary>
        /// Opens all resources referenced in this TestPlan (Instruments/DUTs/ResultListeners). 
        /// This can be called before <see cref="TestPlan.Execute()"/> to manually control the opening/closing of the resources.
        /// </summary>
        public void Open(IEnumerable<IResultListener> listeners)
        {
            if (listeners == null)
                throw new ArgumentNullException("listeners");
            if (PrintTestPlanRunSummary)
                listeners = listeners.Concat(new IResultListener[] { summaryListener });

            if (currentExecutionState != null)
                throw new InvalidOperationException("Open has already been called.");
            if (IsRunning)
                throw new InvalidOperationException("This TestPlan is already running.");

            try
            {
                var allSteps = Utils.FlattenHeirarchy(Steps.Where(x => x.Enabled), step => step.GetEnabledChildSteps()).ToList();

                Stopwatch timer = Stopwatch.StartNew();
                currentExecutionState = new TestPlanRun(this, listeners.ToList(), DateTime.Now, Stopwatch.GetTimestamp(), true);
                currentExecutionState.PromptWaitHandle.Set();
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
                    currentExecutionState.ResourceManager.EndStep(this, TestPlanExecutionStage.Open);

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
                    run.ResourceManager.BeginStep(run, this, TestPlanExecutionStage.Open, TapThread.Current.AbortToken);
            }
        }

        /// <summary>
        /// Closes all resources referenced in this TestPlan (Instruments/DUTs/ResultListeners). 
        /// This should be called if <see cref="TestPlan.Open()"/> was called earlier to manually close the resources again.
        /// </summary>
        public void Close()
        {
            if (IsRunning)
                throw new InvalidOperationException("Cannot close TestPlan while it is running.");
            if (currentExecutionState == null)
                throw new InvalidOperationException("Call open first.");

            Stopwatch timer = Stopwatch.StartNew();
            currentExecutionState.ResourceManager.EndStep(this, TestPlanExecutionStage.Open);

            // If we locked the setup earlier, unlock it now that all recourses has been closed:
            foreach (var item in monitors)
                item.ExitTestPlanRun(currentExecutionState);

            currentExecutionState = null;
            Log.Debug(timer, "TestPlan closed.");

        }
    }

    // This object has a special data annotator that embeds metadata properties
    // from Resources into it.
    class MetadataPromptObject
    {
        public string Name { get; private set; } = "Please enter test plan metadata.";
        [Browsable(false)]
        public IEnumerable<IResource> Resources { get; set; }
    }
}
