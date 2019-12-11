//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using OpenTap.EngineUnitTestUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    /// <summary>
    /// Test TestStep used along with EvmMeasurementStep to test IResultSink implementation
    /// </summary>
    [Display("EVM Limit Check Step", "Test TestStep used along with EvmMeasurementStep to test IResultSink implementation", Group: "ResultSink Tests")]
    public class EvmLimitCheckStep : TestStep
    {
        public EvmMeasurementStep EvmSourceTestStep
        {
            get => EvmMeasurementValueSink.SourceTestStep as EvmMeasurementStep;
            set => EvmMeasurementValueSink.SourceTestStep = value;
        }

        public double MaxEvm { get; set; } = 1;

        public ScalarResultSink<double> EvmMeasurementValueSink { get; private set; }

        public EvmLimitCheckStep()
        {
            EvmMeasurementValueSink = new ScalarResultSink<double>(this) { ResultColumnName = "EVM" };
        }

        public override void Run()
        {
            Stopwatch timer = Stopwatch.StartNew();
            double measurement = EvmMeasurementValueSink.GetResult(TapThread.Current.AbortToken);
            if (measurement > MaxEvm)
            {
                Log.Info(timer, "Failed as EVM of {0} is greater than limit.", measurement);
                UpgradeVerdict(Verdict.Fail);
            }
            else
            {
                Log.Info(timer, "Passed as EVM of {0} is within the limit.", measurement);
                UpgradeVerdict(Verdict.Pass);
            }
        }
    }

    /// <summary>
    /// Test TestStep used along with EvmLimitCheckStep to test IResultSink implementation
    /// </summary>
    [Display("EVM Measurement Step", "Test TestStep used along with EvmMeasurementStep to test IResultSink implementation", Group: "ResultSink Tests")]
    public class EvmMeasurementStep : TestStep
    {
        static Random rnd = new Random();

        [Unit("dBm")]
        public double Power { get; set; } = 10;

        public override void Run()
        {
            double evm = rnd.NextDouble() + 0.05 * Power;
            Results.Publish("EVM Measurement", new List<string> { "Power", "EVM" }, Power, evm);
            Log.Info("Measured EVM of {0}", evm);
        }
    }

    [TestFixture]
    public class IResultSinkTests
    {
        [TestCase(0, Verdict.Pass)]
        [TestCase(1,Verdict.Fail)]
        public void RunPlanTest(int stepIndex, Verdict expectedVerdict)
        {
            TestPlan plan = new TestPlan();
            plan.Steps.Add(new EvmMeasurementStep() { Power = 0 });
            plan.Steps.Add(new EvmMeasurementStep() { Power = 21 });
            plan.Steps.Add(new EvmMeasurementStep());
            var limitStep = new EvmLimitCheckStep();
            limitStep.EvmSourceTestStep = plan.Steps[stepIndex] as EvmMeasurementStep;
            plan.Steps.Add(limitStep);
            
            TestTraceListener listener = new TestTraceListener();
            Log.AddListener(listener);
            var planRun = plan.Execute(new List<ResultListener>());
            Log.RemoveListener(listener);
            listener.Flush();
            
            listener.AssertErrors();
            Assert.AreEqual(expectedVerdict, planRun.Verdict,listener.GetLog());
        }

        [TestCase(0, Verdict.Pass)]
        [TestCase(1, Verdict.Fail)]
        public void RunPlanWithLoopTest(int stepIndex, Verdict expectedVerdict)
        {
            TestPlan plan = new TestPlan();
            var loop = new Plugins.BasicSteps.RepeatStep() { Count = 3 };
            plan.Steps.Add(loop);
            loop.ChildTestSteps.Add(new EvmMeasurementStep() { Power = 0 });
            loop.ChildTestSteps.Add(new EvmMeasurementStep() { Power = 21 });
            loop.ChildTestSteps.Add(new EvmMeasurementStep());
            loop.ChildTestSteps.Add(new EvmLimitCheckStep() { EvmSourceTestStep = loop.ChildTestSteps[stepIndex] as EvmMeasurementStep });

            TestTraceListener listener = new TestTraceListener();
            Log.AddListener(listener);
            var planRun = plan.Execute(new List<ResultListener>());
            Log.RemoveListener(listener);
            listener.Flush();

            listener.AssertErrors();
            Assert.AreEqual(expectedVerdict, planRun.Verdict, listener.GetLog());
        }

        [TestCase(0, Verdict.Pass)]
        [TestCase(21, Verdict.Fail)]
        public void ResultSinkInParallel(double power, Verdict expectedVerdict)
        {
            var plan = new TestPlan();
            var parallel = new ParallelStep();
            plan.ChildTestSteps.Add(parallel);
            for (int i = 0; i < 10; i++)
            {
                plan.ChildTestSteps.Add(new DelayStep(){DelaySecs =  0.001});
            }

            var result = new EvmMeasurementStep() {Power = power};
            plan.ChildTestSteps.Add(result);
            var limit = new EvmLimitCheckStep() {EvmSourceTestStep = result};
            plan.ChildTestSteps.Add(limit);
            
            var planRun = plan.Execute(new List<ResultListener>());
            Assert.AreEqual(expectedVerdict, planRun.Verdict);
        }
        
    }
}
