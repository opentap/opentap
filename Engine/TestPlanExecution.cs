//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenTap
{
    partial class TestPlan
    {
        bool runPrePlanRunMethods(IList<ITestStep> steps, TestPlanRun planRun)
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
                            catch (ExpectedException e)
                            {
                                e.Handle(step.Name);
                                step.Verdict = e.Verdict;
                                throw e;
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
                catch (ExpectedException e)
                {
                    throw e;
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
            // Save disconnected ressources to avoid race conditions.
            var waitingRessources = resources.Where(r => !r.IsConnected).ToArray();
            if (waitingRessources.Length == 0)
            {
                return;
            }

            Log.Info("Waiting for resources to open:");
            foreach (var resource in waitingRessources)
            {
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
                    try
                    {
                        // some resources might set metadata in the Open methods.
                        // this information needs to be propagated to result listeners as well.
                        // this returns quickly if its a lazy resource manager.
                        using (TimeoutOperation.Create(
                            () => PrintWaitingMessage(new List<IResource>() {resultListener})))
                            execStage.ResourceManager.WaitUntilAllResourcesOpened(TapThread.Current.AbortToken);
                    }
                    catch
                    {
                         // this error will also be handled somewhere else.
                    }

                    execStage.WaitForSerialization();
                    foreach(var res in execStage.PromptedResources)
                        execStage.Parameters.AddRange(ResultParameters.GetMetadataFromObject(res));
                    resultListener.OnTestPlanRunStart(execStage);
                }
                catch (OperationCanceledException) when(execStage.MainThread.AbortToken.IsCancellationRequested)
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
                
                // Invoke test plan pre run event mixins.
                TestPlanPreRunEvent.Invoke(this);
                
                if (!runPrePlanRunMethods(steps, execStage))
                {
                    return failState.StartFail;
                }
            }
            catch (ExpectedException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                Log.Error(e.GetInnerMostExceptionMessage());
                Log.Debug(e);
                return failState.StartFail;
            }
            finally{
            {
                Log.Debug(sw, "PrePlanRun Methods completed");
            }}
            
            Stopwatch planRunOnlyTimer = Stopwatch.StartNew();
            var runs = new List<TestStepRun>();

            bool didBreak = false;
            void addBreakResult(TestStepRun run)
            {
                if (didBreak) return;
                didBreak = true;
                execStage.Parameters.Add(new ResultParameter(TestPlanRun.SpecialParameterNames.BreakIssuedFrom, run.Id.ToString())); 
            }

            TestStepRun getBreakingRun(TestStepRun first)
            {
                while ((first.Exception as TestStepBreakException)?.Run is { } inner)
                    first = inner;
                return first;
            }

            try
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    if (step.Enabled == false) continue;
                    var run = step.DoRun(execStage, execStage);
                    if (!run.Skipped)
                        runs.Add(run);
                    if (run.BreakConditionsSatisfied())
                    {
                        var breakingRun = getBreakingRun(run);
                        addBreakResult(breakingRun);
                        run.LogBreakCondition();
                        break;
                    }

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
            catch(TestStepBreakException breakEx)
            {
                var breakingRun = getBreakingRun(breakEx.Run);
                addBreakResult(breakingRun);
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

                        if(run.Verdict < Verdict.Aborted)
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
                                    catch (ExpectedException e)
                                    {
                                        e.Handle(step.Name);
                                        step.Verdict = e.Verdict;
                                        throw e;
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
                        catch (ExpectedException e)
                        {
                            throw e;
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
                    catch (Exception e)
                    {
                        Log.Error("Failed to open resource ({0})", e.GetInnerMostExceptionMessage());
                        Log.Debug(e);
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
                    
                    if(PrintTestPlanRunSummary)
                        summaryListener.PrintArtifactsSummary();
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
        /// <summary> When true, prints the test plan run summary at the end of a run. </summary>
        [XmlIgnore]
        [AnnotationIgnore]
        public bool PrintTestPlanRunSummary { get; set; }
            
        /// <summary>
        /// Calls the PromptForDutMetadata delegate for all referenced DUTs.
        /// </summary>
        internal void StartResourcePromptAsync(TestPlanRun planRun, IEnumerable<IResource> _resources)
        {
            var resources = _resources.Where(x => x != null).ToArray();
            
            var componentSettingsWithMetaData = new List<ITypeData>();
            var componentSettings = TypeData.GetDerivedTypes<ComponentSettings>()
                .Where(type => type.CanCreateInstance);
            bool anyMetaData = false;
            planRun.PromptWaitHandle.Reset();

            try
            {
                foreach (var setting in componentSettings)
                {
                    foreach (var member in setting.GetMembers())
                    {
                        var attr = member.GetAttribute<MetaDataAttribute>();
                        if (attr != null && attr.PromptUser)
                        {
                            anyMetaData = true;
                            componentSettingsWithMetaData.Add(setting);
                            break; // avoid adding this multiple times.
                        }
                    }
                }

                foreach (var resource in resources)
                {
                    if (anyMetaData) break;
                    var type = TypeData.GetTypeData(resource);
                    foreach (var prop in type.GetMembers())
                    {
                        var attr = prop.GetAttribute<MetaDataAttribute>();
                        if (attr != null && attr.PromptUser)
                        {
                            anyMetaData = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // this is just a defensive catch to make sure that the WaitHandle is not left unset (and we risk waiting for it indefinitely)
                planRun.PromptWaitHandle.Set();
                throw;
            }
            
            if (anyMetaData && EngineSettings.Current.PromptForMetaData)
            {
                TapThread.Start(() =>
                    {
                        try
                        {
                            List<object> objects = new List<object>();
                            objects.AddRange(componentSettingsWithMetaData.Select(ComponentSettings.GetCurrent));
                            objects.AddRange(resources);
                            objects.RemoveIf<object>(x => x == null); //ComponentSettings.GetCurrent can return null for abstract types.

                            planRun.PromptedResources = resources;
                            var obj = new MetadataPromptObject { Resources = objects };
                            UserInput.Request(obj, false);
                            if (obj.Response == MetadataPromptObject.PromptResponse.Abort)
                                planRun.MainThread.Abort();
                        }
                        catch(Exception e)
                        {
                            Log.Debug(e);
                            planRun.MainThread.Abort("Error occured while executing platform requests. Metadata prompt can be disabled from the Engine settings menu.");
                        }
                        finally
                        {
                            planRun.PromptWaitHandle.Set();
                        }
                    }, name: "Request Metadata");
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

        /// <summary>Executes the test plan asynchronously </summary>
        /// <returns>A task returning the test plan run.</returns>
        public Task<TestPlanRun> ExecuteAsync()
        {
            return ExecuteAsync(ResultSettings.Current, null,null, TapThread.Current.AbortToken);
        }
        
        /// <summary>
        /// Executes the test plan asynchronously.
        /// </summary>
        /// <param name="abortToken">This abort token can be used to abort the operation.</param>
        /// <returns>A task returning the test plan run.</returns>
        public Task<TestPlanRun> ExecuteAsync(CancellationToken abortToken)
        {
            return ExecuteAsync(ResultSettings.Current, null,null, abortToken);
        }
        
        readonly TestPlanRunSummaryListener summaryListener = new TestPlanRunSummaryListener();
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
            var tcs = new TaskCompletionSource<TestPlanRun>();
            TapThread.Start(() =>
            {
                try
                {
                    cancellationToken.Register(TapThread.Current.Abort);
                    var testPlanRun = Execute(resultListeners, metaDataParameters, stepsOverride);
                    tcs.SetResult(testPlanRun);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }, "Plan Thread");
            return tcs.Task;
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
            return executeInContext( resultListeners, metaDataParameters, stepsOverride);
        }

        TestPlanRun executeInContext(IEnumerable<IResultListener> resultListeners,
            IEnumerable<ResultParameter> metaDataParameters, HashSet<ITestStep> stepsOverride)
        {
            using (TapThread.UsingThreadContext())
            {
                return DoExecute(resultListeners, metaDataParameters, stepsOverride);
            }
        }

        private TestPlanRun DoExecute(IEnumerable<IResultListener> resultListeners, IEnumerable<ResultParameter> metaDataParameters, HashSet<ITestStep> stepsOverride)
        {
            if (resultListeners == null)
                throw new ArgumentNullException(nameof(resultListeners));
            
            ResultParameters.ParameterCache.LoadCache();
            
            if (PrintTestPlanRunSummary && !resultListeners.Contains(summaryListener))
                resultListeners = resultListeners.Concat(new IResultListener[] { summaryListener });
            resultListeners = resultListeners.Where(r => r is IEnabledResource ? ((IEnabledResource)r).IsEnabled : true);
            IList<ITestStep> steps;
            if (stepsOverride == null)
                steps = Steps;
            else
            {
                // Remove steps that are already included via their parent steps.
                foreach (var step in stepsOverride)
                {
                    if (step == null)
                        throw new ArgumentException("stepsOverride may not contain null", nameof(stepsOverride));

                    var p = step.GetParent<ITestStep>();
                    while (p != null)
                    {
                        if (stepsOverride.Contains(p))
                            throw new ArgumentException("stepsOverride may not contain steps and their parents.", nameof(stepsOverride));
                        p = p.GetParent<ITestStep>();
                    }
                }
                steps = Utils.FlattenHeirarchy(Steps, step => step.ChildTestSteps).Where(stepsOverride.Contains).ToList();
            }

            long initTimeStamp = Stopwatch.GetTimestamp();
            var initTime = DateTime.Now;

            Log.Info("-----------------------------------------------------------------");

            
            var logStream = new HybridStream();

            var planRunLog = new FileTraceListener(logStream) { IsRelative = true };
            OpenTap.Log.AddListener(planRunLog);

            var allSteps = Utils.FlattenHeirarchy(steps, step => step.ChildTestSteps);
            var allEnabledSteps = Utils.FlattenHeirarchy(steps.Where(x => x.Enabled), step => step.GetEnabledChildSteps());

            var enabledSinks = new HashSet<IResultSink>();
            TestStepExtensions.GetObjectSettings<IResultSink, ITestStep, IResultSink>(allEnabledSteps, true, null, enabledSinks);
            if(enabledSinks.Count > 0)
            {
                var sinkListener = new ResultSinkListener(enabledSinks);
                resultListeners = resultListeners.Append(sinkListener);
            }

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
                currentExecutionState.ResultListenersSealed = false;
                // load result listeners that are _not_ used in the previous runs.
                // otherwise they wont get opened later.
                foreach (var rl in resultListeners)
                {
                    currentExecutionState.AddResultListener(rl);
                }
            }

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
                if (stepsOverride != null)
                {
                    var overrides = stepsOverride.Select(o => o.Id.ToString()).ToArray();
                    // The order of the guids does not really matter. 
                    // Only that the order is the same across runs
                    Array.Sort(overrides); 
                    execStage.Parameters.Add(new ResultParameter(TestPlanRun.SpecialParameterNames.StepOverrideList, string.Join(",", overrides)));
                }

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

                OpenInternal(execStage, continuedExecutionState, allEnabledSteps, Array.Empty<IResource>());
                    
                execStage.WaitForSerialization();
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
            catch (ExpectedException e)
            {
                execStage.UpgradeVerdict(e.Verdict);
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
                    execStage.Exception = e;
                    Log.Error(e.Message);
                    execStage.UpgradeVerdict(Verdict.Error);
                }
                else
                {
                    execStage.Exception = e;
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

                catch (ExpectedException e)
                {
                    execStage.UpgradeVerdict(e.Verdict);
                }
                catch (Exception ex)
                {
                    Log.Error("Error while finishing TestPlan.");
                    Log.Debug(ex);
                }

                OpenTap.Log.RemoveListener(planRunLog);
                planRunLog.Dispose();

                logStream.Dispose();

                // Clean all test steps StepRun, otherwise the next test plan execution will be stuck at TestStep.DoRun at steps that does not have a cleared StepRun.
                foreach (var step in allSteps)
                    step.StepRun = null;

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

        TestPlanRun currentExecutionState = null; 

        /// <summary> true if the plan is in its open state. </summary>
        [AnnotationIgnore]
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
        /// <param name="listeners">Result listeners used in connection with the test plan.</param>
        public void Open(IEnumerable<IResultListener> listeners)
        {
            Open(listeners, Array.Empty<IResource>());
        }
        
        /// <summary>
        /// Opens all resources referenced in this TestPlan (Instruments/DUTs/ResultListeners). 
        /// This can be called before <see cref="TestPlan.Execute()"/> to manually control the opening/closing of the resources.
        /// It can also be called multiple times to add more resources.
        /// </summary>
        /// <param name="additionalResources">Additional resources added as 'static' resources.</param>
        public void Open(IEnumerable<IResource> additionalResources)
        {
            Open(ResultSettings.Current, additionalResources);
        }

        /// <summary>
        /// Opens all resources referenced in this TestPlan (Instruments/DUTs/ResultListeners). 
        /// This can be called before <see cref="TestPlan.Execute()"/> to manually control the opening/closing of the resources.
        /// It can also be called multiple times to add more resources.
        /// </summary>
        /// <param name="listeners">Result listeners used in connection with the test plan.</param>
        /// <param name="additionalResources">Additional resources added as 'static' resources.</param>
        public void Open(IEnumerable<IResultListener> listeners, IEnumerable<IResource> additionalResources)
        {
            if (listeners == null)
                throw new ArgumentNullException(nameof(listeners));
            if (PrintTestPlanRunSummary)
                listeners = listeners.Concat(new IResultListener[] { summaryListener });
                
            if (IsRunning)
                throw new InvalidOperationException("This TestPlan is already running.");

            try
            {
                var allSteps = Utils.FlattenHeirarchy(Steps.Where(x => x.Enabled), step => step.GetEnabledChildSteps()).ToList();

                Stopwatch timer = Stopwatch.StartNew();
                if (currentExecutionState == null)
                {
                    currentExecutionState = new TestPlanRun(this, listeners.ToList(), DateTime.Now, Stopwatch.GetTimestamp(), true);
                    currentExecutionState.Start();
                }
                OpenInternal(currentExecutionState, false, allSteps, additionalResources);
                
                try
                {
                    currentExecutionState.ResourceManager.WaitUntilAllResourcesOpened(TapThread.Current.AbortToken);
                }
                catch (Exception ex)
                {
                    Log.Warning("Caught error while opening resources! See error message for details.");
                    ex.RethrowInner();
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
        
        private void OpenInternal(TestPlanRun run, bool isOpen, List<ITestStep> steps, IEnumerable<IResource> additionalResources)
        {
            monitors = TestPlanRunMonitors.GetCurrent();

            // Enter monitors
            foreach (var item in monitors)
                item.EnterTestPlanRun(run);
            
            run.ResourceManager.EnabledSteps = steps;
            run.ResourceManager.StaticResources = run.ResultListeners.Concat(additionalResources).ToArray();
            run.ResultListenersSealed = true;
            if (!isOpen)
                run.ResourceManager.BeginStep(run, this, TestPlanExecutionStage.Open, TapThread.Current.AbortToken);
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
        public IEnumerable<object> Resources { get; set; }

        public enum PromptResponse
        {
            OK,
            Abort
        }
        
        [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
        [Submit]
        public PromptResponse Response { get; set; }
        
    }
}
    