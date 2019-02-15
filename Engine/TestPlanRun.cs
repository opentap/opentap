//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap
{
    /// <summary>
    /// Object that holds the state of a specific TestPlan run.
    /// Also internally manages resources and threads relating to the <see cref="TestStepRun"/>.
    /// Note: <see cref="ResourceManager"/> manages opening and closing a <see cref="Resource"/>.
    /// </summary>
    [Serializable]
    [DataContract(IsReference = true)]
    public class TestPlanRun : TestRun
    {
        private static readonly TraceSource log =  OpenTap.Log.CreateSource("TestPlan");
        private static readonly TraceSource resultLog =  OpenTap.Log.CreateSource("Resources");
        private TestPlan plan = null;

        string planXml = null;
        /// <summary>
        /// XML for the running test plan.
        /// </summary>
        [DataMember]
        public string TestPlanXml
        {
            get
            {
                if (planXml != null)
                    return planXml;
                if (serializePlanTask == null)
                    return null;
                serializePlanTask.Wait();
                return planXml;
            }
            private set
            {
                planXml = value;
            }
        }

        /// <summary>
        /// Name of the running test plan.
        /// </summary>
        [DataMember]
        [MetaData(macroName: "TestPlanName")]
        public string TestPlanName
        {
            get => Parameters[nameof(TestPlanName)].ToString(); 
            private set => Parameters[nameof(TestPlanName)] = value;
        }

        /// <summary>
        /// A property that is set by the TestPlan execution logic to indicate whether the TestPlan failed to start the TestPlan.
        /// </summary>
        [DataMember]
        public bool FailedToStart { get; set; }

        /// <summary>
        /// A cancellationtoken that represents a user abort.
        /// </summary>
        public CancellationToken AbortToken { get { return this.plan.AbortToken; } }

        /// <summary>
        /// The currently running TestPlanRun.
        /// Null when accessed from threads other than the TestPlan thread, or threads started from the TestPlan thread using <see cref="TapThread.Start"/>.
        /// </summary>
        public static TestPlanRun Current => TestPlan.executingPlanRun.LocalValue;
        
        #region Internal Members used by the TestPlan

        internal IList<IResultListener> ResultListeners;

        internal TaskCompletionSource<int> PromptWaitHandle = new TaskCompletionSource<int>();
        /// <summary> delegates for resetting values set during ResourcePromptTasks.</summary>
        internal List<Action> ResourcePromptReset { get; set; }

        /// <summary> Gets if the plan should be aborted on fail verdicts.</summary>
        internal bool AbortOnStepFail { get; } = EngineSettings.Current.AbortTestPlan.HasFlag(EngineSettings.AbortTestPlanType.Step_Fail);
        /// <summary> Gets if the plan should be aborted on error verdicts (normal case).</summary>
        internal bool AbortOnStepError { get; } = EngineSettings.Current.AbortTestPlan.HasFlag(EngineSettings.AbortTestPlanType.Step_Error);

        internal readonly IResourceManager ResourceManager;
        internal List<ITestPlanExecutionHook> ExecutionHooks;

        internal readonly bool IsCompositeRun;

        #region Result propagation dispatcher system


        

        bool isBusy()
        {
            foreach(var worker in resultWorkers.Values)
            {
                if (worker.QueueSize > 0) return true;
            }
            return false;
        }

        readonly Dictionary<IResultListener, WorkQueue> resultWorkers;
        
        double resultLatencyLimit = EngineSettings.Current.ResultLatencyLimit;
        object workThrottleLock = new object();
        /// <summary> Wait for result queues to become processed if there is too much work in the buffer. The max workload size for any ResultListener is specified by resultLatencyLimit in seconds. </summary>
        internal void ThrottleResultPropagation()
        {

            lock (workThrottleLock)
            {
                foreach (var worker in resultWorkers)
                {
                    var avg = worker.Value.AverageTimeSpent;
                    var estimatedDelay = new TimeSpan(avg.Ticks * worker.Value.QueueSize).TotalSeconds;
                    bool printedMessage = false;
                    if (estimatedDelay > resultLatencyLimit)
                    {
                        var sw = Stopwatch.StartNew();
                        while (worker.Value.QueueSize > 1)
                        {
                            if (!printedMessage && sw.Elapsed.TotalMilliseconds > 100)
                            {
                                printedMessage = true;
                                resultLog.Warning("Estimated processing time for result queue reached {0:0.0}s (Limit is set to {1}s in Engine.xml). Waiting for queue to be processed by {2}...", estimatedDelay, resultLatencyLimit, worker.Key);
                            }
                            Thread.Sleep(20);

                        }
                        var elapsed = sw.Elapsed;
                        if (elapsed.TotalMilliseconds > 50)
                            resultLog.Debug(elapsed, "Waited for result processing for {0}", worker.Key);


                    }
                }
            }
        }
        
        /// <summary>
        /// Waits for result propagation thread to be idle.
        /// </summary>
        public void WaitForResults()
        {
            while (isBusy())
            {
                Thread.Sleep(1);
            }
        }

        internal void WaitForResultListener(IResultListener rl)
        {
            resultWorkers[rl].Wait();
        }

        #endregion

        /// <summary>
        /// List of all TestSteps for which PrePlanRun has already been called.
        /// </summary>
        internal List<ITestStep> StepsWithPrePlanRun = new List<ITestStep>();

        private string GetHash(byte[] testPlanXml)
        {
            using (var algo = System.Security.Cryptography.SHA1.Create())
                return BitConverter.ToString(algo.ComputeHash(testPlanXml),0,8).Replace("-",string.Empty);
        }

        Task serializePlanTask;
        
        internal void AddTestPlanCompleted(HybridStream logStream, bool openCompleted)
        {
            ScheduleInResultProcessingThread<IResultListener>(r => 
            {
                var reslog = ResourceTaskManager.GetLogSource(r);
                if (r.IsConnected)
                {
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        using (var logView = logStream.GetViewStream())
                        {
                            using (TimeoutOperation.Create(() => log.Info("Waiting for OnTestPlanRunCompleted for {0}.", r)))
                                r.OnTestPlanRunCompleted(this, logView);
                        }
                        reslog.Debug(sw, "OnTestPlanRunCompleted for {0}.", r);
                    }
                    catch (Exception ex)
                    {
                        reslog.Error("Error in OnTestPlanRunCompleted for '{0}': '{1}'", r, ex.Message);
                        reslog.Debug(ex);
                    }
                }
                else
                {
                    if (!openCompleted)
                        reslog.Warning("Run Completed was not called for '{0}' as it failed to open.", r);
                }
            });

            // now clean up the result listener workers and wait for them to end.
            foreach(var kw in resultWorkers)
            {
                kw.Value.Dispose();
            }

            foreach (var kw in resultWorkers)
            {
                var sw = Stopwatch.StartNew();
                using (TimeoutOperation.Create(() => log.Info("Waiting for result propagation for {0}", kw.Key)))
                {
                    kw.Value.Wait();
                }
                if (TestPlan.Aborted)
                    UpgradeVerdict(Verdict.Aborted);
            }
        }

        internal TestPlanRun(TestPlanRun original, DateTime startTime, long startTimeStamp)
        {
            this.FailedToStart = false;
            resultWorkers = original.resultWorkers;
            
            this.Parameters = original.Parameters; // set Parameters before setting Verdict.
            this.Verdict = Verdict.NotSet;
            this.ResultListeners = original.ResultListeners;
            foreach(var resultListener in ResultListeners)
            {
                resultWorkers[resultListener] = new WorkQueue(WorkQueue.Options.LongRunning | WorkQueue.Options.TimeAveraging, resultListener);
            }
            
            this.ResourceManager = original.ResourceManager;
            this.StartTime = startTime;
            this.StartTimeStamp = startTimeStamp;
            this.IsCompositeRun = original.IsCompositeRun;
            Id = original.Id;
            serializePlanTask = original.serializePlanTask;
            TestPlanXml = original.TestPlanXml;
            TestPlanName = original.TestPlanName;
            this.plan = original.plan;
        }

        /// <summary>
        /// Returns the number of threads queued.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="r"></param>
        /// <returns></returns>
        internal int ScheduleInResultProcessingThread<T>(Action<T> r)
        {
            int count = 0;
            foreach (var item in resultWorkers)
            {
                if (item.Key is T x)
                {
                    count++;
                    item.Value.EnqueueWork(() => r(x));
                }
            }
            return count;
        }

        /// <summary>
        /// like ScheduleInResultProcessingThread, but if blocking is used, it will block until the work is finished for all result listeners.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="r"></param>
        /// <param name="blocking"></param>
        /// <returns></returns>
        internal int ScheduleInResultProcessingThread<T>(Action<T> r, bool blocking = false)
        {
            if (!blocking) { 
                return ScheduleInResultProcessingThread(r);
            }
            if (resultWorkers.Count == 0) return 0;
            
            SemaphoreSlim sem = new SemaphoreSlim(0, resultWorkers.Count);
            int count = ScheduleInResultProcessingThread<T>(l =>
            {
                try
                {
                    r(l);
                }
                finally
                {
                    sem.Release();
                }
            });

            for (int i = 0; i < count; i++)
                sem.Wait();
            sem.Dispose();
            return count;
        }

        internal void AddTestStepRunStart(TestStepRun stepRun)
        {
            var instant = Stopwatch.GetTimestamp();
            // Create a clone, because running the test step may change the
            // values inside the active instance of TestStepRun.
            // TODO: 9.0 make TestStepRun a clone.
            var clone = stepRun; //not actually a clone yet. //stepRun.Clone();

            ScheduleInResultProcessingThread<IResultListener>(listener =>
            {
                try
                {
                    listener.OnTestStepRunStart(clone);

                    if (listener is IExecutionListener ex)
                        ex.OnTestStepExecutionChanged(stepRun.TestStepId, stepRun, StepState.Running, instant);
                }
                catch (Exception e)
                {
                    log.Error("Error during Test Step Run Start for {0}", listener);
                    log.Debug(e);
                    RemoveFaultyResultListener(listener);
                }
            });
        }

        internal void AddTestStepStateUpdate(Guid stepID, TestStepRun stepRun, StepState state)
        {
            var instant = Stopwatch.GetTimestamp();

            ScheduleInResultProcessingThread<IExecutionListener>(listener =>
            {
                try
                {
                    listener.OnTestStepExecutionChanged(stepID, stepRun, state, instant);
                }
                catch (Exception e)
                {
                    log.Error("Error at {1} event for {0}", listener, state);
                    log.Debug(e);
                    RemoveFaultyResultListener(listener);
                }
            });
        }

        internal void AddTestStepRunCompleted(TestStepRun stepRun)
        {
            var instant = Stopwatch.GetTimestamp();

            ScheduleInResultProcessingThread<IResultListener>(listener =>
            {
                try
                {
                    listener.OnTestStepRunCompleted(stepRun);

                    if (listener is IExecutionListener ex)
                        ex.OnTestStepExecutionChanged(stepRun.TestStepId, stepRun, StepState.Idle, instant);
                }
                catch (Exception e)
                {
                    log.Error("Error at Step Run Completed for {0}", listener);
                    log.Debug(e);
                    RemoveFaultyResultListener(listener);
                }
            });
        }

        internal void RemoveFaultyResultListener(IResultListener resultListener)
        {
            try
            {
                resultListener.Close();
            }
            catch (Exception)
            {

            }
            ResultListeners.Remove(resultListener);
            log.Warning("Removing faulty ResultListener '{0}'", resultListener);
        }

        #endregion

        /// <summary>
        /// Starts tasks to open resources. All referenced instruments and duts as well as supplied resultListeners to the plan.
        /// </summary>
        /// <param name="plan">Property Plan</param>
        /// <param name="resultListeners">The ResultListeners for this test plan run.</param>
        /// <param name="startTime">Property StartTime.</param>
        /// <param name="startTimeStamp"></param>
        /// <param name="isCompositeRun"></param>
        public TestPlanRun(TestPlan plan, IList<IResultListener> resultListeners, DateTime startTime, long startTimeStamp, bool isCompositeRun = false)
            : this(plan, resultListeners, startTime, startTimeStamp, testPlanXml: null, isCompositeRun: isCompositeRun)
        {

        }
        
        /// <summary>
        /// Starts tasks to open resources. All referenced instruments and duts as well as supplied resultListeners to the plan.
        /// </summary>
        /// <param name="plan">Property Plan</param>
        /// <param name="resultListeners">The ResultListeners for this test plan run.</param>
        /// <param name="startTime">Property StartTime.</param>
        /// <param name="startTimeStamp"></param>
        /// <param name="isCompositeRun"></param>
        /// <param name="testPlanXml">Predefined test plan XML. Allowed to be null.</param>
        public TestPlanRun(TestPlan plan, IList<IResultListener> resultListeners, DateTime startTime, long startTimeStamp, string testPlanXml, bool isCompositeRun = false)
        {
            if (plan == null)
            {
                throw new ArgumentNullException("plan");
            }
            this.FailedToStart = false;
            resultWorkers = new Dictionary<IResultListener, WorkQueue>();
            this.IsCompositeRun = isCompositeRun;
            Parameters = ResultParameters.GetComponentSettingsMetadata();
            // Add metadata from the plan itself.
            Parameters.AddRange(ResultParameters.GetMetadataFromObject(plan));


            this.Verdict = Verdict.NotSet; // set Parameters before setting Verdict.
            ResultListeners = resultListeners ?? Array.Empty<IResultListener>();

            foreach (var res in ResultListeners)
            {
                resultWorkers[res] = new WorkQueue(WorkQueue.Options.LongRunning | WorkQueue.Options.TimeAveraging, res);
            }

            if (EngineSettings.Current.ResourceManagerType == null)
                ResourceManager = new ResourceTaskManager();
            else
                ResourceManager = (IResourceManager)EngineSettings.Current.ResourceManagerType.GetType().CreateInstance();

            StartTime = startTime;
            StartTimeStamp = startTimeStamp;

            serializePlanTask = Task.Factory.StartNew(() =>
            {
                if (testPlanXml != null)
                {
                    TestPlanXml = testPlanXml;
                    Parameters.Add(new ResultParameter("Test Plan", "Hash", GetHash(Encoding.UTF8.GetBytes(testPlanXml)), new MetaDataAttribute(), 0));
                    return;
                }
                using (var memstr = new MemoryStream(128))
                {
                    try
                    {
                        plan.Save(memstr);
                        var testPlanBytes = memstr.ToArray();
                        TestPlanXml = Encoding.UTF8.GetString(testPlanBytes);
                        Parameters.Add(new ResultParameter("Test Plan", "Hash", GetHash(testPlanBytes), new MetaDataAttribute(), 0));
                    }
                    catch (Exception e)
                    {
                        log.Warning("Unable to XML serialize test plan.");
                        log.Debug(e);
                    }
                }
            });
            // waits for prompt before loading the parameters.
            ResourceManager.ResourceOpened += res =>
            {
                Parameters.AddRange(ResultParameters.GetMetadataFromObject(res));
            };
            TestPlanName = plan.Name;
            this.plan = plan;
        }

        /// <summary> Request to abort the test plan represented by this. </summary>
        public void RequestAbort()
        {
            this.plan.RequestAbort();
        }
        
        /// <summary> Abort the test plan run due to a specified reason. </summary>
        /// <param name="reason"></param>
        public void RequestAbort(string reason)
        {
            this.plan.RequestAbort(reason);
        }
        
        /// <summary>
        /// Waits for the given resources to become opened.
        /// </summary>
        /// <param name="cancel"></param>
        /// <param name="resources"></param>
        public void WaitForResourcesOpened(CancellationToken cancel, params IResource[] resources)
        {
            ResourceManager.WaitUntilResourcesOpened(cancel, resources);
        }
    }
}
