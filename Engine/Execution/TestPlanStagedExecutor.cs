//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace OpenTap
{
    class TestPlanStagedExecutor : ITestPlanExecutor
    {
        private static readonly TraceSource log = Log.CreateSource("Executor");
        StagedExecutor executor = new StagedExecutor(TypeData.FromType(typeof(TestPlanExecutionStageBase)));

        public readonly TestPlan TestPlan;

        public TestPlanStagedExecutor(TestPlan plan)
        {
            this.TestPlan = plan;
            context = new TestPlanExecutionContext()
            {
                Plan = plan
            };
        }

        internal readonly static TestPlanRunSummaryListener summaryListener = new TestPlanRunSummaryListener();
        private TestPlanExecutionContext context;
        public bool PrintTestPlanRunSummary
        {
            get => context.PrintTestPlanRunSummary;
            set => context.PrintTestPlanRunSummary = value;
        }

        public TestPlanRun CurrentRun => context?.Run;
        public TestPlanRun State 
        { 
            get => context?.currentExecutionState; 
            set => context.currentExecutionState = value; 
        }

        public TestPlanRun Execute(IEnumerable<IResultListener> resultListeners, IEnumerable<ResultParameter> metaDataParameters, HashSet<ITestStep> stepsOverride)
        {
            if (resultListeners == null)
                throw new ArgumentNullException("resultListeners");

            if (PrintTestPlanRunSummary && !resultListeners.Contains(summaryListener))
                resultListeners = resultListeners.Concat(new IResultListener[] { summaryListener });
            resultListeners = resultListeners.Where(r => r is IEnabledResource ? ((IEnabledResource)r).IsEnabled : true);

            context.resultListeners = resultListeners;
            context.metaDataParameters = metaDataParameters;
            context.stepsOverride = stepsOverride;
            // Todo: add actual stages...
            return executor.Execute<TestPlanRun>(context);
        }

        public void Open(IEnumerable<IResultListener> listeners)
        {
            if (listeners == null)
                throw new ArgumentNullException(nameof(listeners));
            if (PrintTestPlanRunSummary)
                listeners = listeners.Concat(new IResultListener[] { summaryListener });

            if (context.currentExecutionState != null)
                throw new InvalidOperationException("Open has already been called.");
            bool IsRunning = CurrentRun != null;
            if (IsRunning)
                throw new InvalidOperationException("This TestPlan is already running.");

            var monitors = TestPlanRunMonitors.GetCurrent();
            try
            {
                var allSteps = Utils.FlattenHeirarchy(context.Plan.Steps.Where(x => x.Enabled), step => step.GetEnabledChildSteps()).ToList();

                Stopwatch timer = Stopwatch.StartNew();
                context.currentExecutionState = new TestPlanRun(context.Plan, listeners.ToList(), DateTime.Now, Stopwatch.GetTimestamp(), true);
                context.currentExecutionState.Start();
                //OpenInternal(context.currentExecutionState, false, listeners.Cast<IResource>().ToList(), allSteps); 
                var run = context.currentExecutionState;
                try
                {
                    // Enter monitors
                    foreach (var item in monitors)
                        item.EnterTestPlanRun(run);
                }
                finally   // We need to make sure OpenAllAsync is always called (even when CheckResources throws an exception). 
                {         // Otherwise we risk that e.g. ResourceManager.WaitUntilAllResourcesOpened() will hang forever.
                    run.ResourceManager.EnabledSteps = allSteps;
                    run.ResourceManager.StaticResources = listeners.Cast<IResource>().ToList();
                    run.ResourceManager.BeginStep(run, context.Plan, TestPlanExecutionStage.Open, TapThread.Current.AbortToken);
                }

                try
                {
                    context.currentExecutionState.ResourceManager.WaitUntilAllResourcesOpened(TapThread.Current.AbortToken);
                }
                catch
                {
                    log.Warning("Caught error while opening resources! See error message for details.");
                    throw;
                }

                log.Debug(timer, "TestPlan opened.");
            }
            catch
            {
                // If there is an error, reset the state to allow calling open again later 
                // when the user has fixed the error.
                if (context.currentExecutionState != null)
                    context.currentExecutionState.ResourceManager.EndStep(context.Plan, TestPlanExecutionStage.Open);

                if (monitors != null)
                    foreach (var item in monitors)
                        item.ExitTestPlanRun(context.currentExecutionState);

                context.currentExecutionState = null;
                throw;
            }
        }

        public void Close()
        {
            bool IsRunning = CurrentRun != null;
            if (IsRunning)
                throw new InvalidOperationException("Cannot close TestPlan while it is running.");
            if (context.currentExecutionState == null)
                throw new InvalidOperationException("Call open first.");

            Stopwatch timer = Stopwatch.StartNew();
            context.currentExecutionState.ResourceManager.EndStep(context.Plan, TestPlanExecutionStage.Open);

            // If we locked the setup earlier, unlock it now that all recourses has been closed:
            var monitors = TestPlanRunMonitors.GetCurrent();
            foreach (var item in monitors)
                item.ExitTestPlanRun(context.currentExecutionState);

            context.currentExecutionState = null;
            log.Debug(timer, "TestPlan closed.");
        }
    }

    class TestPlanExecutionContext : ExecutionStageContext
    {
        public TestPlan Plan { get; set; }
        public TestPlanRun Run { get; set; }
        public IEnumerable<IResultListener> resultListeners { get; set; }
        public IEnumerable<ResultParameter> metaDataParameters { get; set; }
        public HashSet<ITestStep> stepsOverride { get; set; }
        public TestPlanRun currentExecutionState { get; internal set; }
        public bool PrintTestPlanRunSummary { get; internal set; }
    }

    abstract class TestPlanExecutionStageBase : IExecutionStage
    {
        public void Execute(ExecutionStageContext context)
        {
            Execute((TestPlanExecutionContext)context);
        }

        protected abstract void Execute(TestPlanExecutionContext context);
    }

    class StepOverrideStage : TestPlanExecutionStageBase
    {
        public IList<ITestStep> steps { get; set; }
        public List<ITestStep> allSteps { get; private set; }
        public List<ITestStep> allEnabledSteps { get; private set; }

        protected override void Execute(TestPlanExecutionContext context)
        {
            if (context.stepsOverride == null)
                steps = context.Plan.Steps;
            else
            {
                // Remove steps that are already included via their parent steps.
                foreach (var step in context.stepsOverride)
                {
                    if (step == null)
                        throw new ArgumentException("stepsOverride may not contain null", "stepsOverride");

                    var p = step.GetParent<ITestStep>();
                    while (p != null)
                    {
                        if (context.stepsOverride.Contains(p))
                            throw new ArgumentException("stepsOverride may not contain steps and their parents.", "stepsOverride");
                        p = p.GetParent<ITestStep>();
                    }
                }
                steps = Utils.FlattenHeirarchy(context.Plan.Steps, step => step.ChildTestSteps).Where(context.stepsOverride.Contains).ToList();
            }

            allSteps = Utils.FlattenHeirarchy(steps, step => step.ChildTestSteps);
            allEnabledSteps = Utils.FlattenHeirarchy(steps.Where(x => x.Enabled), step => step.GetEnabledChildSteps());

        }
    }

    class ResetVerdicts : TestPlanExecutionStageBase
    {
        public StepOverrideStage StepsStage { get; set; }
        protected override void Execute(TestPlanExecutionContext context)
        {
            foreach (var step in StepsStage.allSteps)
            {
                if (step.Verdict != Verdict.NotSet)
                {
                    step.Verdict = Verdict.NotSet;
                    step.OnPropertyChanged("Verdict");
                }
            }
        }
    }

    class CreateLogStage : TestPlanExecutionStageBase
    {
        internal static readonly TraceSource Log = OpenTap.Log.CreateSource("Executor(CL)");

        public string fileStreamFile { get; private set; }
        public HybridStream logStream { get; private set; }
        public FileTraceListener planRunLog { get; private set; }

        protected override void Execute(TestPlanExecutionContext context)
        {
            Log.Info("-----------------------------------------------------------------"); // Print this to the session log, just before attaching the run log

            fileStreamFile = FileSystemHelper.CreateTempFile();
            logStream = new HybridStream(fileStreamFile, 1024 * 1024);

            planRunLog = new FileTraceListener(logStream) { IsRelative = true };
            OpenTap.Log.AddListener(planRunLog);
        }
    }

    class CreateRunStage : TestPlanExecutionStageBase
    {
        internal static readonly TraceSource Log = OpenTap.Log.CreateSource("Executor(CR)");

        public StepOverrideStage StepsStage { get; set; }
        public TestPlanRun execStage { get; private set; }
        public bool continuedExecutionState { get; private set; }

        public Stopwatch preRun_Run_PostRunTimer { get; private set; }

        protected override void Execute(TestPlanExecutionContext context)
        {
            long initTimeStamp = Stopwatch.GetTimestamp();
            var initTime = DateTime.Now;

            var enabledSinks = new HashSet<IResultSink>();
            TestStepExtensions.GetObjectSettings<IResultSink, ITestStep, IResultSink>(StepsStage.allEnabledSteps, true, null, enabledSinks);
            if (enabledSinks.Count > 0)
            {
                var sinkListener = new ResultSinkListener(enabledSinks);
                context.resultListeners = context.resultListeners.Append(sinkListener);
            }

            Log.Info("Starting TestPlan '{0}' on {1}, {2} of {3} TestSteps enabled.", context.Plan.Name, initTime, StepsStage.allEnabledSteps.Count, StepsStage.allSteps.Count);

            if (context.currentExecutionState != null)
            {
                // load result listeners that are _not_ used in the previous runs.
                // otherwise they wont get opened later.
                foreach (var rl in context.resultListeners)
                {
                    if (!context.currentExecutionState.ResultListeners.Contains(rl))
                        context.currentExecutionState.ResultListeners.Add(rl);
                }
            }

            continuedExecutionState = false;
            if (context.currentExecutionState != null)
            {
                execStage = new TestPlanRun(context.currentExecutionState, initTime, initTimeStamp);
                continuedExecutionState = true;
            }
            else
            {
                execStage = new TestPlanRun(context.Plan, context.resultListeners.ToList(), initTime, initTimeStamp);
                execStage.Start();

                execStage.Parameters.AddRange(PluginManager.GetPluginVersions(StepsStage.allEnabledSteps));
                execStage.ResourceManager.ResourceOpened += r =>
                {
                    execStage.Parameters.AddRange(PluginManager.GetPluginVersions(new List<object> { r }));
                };
            }

            if (context.metaDataParameters != null)
                execStage.Parameters.AddRange(context.metaDataParameters);

            execStage.MainThread = TapThread.Current.Parent;

            context.Run = execStage;
            preRun_Run_PostRunTimer = Stopwatch.StartNew();
        }
    }

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

            run.WaitForSerialization();
            run.ResourceManager.BeginStep(run, context.Plan, TestPlanExecutionStage.Execute, TapThread.Current.AbortToken);

            if (CreateRun.continuedExecutionState)
            {  // Since resources are not opened, getting metadata cannot be done in the wait for resources continuation
               // like shown in TestPlanRun. Instead we do it here.
                foreach (var res in run.ResourceManager.Resources)
                    run.Parameters.AddRange(ResultParameters.GetMetadataFromObject(res));
            }
        }
    }

    class MetadataPromptStage : TestPlanExecutionStageBase
    {
        public CreateRunStage CreateRunStage { get; set; }

        public OpenResourcesStage OpenResourcesStage { get; set; } // Only run this after resources have been opened
        protected override void Execute(TestPlanExecutionContext context)
        {
            TestPlanRun run = CreateRunStage.execStage;
            
            var resources = ResourceManagerUtils.GetResourceNodes(run.ResourceManager.StaticResources.Cast<object>().Concat(run.ResourceManager.EnabledSteps));

            TestPlanExecutonHelpers.StartResourcePromptAsync(run, resources.Select(res => res.Resource));
            
            WaitHandle.WaitAny(new[] { run.PromptWaitHandle, TapThread.Current.AbortToken.WaitHandle });
        }
    }

    class ExecutePlanStage : TestPlanExecutionStageBase
    {
        internal static readonly TraceSource Log = OpenTap.Log.CreateSource("Executor(EP)");

        public StepOverrideStage StepsStage { get; set; }
        public CreateRunStage CreateRun { get; set; }

        public MetadataPromptStage MetadataPromptStage { get; set; } // wait for prompts before executing

        protected override void Execute(TestPlanExecutionContext context)
        {
            var execStage = CreateRun.execStage;
            var steps = StepsStage.steps;
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

                    execStage.WaitForSerialization();
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
            {
                execStage.FailedToStart = true;
                return;
            }

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
