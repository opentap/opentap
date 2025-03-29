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
using OpenTap.Engine.UnitTests.TestTestSteps;
using System.Text;

namespace OpenTap.Engine.UnitTests
{
    /// <summary>
    ///This is a test class for TestPlanTest and is intended
    ///to contain all TestPlanTest Unit Tests
    ///</summary>
    [TestFixture]
    public class TestPlanTest 
    {


        public class TestStepExceptionTest : TestStep
        {
            public override void Run()
            {
                throw new Exception("test");
            }
        }

        public enum FailPoint { Run, PreRun, PostRun }
        public class TestStepExpectedExceptionTest : TestStep
        {
            private Verdict _verdict;
            private FailPoint _failPoint;

            public TestStepExpectedExceptionTest(Verdict verdict, FailPoint failPoint)
            {
                _verdict = verdict;
                _failPoint = failPoint;
            }

            public override void PrePlanRun()
            {
                if (_failPoint == FailPoint.PreRun)
                    throw new ExpectedException("", _verdict);
            }

            public override void Run()
            {
                if (_failPoint == FailPoint.Run)
                    throw new ExpectedException("", _verdict);
            }
            public override void PostPlanRun()
            {
                if (_failPoint == FailPoint.PostRun)
                    throw new ExpectedException("", _verdict);
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
        [Pairwise]
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

            var testFile = new byte[1500];
            var testFileName = $"OpenTap.UnitTests.{nameof(PrintTestPlanRunSummaryTest)}.testFile.bin";
            File.Delete(testFileName);
            File.WriteAllBytes(testFileName, testFile);

            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);

            TestPlan target = new TestPlan();
            target.PrintTestPlanRunSummary = true;
            target.Steps.Add(new TestStepTest() {PublishArtifact = testFileName});
            PlanRunCollectorListener pl = new PlanRunCollectorListener();
            var planRun = target.Execute(ResultSettings.Current.Concat(new IResultListener[] { pl }));
            Log.Flush();
            Log.RemoveListener(trace);
            var summaryLines = trace.allLog.ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(l => l.Contains(": Summary ")).ToArray();
            Assert.AreEqual(6, summaryLines.Count(), "Did not find the expected number of summary lines in the log.");
            Assert.AreEqual(Verdict.Pass, pl.StepRuns.First().Verdict);
            Assert.IsTrue(summaryLines[4].Contains("1 artifacts registered"));
            Assert.IsTrue(summaryLines[5].Contains(testFileName));
            Assert.IsTrue(summaryLines[5].EndsWith("[1.5 kB]"));
        }
        
        [Test]
        public void TestRunSelectedResultParameter([Values(null, 1, 3)] int? runSelected)
        { 
            var plan = new TestPlan();
            var steps = Enumerable.Range(0, 10).Select(_ => new VerdictStep()).ToArray();
            plan.ChildTestSteps.AddRange(steps);

            HashSet<ITestStep> selectedSteps = null;
            if (runSelected.HasValue)
                selectedSteps = new HashSet<ITestStep>(steps.Take(runSelected.Value));
            var results = plan.Execute( Array.Empty<IResultListener>(), Array.Empty<ResultParameter>(), selectedSteps).Parameters;
            var result = results.FirstOrDefault(param => param.Name == TestPlanRun.SpecialParameterNames.StepOverrideList);
            if (runSelected.HasValue)
            {
                Assert.That(result, Is.Not.Null);
                var ids = result.Value.ToString().Split(',');
                Assert.That(ids.Length, Is.EqualTo(runSelected.Value));
                for (int i = 0; i < runSelected; i++)
                {
                    var id = steps[i].Id.ToString();
                    Assert.That(ids.Any(r => r == id));
                }
            }
            else
            {
                Assert.That(result, Is.Null);
            }
        }
        
        [Test]
        public void TestBreakConditionResultParameter([Values(true, false)] bool doBreak)
        { 
            var l = new PlanRunCollectorListener();
            var plan = new TestPlan();

            var sequenceStep = new SequenceStep();
            plan.ChildTestSteps.Add(sequenceStep);
            BreakConditionProperty.SetBreakCondition(sequenceStep, BreakCondition.BreakOnFail);

            var verdictStep = new VerdictStep() { VerdictOutput = doBreak ? Verdict.Fail : Verdict.Pass };
            sequenceStep.ChildTestSteps.Add(verdictStep);

            var run = plan.Execute(new[] { l });
            
            var breakResult = run.Parameters.FirstOrDefault(param => param.Name == TestPlanRun.SpecialParameterNames.BreakIssuedFrom);
            if (doBreak)
            {
                var stepRun = l.StepRuns.First(r => r.TestStepId == verdictStep.Id);
                Assert.That(breakResult, Is.Not.Null);
                Assert.That(breakResult.Value.ToString(), Is.EqualTo(stepRun.Id.ToString()));
            }
            else 
                Assert.That(breakResult, Is.Null);
        }

        [Test]
        public void TestCaughtBreakConditionNotPropagated([Values(true, false)] bool doCatch)
        {
            var l = new PlanRunCollectorListener();
            var plan = new TestPlan();

            var sequenceStep = new SequenceStep();
            plan.ChildTestSteps.Add(sequenceStep);

            var verdictStep = new VerdictStep() { VerdictOutput = Verdict.Fail };
            sequenceStep.ChildTestSteps.Add(verdictStep);

            BreakConditionProperty.SetBreakCondition(verdictStep, BreakCondition.BreakOnFail);
            BreakConditionProperty.SetBreakCondition(plan, BreakCondition.BreakOnFail);
            if (doCatch)
            {
                // Since sequence step breaks on error, it will 'catch' the break issued from verdictstep
                BreakConditionProperty.SetBreakCondition(sequenceStep, BreakCondition.BreakOnError); 
            }
            else
            {
                BreakConditionProperty.SetBreakCondition(sequenceStep, BreakCondition.BreakOnFail); 
            }
            
            var run = plan.Execute(new[] { l });

            var breakResult = run.Parameters.FirstOrDefault(param => param.Name == TestPlanRun.SpecialParameterNames.BreakIssuedFrom);
            if (doCatch)
            {
                Assert.That(breakResult, Is.Null);
            }
            else
            { 
                var stepRun = l.StepRuns.First(r => r.TestStepId == verdictStep.Id);
                Assert.That(breakResult, Is.Not.Null);
                Assert.That(breakResult.Value.ToString(), Is.EqualTo(stepRun.Id.ToString()));
            }
        }

        [Test]
        public void TestMoveSteps()
        {
            var plan = new TestPlan();
            var par = new ParallelStep();
            var repeat = new RepeatStep();
            var dialog = new DialogStep();
            var ifVerdict = new IfStep();

            plan.ChildTestSteps.Add(par);
            plan.ChildTestSteps.Add(repeat);
            repeat.ChildTestSteps.Add(dialog);
            repeat.ChildTestSteps.Add(ifVerdict);


            { // Set dialog as the input step for ifVerdict
                ifVerdict.InputVerdict.Step = dialog;
                Assert.That(ifVerdict.InputVerdict.Step, Is.EqualTo(dialog));
            }

            { // Move repeat step into the parallel step                        
                plan.ChildTestSteps.Remove(repeat);
                par.ChildTestSteps.Add(repeat);

                // Verify the step can still be computed after adding it back
                Assert.That(ifVerdict.InputVerdict.Step, Is.EqualTo(dialog));

                // after removing the dialog step the input step should be null.
                repeat.ChildTestSteps.Remove(dialog);
                Assert.That(ifVerdict.InputVerdict.Step, Is.Null);
            } 
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
        [Pairwise]
        public void TestPlanStepExpectedExceptionTest(
            [Values(Verdict.Pass, Verdict.Aborted, Verdict.Inconclusive, Verdict.NotSet, Verdict.Error, Verdict.Fail)] Verdict verdict,
            [Values(FailPoint.PreRun, FailPoint.Run, FailPoint.PostRun)] FailPoint failPoint)
        {
            TestPlan plan = new TestPlan();
            TestStepExpectedExceptionTest step = new TestStepExpectedExceptionTest(verdict, failPoint);

            plan.Steps.Add(step);

            TestPlanRun result = plan.Execute();
            Assert.AreEqual(verdict, result.Verdict);
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
        public void OpenAdditionalResources()
        {
            var instr1 = new DummyInstrument();
            var instr2 = new DummyInstrument();
            var instr3 = new DummyInstrument();
            var plan = new TestPlan();
            
            plan.Open();
            
            Assert.IsFalse(instr1.IsConnected);
            Assert.IsFalse(instr2.IsConnected);
            Assert.IsFalse(instr3.IsConnected);
            
            plan.Open(new IResource[]{instr1,instr2});
            Assert.IsTrue(instr1.IsConnected);
            plan.Open(new IResource[]{instr3,instr2});
            Assert.IsTrue(instr3.IsConnected);
            Assert.IsTrue(instr2.IsConnected);
            Assert.IsTrue(instr1.IsConnected);
            plan.Close();
            
            Assert.IsFalse(instr1.IsConnected);
            Assert.IsFalse(instr2.IsConnected);
            Assert.IsFalse(instr3.IsConnected);
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
        public void TestPlanResourceCrashTest()
        {
            var inst = new InstrumentTest() {CrashPhase = InstrumentTest.InstrPhase.Open};
            var step = new InstrumentTestStep();
            step.Instrument = inst;
            
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(step);

            string logString = null;
            InstrumentSettings.Current.Add(inst);
            try
            {
                TestTraceListener trace = new TestTraceListener();
                Log.AddListener(trace);
                var run = plan.Execute();
                Log.Flush();
                logString = trace.GetLog();
                var err = trace.ErrorMessage;
                Assert.AreEqual(Verdict.Error, run.Verdict);
                // improvement: only one error in the log.
                Assert.AreEqual(2, err.Count);
                // the log should not be really long.
                Assert.IsTrue(logString.Split('\n').Length < 70);
            }
            finally
            {
                InstrumentSettings.Current.Remove(inst);    
            }
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

        class HashCheckResultListener : ResultListener
        {
            TestPlan plan;
            public HashCheckResultListener(TestPlan plan) => this.plan = plan;

            public override void OnTestPlanRunStart(TestPlanRun planRun)
            {
                string getHash(string testPlanXml)
                {
                    // this is the same code as inside TestPlanRun.
                    using (var algo = System.Security.Cryptography.SHA1.Create())
                        return BitConverter.ToString(algo.ComputeHash(System.Text.Encoding.UTF8.GetBytes(testPlanXml)), 0, 8).Replace("-", string.Empty);
                }

                var newxml = new TapSerializer().SerializeToString(plan);

                if (newxml != planRun.TestPlanXml)
                    planRun.MainThread.Abort(); // cause Verdict.Abort.

                if(planRun.Hash != getHash(newxml))
                    planRun.MainThread.Abort();

            }
        }

        [Test]
        public void TestPlanHash()
        {
            var plan = new TestPlan();
            var step = new LogStep { LogMessage = "A" };
            plan.ChildTestSteps.Add(step);
            plan.Open(new[] { new HashCheckResultListener(plan) });
            Assert.AreEqual(Verdict.NotSet, plan.Execute().Verdict);
            step.LogMessage = "B";
            Assert.AreEqual(Verdict.NotSet, plan.Execute().Verdict);
            plan.Close();
        }

        class AbortImmediatelyResultListener : ResultListener
        {
            public override void OnTestPlanRunStart(TestPlanRun planRun) => planRun.MainThread.Abort();
        }

        [Test]
        public void TestPlanAbortFromResultListener()
        {
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(new DelayStep());
            var run = plan.Execute(new[] { new AbortImmediatelyResultListener() });
            Assert.AreEqual(Verdict.Aborted, run.Verdict);
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
            InstrumentSettings.Current.Clear();
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

            Assert.AreEqual(1, pl.StepRuns.Count);
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
        /// <summary>
        /// Tests that if the SuggestedNextStep is set to the current ID, thn the testStep will be repeated. 
        /// </summary>
        [Test]
        public void SuggestedNextStepTest()
        {
            using (Session.Create(SessionOptions.OverlayComponentSettings))
            { 
                var plan = new TestPlan();
                var parallelStep = new ParallelStep();
                var nextStepRepeater = new SuggestedNextStepRepeater();
                nextStepRepeater.RepeatCount = 10;
                parallelStep.ChildTestSteps.Add(nextStepRepeater);
                plan.ChildTestSteps.Add(parallelStep);
                PlanRunCollectorListener planRunListener = new PlanRunCollectorListener();
                ResultSettings.Current.Add(planRunListener);

                var planRun = plan.Execute();
                
                var stepRuns = planRunListener.StepRuns;

                //We compare repeatcount + 1 as the parallelStep is also counted in StepsRun.
                Assert.AreEqual(nextStepRepeater.RepeatCount + 1, stepRuns.Count);            
            }
        }

        public class SuggestedNextStepRepeater : TestStep
        {
            public int RepeatCount { get; set; }
            public int Repeats { get; set; } 

            public override void Run()
            {
                if(Repeats < RepeatCount -1)
                {
                    this.StepRun.SuggestedNextStep = this.Id;
                    Repeats++;
                }
            }
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

        class PrePostPlanTestStep : TestStep
        {
            public bool VerifyPrePlanRun;
            public bool VerifyPostPlanRun;
            public bool VerifyRun;
            public bool VerifyDefer;
            public override void PrePlanRun()
            {
                base.PrePlanRun();
                VerifyPrePlanRun = false;
                VerifyPostPlanRun = false;
                VerifyRun = false;
                VerifyDefer = false;
                Assert.IsNotNull(PlanRun);
                Assert.IsNull(StepRun);
                VerifyPrePlanRun = true;
            }
            public override void Run()
            {
                VerifyRun = true;
                Assert.IsNotNull(PlanRun);
                Assert.IsNotNull(StepRun);
                Results.Defer(() =>
                {
                    Assert.IsNotNull(PlanRun);
                    Assert.IsNotNull(StepRun);
                    VerifyDefer = true;
                    UpgradeVerdict(Verdict.Pass);
                });
            }

            public override void PostPlanRun()
            {
                base.PostPlanRun();
                Assert.IsNotNull(PlanRun);
                Assert.IsNull(StepRun);
                VerifyPostPlanRun = true;
            }
        }

        [Test]
        public void VerifyPrePostPlanRun()
        {
            var tp = new TestPlan();
            var step = new PrePostPlanTestStep();
            tp.ChildTestSteps.Add(step);
            for (int i = 0; i < 4; i++)
            {
                var run = tp.Execute();
                Assert.AreEqual(Verdict.Pass, run.Verdict);
                Assert.IsTrue(step.VerifyDefer);
                Assert.IsTrue(step.VerifyRun);
                Assert.IsTrue(step.VerifyPostPlanRun);
                Assert.IsTrue(step.VerifyPrePlanRun);
            }
        }

        class DeferredResultsStep : TestStep
        {
            public bool IsDone = false;
            public Semaphore sem = new Semaphore(0, 1);
            public Semaphore canFinish = new Semaphore(0, 1);
            public override void Run()
            {
                Results.Defer(() =>
                {
                    sem.Release();
                    canFinish.WaitOne();
                });
            }
        }

        [Test]
        public void DeferAndAbort([Values(true, false)] bool WrapSequence)
        {
            var defer = new DeferredResultsStep();
            var plan = new TestPlan();
            if (WrapSequence)
            {
                var sequenceStep = new SequenceStep();
                sequenceStep.ChildTestSteps.Add(defer);
                plan.ChildTestSteps.Add(sequenceStep);
            }
            else
            {
                plan.ChildTestSteps.Add(defer);
            }

            var sem2 = new Semaphore(0, 1);
            var thread = TapThread.Start(() =>
            {
                try
                {
                    plan.Execute();
                }
                finally
                {
                    sem2.Release();
                }
            });
            defer.sem.WaitOne();
            thread.Abort();
            try
            {
                bool hangs = sem2.WaitOne(50) == false;
                Assert.IsTrue(hangs, "Deferred results step should wait.");
            }
            finally
            {
                defer.canFinish.Release();    
            }
            bool isDone = sem2.WaitOne(10000);
            if(!isDone)
                Assert.Fail("Test plan timed out");
            Assert.IsTrue(isDone);
        }
        [Test]
        public void RecursiveTestPlanReferenceTest()
        {
            TestPlan plan = new TestPlan();
            var step = new TestPlanReference();
            string filePath = "plan.TestPlan";
            step.Filepath.Text = filePath;
            plan.Steps.Add(step);
            plan.Save(filePath);

            // this threw an exception at one point.
            step.LoadTestPlan();

            TestTraceListener trace = new TestTraceListener();
            Log.AddListener(trace);
            var plan2 = TestPlan.Load(filePath);
            Log.RemoveListener(trace);
            StringAssert.Contains("Test plan reference is trying to load itself leading to recursive loop.", trace.GetLog());
            File.Delete(filePath);
        }
        
        [Test]
        public void ParameterizedTestPlanReferenceTest()
        {
            var path = Path.GetTempFileName();
            
            { // Create test plan with external parameter
                var subplan = new TestPlan();
                var logStep = new LogStep() { LogMessage = "Initial Value" };
                var logInfo = TypeData.GetTypeData(logStep);
                subplan.ChildTestSteps.Add(logStep);
                subplan.ExternalParameters.Add(logStep, logInfo.GetMember(nameof(logStep.LogMessage)));
                subplan.Save(path); 
            }

            try
            { // Add the created test plan as a child step of a sweep loop
                var reference = new TestPlanReference { Filepath = { Text = path } };
                var sweep = new SweepLoop() {
                    ChildTestSteps =
                    {
                        reference
                    }
                }; 

                string GetLogMessageParameter(AnnotationCollection a)
                {
                    var avail = a.Get<IMembersAnnotation>().Members.FirstOrDefault(m => m.Name == "Sweep Parameters")
                        ?.Get<IAvailableValuesAnnotationProxy>();
                    var v = avail?.AvailableValues.FirstOrDefault(v => v.Name == "ExpandedMemberData");
                    return v?.Get<IStringValueAnnotation>()?.Value;
                }

                var a = AnnotationCollection.Annotate(sweep);
                Assert.That(GetLogMessageParameter(a), Is.Null);
                reference.LoadTestPlan();
                a.Read();
                Assert.That(GetLogMessageParameter(a), Is.EqualTo("Log Message"));
            }
            finally
            {
                File.Delete(path);
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
                    step.Filepath.Text = "<TestPlanDir>/TestDir/plan.TestPlan";
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
            var bigplan = TestPlan.Load("RelativeTestPlanTest_TestDir/plan.TestPlan");
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

            tp.ExternalParameters.Add(delaystep, TypeData.GetTypeData(delaystep).GetMember("Prop"), "Prop");
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
            Assert.IsNotNull(TypeData.GetTypeData(step2).GetMember("Prop"), "Could not find external parameter property on testplan");

            var sweep = new OpenTap.Plugins.BasicSteps.SweepLoop();

            tp.ChildTestSteps.Add(sweep);
            tp.ChildTestSteps.Remove(step2);
            sweep.ChildTestSteps.Add(step2);

            sweep.SweepParameters = new List<OpenTap.Plugins.BasicSteps.SweepParam> { new OpenTap.Plugins.BasicSteps.SweepParam(new List<IMemberData> {  TypeData.GetTypeData(step2).GetMember("Prop") }, 1337) };

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
        public void ChildITestStepLazy()
        {
            var prevResourceManager = EngineSettings.Current.ResourceManagerType;
            try
            {
                 
                EngineSettings.Current.ResourceManagerType = new LazyResourceManager();
                ChildITestStep();

            }
            finally
            {
                EngineSettings.Current.ResourceManagerType = prevResourceManager;
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
                if (EngineSettings.Current.ResourceManagerType is LazyResourceManager)
                {
                    return instr.IsConnected ? Verdict.Fail : Verdict.Pass;
                }
                return instr.IsConnected ? Verdict.Pass : Verdict.Fail;
            }

            var step = new TestITestStep();
            step.Instrument = instr;
            plan.Steps.Add(new DelegateStep { Action = checkInstr });
            plan.Steps.Add(seq);
            plan.Steps.Add(new DelegateStep { Action = checkInstr });
            seq.ChildTestSteps.Add(step);
            
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
        public void ComponentSettingMetadataTest()
        {
            UserInput.SetInterface(new ComponentSettingPrompt());
            try
            {
                TestComponentSettingList.Current.Clear();
                TestComponentSettingList.Current.Add(new TestComponentSetting());

                EngineSettings.Current.PromptForMetaData = true;

                {
                    var plan = new TestPlan();
                    plan.Steps.Add(new ComponentSettingStep());

                    var run = plan.Execute();
                    Assert.IsFalse(run.FailedToStart);
                    Assert.AreEqual(Verdict.Pass, run.Verdict);
                }

                TestComponentSettingList.Current.OfType<TestComponentSetting>().ForEach(f => f.MetaComment = "Test meta data comment");

                {
                    var plan = new TestPlan();
                    plan.Steps.Add(new ComponentSettingStep());

                    {
                        var run = plan.Execute();
                        Assert.IsFalse(run.FailedToStart);
                        Assert.AreEqual(Verdict.Pass, run.Verdict);
                    }
                }
            }
            finally
            {
                UserInput.SetInterface(null);
                EngineSettings.Current.PromptForMetaData = false;
            }
        }

        [Test]
        public void DutPromptMetadataTest()
        {
            UserInput.SetInterface(new DutInfoPrompt());

            try
            {
                DutSettings.Current.Clear();
                DutSettings.Current.Add(new TestDut());

                EngineSettings.Current.PromptForMetaData = true;

                {
                    var plan = new TestPlan();
                    plan.Steps.Add(new DutStep() { Dut = DutSettings.Current.OfType<TestDut>().FirstOrDefault() });

                    var run = plan.Execute();
                    Assert.IsFalse(run.FailedToStart);
                    Assert.AreEqual(Verdict.Pass, run.Verdict);
                }

                DutSettings.Current.OfType<TestDut>().ToList().ForEach(f => f.Comment = "test");

                {
                    var plan = new TestPlan();
                    plan.Steps.Add(new DutStep() { Dut = DutSettings.Current.OfType<TestDut>().FirstOrDefault() });

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
                EngineSettings.Current.PromptForMetaData = false;
            }
        }

        public class StepWithArray : TestStep
        {
            public double[] ArrayValues { get; set; } = Array.Empty<double>();
            public override void Run()
            {
                ArrayValues = new double[] { 1, 2, 3, 4, 5, 6 };
            }
        }

        [Test]
        public void TestStepWithArray()
        {
            PlanRunCollectorListener pl = new PlanRunCollectorListener();
            var arrayStep = new StepWithArray();
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(arrayStep);
            var planrun = plan.Execute(new[] { pl });

            var steprun = pl.StepRuns.First();
            Assert.IsTrue(steprun.Parameters["ArrayValues", ""].ToString() == new NumberFormatter(System.Globalization.CultureInfo.CurrentCulture){UseRanges = false}.FormatRange(arrayStep.ArrayValues));
        }

        [Test]
        [Ignore("This test is very unstable in limited CPU situations. (for example on a server that is already busy with other things)")]
        public void TestPlanDeferError()
        {
            //
            // This unit test verifies that the verdict of the test plan behaves predictably when a test step throws an exception or aborts the plan
            // during defer actions. It also tests what happens in general when a step is being aborted when something else is happening in parallel.
            //

            /* // instabilities can be tested by doing a bunch of processing in other threads while testing.
            bool workhard = true;
            for(int i = 0; i < 16; i++)
            {
                TapThread.Start(() =>
                {
                    while (workhard)
                    {
                        Enumerable.Range(0, 1000).Count().ToString().GetHashCode();
                    }
                });
            }*/
            var plan = new TestPlan();
            var err = new ThrowInDefer() { WaitMs = 10 };
            var delay = new DelayStep() { DelaySecs = 1.0 };
            
            
            plan.ChildTestSteps.Add(err);
            plan.ChildTestSteps.Add(delay);
            
            var prevAbortPlanType = EngineSettings.Current.AbortTestPlan;
            var sw = Stopwatch.StartNew();
            // The abort mode decides which verdict the plan but also the delay step will get.
            var abortModes = new[] { EngineSettings.AbortTestPlanType.Step_Error, (EngineSettings.AbortTestPlanType)0 };
            foreach(var abortMode in abortModes)
            {
                EngineSettings.Current.AbortTestPlan = abortMode;
                for (int i = 0; i < 5; i++)
                {
                    bool isErrorDuringDelay = i != 4;
                    err.Throw = i % 2 == 0;
                    if (!isErrorDuringDelay)
                    {
                        // in this case the error will happen after delay has run
                        err.WaitMs = 50;
                        delay.DelaySecs = 0;
                    }
                    else
                    {
                        // in this case the error will happen during delay
                        err.WaitMs = 10;
                        delay.DelaySecs = 1.0;
                    }

                    if(isErrorDuringDelay && abortMode.HasFlag(EngineSettings.AbortTestPlanType.Step_Error) == false)
                    {
                        // since we dont abort on step errors, we dont want to wait 1s for this.
                        delay.DelaySecs = 0.1;
                    }

                    
                    for (int iterations = 0; iterations < 2; iterations++)
                    {
                        // Iterate a bit to faster catch race condition errors.
                        PlanRunCollectorListener pl = new PlanRunCollectorListener();
                        TestPlanRun planRun = plan.ExecuteAsync(new[] { (IResultListener)pl }, null, null, CancellationToken.None).Result;
                        
                        var delayRun = pl.StepRuns.FirstOrDefault(x => x.TestStepId == delay.Id);
                        var errorStepRun = pl.StepRuns.FirstOrDefault(x => x.TestStepId == err.Id);

                        // The error step verdict is always 'Error'.
                        Assert.AreEqual(Verdict.Error, errorStepRun.Verdict);

                        // The plan verdict depends on abortMode.
                        if (abortMode.HasFlag(EngineSettings.AbortTestPlanType.Step_Error))
                            Assert.AreEqual(Verdict.Aborted, planRun.Verdict);
                        else
                            Assert.AreEqual(Verdict.Error, planRun.Verdict);

                        // the verdict of delayRun depends on if the delay step completed before the error or abortMode.
                        if (abortMode.HasFlag(EngineSettings.AbortTestPlanType.Step_Error))
                        {
                            if (isErrorDuringDelay)
                                Assert.AreEqual(Verdict.Aborted, delayRun.Verdict);
                            else
                                Assert.AreEqual(Verdict.NotSet, delayRun.Verdict);
                        }
                        else
                            Assert.AreEqual(Verdict.NotSet, delayRun.Verdict);

                    }
                }
            }
            EngineSettings.Current.AbortTestPlan = prevAbortPlanType;
            Debug.WriteLine("TestPlanDeferError {0}", sw.Elapsed);
            //workhard = false;
        }

        class ComponentSettingPrompt : IUserInputInterface
        {
            public void RequestUserInput(object dataObject, TimeSpan Timeout, bool modal)
            {
                var sub = AnnotationCollection.Annotate(dataObject).Get<IForwardedAnnotations>().Forwarded.ToArray();
                foreach(string name in new []{"MetaComment", "MetaComment2"}){
                    var comments = sub.Where(x => x.Get<IMemberAnnotation>().Member.Name == name);
                    Assert.AreEqual(1, comments.Count());
                    var comment = comments.First();

                    Assert.IsNotNull(comment != null);
                    comment.Get<IStringValueAnnotation>().Value = "Just another meta comment";
                    comment.Write();
                }
                
            }
        }

        public class TestComponentSettingList : ComponentSettingsList<TestComponentSettingList, TestComponentSetting> { }

        public class TestComponentSetting : ComponentSettings<TestComponentSetting>
        {
            [MetaData(true)]
            [Display("MetaComment", Description: "Some comment for meta data")]
            public string MetaComment { get; set; }
            
            [MetaData(true)]
            [Display("MetaComment2", Description: "Some comment for meta data 2")]
            public string MetaComment2 { get; set; }

            public TestComponentSetting() { }
        }

        public class ComponentSettingStep : TestStep
        {
            public override void Run()
            {
                if (string.Compare("Just another meta comment", TestComponentSetting.Current.MetaComment, true) != 0)
                    throw new InvalidOperationException();
                if (string.Compare("Just another meta comment", TestComponentSetting.Current.MetaComment2, true) != 0)
                    throw new InvalidOperationException();
                UpgradeVerdict(Verdict.Pass);
            }
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
        
        public class TestDut : Dut
        {
        }

        public class DutStep : TestStep
        {
            public Dut Dut { get; set; }
            public int Sleep { get; set; }
            public override void Run()
            {
                if(Sleep != 0)
                    TapThread.Sleep(Sleep);
                if (string.Compare("Some comment", Dut.Comment, true) != 0)
                    throw new InvalidOperationException();
                if (Dut.IsConnected == false)
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
                    tm.Enqueue(new TapThread(TapThread.Current,() =>
                    {

                        //Thread.Sleep(10);
                        Interlocked.Increment(ref counter);
                        sem.Release();
                    }));
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

        /// <summary>
        /// This test proves that each WorkQueue can process things in sequence, while working in parallel.
        /// </summary>
        [Test]
        public void DeferSynchronization()
        {
            int testCount = 1000;
            
            var tw = new WorkQueue(WorkQueue.Options.None, "Test");
            var tw2 = new WorkQueue(WorkQueue.Options.None, "Test");
            int counter = 0;
            int counter2 = 0;
            var sem = new SemaphoreSlim(0);
            var sem2a = new SemaphoreSlim(0);
            var sem2b = new SemaphoreSlim(0);
            
            // enqueue a bunch of work.
            for (int i = 0; i < testCount; i++)
            {
                tw.EnqueueWork(() =>
                {
                    sem2a.Wait();
                    counter += 1;
                    sem.Release();
                });
                tw2.EnqueueWork(() =>
                {
                    sem2b.Wait();
                    counter2 += 1;
                    sem.Release();
                });
            }

            // Verify execution in parallel bit by bit (first half only).
            for (int i = 0; i < testCount / 2; i++)
            {
                sem2a.Release();
                sem2b.Release();
                for (int j = 0; j < 2; j++)
                {
                    if (!sem.Wait(TimeSpan.FromSeconds(10)))
                    {
                        throw new TimeoutException();
                    }
                }
                Assert.AreEqual(counter, i + 1);
                Assert.AreEqual(counter2, i + 1);
            }

            // second half, execute as fast as possible without locks.
            sem2a.Release(testCount / 2);
            sem2b.Release(testCount / 2);
            for(int i = 0; i < testCount/2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    if (!sem.Wait(TimeSpan.FromSeconds(10)))
                    {
                        throw new TimeoutException();
                    }
                }
            }
            // verify it works at full speed.
            Assert.AreEqual(testCount, counter);
            Assert.AreEqual(testCount, counter2);
        }

        [Test]
        public void RunMassiveSynchronousPlan()
        {
            // run an excessively long test plan.

            int Count = 500;
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
        
        class DeferringStep : TestStep
        {
            public override void Run()
            {
                Results.Defer(() => { });
            }
        }
        
        [Test]
        public void RepeatRunDeferPlan()
        {
            var seq = new ParallelStep();
            for (int i = 0; i < 4; i++)
            {
                var def = new DeferringStep();
                seq.ChildTestSteps.Add(def);
            }

            var plan = new TestPlan();
            plan.ChildTestSteps.Add(seq);
            for (int i = 0; i < 100; i++)
            {
                var sem = new Semaphore(0, 1);
                var trd = TapThread.Start(() => { plan.Execute();
                    sem.Release();
                });

                if (!sem.WaitOne(100000))
                {
                    trd.Abort();
                    Assert.Fail("Deadlock occured in test plan");
                }
            }
        }

        class PlatformCheckResultListener : ResultListener
        {
            public bool TestPlanRunStarted;
            public string CommentWas;
            public string SerialWas;
            
            public override void OnTestPlanRunStart(TestPlanRun planRun)
            {
                CommentWas = planRun.Parameters["Comment", "DUT"]?.ToString();
                SerialWas = planRun.Parameters["Serial"]?.ToString();
                TestPlanRunStarted = true;
                base.OnTestPlanRunStart(planRun);
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
            string expectedComment = "Some comment";
            var listener = new PlatformCheckResultListener();
            int requestCount = 0;
            bool failure = false;

            void waitFormInput(object obj, TimeSpan timeout, bool modal)
            {
                Assert.IsFalse(listener.TestPlanRunStarted);
                Thread.Sleep(20); // a short sleep to make sure that the error would appear.
                var mpo = obj as MetadataPromptObject;
                foreach(var dut in mpo.Resources.OfType<DummyDut>())
                {
                    dut.Comment = expectedComment;
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
                DummyDut dut1 = new DummyDut() {Comment = "Comment0", ID = "ID0"};
                DutSettings.Current.Clear();
                DutSettings.Current.Add(dut1);
                TestPlan plan = new TestPlan();
                DutStep step = new DutStep();
                step.Dut = dut1;
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
            Assert.AreEqual(expectedComment, listener.CommentWas);
            if(!lazy)
                Assert.AreEqual(DummyDut.SerialNumber, listener.SerialWas);
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
                        Dut = dut1,
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
            
            //---------------------------------------------------------------------------------------------------------
            var tapThread1 = TapThread.Start(() =>
            {
                try
                {
                    plan1.Execute(new IResultListener[] {new TestPlanResultListener(ewhPlan1IsRunning)}, null,
                        new HashSet<ITestStep>(plan1.ChildTestSteps));
                }
                finally
                {
                    //TestPlan.Current is null after the TestPlan starts running
                    ewhPlan1StoppedRunning.Set();
                }
            });

            //---------------------------------------------------------------------------------------------------------            
            var tapThread2= TapThread.Start(() =>
            {
                try
                {
                    plan2.Execute(new IResultListener[] {new TestPlanResultListener(ewhPlan2IsRunning)}, null,
                        new HashSet<ITestStep>(plan2.ChildTestSteps));
                }
                finally
                {
                    ewhPlan2StoppedRunning.Set();
                }
            });

            if(!WaitHandle.WaitAll(new WaitHandle[] { ewhPlan1IsRunning, ewhPlan2IsRunning }, 120000))
                Assert.Fail("Test plan running timed out.");
            
            Assert.AreEqual(2, plan1.ChildTestSteps.Count);
            Assert.AreEqual(false, tapThread1.AbortToken.IsCancellationRequested);
            Assert.AreEqual(true, plan1.IsRunning, "1 Running");

            Assert.AreEqual(3, plan2.ChildTestSteps.Count);
            Assert.AreEqual(false, tapThread2.AbortToken.IsCancellationRequested);
            Assert.AreEqual(true, plan2.IsRunning, "2 Running");


            tapThread1.Abort();
            Assert.AreEqual(true, tapThread1.AbortToken.IsCancellationRequested, "Abort 1.1");
            Assert.AreEqual(false, tapThread2.AbortToken.IsCancellationRequested, "Abort 1.2");

            tapThread2.Abort();
            Assert.AreEqual(true, tapThread1.AbortToken.IsCancellationRequested, "Abort 2.1");
            Assert.AreEqual(true, tapThread2.AbortToken.IsCancellationRequested, "Abort 2.2");

            WaitHandle.WaitAll(new WaitHandle[] { ewhPlan1StoppedRunning, ewhPlan2StoppedRunning });

            Assert.AreEqual(false, plan1.IsRunning, "1 Running");
            Assert.AreEqual(false, plan2.IsRunning, "2 Running");
        }

        [Test]
        public void DuplicateStepInPlan()
        {
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(new SequenceStep());
            try
            {
                plan.ChildTestSteps.Add(plan.ChildTestSteps[0]);
                Assert.Fail("This should have failed");
            }
            catch(InvalidOperationException)
            {

            }

            Assert.AreEqual(1, plan.ChildTestSteps.Count);

            var startId = plan.ChildTestSteps[0].Id;
            var seq2 = new SequenceStep() { Id = startId };
            plan.ChildTestSteps.Add(seq2);
            Assert.AreNotEqual(startId, seq2.Id);
            Assert.AreEqual(plan.ChildTestSteps[0].Id, startId);

        }

        [Test]
        public void TestPlanDeserializeError()
        {
            var listener = new TestTraceListener();
            var dut = new DummyDut() {Name = "DUT_TEST"};
            try
            {
                var plan = new TestPlan();
                var step = new DutStep();
                DutSettings.Current.Add(dut);
                step.Dut = dut;
                plan.Steps.Add(step);
                using (var buffer = new MemoryStream())
                {
                    plan.Save(buffer);
                    buffer.Seek(0, SeekOrigin.Begin);
                    var str = Encoding.UTF8.GetString(buffer.ToArray());
                    DutSettings.Current.Remove(dut);
                    Log.AddListener(listener);
                    var serializer = new TapSerializer();
                    serializer.Deserialize(buffer);
                }

                Log.Flush();
                // there should be an error message
                // since the DUT was not available for de-serialization.
                Assert.IsTrue(listener.ErrorMessage.Count > 0);
                
            }
            finally
            {
                Log.RemoveListener(listener);
                DutSettings.Current.Remove(dut);
            }
        }

        [Test]
        public void DefaultPlanMetadata()
        {
            PlanRunCollectorListener pl1 = new PlanRunCollectorListener();
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(new ManySettingsStep());
            var run = plan.Execute(new[] {pl1});
            
            var parameters = run.Parameters;
            Assert.IsNotNull(parameters.Find(("Station", "Operator")));
            Assert.IsNull(parameters.Find(("Allow Metadata Prompt", "Engine")));
            var stepParameters = pl1.StepRuns.FirstOrDefault().Parameters;
            Assert.IsNotNull(stepParameters.Find("A", ""));
            Assert.IsNotNull(stepParameters.Find("Verdict", ""));
            Assert.IsNotNull(stepParameters.Find("Duration", ""));
            Assert.IsNull(stepParameters.Find(nameof(ManySettingsStep.Id), "")); // Id is not saved as Parameter
            Assert.IsNotNull(run.TestPlanXml);
            Assert.IsNotNull(run.Hash);
        }

        class CheckTestPlanXmlListener : ResultListener
        {   
            public override void OnTestPlanRunStart(TestPlanRun planRun)
            {
                base.OnTestPlanRunStart(planRun);
                if (planRun.TestPlanXml == null)
                    throw new Exception("Test plan XML is null!!");
            }
        }

        [Test]
        public void TestLoadCacheAndRun([Values(true, false)] bool cacheXml)
        {
            // this unit test checks that the TestPlanRun.TestPlanXml is not null
            // depending on CacheXML=true.
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(new LogStep());
            var memstr = new MemoryStream();
            plan.Save(memstr);
            memstr.Seek(0, SeekOrigin.Begin);
            plan = TestPlan.Load(memstr, "test", cacheXml);
            var run = plan.Execute(new []{new CheckTestPlanXmlListener()});
            Assert.AreEqual(Verdict.NotSet, run.Verdict);
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

                Assert.AreEqual(1, ResourceManageValidator.StageCounter[TestPlanExecutionStage.Open]);

                var run = tp.Execute();

                Assert.AreEqual(1, ResourceManageValidator.StageCounter[TestPlanExecutionStage.Open]);

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
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[TestPlanExecutionStage.Open]);
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[TestPlanExecutionStage.Execute]);
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[TestPlanExecutionStage.PrePlanRun]);
            }

            public override void Run()
            {
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[TestPlanExecutionStage.Open]);
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[TestPlanExecutionStage.Execute]);
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[TestPlanExecutionStage.Run]);

                UpgradeVerdict(Verdict.Pass);
            }

            public override void PostPlanRun()
            {
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[TestPlanExecutionStage.Open]);
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[TestPlanExecutionStage.Execute]);
                Assert.AreEqual(1, ResourceManageValidator.StageCounter[TestPlanExecutionStage.PostPlanRun]);
            }
        }

        public class ResourceManageValidator : IResourceManager
        {
            public static Dictionary<TestPlanExecutionStage, int> StageCounter = new Dictionary<TestPlanExecutionStage, int>();

            public IEnumerable<IResource> Resources => StaticResources;

            public IEnumerable<IResource> StaticResources { get; set; }
            public List<ITestStep> EnabledSteps { get; set; }

            public event Action<IResource> ResourceOpened;

            public void BeginStep(TestPlanRun planRun, ITestStepParent item, TestPlanExecutionStage stage, CancellationToken cancellationToken)
            {
                if (!StageCounter.ContainsKey(stage)) StageCounter[stage] = 0;
                StageCounter[stage]++;
                if (stage == TestPlanExecutionStage.Execute)
                {
                    if (item is TestPlan testplan)
                    {
                        var resources = ResourceManagerUtils.GetResourceNodes(StaticResources.Cast<object>().Concat(EnabledSteps));
                        testplan.StartResourcePromptAsync(planRun, resources.Select(res => res.Resource));
                    }
                }
            }

            public void EndStep(ITestStepParent item, TestPlanExecutionStage stage)
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

        class CrashInstrument : Instrument
        {
            public bool OpenThrow { get; set; }
            public bool OpenWait { get; set; }
            public bool CloseThrow { get; set; }
            public bool ClosedDuringOpen { get; set; }
            bool isOpening;

            public bool CloseCalled { get; set; }
            public override void Open()
            {
                if (IsConnected)
                    throw new InvalidOperationException("Instrument already connected");
                base.Open();
                isOpening = true;
                try
                {
                    if (OpenWait)
                        Thread.Sleep(20);

                    if (OpenThrow)
                        throw new Exception("Intended failure");
                }
                finally
                {
                    isOpening = false;
                }    
            }
            
            public override void Close()
            {
                CloseCalled = true;
                if (isOpening)
                {
                    ClosedDuringOpen = true;
                    Assert.Fail("Close called before open done.");
                }

                if (CloseThrow)
                    throw new Exception("Intended failure");
            }
        }

        [Test]
        public void OpenCloseOrder()
        {
            var instrA = new CrashInstrument {OpenThrow = true, CloseThrow = true, Name= "A"};
            var instrB = new CrashInstrument {OpenWait = true, Name= "B"};
            var parallel = new ParallelStep();
            // create a bunch of steps to make sure that we test race conditions
            // in the resource managers.
            for (int i = 0; i < 10; i++)
            {
                var stepA = new InstrumentTestStep { Instrument = instrA };
                var stepB = new InstrumentTestStep { Instrument = instrB };
                parallel.ChildTestSteps.Add(stepA);
                parallel.ChildTestSteps.Add(stepB);
            }
            
            var plan = new TestPlan();
            plan.Steps.Add(parallel);
            
            var run = plan.Execute();
            Assert.AreEqual(Verdict.Error, run.Verdict);
            Assert.IsFalse(instrA.ClosedDuringOpen);
            Assert.IsFalse(instrB.ClosedDuringOpen); 
            
            // close has to be called even though Open failed.
            Assert.IsTrue(instrA.CloseCalled, "instrA.ClosedCalled == false");
            Assert.IsTrue(instrB.CloseCalled, "instrB.ClosedCalled == false"); 
        }
        
        [Test]
        public void OpenCloseOrderSmall()
        {
            var instrA = new CrashInstrument {OpenThrow = true, CloseThrow = true, Name= "A"};
            
            var parallel = new ParallelStep();
            for (int i = 0; i < 10; i++)
                parallel.ChildTestSteps.Add(new InstrumentTestStep { Instrument = instrA });
            
            var plan = new TestPlan();
            plan.Steps.Add(parallel);
            
            var run = plan.Execute();
            Assert.AreEqual(Verdict.Error, run.Verdict);
            Assert.IsFalse(instrA.ClosedDuringOpen);
            
            // close has to be called even though Open failed.
            Assert.IsTrue(instrA.CloseCalled, "instrA.ClosedCalled == false");
        }
        

        [Test]
        public void TestFastTapThreads()
        {
            using (TapThread.UsingThreadContext())
            {
                var startThread = TapThread.Current;
                long startedThreads = 0;
                var sem = new Semaphore(0, 100);
                void newThread()
                {
                    Interlocked.Increment(ref startedThreads);
                    TapThread.Start(() =>
                    {
                        try
                        {
                            if (TapThread.Current.AbortToken.IsCancellationRequested)
                                return;
                            if (startedThreads >= 100)
                                startThread.Abort("end");
                            newThread();
                        }
                        finally
                        {
                            sem.Release();
                        }
                    });
                }
                newThread();
                for (long i = 0; i < startedThreads; i++)
                    sem.WaitOne();
            }
        }
        
        [Test]
        public void TestFastTapThreadsWaitWait()
        {
            using (TapThread.UsingThreadContext())
            {
                int concurrentThreads = 20;
                var evt = new ManualResetEvent(false);
                long startedThreads = 0;
                var sem = new Semaphore(0,concurrentThreads);
                void newThread()
                {
                    TapThread.Start(() =>
                    {
                        if (Interlocked.Increment(ref startedThreads) == concurrentThreads)
                            evt.Set();
                        
                        try
                        {
                            evt.WaitOne();
                            TapThread.Sleep(10);
                        }
                        finally
                        {
                            sem.Release();
                        }
                    });
                }

                for (int i = 0; i < concurrentThreads; i++)
                    newThread();
                for (long i = 0; i < concurrentThreads; i++)
                    sem.WaitOne();
            }
        }
        
        [Test]
        [Repeat(10)]
        public void OpenCloseOrderLazyRM()
        {
            var lastrm = EngineSettings.Current.ResourceManagerType;
            EngineSettings.Current.ResourceManagerType = new LazyResourceManager();
            try
            {
                OpenCloseOrder();
            }
            finally
            {
                EngineSettings.Current.ResourceManagerType = lastrm;
            }
        }
        
        [Test]
        [Repeat(10)]
        public void OpenCloseOrderLazyRM_Reduced()
        {
            var lastrm = EngineSettings.Current.ResourceManagerType;
            EngineSettings.Current.ResourceManagerType = new LazyResourceManager();
            try
            {
                OpenCloseOrderSmall();
            }
            finally
            {
                EngineSettings.Current.ResourceManagerType = lastrm;
            }
        }
        
        private class DeferredResultsStep : TestStep
        {
            public bool DeferredDone;
            public bool FailedDeferExecuted;
            public override void Run()
            {
                DeferredDone = false;
                FailedDeferExecuted = false;
                var sem = new Semaphore(0, 1);
                Results.Defer(() =>
                {
                    sem.WaitOne();
                    DeferredDone = true;
                    Log.Info("Deferred Step also done");
                });

                TapThread.Start(() =>
                {
                    try
                    {
                        Results.Defer(() =>
                        {
                            // this should fail.
                            Log.Error("This should never be called!!!");
                        });
                        FailedDeferExecuted = false;
                    }
                    catch
                    {
                        FailedDeferExecuted = true;
                    }

                    sem.Release();
                });
            }
        }

        [Test]
        public void TestDeferResultsStepInParallel()
        {
            var plan = new TestPlan();
            var parallel = new ParallelStep();
            var defer = new DeferredResultsStep();
            plan.Steps.Add(parallel);
            parallel.ChildTestSteps.Add(defer);
            plan.Execute();
            Assert.IsTrue(defer.DeferredDone);
            Assert.IsTrue(defer.FailedDeferExecuted);
        }

        private class DeferredResultsStep2 : TestStep
        {
            public ManualResetEvent CompleteDefer = new ManualResetEvent(false);
            public ManualResetEvent DeferStarted = new ManualResetEvent(false);
            public ManualResetEvent RunCompleted = new ManualResetEvent(false);
            public override void Run()
            {
                Results.Defer(() =>
                {
                    DeferStarted.Set();
                    CompleteDefer.WaitOne();
                    Log.Info("Deferred Step also done");
                });
                RunCompleted.Set();
            }
        }

        [Test]
        public void TestExecuteWaitForDefer_Simpler()
        {
            var plan = new TestPlan();
            var defer = new DeferredResultsStep2();
            plan.Steps.Add(defer);
            Task<TestPlanRun> t = Task.Run(() => plan.Execute());
            defer.CompleteDefer.Set();
            Assert.IsTrue(t.Wait(100000));
        }

        [Test]
        public void TestExecuteWaitForDefer_Simple()
        {
            var plan = new TestPlan();
            var defer = new DeferredResultsStep2();
            plan.Steps.Add(defer);
            Task<TestPlanRun> t = Task.Run(() => plan.Execute());
            defer.RunCompleted.WaitOne();
            defer.DeferStarted.WaitOne();
            // do something that takes about the time for a test plan to complete
            TestExecuteWaitForDefer_WithParallel_Simpler(); 
            
            Assert.IsFalse(t.IsCompleted);
            defer.CompleteDefer.Set();
            Assert.IsTrue(t.Wait(100000));
        }

        [Test]
        public void TestExecuteWaitForDefer_WithParallel_Simpler()
        {
            var plan = new TestPlan();
            var parallel = new ParallelStep();
            var defer = new DeferredResultsStep2();
            plan.Steps.Add(parallel);
            parallel.ChildTestSteps.Add(defer);
            Task<TestPlanRun> t = Task.Run(() => plan.Execute());
            defer.CompleteDefer.Set();
            Assert.IsTrue(t.Wait(100000));
        }
        
        [Test]
        public void TestExecuteWaitForDefer_WithParallel()
        {
            var plan = new TestPlan();
            var parallel = new ParallelStep();
            var defer = new DeferredResultsStep2();
            plan.Steps.Add(parallel);
            parallel.ChildTestSteps.Add(defer);
            Task<TestPlanRun> t = Task.Run(() => plan.Execute());
            defer.DeferStarted.WaitOne();
            defer.RunCompleted.WaitOne();
            
            // do something that takes about the time for a test plan to complete
            TestExecuteWaitForDefer_WithParallel_Simpler();
            
            Assert.IsFalse(t.IsCompleted);
            defer.CompleteDefer.Set();
            
            Assert.IsTrue(t.Wait(100000));
        }
        
        [Test]
        public void TestBreakOnPlanCompleted()
        {
            var rl = new resultListenerCrash() {CrashResultPhase = resultListenerCrash.ResultPhase.PlanRunCompleted};
            var plan = new TestPlan();
            var step = new SequenceStep();
            plan.ChildTestSteps.Add(step);
            var run = plan.Execute(new IResultListener[] {rl});
            Assert.AreEqual(Verdict.Error, run.Verdict);
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

    public class ManySettingsStep : TestStep
    {
        public int A { get; set; } = 123;
        public int[] B { get; set; } = new[] {1, 2, 3};
        public Instrument[] C { get; set; } = Array.Empty<Instrument>();
        public Instrument[] D { get; set; }= Array.Empty<Instrument>();
        public Instrument[] E { get; set; }= Array.Empty<Instrument>();
        public string F { get; set; } = "Hello world!!";
        
        [EnabledIf(nameof(A), 123)]
        public Enabled<string> G { get; set; } = new Enabled<string>() {Value = "Hello"};
        [EnabledIf(nameof(A), 123)]
        public List<string> H { get; set; } = new List<string>{"1 2 3"};
        [EnabledIf(nameof(A), 123)]
        public Enabled<double> I { get; set; } = new Enabled<double>();
        [EnabledIf(nameof(A), 123)]
        public ITestStep Step { get; set; }
        
        public override void Run()
        {
            
        }
    }
}
