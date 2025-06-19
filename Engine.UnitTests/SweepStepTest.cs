//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Plugins.BasicSteps;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
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

            [ResourceOpen(ResourceOpenBehavior.Ignore)]
            public IsOpenedDut Resource2 { get; set; }
            
            [ResourceOpen(ResourceOpenBehavior.Ignore)]
            public IsOpenedDut[] Resource3 { get; set; } 

            public override void Run()
            {
                Assert.IsTrue(Resource.IsConnected);
                if(Resource2 != Resource)
                    Assert.IsFalse(Resource2.IsConnected);
                foreach(var res in Resource3)
                    if(res != Resource)
                        Assert.IsFalse(res.IsConnected);
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
                var otherdut = new IsOpenedDut();

                step.ChildTestSteps.Add(new IsOpenUsedTestStep() { Resource = new IsOpenedDut(), Resource2 = otherdut, Resource3 = new []{new IsOpenedDut() }});
                step.SweepParameters.Add(new OpenTap.Plugins.BasicSteps.SweepParam(new IMemberData[] { TypeData.FromType(typeof(IsOpenUsedTestStep)).GetMember("Resource") }, theDuts));
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

            public int Value { get; private set; }


            public override void Run()
            {
                Value += SweepProp;
                Results.Publish("ABC", new List<string> { "abc" }, SweepProp);
            }
        }

        [Test]
        [Pairwise]
        public void RunSweep([Values(true,false)] bool acrossRuns, [Values(true, false)]bool allEnabled)
        {
            var tp = new TestPlan();

            var sl = new SweepLoop()
            {
                CrossPlan = acrossRuns ? SweepLoop.SweepBehaviour.Across_Runs : SweepLoop.SweepBehaviour.Within_Run
            };
            
            var ds = new SweepTestStep();

            sl.ChildTestSteps.Add(ds);
            sl.SweepParameters.Add(new SweepParam(new[] { TypeData.GetTypeData(ds).GetMember("SweepProp") }, 2,3,5,7));
            sl.EnabledRows = new bool[] { true, allEnabled, true, true };

            tp.ChildTestSteps.Add(sl);

            using (var st = new MemoryStream())
            {
                tp.Save(st);
                st.Seek(0, 0);
                tp = TestPlan.Load(st, tp.Path);
            }
            ds = tp.ChildTestSteps[0].ChildTestSteps[0] as SweepTestStep;
            if (acrossRuns)
            {
                foreach(var rowEnabled in sl.EnabledRows)
                {
                    if (rowEnabled)
                    {
                        var pr = tp.Execute();
                        Assert.IsFalse(pr.FailedToStart);
                        Assert.AreEqual(Verdict.NotSet, pr.Verdict);
                    }
                }
            }
            else
            {
                var pr = tp.Execute();
                Assert.IsFalse(pr.FailedToStart);
            }
            if(allEnabled)
                Assert.AreEqual(17, ds.Value);
            else
                Assert.AreEqual(14, ds.Value);
        }

        [Test]
        public void SweepPropertiesUpdateTest()
        {
            var savestream = new MemoryStream();
            
            var tp = new TestPlan();
            {
                var range = new SweepLoopRange()
                {
                    SweepPoints = 3,
                    SweepStart = 1,
                    SweepEnd = 2
                };

                tp.ChildTestSteps.Add(range);
                tp.Save(savestream);
            }

            savestream.Seek(0, SeekOrigin.Begin);
            
            // Verify that the removal event is triggered both on new and serialized sweep ranges
            var plans = new[] { tp, TestPlan.Load(savestream, Path.GetTempFileName()) };

            foreach (var plan in plans)
            {
                var sweep = plan.ChildTestSteps[0] as SweepLoopRange;
                
                CollectionAssert.IsEmpty(sweep.SweepProperties);
                var delay1 = new DelayStep();
                var delay2 = new DelayStep();
                sweep.ChildTestSteps.Add(delay1);
                sweep.ChildTestSteps.Add(delay2);
                sweep.SweepProperties = new List<IMemberData>() { TypeData.FromType(typeof(DelayStep)).GetMember(nameof(DelayStep.DelaySecs)) };
                
                // Verify that the sweep property was added
                Assert.AreEqual(1, sweep.SweepProperties.Count);

                // Removing the first delay should do nothing
                sweep.ChildTestSteps.Remove(delay1);
                Assert.AreEqual(1, sweep.SweepProperties.Count);
                
                // Removing the final delay should remove the property
                sweep.ChildTestSteps.Remove(delay2);
                CollectionAssert.IsEmpty(sweep.SweepProperties);
            }

        }

        [Test]
        public void SerializeNestedSweepLoopRange()
        {
            var tp = new TestPlan();
            var s1 = new SweepLoopRange();
            var s2 = new SweepLoopRange();
            var s3 = new DelayStep() { DelaySecs = 0 };
            tp.ChildTestSteps.Add(s1);
            s1.ChildTestSteps.Add(s2);
            s2.ChildTestSteps.Add(s3);
            s1.SweepProperties = new List<IMemberData>() { TypeData.FromType(typeof(DelayStep)).GetMember(nameof(DelayStep.DelaySecs)) };
            s2.SweepProperties = new List<IMemberData>() { TypeData.FromType(typeof(DelayStep)).GetMember(nameof(DelayStep.DelaySecs)) };

            using (var st = new System.IO.MemoryStream())
            {
                tp.Save(st);
                st.Seek(0, 0);
                tp = TestPlan.Load(st, tp.Path);
            }
            s1 = tp.ChildTestSteps[0] as SweepLoopRange;
            s2 = s1.ChildTestSteps[0] as SweepLoopRange;

            Assert.AreEqual(1, s1.SweepProperties.Count);
            Assert.AreEqual(1, s2.SweepProperties.Count);
            Assert.AreEqual(s1.SweepPropertyName, s2.SweepPropertyName);

        }


        [Ignore("This is very unstable on CI runners")]
        [TestCase(true)]
        [TestCase(false)]
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
                sweepRange.SweepProperties = new List<IMemberData>() { TypeData.FromType(typeof(SweepTestStep)).GetMember("SweepProp") };
                sweep = sweepRange;
                check = Enumerable.Range(10, (int)(sweepRange.SweepEnd - sweepRange.SweepStart + 1));
            }
            else
            {
                check = Enumerable.Range(10, 20);
                var sweepRange = new SweepLoop();
                var lst = new List<SweepParam>();
                lst.Add(new SweepParam(new[] { TypeData.FromType(typeof(SweepTestStep)).GetMember("SweepProp") }, check.Cast<object>().ToArray()));
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
            checkif.InputVerdict.Property = TypeData.FromType(typeof(TestStep)).GetMember(nameof(TestStep.Verdict));



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
            var stream = File.OpenRead("TestTestPlans/whiletest.TapPlan");
            TestPlan plan = (TestPlan)new TapSerializer().Deserialize(stream, type: TypeData.FromType(typeof(TestPlan)));
            var run = plan.Execute();
            Assert.AreEqual(Verdict.Pass, run.Verdict);
        }

        [Test]
        public void SweepRaceBug()
        {
            // test that validation rules can be checked while the test plan is running
            // without causing an error. The validation rules does not need to do actual validation
            // but since SweepLoop and SweepLoopRange modifies its child steps this could cause an error
            // as shown by SweepRaceBugCheckStep and SweepRaceBugStep.
            var plan = new TestPlan();
            var repeat = new RepeatStep { Count =  10, Action = RepeatStep.RepeatStepAction.Fixed_Count};
            var loop = new SweepLoop();
            repeat.ChildTestSteps.Add(loop);
            loop.ChildTestSteps.Add(new SweepRaceBugStep(){});
            loop.ChildTestSteps.Add(new SweepRaceBugCheckStep(){ });
            var steptype = TypeData.FromType(typeof(SweepRaceBugStep)); 
            var member = steptype.GetMember(nameof(SweepRaceBugStep.Frequency));
            var member2 = TypeData.FromType(typeof(SweepRaceBugCheckStep)).GetMember(nameof(SweepRaceBugCheckStep.Frequency2));

            var lst = new List<SweepParam>();
            double[] values = new double[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
            lst.Add(new SweepParam(new[] { member}, values.Cast<object>().ToArray()));
            lst.Add(new SweepParam(new[] { member2 }, values.Cast<object>().ToArray()));
            loop.SweepParameters = lst;

            var loopRange = new SweepLoopRange();
            
            loopRange.SweepStart = 1;
            loopRange.SweepEnd = 10;
            loopRange.SweepPoints = 10;
            loopRange.ChildTestSteps.Add(new SweepRaceBugStep() { });
            loopRange.ChildTestSteps.Add(new SweepRaceBugCheckStep() { });
            loopRange.SweepProperties = new List<IMemberData> { member, member2 };
            var repeat2 = new RepeatStep { Count = 10, Action = RepeatStep.RepeatStepAction.Fixed_Count };
            
            repeat2.ChildTestSteps.Add(loopRange);
            var parallel = new ParallelStep();
            plan.ChildTestSteps.Add(parallel);
            parallel.ChildTestSteps.Add(repeat);
            parallel.ChildTestSteps.Add(repeat2);

            TestPlanRun run = null;
            TapThread.Start(() => run = plan.Execute());
            TapThread.Start(() =>
            {
                while (run == null)
                {
                    loopRange.Error.ToList();
                }
            });
            while(run == null)
            {
                loop.Error.ToList();
            }
            
            Assert.AreEqual(Verdict.NotSet, run.Verdict);
       }

        public class SweepRaceBugStep : TestStep
        {
            public double Frequency { get; set; }
            public double SharedFrequency { get; set; }
            public bool Check { get; set; }
            public override void Run()
            {
                SharedFrequency = Frequency;   
            }
        }
        public class SweepRaceBugCheckStep : TestStep
        {
            public double Frequency2 { get; set; }
            public override void Run()
            {
                
                if ((Parent.ChildTestSteps[0] as SweepRaceBugStep).SharedFrequency != Frequency2)
                    throw new Exception("Error occured");
                
            }
        }

        public class StepTypeA : TestStep
        {
            [Display("Property", Group: "A")]
            public double Property { get; set; }

            public override void Run()
            {
                if (Property != 5.0) throw new Exception();
                UpgradeVerdict(Verdict.Pass);
            }
        }
        
        public class StepTypeB : TestStep
        {
            [Display("Property", Group: "B")]
            public double Property { get; set; }
            public override void Run()
            {
                if (Property != 15.0) throw new Exception();
                UpgradeVerdict(Verdict.Pass);
            }
        }

        [Test]
        public void SweepSameNameDifferentGroup()
        {
            var loop = new SweepLoop();
            
            var a = new StepTypeA();
            var b = new StepTypeB();
            loop.ChildTestSteps.Add(a);
            loop.ChildTestSteps.Add(b);
            
            loop.SweepParameters.Add(new SweepParam(new []{TypeData.GetTypeData(a).GetMember(nameof(StepTypeA.Property))}, (double)5, (double)5));
            loop.SweepParameters.Add(new SweepParam(new []{TypeData.GetTypeData(b).GetMember(nameof(StepTypeB.Property))}, (double)15, (double)15));
            var plan = new TestPlan();
            plan.Steps.Add(loop);
            var run = plan.Execute();
            Assert.AreEqual(Verdict.Pass, run.Verdict);

        }

        [Test]
        public void SweepParametersWithIterations()
        {
            string[] testValues = ["a", "b", "c"];
            var plan = new TestPlan();
            var sweep = new SweepParameterStep();
            var step = new LogStep();
            var step2 = new LogStep();

            plan.ChildTestSteps.Add(sweep);
            sweep.ChildTestSteps.Add(step);
            sweep.ChildTestSteps.Add(step2);

            TypeData.GetTypeData(step).GetMember(nameof(step.LogMessage)).Parameterize(sweep, step, "A");
            
            foreach (var value in testValues)
            {
                sweep.SweepValues.Add(new SweepRow(){Values = {{"Enabled", true}, {"A", value}}});    
            }

            var runData = plan.ExecuteReturnData();
            
            var stepRunData = runData.StepRuns.Where(stepRun => stepRun.TestStepId == step.Id);
            var step2RunData = runData.StepRuns.Where(stepRun => stepRun.TestStepId == step2.Id);
            var iterationValues = stepRunData.Select(run => (int) run.Parameters.Find("Iteration", "Sweep").Value).ToArray();
            Assert.IsTrue(iterationValues.Distinct().Count() == iterationValues.Length);
            Assert.AreEqual(1, iterationValues[0]);
            var iterationValues2 = step2RunData.Select(run => (int) run.Parameters.Find("Iteration", "Sweep").Value).ToArray();
            Assert.IsTrue(iterationValues.SequenceEqual(iterationValues2));
        }

        [Test]
        public void MultilevelSweepParametersWithIterations()
        {
            string[] testValuesInner = ["a", "b", "c"];
            LogSeverity[] testValuesOuter = [LogSeverity.Info, LogSeverity.Debug, LogSeverity.Warning];
            
            var plan = new TestPlan();
            var innerLoop = new SweepParameterStep();
            var outerLoop = new SweepParameterStep();
            var step = new LogStep();
            var step2 = new LogStep();

            plan.ChildTestSteps.Add(outerLoop);
            outerLoop.ChildTestSteps.Add(innerLoop);
            innerLoop.ChildTestSteps.Add(step);
            innerLoop.ChildTestSteps.Add(step2);

            TypeData.GetTypeData(step).GetMember(nameof(step.LogMessage)).Parameterize(innerLoop, step, "A");
            TypeData.GetTypeData(step).GetMember(nameof(step.Severity)).Parameterize(outerLoop, step, "B");
            
            foreach (var value in testValuesInner)
            {
                innerLoop.SweepValues.Add(new SweepRow(){Values = {{"Enabled", true}, {"A", value}}});    
            }
            foreach (var value in testValuesOuter)
            {
                outerLoop.SweepValues.Add(new SweepRow(){Values = {{"Enabled", true}, {"B", value}}});    
            }

            var runData = plan.ExecuteReturnData();
            var stepRunData = runData.StepRuns.ToLookup(run => plan.ChildTestSteps.GetStep(run.TestStepId));
            var step1RunData = stepRunData[step];
            var innerLoopData = stepRunData[innerLoop];
            var outerLoopData = stepRunData[outerLoop];
            
            Assert.IsTrue(outerLoopData.Count() == 1);
            Assert.IsTrue(innerLoopData.Count() == testValuesOuter.Length);
            Assert.IsTrue(step1RunData.Count() == testValuesOuter.Length * testValuesInner.Length);

            {
                var iterationSweepValues = step1RunData.Select(step => (int)step.Parameters.Find("Iteration", "Sweep").Value).ToArray();
                int index = 0;
                for (int i = 0; i < testValuesOuter.Length; i++)
                {
                    for (int j = 0; j < testValuesInner.Length; j++)
                    {
                        Assert.IsTrue(iterationSweepValues[index] == (j + 1));
                        index++;
                    }
                }
            }
            {
                var iterationSweepValues = innerLoopData.Select(step => (int)step.Parameters.Find("Iteration", "Sweep").Value).ToArray();

                for (int j = 0; j < testValuesInner.Length; j++)
                {
                    Assert.IsTrue(iterationSweepValues[j] == (j+1));
                }
            }
        }

        class DoubleTestStep : TestStep
        {
            public double Value { get; set; }
            public override void Run()
            {
                
            }
        }
        [Test]
        public void SweepParameterRangeWithIterations()
        {
            var plan = new TestPlan();
            var loop = new SweepParameterRangeStep();
            var step = new DoubleTestStep();

            plan.ChildTestSteps.Add(loop);
            loop.ChildTestSteps.Add(step);

            TypeData.GetTypeData(step).GetMember(nameof(step.Value)).Parameterize(loop, step, "A");
            loop.SweepStart = 0;
            loop.SweepEnd = 10;
            loop.SweepStep = 1;

            var runData = plan.ExecuteReturnData();
            var iterations = runData.StepRuns.Where(stepRun => stepRun.TestStepId == step.Id).Select(stepRun => (int)stepRun.Parameters.Find("Iteration", "Sweep").Value).ToArray();
            Assert.AreEqual(1, iterations[0]);
            Assert.IsTrue(iterations.Count() == loop.SweepPoints);
            Assert.IsTrue(iterations.Distinct().Count() == iterations.Length);


        }
    }
}
