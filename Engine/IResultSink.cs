//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;

namespace OpenTap
{
    /// <summary>
    /// Object used to indicate that a TestStep is interested in results from another TestStep. 
    /// A public property of this type should exist on the interested TestStep.
    /// Methods defined in this interface are called on all instances found as properties on TestSteps in the TestPlan.
    /// </summary>
    public interface IResultSink
    {
        /// <summary>
        /// Called when a TestStep publishes results. This is happening in a background thread.
        /// </summary>
        void OnResultPublished(TestStepRun run, ResultTable table);

        /// <summary>
        /// Called when a TestStep is starting. This is happening in a background thread.
        /// </summary>
        void OnTestStepRunStart(TestStepRun stepRun);

        /// <summary>
        /// Called when a TestStep is completed. This is happening in a background thread. ResultPublished will not be called anymore with this TestStepRun after this.
        /// </summary>
        void OnTestStepRunCompleted(TestStepRun stepRun);

        /// <summary>
        /// Called when a TestPlan starts running. This is happening in a background thread.
        /// </summary>
        void OnTestPlanRunStart(TestPlanRun stepRun);

        /// <summary>
        /// Called when a TestPlan run is completed. This is happening in a background thread. 
        /// </summary>
        void OnTestPlanRunCompleted(TestPlanRun stepRun);
    }

    [Browsable(false)]
    internal class ResultSinkListener : ResultListener
    {
        private IEnumerable<IResultSink> currentSinks;
        Dictionary<Guid, TestStepRun> currentStepRuns = new Dictionary<Guid, TestStepRun>();

        public ResultSinkListener(IEnumerable<IResultSink> sinks)
        {
            currentSinks = sinks;
        }

        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            foreach (IResultSink sink in currentSinks)
            {
                try
                {
                    sink.OnTestPlanRunStart(planRun);
                }
                catch(Exception ex)
                {
                    Log.Error($"{TypeData.GetTypeData(sink).Name} caused an error.");
                    Log.Debug(ex);
                }
            }
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream log)
        {
            foreach (IResultSink sink in currentSinks)
            {
                try
                {
                    sink.OnTestPlanRunCompleted(planRun);
                }
                catch (Exception ex)
                {
                    Log.Error($"{TypeData.GetTypeData(sink).Name} caused an error.");
                    Log.Debug(ex);
                }
            }
        }

        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            currentStepRuns.Add(stepRun.Id, stepRun);
            foreach (IResultSink sink in currentSinks)
            {
                try
                {
                    sink.OnTestStepRunStart(stepRun);
                }
                catch (Exception ex)
                {
                    Log.Error($"{TypeData.GetTypeData(sink).Name} caused an error.");
                    Log.Debug(ex);
                }
            }
        }

        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        {
            currentStepRuns.Remove(stepRun.Id);
            foreach (IResultSink sink in currentSinks)
            {
                try
                {
                    sink.OnTestStepRunCompleted(stepRun);
                }
                catch (Exception ex)
                {
                    Log.Error($"{TypeData.GetTypeData(sink).Name} caused an error.");
                    Log.Debug(ex);
                }
            }
        }

        public override void OnResultPublished(Guid stepRunId, ResultTable result)
        {
            foreach (IResultSink sink in currentSinks)
            {
                try
                {
                    sink.OnResultPublished(currentStepRuns[stepRunId], result);
                }
                catch (Exception ex)
                {
                    Log.Error($"{TypeData.GetTypeData(sink).Name} caused an error.");
                    Log.Debug(ex);
                }
            }
        }
    }

    /// <summary>
    /// ResultSink that will provide the first result from a given result column published by a given TestStep.
    /// When SourceTestStep is inside a loop step, only results from the last iteration of SourceTestStep is accessible.
    /// </summary>
    public class ScalarResultSink<T> : IResultSink where T : IConvertible
    {
        private Queue<T> Result;
        private ManualResetEvent ItemsInQueue;

        /// <summary>
        /// The ID of the source TestStep that we are interested in results from.
        /// </summary>
        public ITestStep SourceTestStep { get; set; }

        /// <summary>
        /// The name of the result column to get the result from.
        /// </summary>
        public string ResultColumnName { get; set; }

        private ITestStep ListeningStep;

        /// <summary>
        /// Creates an instance. This should probably be called from the constructor of the TestStep.
        /// </summary>
        /// <example>
        /// <code>
        ///public class ListeningStepExample : TestStep
        ///{
        ///    public ITestStep SourceStep { get => Sink.SourceTestStep; set => Sink.SourceTestStep = value; }
        ///    public ScalarResultSink&lt;double&gt; Sink { get; set; }
        ///
        ///    public ListeningStepExample()
        ///    {
        ///        Sink = new ScalarResultSink&lt;double&gt;(this);
        ///    }
        ///
        ///    public override void Run()
        ///    {
        ///        log.Debug("Result was: {0}", Sink.GetResult(TapThread.Current.AbortToken));
        ///    }
        ///}
        /// </code>
        /// </example>
        /// <param name="listeningStep"></param>
        public ScalarResultSink(ITestStep listeningStep)
        {
            Result = new Queue<T>();
            ListeningStep = listeningStep;
        }

        /// <summary>
        /// Called by TestSteps when the result is needed. Blocks until a result is available.
        /// </summary>
        public T GetResult(CancellationToken ct)
        {
            WaitHandle.WaitAny(new [] { ItemsInQueue, ct.WaitHandle });
            ct.ThrowIfCancellationRequested();
            lock (Result)
            {
                var res = Result.Dequeue();
                if (!Result.Any())
                    ItemsInQueue.Reset();
                return res;
            }
        }

        /// <summary>
        /// Called by TestSteps when the result is needed. Returns true if a result is available.
        /// </summary>
        public bool TryGetResult(out T result)
        {
            lock (Result)
            {
                if (Result.Any())
                {
                    result = Result.Dequeue();
                    if (!Result.Any())
                        ItemsInQueue.Reset();
                    return true;
                }
            }
            result = default(T);
            return false;
        }

        /// <summary>
        /// Resets result collection when a new run of the <see cref="SourceTestStep"/> is started.
        /// </summary>
        public void OnTestStepRunStart(TestStepRun run)
        {
            if (run.TestStepId == SourceTestStep?.Id)
            {
                lock (Result)
                {
                    ItemsInQueue.Reset();
                    Result.Clear();
                }
            }
        }

        /// <summary>
        /// Called when a TestStep is completed.
        /// </summary>
        public void OnTestStepRunCompleted(TestStepRun run) { }

        /// <summary>
        /// Called by OpenTAP when the source TestStep publishes results. This is happening in a background thread.
        /// </summary>
        public void OnResultPublished(TestStepRun stepRun, ResultTable result)
        {
            if (stepRun.TestStepId == SourceTestStep?.Id)
            {
                ResultColumn column = result.Columns.FirstOrDefault(col => col.Name == ResultColumnName);
                if (column != null)
                {
                    lock (Result)
                    {
                        Result.Enqueue(column.GetValue<T>(0));
                        ItemsInQueue.Set();
                    }
                }
            }
        }

        /// <summary> Initializes this instance. </summary>
        public void OnTestPlanRunStart(TestPlanRun run)
        {
            ItemsInQueue = new ManualResetEvent(false);
        }
        
        /// <summary> Cleans up after this instance </summary>
        public void OnTestPlanRunCompleted(TestPlanRun run)
        {
            ItemsInQueue.Dispose();
        }
    }
}
