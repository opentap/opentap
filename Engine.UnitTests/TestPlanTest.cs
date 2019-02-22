//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using OpenTap.EngineUnitTestUtils;
using OpenTap.Plugins.BasicSteps;
using System.ComponentModel;
using System.Threading;
using OpenTap;
using OpenTap.Engine.UnitTests.TestTestSteps;
using System.Diagnostics.CodeAnalysis;

namespace OpenTap.Engine.UnitTests
{
    /// <summary>
    ///This is a test class for TestPlanTest and is intended
    ///to contain all TestPlanTest Unit Tests
    ///</summary>
    [TestFixture]
    public class TestPlanTest : EngineTestBase
    {


        public class TestStepExceptionTest : TestStep
        {
            public override void Run()
            {
                throw new Exception("test");
            }
        }

        public class TestStepPreExceptionTest : TestStep
        {
            public override void PrePlanRun()
            {
                throw new Exception("test");
            }
            public override void Run()
            {
            }
        }

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>

        [Test]
        public void SaveLoadWithNestedTypes()
        {
            TestPlan plan = new TestPlan();
            plan.Steps.Add(new TestStepTest.NestedStep());
            plan.Steps.Add(new TestStepTest2.NestedStep());
            string fileName = Path.GetTempFileName();
            plan.Save(fileName);

            TestPlan plan2 = TestPlan.Load(fileName);

            Assert.AreEqual(plan.Steps.Count, plan2.Steps.Count);
        }

        [Test]
        public void ChildGetMethods()
        {
            TestPlan target = new TestPlan();
            target.Steps.Add(new TestStepTest());
            target.Steps.Add(new TestStepTest2());
            target.Steps.Add(new TestStepTest { Enabled = false });
            var recTest = new RecursiveTestStep();
            target.Steps.Add(recTest);
            var recursivelyLast = new TestStepTest3 { Enabled = false };
            recTest.ChildTestSteps.Add(new TestStepTest3());
            recTest.ChildTestSteps.Add(recursivelyLast);
            try
            {
                recTest.ChildTestSteps.Add(new TestStepTest());
                //throw new Exception("test exception"); saved for future conformance testing
            }
            catch (Exception e)
            {
                Assert.AreNotEqual(e.Message, "test exception");
            }
            Assert.AreEqual(2, recTest.RecursivelyGetChildSteps(TestStepSearch.All).Count());
            Assert.AreEqual(6, target.Steps.RecursivelyGetAllTestSteps(TestStepSearch.All).Count());
            Assert.AreEqual(4, target.Steps.RecursivelyGetAllTestSteps(TestStepSearch.EnabledOnly).Count());
            // saved for future.. Assert.AreEqual(2, target.Steps.RecursivelyGetAllTestSteps(TestStep.Search.NotEnabledOnly).Count());
            Assert.AreEqual(recursivelyLast, target.Steps.RecursivelyGetAllTestSteps(TestStepSearch.All).Last());
        }

        /// <summary>
        ///A test for Run
        ///</summary>
        [Test]
        public void RunTest([Values(false, true)] bool open)
        {
            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);

            TestPlan target = new TestPlan();
            target.Steps.Add(new TestStepTest());

            ResultOrderTester orderTester = new ResultOrderTester();
            ResultSettings.Current.Add(orderTester);
            PlanRunCollectorListener pl = new PlanRunCollectorListener();
            if (open)
                target.Open();
            var planRun = target.Execute(new IResultListener[] { orderTester, pl });
            if (open)
            {
                Assert.IsTrue(orderTester.IsConnected);
                Assert.IsTrue(pl.IsConnected);
                target.Close();
            }
            Assert.AreEqual(true, orderTester.StepRunCompletedRan);
            Assert.AreEqual(true, orderTester.PlanRunCompletedRan);
            Log.RemoveListener(trace);
            ResultSettings.Current.Remove(orderTester);
            trace.AssertErrors(new string[] { "No instruments found.", "Keysight Internal! This version is not licensed. Do not distribute outside Keysight." });
            Assert.AreEqual(Verdict.Pass, planRun.Verdict);
            Assert.AreEqual(Verdict.Pass, pl.StepRuns.First().Verdict);
            ResultSettings.Current.Clear();
            Assert.AreEqual(0, orderTester.Errors.Count());
        }

        [Test]
        public void PrintTestPlanRunSummaryTest()
        {

            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);

            TestPlan target = new TestPlan();
            target.PrintTestPlanRunSummary = true;
            target.Steps.Add(new TestStepTest());
            PlanRunCollectorListener pl = new PlanRunCollectorListener();
            var planRun = target.Execute(ResultSettings.Current.Concat(new IResultListener[] { pl }));
            Log.Flush();
            Log.RemoveListener(trace);
            var summaryLines = trace.allLog.ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(l => l.Contains(": Summary "));
            Assert.AreEqual(4, summaryLines.Count(), "Did not find the expected number of summary lines in the log.");

            trace.AssertErrors(new string[] { "No instruments found.", "Keysight Internal! This version is not licensed. Do not distribute outside Keysight." });
            Assert.AreEqual(Verdict.Pass, pl.StepRuns.First().Verdict);
            ResultSettings.Current.Clear();
        }

        [Test]
        public void TestPlanStepExceptionTest()
        {
            var preAbortTestPlan = EngineSettings.Current.AbortTestPlan;
            EngineSettings.Current.AbortTestPlan = default(EngineSettings.AbortTestPlanType);
            try
            {
                PlanRunCollectorListener pl = new PlanRunCollectorListener();
                TestPlan target = new TestPlan();
                target.Steps.Add(new TestStepExceptionTest());
                target.Steps.Add(new TestStepTest());

                ResultSettings.Current.Add(pl);
                target.Execute();
                ResultSettings.Current.Remove(pl);

                Assert.AreEqual(Verdict.Error, pl.StepRuns.ElementAt(0).Verdict, "Exception in teststep did not cause verdict to become 'Error'.");
                Assert.AreEqual(Verdict.Pass, pl.StepRuns.ElementAt(1).Verdict, "TestStep did not pass after previous step caused an exception.");

                ResultSettings.Current.Clear();
            }
            finally
            {
                EngineSettings.Current.AbortTestPlan = preAbortTestPlan;
            }
        }

        [Test]
        public void TestPlanExecuteResultListenerCheck()
        {
            // Verifies that only the ResultListeners added in execute are used, if that overload is selected.
            PlanRunCollectorListener pl1 = new PlanRunCollectorListener();
            PlanRunCollectorListener pl2 = new PlanRunCollectorListener();
            TestPlan target = new TestPlan();
            target.Steps.Add(new TestStepTest());
            ResultSettings.Current.Add(pl2);
            target.Open(new[] { pl1 });
            target.Execute(new[] { pl1 });
            target.Close();
            
            target.Execute(new[] { pl1 });

            ResultSettings.Current.Remove(pl2);
            Assert.AreEqual(0, pl2.StepRuns.Count);
            Assert.AreEqual(2, pl1.StepRuns.Count);
            Assert.AreEqual(true, pl1.WasOpened);
            Assert.AreEqual(false, pl2.WasOpened);
        }

        [Test]
        public void TestPlanLogErrorTest()
        {
            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);

            TestPlan target = new TestPlan();
            target.Steps.Add(new TestStepTest());

            var planRun = target.Execute();

            Log.RemoveListener(trace);

            string logStr = trace.GetLog();

            Assert.IsFalse(Regex.IsMatch(logStr, "TestPlan\\s+; Error"));
        }

        [Test]
        public void TestPlanSaveLoadTest()
        {

            TestPlan target = new TestPlan();

            TestStepTest step1 = new TestStepTest();
            step1.TestPoints.Add(new TestStepTest.TestPoint { Channel = 12 });
            target.Steps.Add(step1);

            TestStepTest2 step2 = new TestStepTest2();
            step2.TestPoints.Add(new TestStepTest2.TestPoint { });
            step2.ObjectProperty = new TestStepTest2.SomeObject { };
            target.Steps.Add(step2);

            SubSpace.TestStepTest step3 = new SubSpace.TestStepTest();
            target.Steps.Add(step3);

            TestPlan loadedPlan;
            string xmlText;
            using (Stream str = new MemoryStream(20000))
            using (StreamReader reader = new StreamReader(str))
            {
                target.Save(str);
                str.Seek(0, 0);
                xmlText = reader.ReadToEnd();
                str.Seek(0, 0);
                loadedPlan = TestPlan.Load(str,target.Path);
            }
            Assert.AreEqual(loadedPlan.Steps.Count, target.Steps.Count);
            Assert.AreEqual(loadedPlan.Steps[0].Name, target.Steps[0].Name);
        }

        [Test]
        public void TestPlan_AbortIfNoResource()
        {
            PlanRunCollectorListener pl = new PlanRunCollectorListener();
            TestPlan target = new TestPlan();

            ScpiTestStep step1 = new ScpiTestStep();
            target.Steps.Add(step1);
            step1.Instrument = null;

            var t = Task<TestPlanRun>.Factory.StartNew(() =>
            {
                return target.Execute(ResultSettings.Current.Concat(new IResultListener[] { pl }));
            });
            t.Wait(1000);
            if (t.Status == TaskStatus.Running)
                Assert.Fail("TestPlan never completed.");
            Assert.AreEqual(0, pl.StepRuns.Count(), "Step was run although resource was not set");

        }

        [Test]
        public void TestResultOrderTester()
        {
            ResultOrderTester tester = new ResultOrderTester();
            var testTestPlanRun = new TestPlanRun(new TestPlan(), new List<IResultListener>(), DateTime.Now, Stopwatch.GetTimestamp());
            try
            {
                tester.OnTestPlanRunCompleted(testTestPlanRun, new MemoryStream());
                throw new Exception("This should not happen");
            }
            catch (InvalidOperationException)
            {
            }
            try
            {
                TestStepRun stepRun = new TestStepRun(new TestStepTest(), testTestPlanRun.Id);
                tester.OnTestStepRunCompleted(stepRun);
                throw new Exception("This should not happen");
            }
            catch (InvalidOperationException)
            {
            }
            var errs = tester.Errors;
            Assert.AreEqual(2, errs.Count());

        }

        /// <summary> Tests to see if a prerun error in a teststep results in the run of the result listeners.</summary>
        [Test]
        public void TestStepPrePostRunExceptionsIntoResultListener()
        {
            PlanRunCollectorListener pl = new PlanRunCollectorListener();
            TestPlan testPlan = new TestPlan();
            testPlan.ChildTestSteps.Add(new TestStepPreExceptionTest());
            ResultSettings.Current.Add(pl);
            testPlan.Execute();
            ResultSettings.Current.Remove(pl);

            Assert.AreEqual(0, pl.StepRuns.Count);
            Assert.AreEqual(1, pl.PlanRuns.Count);
            Assert.AreEqual(true, pl.PlanRuns.ElementAt(0).FailedToStart);
        }

        [Test]
        public void MissingInstReferencesIntoResultListener()
        {
            PlanRunCollectorListener pl = new PlanRunCollectorListener();
            TestPlan testPlan = new TestPlan();
            ScpiTestStep step = new ScpiTestStep();

            testPlan.ChildTestSteps.Add(step);
            ResultSettings.Current.Add(pl);
            var tpr = testPlan.Execute();
            ResultSettings.Current.Remove(pl);

            Assert.IsTrue(tpr.FailedToStart);
        }

        [Test]
        public void InstExceptionIntoResultListener()
        {
            PlanRunCollectorListener pl = new PlanRunCollectorListener();
            TestPlan testPlan = new TestPlan();
            InstrumentTestStep step = new InstrumentTestStep();
            InstrumentTest openCrash = new InstrumentTest { CrashPhase = InstrumentTest.InstrPhase.Open };
            step.Instrument = openCrash;

            testPlan.ChildTestSteps.Add(step);
            ResultSettings.Current.Add(pl);
            var planrun = testPlan.Execute();
            ResultSettings.Current.Remove(pl);

            Assert.AreEqual(0, pl.StepRuns.Count);
            Assert.AreEqual(1, pl.PlanRuns.Count);
            Assert.AreEqual(Verdict.Error, pl.PlanRuns.ElementAt(0).Verdict);
        }

        [Test]
        public void BadResultListenerIntoGoodResultListener()
        {
            PlanRunCollectorListener pl = new PlanRunCollectorListener();
            TestPlan testPlan = new TestPlan();

            testPlan.ChildTestSteps.Add(new TestStepTest());
            ResultSettings.Current.Add(pl);

            // Now, we add a resultlistener that has exceptions
            var erl = new resultListenerCrash { CrashResultPhase = resultListenerCrash.ResultPhase.Open };
            ResultSettings.Current.Add(erl);

            testPlan.Execute();

            ResultSettings.Current.Clear();

            Assert.AreEqual(0, pl.StepRuns.Count);
            Assert.AreEqual(1, pl.PlanRuns.Count);
            Assert.AreEqual(true, pl.PlanRuns.ElementAt(0).FailedToStart);
        }

        /// <summary>
        /// Run child steps in a loop.
        /// </summary>
        [AllowAnyChild]
        class RepeatRunChildSteps : TestStep
        {
            public int Repeats { get; set; }
            public override void Run()
            {
                for(int i = 0; i < Repeats; i++)
                {
                    RunChildSteps();
                }
            }
        }

        /// <summary>
        /// A result listener that just takes a bit of time in the result processing thread.
        /// Not too much or the test is too slow.
        /// </summary>
        class SlowResultListener : ResultListener
        {
            public override void OnTestStepRunCompleted(TestStepRun stepRun)
            {
                base.OnTestStepRunCompleted(stepRun);
            }

            public override void OnTestStepRunStart(TestStepRun stepRun)
            {
                base.OnTestStepRunStart(stepRun);
                TapThread.Sleep(10);
            }
        }

        /// <summary>
        /// Issue (#3898) is caused by a slight delay in the result processing thread that provokes a race condition 
        /// this affects steps that run child steps multiple times without waiting for defer completion.
        /// </summary>
        [Test]
        public void RepeatChildStepsFailure()
        {

            var plan = new TestPlan();
            var repeat = new RepeatRunChildSteps { Repeats = 10 };
            var repeat2 = new RepeatRunChildSteps { Repeats = 10 };
            repeat2.ChildTestSteps.Add(new SequenceStep());
            repeat.ChildTestSteps.Add(repeat2);
            plan.ChildTestSteps.Add(repeat);
            var run = plan.Execute(new IResultListener[] { new SlowResultListener() });
            Assert.AreEqual(Verdict.NotSet, run.Verdict);
        }



        public class VerifyTestStep : TestStep
        {
            public static int Value = 0;

            public int Prop { get; set; }

            public VerifyTestStep()
            {
                Prop = 123;
            }

            public override void Run()
            {
                Value = Prop;
            }
        }

        //[Test]
        public void TestLoadReferenceCodev2()
        {
            for(int i = 0; i < 10; i++)
            {
                var pln = new TestPlan();
                var del = new DelayStep();
                pln.ChildTestSteps.Add(del);
                pln.ExternalParameters.Add(del, CSharpTypeInfo.Create(typeof(DelayStep)).GetMember("DelaySecs"));
                var pms = new List<ExternalParameter>();
                var sw = Stopwatch.StartNew();
                //TestPlanReference._fromProperties(pln.ExternalParameters.Entries.ToList());
                //var e = sw.Elapsed;
                //Debug.WriteLine("time spent: {0}", e);
            }
        }

        [Test]
        public void RelativeTestPlanTest()
        {
            int depth = 10;
            string path = "RelativeTestPlanTest_TestDir";

            for (int i = 0; i < depth; i++)
            {
                TestPlan plan = new TestPlan();
                Directory.CreateDirectory(path);
                var filepath = Path.Combine(path, "plan.TestPlan");
                if (i < depth - 1)
                {
                    var step = new TestPlanReference();
                    step.Filepath.Text = "<TestPlanDir>\\TestDir\\plan.TestPlan";
                    plan.Steps.Add(step);
                }
                else
                {
                    var step = new DelayStep();
                    step.DelaySecs = 0.001;
                    plan.Steps.Add(step);

                }

                plan.Save(filepath);
                path = Path.Combine(path, "TestDir");
            }
            var bigplan = TestPlan.Load("RelativeTestPlanTest_TestDir\\plan.TestPlan");
            var run = bigplan.Execute();
            Assert.AreEqual(depth, run.StepsWithPrePlanRun.Count);
            Directory.Delete("RelativeTestPlanTest_TestDir", true);

        }
        [Test]
        public void SweepDynamic()
        {
            string TempName = Path.GetTempFileName();

            TestPlan tp = new TestPlan();
            tp.ChildTestSteps.Add(new VerifyTestStep());

            var delaystep = tp.ChildTestSteps.First() as VerifyTestStep;

            tp.ExternalParameters.Add(delaystep, TypeInfo.GetTypeInfo(delaystep).GetMember("Prop"), "Prop");
            tp.Save(TempName);

            // Sanity check to verify that the step itself works
            tp.ExternalParameters.Entries[0].Value = 1234;
            VerifyTestStep.Value = 0;
            var planrun = tp.Execute();
            Assert.AreEqual(1234, VerifyTestStep.Value, "Wrong value before serialization");

            // Test running after construction by code
            tp = new TestPlan();

            var step = new OpenTap.Plugins.BasicSteps.TestPlanReference();
            tp.ChildTestSteps.Add(step);
            step.Filepath.Text = TempName;
            step.LoadTestPlan();

            // The step is now replaced
            ITestStep step2 = tp.ChildTestSteps.First();

            Assert.AreEqual(1, step2.ChildTestSteps.Count, "Number of childsteps");
            Assert.IsNotNull(TypeInfo.GetTypeInfo(step2).GetMember("Prop"), "Could not find external parameter property on testplan");

            var sweep = new OpenTap.Plugins.BasicSteps.SweepLoop();

            tp.ChildTestSteps.Add(sweep);
            tp.ChildTestSteps.Remove(step2);
            sweep.ChildTestSteps.Add(step2);

            sweep.SweepParameters = new List<OpenTap.Plugins.BasicSteps.SweepParam> { new OpenTap.Plugins.BasicSteps.SweepParam(new List<IMemberInfo> {  TypeInfo.GetTypeInfo(step2).GetMember("Prop") }, 1337) };

            VerifyTestStep.Value = 0;
            tp.Execute();
            Assert.AreEqual(1337, VerifyTestStep.Value, "Wrong value after first run");

            string TempName2 = Path.GetTempFileName();
            tp.Save(TempName2);

            // Test running after deserialization
            tp = TestPlan.Load(TempName2);
            VerifyTestStep.Value = 0;
            tp.Execute();
            Assert.AreEqual(1337, VerifyTestStep.Value, "Wrong value after deserialization");
        }

        [Test]
        public void NullResourceTest()
        {
            InstrumentSettings.Current.Clear();
            try
            {
                InstrumentSettings.Current.Add(new InstrumentTest() { CrashPhase = InstrumentTest.InstrPhase.Open });
                // Plan with 4 steps.
                //> Sequence   disabled
                //   > Scpi    enabled
                //> Scpi       disabled
                //> LogStep    enabled
                TestPlan plan = new TestPlan();
                ScpiTestStep step1 = new ScpiTestStep { Enabled = true, Instrument = null };
                LogStep step2 = new LogStep { Enabled = true };
                ScpiTestStep step3 = new ScpiTestStep { Enabled = false, Instrument = null };
                InstrumentTestStep step4 = new InstrumentTestStep() { Instrument = InstrumentSettings.Current.First(), Enabled = false };
                SequenceStep parent = new SequenceStep { Enabled = false };
                parent.ChildTestSteps.Add(step1);

                plan.ChildTestSteps.Add(parent);
                plan.ChildTestSteps.Add(step2);
                plan.ChildTestSteps.Add(step3);
                plan.ChildTestSteps.Add(step4);

                var planRun = plan.Execute();
                Assert.AreEqual(Verdict.NotSet, planRun.Verdict);

                plan.Open();
                Assert.IsFalse(InstrumentSettings.Current.First().IsConnected);
                var planRun2 = plan.Execute();

                plan.Close();
                Assert.AreEqual(Verdict.NotSet, planRun2.Verdict);
            }
            finally
            {
                InstrumentSettings.Current.Clear();
            }
        }

        public class DelegateStep : TestStep
        {
            [System.Xml.Serialization.XmlIgnore]
            [Browsable(true)]
            public Func<Verdict> Action { get; set; }
            [Browsable(true)]
            [System.Xml.Serialization.XmlIgnore]
            public Action Action2 { get; set; }
            [Browsable(true)]
            public void Action3()
            {
                Log.Info("Action3 called");
            }
            public DelegateStep()
            {
                Action2 = () => Log.Info("Action2 called");
                Action = () => Verdict.Error;
            }
            public override void Run()
            {
                Thread.Sleep(10);
                Verdict = Action();
                Thread.Sleep(10);
                
            }
        }


        [Test]
        public void ChildITestStep()
        {
            // Trigger issue when running child steps that is an ITestStep.
            SequenceStep seq = new SequenceStep();
            TestPlan plan = new TestPlan();

            var instr = new InterfaceTestInstrument();
            
            Verdict checkInstr()
            {
#if ARBITER_FEATURES
                if (EngineSettings.Current.ResourceManagerType is LazyResourceManager)
                {
                    return instr.IsConnected ? Verdict.Fail : Verdict.Pass;
                }
#endif
                return instr.IsConnected ? Verdict.Pass : Verdict.Fail;
            }

            var step = new TestITestStep();
            step.Instrument = instr;
            plan.Steps.Add(new DelegateStep { Action = checkInstr });
            plan.Steps.Add(seq);
            plan.Steps.Add(new DelegateStep { Action = checkInstr });
            seq.ChildTestSteps.Add(step);

#if ARBITER_FEATURES
            var oldResourceManager = EngineSettings.Current.ResourceManagerType;
            EngineSettings.Current.ResourceManagerType = new LazyResourceManager();
            var runLazy = plan.Execute();
            Assert.IsTrue(runLazy.Verdict == Verdict.Pass);
            Assert.IsTrue(step.WasRun);
            step.WasRun = false;
#endif
            EngineSettings.Current.ResourceManagerType = new ResourceTaskManager();

            var run = plan.Execute();
            Assert.IsTrue(run.Verdict == Verdict.Pass);
            Assert.IsTrue(step.WasRun);
                        
            // Trigger issue with cycle on step.Parent / step.Parent.ChildTestSteps.
            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);
            var file = "ChildITestStep.TapPlan";
            plan.Save(file);
            Log.RemoveListener(trace);
            File.Delete(file);
            StringAssert.Contains("Cycle detected", trace.GetLog());

            // Trigger possible issue with null Name.
            Assert.IsTrue(string.IsNullOrWhiteSpace(step.GetFormattedName()));

        }

        [Test]
        public void TestChildStepsChanged()
        {
            List<TestStepList.ChildStepsChangedAction> lst = new List<TestStepList.ChildStepsChangedAction>();

            var a = new SweepLoop();
            a.ChildTestSteps.ChildStepsChanged += (s, action, obj, index) => lst.Add(action);
            var b = new SequenceStep();
            var c = new SequenceStep();
            a.ChildTestSteps.Add(b);
            Assert.AreEqual(1, lst.Count);
            var tl2 = new TestStepList();
            tl2.ChildStepsChanged += (s, action, obj, index) => lst.Add(action);
            tl2.Add(c);
            a.ChildTestSteps = tl2;
            c.ChildTestSteps.Add(b);
            c.ChildTestSteps.Remove(b);
            c.ChildTestSteps = new TestStepList();

            Assert.AreEqual(6, lst.Count);
            Assert.IsTrue(lst.Contains(TestStepList.ChildStepsChangedAction.ListReplaced));
            Assert.IsTrue(lst.Contains(TestStepList.ChildStepsChangedAction.RemovedStep));
            Assert.IsTrue(lst.Contains(TestStepList.ChildStepsChangedAction.AddedStep));
            Assert.AreEqual(2, lst.Count(x => x == TestStepList.ChildStepsChangedAction.ListReplaced));
        }

        /// <summary> Test running a sub-section of a test plan with an overload of TestPlan.Execute.</summary>
        [Test]
        public void SubPlanRunTest()
        {
            SequenceStep seq = new SequenceStep();
            TestPlan plan = new TestPlan();
            var step = new TestITestStep
            {
                Instrument = new InterfaceTestInstrument()
            };

            plan.Steps.Add(seq);
            seq.ChildTestSteps.Add(step);
            var run = plan.Execute(ResultSettings.Current, stepsOverride: new HashSet<ITestStep>(new[] { step }));
            
            Assert.IsTrue(run.Verdict < Verdict.Pass);
            Assert.IsTrue(step.WasRun);
            Assert.AreEqual(1, run.StepsWithPrePlanRun.Count);
        }

        [AllowAnyChild]
        internal class TestSubStep : TestStep
        {
            internal static int PreRuns = 0;
            internal static int Runs = 0;

            public override void PrePlanRun()
            {
                base.PrePlanRun();
                PreRuns++;
            }

            public override void PostPlanRun()
            {
                base.PostPlanRun();
            }

            public override void Run()
            {
                Runs++;
                RunChildSteps();
            }

            internal static void Clear()
            {
                PreRuns = 0;
                Runs = 0;
            }
        }

        /// <summary> Test running a sub-section of a test plan with an overload of TestPlan.Execute.</summary>
        [Test]
        public void SubPlanRunTest2()
        {
            TestPlan plan = new TestPlan();

            TestSubStep.Clear();

            var s = new TestSubStep();
            s.ChildTestSteps.Add(new TestSubStep());
            plan.ChildTestSteps.Add(s);
            plan.ChildTestSteps.Add(new TestSubStep());
            
            var run = plan.Execute(ResultSettings.Current, stepsOverride: new HashSet<ITestStep>(plan.ChildTestSteps));

            Assert.AreEqual(3, TestSubStep.PreRuns);
            Assert.AreEqual(3, TestSubStep.Runs);
            Assert.AreEqual(3, run.StepsWithPrePlanRun.Count);
        }

        /// <summary> Test running a sub-section of a test plan with an overload of TestPlan.Execute.</summary>
        [Test]
        public void SubPlanRunTest3()
        {
            TestPlan plan = new TestPlan();

            TestSubStep.Clear();

            var s = new TestSubStep();
            var sub = new TestSubStep();
            s.ChildTestSteps.Add(sub);
            plan.ChildTestSteps.Add(s);
            plan.ChildTestSteps.Add(new TestSubStep());
            
            var run = plan.Execute(ResultSettings.Current, stepsOverride: new HashSet<ITestStep>(new [] { sub, sub, sub }));

            Assert.AreEqual(1, TestSubStep.PreRuns);
            Assert.AreEqual(1, TestSubStep.Runs);
            Assert.AreEqual(1, run.StepsWithPrePlanRun.Count);
        }
        
        [Test]
        public void PromptMetadataTest()
        {
            UserInput.SetInterface(new DutInfoPrompt());

            try
            {
                DutSettings.Current.Clear();
                DutSettings.Current.Add(new MyDut());

                EngineSettings.Current.PromptForMetaData = true;

                {
                    var plan = new TestPlan();
                    plan.Steps.Add(new DutStep() { MyDut = DutSettings.Current.OfType<MyDut>().FirstOrDefault() });

                    var run = plan.Execute();
                    Assert.IsFalse(run.FailedToStart);
                    Assert.AreEqual(Verdict.Pass, run.Verdict);
                }

                DutSettings.Current.OfType<MyDut>().ToList().ForEach(f => f.Comment = "test");

                {
                    var plan = new TestPlan();
                    plan.Steps.Add(new DutStep() { MyDut = DutSettings.Current.OfType<MyDut>().FirstOrDefault() });

                    plan.Open();

                    {
                        var run = plan.Execute();
                        Assert.IsFalse(run.FailedToStart);
                        Assert.AreEqual(Verdict.Pass, run.Verdict);
                    }

                    {
                        var run = plan.Execute();
                        Assert.IsFalse(run.FailedToStart);
                        Assert.AreEqual(Verdict.Pass, run.Verdict);
                    }

                    plan.Close();
                }
            }
            finally
            {
                UserInput.SetInterface(null);
            }
        }

        [Test]
        public void TestPlanDeferError()
        {

            var plan = new TestPlan();
            var seq = new SequenceStep();
            var err = new ThrowInDefer() { WaitMs = 10 };
            var delay = new DelayStep() { DelaySecs = 1.0 };
            
            seq.ChildTestSteps.Add(err);
            seq.ChildTestSteps.Add(delay);
            plan.ChildTestSteps.Add(seq);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {    
                err.Throw = i % 2 == 0;
                if(i == 4)
                {
                    err.WaitMs = 50;
                    delay.DelaySecs = 0; // now it will 
                }
                
                PlanRunCollectorListener pl = new PlanRunCollectorListener();

                TestPlanRun run = plan.ExecuteAsync(new[] { (IResultListener)pl }, null, null, CancellationToken.None).Result;

                var delayRun = pl.StepRuns.FirstOrDefault(x => x.TestStepId == delay.Id);
                var errRun = pl.StepRuns.FirstOrDefault(x => x.TestStepId == err.Id);

                Assert.AreEqual(Verdict.Error, run.Verdict);
                Assert.AreEqual(Verdict.Error, errRun.Verdict);
                Assert.AreEqual(Verdict.NotSet, delayRun.Verdict);
            }
            
            Debug.WriteLine("TestPlanDeferError {0}", sw.Elapsed);
        }

        class DutInfoPrompt : IUserInputInterface
        {
            public void RequestUserInput(object dataObject, TimeSpan Timeout, bool modal)
            {
                var sub = AnnotationCollection.Annotate(dataObject).Get<IForwardedAnnotations>().Forwarded.ToArray();
                var comment = sub.First(x => x.Get<IMemberAnnotation>().Member.Name == "Comment");
                Assert.IsNotNull(comment != null);
                comment.Get<IStringValueAnnotation>().Value = "some comment";
                comment.Write();
                Assert.IsNotNull(sub.First(x => x.Get<IMemberAnnotation>().Member.Name == "ID"));

            }
        }
        
        public class MyDut : Dut
        {
        }

        public class DutStep : TestStep
        {
            public Dut MyDut { get; set; }
            public int Sleep { get; set; }
            public override void Run()
            {
                if(Sleep != 0)
                    TapThread.Sleep(Sleep);
                if (string.Compare("Some comment", MyDut.Comment, true) != 0)
                    throw new InvalidOperationException();
                if (MyDut.IsConnected == false)
                    throw new InvalidOperationException("Dut is closed!");
                UpgradeVerdict(Verdict.Pass);
            }
        }

        [Test]
        public void ErrorStackOverflow()
        {
            var repa = new RepeatStep();
            var repb = new RepeatStep();
            repa.TargetStep = repb;
            repb.TargetStep = repa;

            var error = repa.Error; // this could once throw a stackoverflow exception due to forwarded errors.


            Assert.IsTrue(string.IsNullOrWhiteSpace(error));
        }

        [Test]
        public void DeferSynchronizationManager()
        {
            using (var tm = new ThreadManager())
            {
                Stopwatch sw = Stopwatch.StartNew();
                int finalCount = 10000;
                var sem = new Semaphore(0, finalCount);
                int counter = 0;
                for (int i = 0; i < finalCount; i++)
                {
                    tm.Enqueue(() =>
                    {

                        //Thread.Sleep(10);
                        Interlocked.Increment(ref counter);
                        sem.Release();
                    });
                }
                for (int i = 0; i < finalCount; i++)
                {
                    sem.WaitOne();
                }
                
                Assert.AreEqual(finalCount, counter);
                var elapsed = sw.Elapsed;
                Debug.WriteLine("{0}", elapsed.TotalMilliseconds);
            }
        }

        [Test]
        public void DeferSynchronization()
        {
            var tw = new WorkQueue(WorkQueue.Options.None, "Test");
            var tw2 = new WorkQueue(WorkQueue.Options.None, "Test");
            int counter = 0;
            int counter2 = 0;
            for (int i = 0; i < 10; i++)
            {
                tw.EnqueueWork(() =>
                {
                    Thread.Sleep(10);
                    counter += 1;
                });
                tw2.EnqueueWork(() =>
                {
                    Thread.Sleep(10);
                    counter2 += 1;
                });
            }
            Thread.Sleep(200);
            Assert.AreEqual(10, counter);
            Assert.AreEqual(10, counter2);
        }

        [Test]
        public void RunMassiveSynchronousPlan()
        {
            // run an excessively long test plan.

            int Count = 5000;
            double maxDuration = 50;
            TestPlan plan = new TestPlan();
            TimeGuardStep guard = new TimeGuardStep() { Timeout = maxDuration, StopOnTimeout = true };
            plan.ChildTestSteps.Add(guard);
            for(int i = 0; i < Count; i++)
            {
                var seq1 = new SequenceStep();
                var verdict = new VerdictStep() { VerdictOutput = Verdict.Pass };
                seq1.ChildTestSteps.Add(verdict);
                guard.ChildTestSteps.Add(seq1);
            }
            var rl = new ResultListenerValidator();
            var result = plan.Execute(new[] { rl });
            Assert.AreEqual(Verdict.Pass, result.Verdict);
            Assert.IsNull(rl.Exception);
        }

        [Test]
        public void RunMassivelyParallelPlan()
        {
            

            // run an excessively long and parallel test plan.
            // since its parallel, this should go very fast still.
            int Count = 100; 
            double maxDuration = 50;

            TestPlan plan = new TestPlan();
            TimeGuardStep guard = new TimeGuardStep() { Timeout = maxDuration, StopOnTimeout = true };
            var parallel = new ParallelStep();
            plan.ChildTestSteps.Add(guard);
            guard.ChildTestSteps.Add(parallel);
            for (int i = 0; i < Count; i++)
            {
                var seq1 = new SequenceStep();
                var verdict = new VerdictStep() { VerdictOutput = Verdict.Pass }; // make sure the plan gets verdict 'Pass' if everything goes well.
                var delay = new DelayStep() { DelaySecs = 0.1 };
                seq1.ChildTestSteps.Add(verdict);
                seq1.ChildTestSteps.Add(delay);
                
                parallel.ChildTestSteps.Add(seq1);
            }
            
            for (int i = 0; i < 2; i++)
            {
                var rl = new ResultListenerValidator();
                var result = plan.Execute(new[] { rl });
                Assert.IsNull(rl.Exception);
                log.Debug(result.Duration, "Massively Parallel Plan");
                Assert.AreEqual(Verdict.Pass, result.Verdict);
            }
        }

        class PlatformCheckResultListener : ResultListener
        {
            public bool TestPlanRunStarted;
            public override void OnTestPlanRunStart(TestPlanRun planRun)
            {
                TestPlanRunStarted = true;
                base.OnTestPlanRunStart(planRun);
            }

            public override void OnTestStepRunStart(TestStepRun stepRun)
            {
                base.OnTestStepRunStart(stepRun);
            }

            public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
            {
                base.OnTestPlanRunCompleted(planRun, logStream);
            }
        }

        class FcnUserInput : IUserInputInterface
        {
            public Action<object, TimeSpan, bool> F;

            public void RequestUserInput(object dataObject, TimeSpan Timeout, bool modal)
            {
                F(dataObject, Timeout, modal);
            }
        }


        [Test]
        [Pairwise]
        public void PlatformInteractionRace([Values(true,false)] bool runOpen,[Values(true, false)] bool lazy)
        {
            // things needs to happen in the right order.
            // PlatformInteraction should happen before anything on the ResultListener is called, except Open/Close.
            var listener = new PlatformCheckResultListener();
            int requestCount = 0;
            bool failure = false;

            void waitFormInput(object obj, TimeSpan timeout, bool modal)
            {
                Thread.Sleep(20); // a short sleep to make sure that the error would appear.
                var mpo = obj as MetadataPromptObject;
                foreach(var dut in mpo.Resources.OfType<DummyDut>())
                {
                    dut.Comment = "Some comment";
                }
                requestCount += 1;
            }

            UserInput.SetInterface(new FcnUserInput { F = waitFormInput });

            bool prevWait = EngineSettings.Current.PromptForMetaData;
            EngineSettings.Current.PromptForMetaData = true;
            var prevManager = EngineSettings.Current.ResourceManagerType;
            if (lazy)
            {
                EngineSettings.Current.ResourceManagerType = new LazyResourceManager();
            }
            else
            {
                EngineSettings.Current.ResourceManagerType = new ResourceTaskManager();
            }
            
            try
            {
                DummyDut dut1 = new DummyDut();
                DutSettings.Current.Clear();
                DutSettings.Current.Add(dut1);
                TestPlan plan = new TestPlan();
                DutStep step = new DutStep();
                step.MyDut = dut1;
                plan.ChildTestSteps.Add(step);
                if (runOpen)
                    plan.Open();
                try
                {
                    var run = plan.Execute(new[] { listener });
                    Assert.AreEqual(Verdict.Pass, run.Verdict);
                }
                finally
                {
                    if (runOpen)
                        plan.Close();
                }
            }
            finally
            {
                
                EngineSettings.Current.PromptForMetaData = prevWait;
                UserInput.SetInterface(null);
                EngineSettings.Current.ResourceManagerType = prevManager;
            }

            Assert.IsFalse(failure);
            Assert.AreEqual(1, requestCount);
        }

        [Test]
        [Ignore("Reenable when the resource manager supports multiple test plans")]
        public void TryMultipleRunsWithResources()
        {
            DummyDut dut1 = new DummyDut() { Comment = "Some comment" };
            DutSettings.Current.Clear();
            DutSettings.Current.Add(dut1);
            var sw = Stopwatch.StartNew();
            List<TapThread> lst = new List<TapThread>();
            List<TestPlanRun> runs = new List<TestPlanRun>();
            int maxcount = 2;
            Semaphore sem = new Semaphore(0, maxcount);
            for (int i = 0; i < maxcount; i++)
            {
                int cnt2 = 2;
                TestPlan plan = new TestPlan();
                for (int j = 0; j < cnt2; j++)
                {
                    DutStep step = new DutStep()
                    {
                        MyDut = dut1,
                        Sleep = i * 5
                    };
                    plan.ChildTestSteps.Add(step);
                }
                lst.Add(TapThread.Start(() =>
                {
                    var run = plan.Execute();

                    Assert.AreEqual(cnt2, run.StepsWithPrePlanRun.Count);
                    lock (runs)
                        runs.Add(run);
                    sem.Release();
                }));
            }

            for (int i = 0; i < maxcount; i++)
                sem.WaitOne();
            var elaps = sw.Elapsed;
            Debug.WriteLine("Elapsed: {0}", elaps.TotalMilliseconds);
            foreach (var run in runs)
            {
                Assert.AreEqual(Verdict.Pass, run.Verdict);
            }
            


        }

        static TraceSource log = Log.CreateSource("test");

        private class TestPlanResultListener : IResultListener
        {
            private EventWaitHandle eventWaitHandle = null;

            public string Name { get; set; } = "ResultListener1";

            public bool IsConnected { get; private set; }

            // instead of #pragma warning disable/restore CS0067 SuppressMessageAttribute can be used
#pragma warning disable CS0067
            public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

            public TestPlanResultListener(EventWaitHandle eventWaitHandle)
            {
                this.eventWaitHandle = eventWaitHandle;
            }

            public void Close()
            {
                IsConnected = false;
            }

            public void OnResultPublished(Guid stepRunID, ResultTable result)
            {
            }

            public void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
            {
            }

            public void OnTestPlanRunStart(TestPlanRun planRun)
            {
                eventWaitHandle.Set();
            }

            public void OnTestStepRunCompleted(TestStepRun stepRun)
            {
            }

            public void OnTestStepRunStart(TestStepRun stepRun)
            {
            }

            public void Open()
            {
                IsConnected = true;
            }
        }

        [Test]
        public void TestSleepOneTestPlan()
        {
            //---------------------------------------------------------------------------------------------------------
            EventWaitHandle ewhPlan1IsRunning = new EventWaitHandle(false, EventResetMode.AutoReset);
            EventWaitHandle ewhPlan1StoppedRunning = new EventWaitHandle(false, EventResetMode.AutoReset);

            TestPlan plan1 = new TestPlan();
            var plan1Step1 = new DelayStep() { DelaySecs = 10.0 };
            var plan1Step2 = new DelayStep() { DelaySecs = 10.0 };
            plan1.ChildTestSteps.Add(plan1Step1);
            plan1.ChildTestSteps.Add(plan1Step2);
            CancellationToken ctPlan1 = new CancellationToken();

            //---------------------------------------------------------------------------------------------------------
            EventWaitHandle ewhPlan2IsRunning = new EventWaitHandle(false, EventResetMode.AutoReset);
            EventWaitHandle ewhPlan2StoppedRunning = new EventWaitHandle(false, EventResetMode.AutoReset);

            TestPlan plan2 = new TestPlan();
            var plan2Step1 = new DelayStep() { DelaySecs = 10.0 };
            var plan2Step2 = new DelayStep() { DelaySecs = 10.0 };
            var plan2Step3 = new DelayStep() { DelaySecs = 10.0 };
            plan2.ChildTestSteps.Add(plan2Step1);
            plan2.ChildTestSteps.Add(plan2Step2);
            plan2.ChildTestSteps.Add(plan2Step3);            
            CancellationToken ctPlan2 = new CancellationToken();

            //---------------------------------------------------------------------------------------------------------
            TapThread tapThread1 = TapThread.Start(() =>
            {
                //TestPlan.Current is null before the TestPlan starts running
                ctPlan1.Register(TapThread.Current.Abort);
                plan1.Execute(new IResultListener[] { new TestPlanResultListener(ewhPlan1IsRunning) }, null, new HashSet<ITestStep>(plan1.ChildTestSteps));
                //TestPlan.Current is null after the TestPlan starts running
                ewhPlan1StoppedRunning.Set();
            });

            //---------------------------------------------------------------------------------------------------------            
            TapThread tapThread2= TapThread.Start(() =>
            {
                //TestPlan.Current is null before the TestPlan starts running
                ctPlan2.Register(TapThread.Current.Abort);
                plan2.Execute(new IResultListener[] { new TestPlanResultListener(ewhPlan2IsRunning) }, null, new HashSet<ITestStep>(plan2.ChildTestSteps));
                //TestPlan.Current is null after the TestPlan starts running
                ewhPlan2StoppedRunning.Set();
            });

            WaitHandle.WaitAll(new WaitHandle[] { ewhPlan1IsRunning, ewhPlan2IsRunning });
            
            Assert.AreEqual(2, plan1.ChildTestSteps.Count);
            Assert.AreEqual(false, tapThread1.AbortToken.IsCancellationRequested);
            Assert.AreEqual(true, plan1.IsRunning);

            Assert.AreEqual(3, plan2.ChildTestSteps.Count);
            Assert.AreEqual(false, tapThread2.AbortToken.IsCancellationRequested);
            Assert.AreEqual(true, plan2.IsRunning);


            tapThread1.Abort();
            Assert.AreEqual(true, tapThread1.AbortToken.IsCancellationRequested);
            Assert.AreEqual(false, tapThread2.AbortToken.IsCancellationRequested);

            tapThread2.Abort();
            Assert.AreEqual(true, tapThread1.AbortToken.IsCancellationRequested);
            Assert.AreEqual(true, tapThread2.AbortToken.IsCancellationRequested);


            Assert.AreEqual(true, plan1.IsRunning);
            Assert.AreEqual(true, plan2.IsRunning);

            WaitHandle.WaitAll(new WaitHandle[] { ewhPlan1StoppedRunning, ewhPlan2StoppedRunning });

            Assert.AreEqual(false, plan1.IsRunning);
            Assert.AreEqual(false, plan2.IsRunning);
        }
    }

    [TestFixture]
    public class ResourceManagementTests
    {
        [Test]
        public void ResourceManagementValidation()
        {
            var old = EngineSettings.Current.ResourceManagerType;
            try
            {
                EngineSettings.Current.ResourceManagerType = new ResourceManageValidator();

                var tp = new TestPlan();
                tp.Steps.Add(new ValidationStep());

                tp.Open();

                Assert.AreEqual(1, ResourceManageValidator.StageCounter[Stage.Open]);

                var run = tp.Execute();

                Assert.AreEqual(1, ResourceManageValidator.StageCounter[Stage.Open]);

                tp.Close();

                Assert.IsFalse(run.FailedToStart);
                Assert.AreEqual(Verdict.Pass, run.Verdict);

                foreach (var kvp in ResourceManageValidator.StageCounter)
                    if (kvp.Value != 0)
                        Assert.AreEqual(0, kvp.Value, "Stage counter for {0} should be 0 but is {1}", kvp.Key, kvp.Value);
            }
            finally
            {
                EngineSettings.Current.ResourceManagerType = old;
            }
        }

        public class ValidationStep : TestStep
        {
            public override void PrePlanRun()
            {
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[Stage.Open]);
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[Stage.Execute]);
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[Stage.PrePlanRun]);
            }

            public override void Run()
            {
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[Stage.Open]);
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[Stage.Execute]);
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[Stage.Run]);

                UpgradeVerdict(Verdict.Pass);
            }

            public override void PostPlanRun()
            {
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[Stage.Open]);
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[Stage.Execute]);
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[Stage.PostPlanRun]);
            }
        }

        public class ResourceManageValidator : IResourceManager
        {
            public static Dictionary<Stage, int> StageCounter = new Dictionary<Stage, int>();

            public IEnumerable<IResource> Resources => StaticResources;

            public IEnumerable<IResource> StaticResources { get; set; }
            public List<ITestStep> EnabledSteps { get; set; }

            public event Action<IResource> ResourceOpened;

            public void BeginStep(TestPlanRun planRun, ITestStepParent item, Stage stage, CancellationToken cancellationToken)
            {
                if (!StageCounter.ContainsKey(stage)) StageCounter[stage] = 0;
                StageCounter[stage]++;
                if (stage == Stage.Execute)
                {
                    if (item is TestPlan testplan)
                    {
                        var resources = ResourceManagerUtils.GetResourceNodes(StaticResources.Cast<object>().Concat(EnabledSteps));
                        testplan.StartResourcePromptAsync(planRun, resources.Select(res => res.Resource));
                    }
                }
            }

            public void EndStep(ITestStepParent item, Stage stage)
            {
                StageCounter[stage]--;
            }

            public void WaitUntilAllResourcesOpened(CancellationToken cancellationToken)
            {
            }

            public void WaitUntilResourcesOpened(CancellationToken cancellationToken, params IResource[] targets)
            {
                if (ResourceOpened != null)
                    targets.ToList().ForEach(ResourceOpened);
            }
        }
    }

    public class TestITestStep : ValidatingObject, ITestStep
    {

        public IInstrument Instrument { get; set; }

        public TestITestStep()
        {
            Name = "";
            Enabled = true;
        }

        TestStepList list = new TestStepList();

        public TestStepList ChildTestSteps
        {
            get { return list; }
        }

        public bool Enabled { get; set; }
            
        public Guid Id
        {
            get;set;
        }

        public bool IsReadOnly { get; set; }
        public string Name { get; set; }

        // Should cause StackOverflowException in serializer unless cycles are detected.
        //[System.Xml.Serialization.XmlIgnore]
        public ITestStepParent Parent { get; set; } 

        [System.Xml.Serialization.XmlIgnore]
        public TestPlanRun PlanRun { get; set; }

        [System.Xml.Serialization.XmlIgnore]
        public TestStepRun StepRun { get; set; }

        [System.Xml.Serialization.XmlIgnore]
        public string TypeName
        {
            get { return GetType().GetDisplayAttribute().GetFullName(); }
        }

        [System.Xml.Serialization.XmlIgnore]
        public Verdict Verdict { get; set; }
            
        public void PostPlanRun()
        {
                
        }

        public void PrePlanRun()
        {
                
        }
        public bool WasRun = false;
        public void Run()
        {
            WasRun = true;
            if(Instrument.IsConnected == false)
            {
                throw new InvalidOperationException("Instrument should be open");
            }
        }
    }

    /// <summary>
    /// Device to test run resultlistener run conformance.
    /// </summary>
    
    class ResultOrderTester : ResultListener, IExecutionListener
    {
        TestPlanRun currentPlanRun = null;
        readonly HashSet<Guid> currentStepRun = new HashSet<Guid>();
        public bool PlanRunCompletedRan = false;
        public bool StepRunCompletedRan = false;

        List<Exception> errors = new List<Exception>();
        public IEnumerable<Exception> Errors => errors.AsEnumerable();

        void Assert(bool condition)
        {
            if (!condition)
            {
                var err = new InvalidOperationException("Assert failed");
                errors.Add(err);
                throw err;
            }
        }

        public override void Open()
        {
            Assert(IsConnected == false);
            base.Open();
            
        }

        public override void Close()
        {
            Assert(IsConnected);
            base.Close();
        }

        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            Assert(IsConnected);
            Assert(currentPlanRun == null);
            Assert(currentStepRun.Count == 0);
            currentPlanRun = planRun;
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {
            Assert(IsConnected);
            Assert(currentPlanRun != null);
            Assert(currentStepRun.Count == 0);
            Assert(currentPlanRun.Id == planRun.Id);
            currentPlanRun = null;
            PlanRunCompletedRan = true;
        }
        
        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            Assert(IsConnected);
            Assert(currentPlanRun != null);
            Assert(currentStepRun.Contains(stepRun.Id) == false);
            currentStepRun.Add(stepRun.Id);
        }
        public override void OnTestStepRunCompleted(TestStepRun stepRun)
        {
            Assert(IsConnected);
            Assert(currentPlanRun != null);
            Assert(currentStepRun.Contains(stepRun.Id));
            currentStepRun.Remove(stepRun.Id);
            StepRunCompletedRan = true;
        }

        public override void OnResultPublished(Guid stepRunid, ResultTable result)
        {
            Assert(IsConnected);
            Assert(currentStepRun.Contains(stepRunid));
            Assert(currentPlanRun != null);
        }

        public void OnTestStepExecutionChanged(Guid stepId, TestStepRun stepRun, StepState newState, long changeTime)
        {
            Assert(IsConnected);
            switch (newState)
            {
                case StepState.Idle:
                    break;
                case StepState.PrePlanRun:
                    break;
                case StepState.Running:
                    break;
                case StepState.Deferred:
                    break;
                case StepState.PostPlanRun:
                    break;
            }
        }
    }

    /// <summary>
    /// A steps that uses a thread 100%.
    /// </summary>
    public class BusyStep : TestStep
    {
        [System.ComponentModel.Description("How many seconds to hog the thread.")]
        public double Seconds { get; set; }
        public override void Run()
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < Seconds) { }
        }
    }

    /// <summary>
    /// A SCPI instrument that can connect to any instrument. Use with the SCPI step.
    /// </summary>
    public class ScpiDummyInstrument : ScpiInstrument
    {
        /// <summary>
        /// Tag for identifying the dummy instrument.
        /// </summary>
        public string Tag { get; set; }
    }

    /// <summary>
    /// A steps that sends a single scpi command or query to the instrument. If query it waits for the response.
    /// </summary>
    public class ScpiTestStep : TestStep
    {
        public string Command { get; set; }
        public ScpiInstrument Instrument { get; set; }

        public override void Run()
        {
            if (Command.Contains("?"))
            {
                Instrument.ScpiQuery(Command);
            }
            else
            {
                Instrument.ScpiCommand(Command);
            }
        }

    }

    public class RegexStepTest : RegexOutputStep
    {
        public override bool GeneratesOutput 
        {
            get { return true; }
        }

        public override void Run()
        {
            {
                var Result = "1 2 3";
                this.RegularExpressionPattern = new Enabled<string> { Value = "([1-9])\\s([1-9])\\s([1-9])", IsEnabled = true };
                ProcessOutput(Result);
                Assert.AreEqual(Verdict.Pass, Verdict);
                Verdict = Verdict.NotSet;
            }

            {
                var Result = "dkwaud029jad9aABCjd2;9quwaj;d";
                this.RegularExpressionPattern = new Enabled<string> { Value = ".*(ABC).*", IsEnabled = true };
                ProcessOutput(Result);
                Assert.AreEqual(Verdict.Pass, Verdict);
                Verdict = Verdict.NotSet;
            }
            {
                var Result = "dkwaud029jad9aABjd2;9quwaj;d";
                this.RegularExpressionPattern = new Enabled<string> { Value = ".*(ABC).*", IsEnabled = true };
                ProcessOutput(Result);
                Assert.AreEqual(Verdict.Fail, Verdict);
                Verdict = Verdict.NotSet;
            }
            Verdict = Verdict.Pass;
        }
    }

    [TestFixture]
    public class RegexStepTester
    {

        [Test]
        public void DoRegexStepTest()
        {
            var regexStep = new RegexStepTest();
            TestPlan plan = new TestPlan();
            plan.ChildTestSteps.Add(regexStep);
            var run = plan.Execute();
            Assert.AreEqual(Verdict.Pass, run.Verdict);
        }
    }

#if ARBITER_FEATURES
    [TestFixture]
    public class ExecutionHookTests
    {
        [Test]
        public void PreHookTest()
        {
            TestPlan plan = new TestPlan();
            plan.ChildTestSteps.Add(new VerdictStep() { VerdictOutput = VerdictOutput.Pass });

            PreHookTester.ReplacementPlan = null;
            PreHookTester.Active = true;
            PreHookTester.HookCount = 0;

            try
            {
                var tpr = plan.Execute();

                Assert.AreEqual(false, tpr.FailedToStart);
                Assert.AreEqual(Verdict.Pass, tpr.Verdict);
                Assert.AreEqual(1, PreHookTester.HookCount, "Hook count");

                Assert.AreEqual(plan, ExecutionHookTester.StartedPlan, "Plan that actually ran.");
                Assert.AreEqual(plan, ExecutionHookTester.RequestedPlan, "Plan that was requested to execute.");
            }
            finally
            {
                PreHookTester.Active = false;
            }
        }

        [Test]
        public void PreHookReplacePlan()
        {
            TestPlan plan = new TestPlan();
            plan.ChildTestSteps.Add(new VerdictStep() { VerdictOutput = VerdictOutput.Pass });

            TestPlan plan2 = new TestPlan();
            plan2.ChildTestSteps.Add(new VerdictStep { VerdictOutput = VerdictOutput.Fail });

            PreHookTester.ReplacementPlan = plan2;
            PreHookTester.Active = true;
            PreHookTester.HookCount = 0;

            try
            {
                var tpr = plan.Execute();

                Assert.AreEqual(false, tpr.FailedToStart);
                Assert.AreEqual(Verdict.Fail, tpr.Verdict);
                Assert.AreEqual(1, PreHookTester.HookCount, "Hook count");

                Assert.AreEqual(plan2, ExecutionHookTester.StartedPlan, "Plan that actually ran.");
                Assert.AreEqual(plan, ExecutionHookTester.RequestedPlan, "Plan that was requested to execute.");
            }
            finally
            {
                PreHookTester.Active = false;
            }
        }
    }
    
    [Browsable(false)]
    public class PreHookTester : ComponentSettings, IPreTestPlanExecutionHook
    {
        public static bool Active = false;
        public static int HookCount = 0;

        public static TestPlan ReplacementPlan = null;

        public void BeforeTestPlanExecute(PreExecutionHookArgs hook)
        {
            if (!Active) return;

            if (ReplacementPlan != null) hook.TestPlan = ReplacementPlan;
            HookCount++;
        }
    }
#endif

    [Browsable(false)]
    public class ExecutionHookTester : ComponentSettings, ITestPlanExecutionHook
    {
        public static TestPlan StartedPlan { get; private set; }
        public static TestPlan RequestedPlan { get; private set; }

        public void AfterTestStepExecute(ITestStep testStep)
        {
        }

        public void BeforeTestStepExecute(ITestStep testStep)
        {
        }

        public void BeforeTestPlanExecute(TestPlan executingPlan)
        {
            StartedPlan = executingPlan;
        }

        public void AfterTestPlanExecute(TestPlan executedPlan, TestPlan requestedPlan)
        {
            RequestedPlan = requestedPlan;
        }
    }
}

