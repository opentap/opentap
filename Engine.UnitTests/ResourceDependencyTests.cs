//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;
using OpenTap.EngineUnitTestUtils;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class ResourceDependencyTests
    {
        public interface ICircDummyInst : IInstrument
        {
            void DoTest();
        }

        public class CircDummyInst : Instrument, ICircDummyInst
        {
            public Enabled<IInstrument> inst { get; set; }

            [ResourceOpen(ResourceOpenBehavior.InParallel)]
            public Enabled<IInstrument> instWeak { get; set; }

            public CircDummyInst()
            {
                inst = new Enabled<IInstrument>();
                instWeak = new Enabled<IInstrument>();
            }

            public void DoTest()
            {
                if (inst.IsEnabled) Assert.IsTrue(inst.Value.IsConnected);
                if (instWeak.IsEnabled) Assert.IsTrue(instWeak.Value.IsConnected);
            }

            public override void Open()
            {
                base.Open();
                if (inst.IsEnabled) Assert.IsTrue(inst.Value.IsConnected);
            }

            public override void Close()
            {
                if (inst.IsEnabled) Assert.IsTrue(inst.Value.IsConnected);
                base.Close();
            }
        }

        public class CircTestStep : TestStep
        {
            public ICircDummyInst Instrument { get; set; }

            public override void Run()
            {
                Instrument.DoTest();
            }
        }

        public class CircInst : Instrument, ICircDummyInst
        {
            public bool Parallel { get; set; }
            [EnabledIf(nameof(Parallel), false)] 
            public IInstrument Instrument => inst;


            [EnabledIf(nameof(Parallel), true)]
            [ResourceOpen(ResourceOpenBehavior.InParallel)]
            public IInstrument Instrumentp => inst;

            public IInstrument inst;
            
            public void DoTest()
            {
                Assert.IsTrue(inst.IsConnected);
            }

            public override void Open()
            {
                base.Open();
                if(!Parallel)
                    Assert.IsTrue(inst.IsConnected);
            }

            public override void Close()
            {
                if(!Parallel)
                    Assert.IsTrue(inst.IsConnected);
                base.Close();
            }
        }

        // when the instruments are opened in parallel, the circular resource reference should not be a problem
        // when the instruments are not opened in the parallel, it means that the order of open/close cannot be determined.
        [TestCase(true)]
        [TestCase(false)]
        public void CircularResourceReference(bool parallel)
        {
            EngineSettings.Current.ResourceManagerType = new ResourceTaskManager();
            InstrumentSettings.Current.Clear();
            try
            {
                var inst0 = new CircInst {Parallel = parallel};
                var inst1 = new CircInst {Parallel = parallel};
                var inst2 = new CircInst {Parallel = parallel};

                inst0.inst = inst2;
                inst1.inst = inst0;
                inst2.inst = inst1;
                
                InstrumentSettings.Current.AddRange([inst0, inst1, inst2]);

                TestPlan plan = new TestPlan();
                var step1 = new CircTestStep() { Instrument = inst0 };

                plan.ChildTestSteps.Add(step1);

                var planRun = plan.Execute();

                Assert.AreEqual(parallel ? Verdict.NotSet : Verdict.Error, planRun.Verdict);
            }
            finally
            {
                InstrumentSettings.Current.Clear();
            }
        }

        [Test]
        public void LostReferenceStep()
        {
            using (Session.Create(SessionOptions.OverlayComponentSettings))
            {
                var inst1 = new CircInst();
                var inst2 = new DummyInstrument();
                inst1.inst = inst2;

                InstrumentSettings.Current.Clear();
                InstrumentSettings.Current.Add(inst1);
                InstrumentSettings.Current.Add(inst2);
                
                TestPlan plan = new TestPlan();
                var step1 = new CircTestStep {Instrument = inst1};
                plan.ChildTestSteps.Add(step1);
                {
                    var planRun = plan.Execute();
                    Assert.AreEqual(Verdict.NotSet, actual: planRun.Verdict);
                }
                InstrumentSettings.Current.Remove(inst2);
                {
                    // this should now fail because a inner reference resource has been removed.
                    var planRun = plan.Execute();
                    var ex = planRun.Exception;
                    Assert.AreEqual(Verdict.Error, actual: planRun.Verdict);
                    Assert.IsNotNull(ex);
                    Assert.IsFalse(ex is NullReferenceException);
                }
            }
        }

        [Test]
        public void CircularResource2Reference()
        {
            EngineSettings.Current.ResourceManagerType = new ResourceTaskManager();
            InstrumentSettings.Current.Clear();
            try
            {
                var inst0 = new CircInst();
                InstrumentSettings.Current.Add(inst0);
                inst0.inst = InstrumentSettings.Current[0];

                TestPlan plan = new TestPlan();
                var step1 = new CircTestStep() { Instrument = inst0 };

                plan.ChildTestSteps.Add(step1);

                var planRun = plan.Execute();
                Assert.AreEqual(Verdict.Error, planRun.Verdict);
            }
            finally
            {
                InstrumentSettings.Current.Clear();
            }
        }

        [Test]
        public void DependentResourceNull()
        {
            EngineSettings.Current.ResourceManagerType = new ResourceTaskManager();
            InstrumentSettings.Current.Clear();
            try
            {
                var inst0 = new CircInst();
                InstrumentSettings.Current.Add(inst0);
                InstrumentSettings.Current.Add(new CircInst { inst = null });
                InstrumentSettings.Current.Add(new CircInst { inst = InstrumentSettings.Current[1] });
                inst0.inst = InstrumentSettings.Current[2];

                TestPlan plan = new TestPlan();
                var step1 = new CircTestStep() { Instrument = inst0 };

                plan.ChildTestSteps.Add(step1);

                var planRun = plan.Execute();
                Assert.IsTrue(planRun.FailedToStart);
                Assert.AreEqual(Verdict.Error, planRun.Verdict);
            }
            finally
            {
                InstrumentSettings.Current.Clear();
            }
        }

        [Test]
        public void DependentEnabledTest()
        {
            InstrumentSettings.Current.Clear();
            try
            {
                var inst0 = new CircDummyInst();
                var inst1 = new CircDummyInst();
                var inst2 = new CircDummyInst();

                inst0.instWeak.Value = inst1;
                inst0.instWeak.IsEnabled = true;

                inst0.inst.Value = inst2;
                inst0.inst.IsEnabled = true;

                inst1.instWeak.Value = inst2;
                inst1.instWeak.IsEnabled = true;

                InstrumentSettings.Current.Add(inst0);
                InstrumentSettings.Current.Add(inst1);
                InstrumentSettings.Current.Add(inst2);

                var listener = new EngineUnitTestUtils.TestTraceListener();
                Log.AddListener(listener);

                TestPlan plan = new TestPlan();
                plan.ChildTestSteps.Add(new CircTestStep() { Instrument = inst0 });
                plan.ChildTestSteps.Add(new CircTestStep() { Instrument = inst1 });
                plan.ChildTestSteps.Add(new CircTestStep() { Instrument = inst2 });

                var planRun = plan.Execute();

                Log.RemoveListener(listener);

                Assert.IsFalse(planRun.FailedToStart, listener.GetLog());
                Assert.AreEqual(Verdict.NotSet, planRun.Verdict);
            }
            finally
            {
                InstrumentSettings.Current.Clear();
            }
        }

        public class ResourceStep : TestStep, IResource
        {
            public IResource Inst { get; set; }

            public bool IsConnected { get; private set; }

            public void Close()
            {
                Assert.IsTrue(Inst.IsConnected);

                IsConnected = false;
            }

            public void Open()
            {
                Assert.IsTrue(Inst.IsConnected);

                IsConnected = true;
            }

            public override void Run()
            {
                Assert.IsTrue(Inst.IsConnected);
                Assert.IsTrue(IsConnected);
            }
        }

        [Test]
        public void ResourceStepTest()
        {
            using (Session.Create(SessionOptions.OverlayComponentSettings))
            {
                InstrumentSettings.Current.Add(new CircDummyInst());

                var tp = new TestPlan();
                tp.ChildTestSteps.Add(new ResourceStep() {Inst = InstrumentSettings.Current[0]});

                var run = tp.Execute();

                Assert.AreEqual(1, run.StepsWithPrePlanRun.Count);
                Assert.IsFalse(run.FailedToStart);
            }
        }

        [Test]
        public void ResourceStepRefTest()
        {
            using (Session.Create(0))
            {
                InstrumentSettings.Current.Add(new CircDummyInst());

                var step1 = new ResourceStep() {Inst = InstrumentSettings.Current[0]};
                var step2 = new ResourceStep() {Inst = step1};

                var tp = new TestPlan();
                tp.ChildTestSteps.Add(step1);
                tp.ChildTestSteps.Add(step2);

                var run = tp.Execute();

                Assert.AreEqual(2, run.StepsWithPrePlanRun.Count);
                Assert.IsFalse(run.FailedToStart);
            }
        }

        public class IgnoredResourceStep : TestStep
        {
            [ResourceOpen(ResourceOpenBehavior.Ignore)]
            public IInstrument resource { get; set; }

            public override void Run()
            {
                UpgradeVerdict(resource.IsConnected ? Verdict.Fail : Verdict.Pass);
            }
        }

        [Test]
        public void IgnoredResource()
        {
            InstrumentSettings.Current.Clear();
            ResultSettings.Current.Clear();

            var inst = new CircDummyInst();
            InstrumentSettings.Current.Add(inst);
            var listener = new EngineUnitTestUtils.TestTraceListener();
            Log.AddListener(listener);
            var tp = new TestPlan();
            tp.ChildTestSteps.Add(new IgnoredResourceStep() { resource = inst });
            var run = tp.Execute();
            Log.RemoveListener(listener);

            Assert.IsFalse(run.FailedToStart);
            Assert.AreEqual(1, run.StepsWithPrePlanRun.Count);
            Assert.AreEqual(Verdict.Pass, run.Verdict, listener.GetLog());
        }
        
		[Test]
        public void TestMethodResourceSettingNullReference()
        {
            TestTraceListener tapTraceListener = new TestTraceListener();
            DummyDut device = new DummyDut();
            try
            {
                Log.AddListener(tapTraceListener);
                DutSettings.Current.Add(device);

                // this works
                var testPlan = new TestPlan();
                var step = new DummyInstrumentAndDutStep() { Device = device, Instrument = null };
                testPlan.ChildTestSteps.Add(step);
                var run = testPlan.Execute();
                
                Assert.AreEqual(Verdict.Error, run.Verdict);
                tapTraceListener.ExpectWarnings("TestPlan aborted.");
                tapTraceListener.ExpectErrors(
                    $"Resource setting {nameof(DummyInstrumentAndDutStep.Instrument)} not set on step {nameof(DummyInstrumentAndDutStep)}. Please configure or disable step.");
            }
            finally
            {
                tapTraceListener.Flush();
                Log.RemoveListener(tapTraceListener);
                DutSettings.Current.Remove(device);
            }
        }
        
        public class DummyInstrumentAndDutStep : TestStep
        {
            public DummyDut Device { get; set; }
            public DummyInstrument Instrument { get; set; }

            public override void Run()
            {
            }
        }
		
        public class SelfHiddenCyclicResource : Instrument
        {
            public object Self => this;
        }

        public class SelfHiddenCyclicResourceTestStep : TestStep
        {
            public SelfHiddenCyclicResource Resource { get; set; }

            public override void Run()
            {
                UpgradeVerdict(Verdict.Pass);
            }
        }

        [Test]
        public void SelfHiddenCyclicResourceTest()
        {
            var step = new SelfHiddenCyclicResourceTestStep
                {Resource = new SelfHiddenCyclicResource()};
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(step);
            var run = plan.Execute();
            Assert.AreEqual(Verdict.Pass, run.Verdict);
        }

    }
}
