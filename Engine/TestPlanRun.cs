//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
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
        private static readonly TraceSource log = Log.CreateSource("TestPlan");
        private static readonly TraceSource resultLog = Log.CreateSource("Resources");
        private TestPlan plan = null;

        string planXml = null;

        /// <summary> XML for the running test plan. </summary>
        [DataMember]
        public string TestPlanXml
        {
            get
            {
                if (planXml != null)
                    return planXml;
                WaitForSerialization();
                return planXml;
            }
            private set
            {
                planXml = value;
            }
        }

        /// <summary> Waits for the test plan to be serialized. </summary>
        internal void WaitForSerialization() => serializePlanTask?.Wait(TapThread.Current.AbortToken);

        /// <summary> The SHA1 hash of XML of the test plan.</summary>
        public string Hash => Parameters[nameof(Hash)]?.ToString();

        /// <summary> Name of the running test plan. </summary>
        [DataMember]
        [MetaData(macroName: nameof(TestPlanName))]
        public string TestPlanName
        {
            get => Parameters[nameof(TestPlanName)].ToString(); 
            private set => Parameters[nameof(TestPlanName)] = value;
        }

        /// <summary> Set by the TestPlan execution logic to indicate whether the TestPlan failed to start the TestPlan. </summary>
        [DataMember]
        public bool FailedToStart { get; set; }

        /// <summary> The thread that started the test plan. Use this to abort the plan thread. </summary>
        public TapThread MainThread { get;  }
        
        #region Internal Members used by the TestPlan

        internal IList<IResultListener> ResultListeners;

        /// <summary> Wait handle that is set when the metadata action is completed. </summary>
        internal ManualResetEvent PromptWaitHandle = new ManualResetEvent(false);
        /// <summary> Resources touched by the prompt metadata action. </summary>
        internal IResource[] PromptedResources = Array.Empty<IResource>();

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

            if (resultWorkers.Count == 0) return;
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
            }
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
            if (!blocking)
                return ScheduleInResultProcessingThread(r);
            if (resultWorkers.Count == 0) return 0;
       
            using (var sem = new SemaphoreSlim(0, resultWorkers.Count))
            {
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
                return count;
            }
        }

        internal void AddTestStepRunStart(TestStepRun stepRun)
        {
            var instant = Stopwatch.GetTimestamp();
            // Create a clone, because running the test step may change the
            // values inside the active instance of TestStepRun.
            var clone = stepRun.Clone();

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
        /// Waits for the given resources to become opened.
        /// </summary>
        /// <param name="cancel"></param>
        /// <param name="resources"></param>
        public void WaitForResourcesOpened(CancellationToken cancel, params IResource[] resources)
        {
            ResourceManager.WaitUntilResourcesOpened(cancel, resources);
        }

        #region constructors
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

        class StringHashPair
        {
            public string Xml { get; set; }
            public string Hash { get; set; }
            public byte[] Bytes { get; set; }
        }
        
        /// <summary> Memorizer for storing pairs of Xml and hash. </summary>
        static ConditionalWeakTable<TestPlan, StringHashPair> testPlanHashMemory = new ConditionalWeakTable<TestPlan, StringHashPair>();
        
        /// <summary>
        /// Starts tasks to open resources. All referenced instruments and duts as well as supplied resultListeners to the plan.
        /// </summary>
        /// <param name="plan">Property Plan</param>
        /// <param name="resultListeners">The ResultListeners for this test plan run.</param>
        /// <param name="startTime">Property StartTime.</param>
        /// <param name="startTimeStamp"></param>
        /// <param name="isCompositeRun"></param>
        /// <param name="testPlanXml">Predefined test plan XML. Allowed to be null.</param>
        public TestPlanRun(TestPlan plan, IList<IResultListener> resultListeners, DateTime startTime, long startTimeStamp, string testPlanXml, bool isCompositeRun = false) : this()
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            var breakCondition = BreakConditionProperty.GetBreakCondition(plan);
            if (breakCondition.HasFlag(BreakCondition.Inherit))
            {
                BreakCondition |= breakCondition;
            }
            else
            {
                BreakCondition = breakCondition;
            }
            resultWorkers = new Dictionary<IResultListener, WorkQueue>();
            this.IsCompositeRun = isCompositeRun;
            Parameters = ResultParameters.GetComponentSettingsMetadata();
            // Add metadata from the plan itself.
            Parameters.IncludeMetadataFromObject(plan);

            this.Verdict = Verdict.NotSet; // set Parameters before setting Verdict.
            ResultListeners = resultListeners ?? Array.Empty<IResultListener>();

            foreach (var res in ResultListeners)
            {
                resultWorkers[res] = new WorkQueue(WorkQueue.Options.LongRunning | WorkQueue.Options.TimeAveraging, res.ToString());
            }

            if (EngineSettings.Current.ResourceManagerType == null)
                ResourceManager = new ResourceTaskManager();
            else
                ResourceManager = (IResourceManager)EngineSettings.Current.ResourceManagerType.GetType().CreateInstance();

            StartTime = startTime;
            StartTimeStamp = startTimeStamp;
            this.plan = plan;
            serializePlanTask = Task.Factory.StartNew(() =>
            {
                if (testPlanXml != null)
                {
                    TestPlanXml = testPlanXml;
                    Parameters.Add("Test Plan", nameof(Hash), GetHash(Encoding.UTF8.GetBytes(testPlanXml)), new MetaDataAttribute());
                    return;
                }

                if (plan.GetCachedXml() is byte[] xml)
                {
                    
                    if(!testPlanHashMemory.TryGetValue(this.plan, out var pair))
                    {
                        if (pair == null)
                        {
                            pair = new StringHashPair();
                            testPlanHashMemory.Add(plan, pair);    
                        }
                    }

                    
                    if (Equals(pair.Bytes, xml) == false)
                    {
                        pair.Xml = Encoding.UTF8.GetString(xml);
                        pair.Hash = GetHash(xml);
                        pair.Bytes = xml;
                    }
                    else
                        TestPlanXml = pair.Xml;

                    Parameters.Add("Test Plan", nameof(Hash), pair.Hash, new MetaDataAttribute());
                    return;
                }

                using (var memstr = new MemoryStream(128))
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        plan.Save(memstr);
                        var testPlanBytes = memstr.ToArray();
                        TestPlanXml = Encoding.UTF8.GetString(testPlanBytes);
                        
                        Parameters.Add(new ResultParameter("Test Plan", nameof(Hash), GetHash(testPlanBytes),
                            new MetaDataAttribute(), 0));
                    }
                    catch (Exception e)
                    {
                        log.Warning("Unable to XML serialize test plan.");
                        log.Debug(e);
                    }
                    finally
                    {
                        log.Debug(sw, "Saved Test Plan XML");
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

        internal TestPlanRun() 
        {
            MainThread = TapThread.Current;
            FailedToStart = false;
            
            {   // AbortCondition
                
                var abort2 = EngineSettings.Current.AbortTestPlan;

                if (abort2.HasFlag(EngineSettings.AbortTestPlanType.Step_Error))
                {
                    if (abort2.HasFlag(EngineSettings.AbortTestPlanType.Step_Fail))
                    {
                        BreakCondition = BreakCondition.BreakOnError | BreakCondition.BreakOnFail;
                    }
                    else
                    {
                        BreakCondition = BreakCondition.BreakOnError;
                    }
                }
                else if (abort2.HasFlag(EngineSettings.AbortTestPlanType.Step_Fail))
                {
                    BreakCondition = BreakCondition.BreakOnFail;
                }
            }
        }
        
        internal TestPlanRun(TestPlanRun original, DateTime startTime, long startTimeStamp) : this()
        {
            resultWorkers = original.resultWorkers;

            this.Parameters = original.Parameters; // set Parameters before setting Verdict.
            this.Verdict = Verdict.NotSet;
            this.ResultListeners = original.ResultListeners;
            foreach (var resultListener in ResultListeners)
            {
                resultWorkers[resultListener] = new WorkQueue(WorkQueue.Options.LongRunning | WorkQueue.Options.TimeAveraging, resultListener.ToString());
            }

            this.ResourceManager = original.ResourceManager;
            this.StartTime = startTime;
            this.StartTimeStamp = startTimeStamp;
            this.IsCompositeRun = original.IsCompositeRun;
            Id = original.Id;
            this.plan = original.plan;
            serializePlanTask = Task.Factory.StartNew(() =>
            {
                using (var memstr = new MemoryStream(128))
                {
                    try
                    {
                        plan.Save(memstr);
                        var testPlanBytes = memstr.ToArray();
                        TestPlanXml = Encoding.UTF8.GetString(testPlanBytes);
                        Parameters.Add(new ResultParameter("Test Plan", nameof(Hash), GetHash(testPlanBytes), new MetaDataAttribute(), 0));
                    }
                    catch (Exception e)
                    {
                        log.Warning("Unable to XML serialize test plan.");
                        log.Debug(e);
                    }
                }
            });
            TestPlanName = original.TestPlanName;
        }
        #endregion
    }
}
