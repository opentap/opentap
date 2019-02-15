//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Plugins.BasicSteps;
using NUnit.Framework;
using OpenTap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.IO;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class SweepStepTest
    {

        public class IsOpenedDut : Dut
        {
            public bool IsOpened = false;
            public bool IsClosed = false;
            public bool IsUsed = false;
            public override void Open()
            {
                base.Open();
                IsOpened = true;
            }
            public override void Close()
            {
                base.Close();
                IsClosed = true;
            }
            public void Use()
            {
                IsUsed = true;
            }
        }

        public class IsOpenUsedTestStep : TestStep
        {
            public IsOpenedDut Resource { get; set; }

            public override void Run()
            {
                Assert.IsTrue(Resource.IsConnected);
                Resource.Use();
            }
        }

        /// <summary>
        /// Tests that instruments/resources are opened when indirectly referenced from SweepLoop.
        /// </summary>
        [Test]
        public void TestResourceReference()
        {
            // Loop seems to provoke a race condition in test plan execution
            for (int i = 0; i < 10; i++)
            {
                EngineSettings.Current.ToString();
                var step = new OpenTap.Plugins.BasicSteps.SweepLoop();

                var theDuts = Enumerable.Range(0, 10).Select(number => new IsOpenedDut()).ToArray();

                step.ChildTestSteps.Add(new IsOpenUsedTestStep() { Resource = new IsOpenedDut() });
                step.SweepParameters.Add(new OpenTap.Plugins.BasicSteps.SweepParam(new PropertyInfo[] { typeof(IsOpenUsedTestStep).GetProperty("Resource") }, theDuts));
                var plan = new TestPlan();
                plan.PrintTestPlanRunSummary = true;
                plan.ChildTestSteps.Add(step);
                var rlistener = new PlanRunCollectorListener();
                var planRun = plan.Execute(new IResultListener[] { rlistener });
                Assert.AreEqual(theDuts.Length + 1, rlistener.StepRuns.Count);
                Assert.IsTrue(planRun.Verdict == Verdict.NotSet);
                Assert.IsTrue(theDuts.All(dut => dut.IsClosed && dut.IsOpened && dut.IsUsed));
            }
        }

        public class SweepTestStep : TestStep
        {
            [Display(" abc ")]
            public int SweepProp { get; set; }

            public static int Value = 0;

            public override void PrePlanRun()
            {
                Value = 0;
            }

            public override void Run()
            {
                Value += SweepProp;
                Results.Publish("ABC", new List<string> { "abc" }, SweepProp);
            }
        }

        [Test]
        public void RunSweep()
        {
            var tp = new TestPlan();

            var sl = new SweepLoop();
            var ds = new SweepTestStep();

            sl.ChildTestSteps.Add(ds);
            sl.SweepParameters.Add(new SweepParam(new[] { ds.GetType().GetProperty("SweepProp") }, 2, 5));

            tp.ChildTestSteps.Add(sl);

            using (var st = new System.IO.MemoryStream())
            {
                tp.Save(st);
                st.Seek(0, 0);
                tp = TestPlan.Load(st, tp.Path);
            }

            var pr = tp.Execute();

            Assert.IsFalse(pr.FailedToStart);
            Assert.AreEqual(7, SweepTestStep.Value);
        }

        [Test]
        public void RunSweepRangeWithInterruptions()
        {
            RunSweepWithInterruptions(true);
            RunSweepWithInterruptions(false);
        }

        public void RunSweepWithInterruptions(bool loopRange)
        {
            IEnumerable<int> check;
            var tp = new TestPlan();
            TestStep sweep;
            if (loopRange)
            {
                var sweepRange = new SweepLoopRange();
                sweepRange.SweepStart = 10;
                sweepRange.SweepEnd = 30;
                sweepRange.SweepStep = 1;
                sweepRange.SweepProperties = new List<PropertyInfo>() { typeof(SweepTestStep).GetProperty("SweepProp") };
                sweep = sweepRange;
                check = Enumerable.Range(10, (int)(sweepRange.SweepEnd - sweepRange.SweepStart + 1));
            }
            else
            {
                check = Enumerable.Range(10, 20);
                var sweepRange = new SweepLoop();
                var lst = new List<SweepParam>();
                lst.Add(new SweepParam(new[] { typeof(SweepTestStep).GetProperty("SweepProp") }, check.Cast<object>().ToArray()));
                sweepRange.SweepParameters = lst;
                sweep = sweepRange;
            }
            var step = new SweepTestStep();

            tp.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(step);
            
            var rlistener = new PlanRunCollectorListener() { CollectResults = true };
            bool done = false;

            void interruptOperations()
            {
                // this is to reproduce an error previously happening when the
                // SweepLoopRange.Error value was getted.
                // this would have changed the value of SweepProp intermiddently.
                while (!done)
                {
                    // so bother as much as possible...
                    var error2 = sweep.Error;
                }
            }

            var trd = new Thread(interruptOperations);
            trd.Start();
            var result = tp.Execute(new[] { rlistener });
            done = true;
            trd.Join();
            var results = rlistener.Results.Select(x => (int)x.Result.Columns[0].Data.GetValue(0)).ToArray();
            Assert.IsTrue(results.SequenceEqual(check));
        }

        [Test]
        public void RunRepeat()
        {
            // plan:
            //    repeat1 (count 3)
            //       repeat2 (count 3)
            //         setVer  - sets verdict to pass
            //         checkif - breaks repeat 2
            //         setVer2 - is never executed.
            // total number of step runs:
            // repeat1: 1
            // repeat2: 3
            // setVer: 3
            // checkif: 3
            // setVer2: 0
            // Total: 10

            var rlistener = new PlanRunCollectorListener();

            var repeat1 = new RepeatStep { Action = RepeatStep.RepeatStepAction.Fixed_Count, Count = 3 };
            var repeat2 = new RepeatStep { Action = RepeatStep.RepeatStepAction.Fixed_Count, Count = 3 };
            var setVer = new TestTestSteps.VerdictStep() { VerdictOutput = Verdict.Pass };
            var checkif = new IfStep() { Action = IfStep.IfStepAction.BreakLoop, TargetVerdict = setVer.VerdictOutput};
            var setVer2 = new TestTestSteps.VerdictStep(); // this one is never executed.
            repeat2.ChildTestSteps.AddRange(new ITestStep[] { setVer, checkif, setVer2 });
            repeat1.ChildTestSteps.Add(repeat2);
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(repeat1);

            checkif.InputVerdict.Step = setVer;
            checkif.InputVerdict.Property = typeof(TestStep).GetProperty("Verdict");



            var planrun = plan.Execute(new[] { rlistener });
            Assert.AreEqual(10, rlistener.StepRuns.Count);
            Assert.AreEqual(5, planrun.StepsWithPrePlanRun.Count);
        }

        [Test]
        public void TestDecadeRange()
        {
            int count = 0;
            foreach(var value in SweepLoopRange.ExponentialRange(0.1M, 1000, 10))
            {
                Assert.IsTrue(value <= 1000.0M && value >= 0.1M);
                count++;
            }
            Assert.AreEqual(10, count);
        }

        [Test]
        public void RepeatWithReferenceOutsideStep()
        {
            var stream = File.OpenRead("TestTestPlans\\whiletest.TapPlan");
            TestPlan plan = (TestPlan)new TapSerializer().Deserialize(stream, type: typeof(TestPlan));
            var run = plan.Execute();
            Assert.AreEqual(Verdict.Pass, run.Verdict);
        }
    }
}
