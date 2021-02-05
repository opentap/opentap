//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

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
        /// <summary>
        /// This is set if the Executor had Opened called. Otherwise null.
        /// </summary>
        public TestPlanRun currentExecutionState { get; internal set; }
        public bool PrintTestPlanRunSummary { get; internal set; }
    }

    abstract class TestPlanExecutionStageBase : IExecutionStage
    {
        public bool Execute(ExecutionStageContext context)
        {
            return Execute((TestPlanExecutionContext)context);
        }

        protected abstract bool Execute(TestPlanExecutionContext context);
    }
}
