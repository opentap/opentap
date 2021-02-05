//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenTap
{
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
}
