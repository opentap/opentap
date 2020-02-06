using System;
using System.Threading;
using NUnit.Framework;
using OpenTap.Engine.UnitTests.TestTestSteps;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class AbortConditionsTest
    {
        public class TestStepFailsTimes : TestStep
        {
            public int fails = 0;
            public int timesRun = 0;

            public override void PrePlanRun()
            {
                fails = 5;
                timesRun = 0;
                base.PrePlanRun();
            }

            public override void Run()
            {
                timesRun += 1;
                if (fails > 0)
                {
                    fails -= 1;
                    throw new Exception("Intended failure!");
                }

                UpgradeVerdict(Verdict.Pass);
            }
        }

        // The new abort condition system gives many possibilities.
        // 1. everything inherits (same as without)
        // 2. step not allowed to fail
        // 3. and or step not allowed to error
        // 4. step allowed to fail
        // 5. and or step allowed to error
        // Retry:
        // 2. Single step retry until not fail.
        //    - Step passes
        //    - Step creates an error
        //    - Step never passes -> break?
        // 3. Parent step retry until child steps pass
        // what about inconclusive??


        [Test]
        public void TestStepFailsNTimesRetry()
        {
            TestPlan plan = new TestPlan();
            var step = new TestStepFailsTimes();
            AbortCondition.SetAbortCondition(step, TestStepVerdictBehavior.RetryOnError);
            AbortCondition.SetRetries(step, 6);
            plan.Steps.Add(step);
            var run = plan.Execute();

            Assert.IsTrue(run.Verdict == Verdict.Pass);
            Assert.IsTrue(step.timesRun == 6);
        }

        [TestCase(2, Verdict.Error)]
        [TestCase(5, Verdict.Pass)]
        public void TestInheritRetryBehavior(int retryTimes, Verdict verdict)
        {
            TestPlan plan = new TestPlan();
            var step = new TestStepFailsTimes();
            step.SetAbortCondition(TestStepVerdictBehavior.Inherit | TestStepVerdictBehavior.BreakOnError);
            step.SetRetries(6);
            var seq = new SequenceStep();
            seq.SetAbortCondition(TestStepVerdictBehavior.RetryOnError);
            seq.SetRetries((uint)retryTimes);
            
            
            seq.ChildTestSteps.Add(step);
            plan.Steps.Add(seq);

            var run = plan.Execute();
            Assert.AreEqual(verdict, run.Verdict);
            Assert.AreEqual(retryTimes, step.timesRun - 1);
        }

        [TestCase(Verdict.Error, TestStepVerdictBehavior.BreakOnError)]
        [TestCase(Verdict.Fail, TestStepVerdictBehavior.BreakOnFail)]
        [TestCase(Verdict.Inconclusive, TestStepVerdictBehavior.BreakOnInconclusive)]
        [TestCase(Verdict.Error, TestStepVerdictBehavior.RetryOnError)]
        [TestCase(Verdict.Fail, TestStepVerdictBehavior.RetryOnFail)]
        [TestCase(Verdict.Inconclusive, TestStepVerdictBehavior.RetryOnInconclusive)]
        [TestCase(Verdict.Error, TestStepVerdictBehavior.BreakOnFail | TestStepVerdictBehavior.RetryOnError)]
        [TestCase(Verdict.Fail, TestStepVerdictBehavior.BreakOnFail | TestStepVerdictBehavior.RetryOnError)]
        [TestCase(Verdict.Inconclusive,
            TestStepVerdictBehavior.BreakOnInconclusive | TestStepVerdictBehavior.BreakOnError)]

        public void TestStepBreakOnError(Verdict verdictOutput, TestStepVerdictBehavior condition)
        {
            var l = new PlanRunCollectorListener();
            TestPlan plan = new TestPlan();
            var verdict = new VerdictStep
            {
                VerdictOutput = verdictOutput
            };
            verdict.SetAbortCondition(condition);
            var verdict2 = new VerdictStep
            {
                VerdictOutput = Verdict.Pass
            };
            
            plan.Steps.Add(verdict);
            plan.Steps.Add(verdict2);
            var run = plan.Execute(new[] {l});
            Assert.AreEqual(verdictOutput, run.Verdict);
            Assert.AreEqual(1, l.StepRuns.Count);
            Assert.AreEqual(TestStepVerdictBehavior.Inherit, verdict2.GetAbortCondition());
        }

        [TestCase(Verdict.Pass, EngineSettings.AbortTestPlanType.Step_Error, 2)]
        [TestCase(Verdict.Fail, EngineSettings.AbortTestPlanType.Step_Error, 2)]
        [TestCase(Verdict.Error, EngineSettings.AbortTestPlanType.Step_Error, 1)]
        [TestCase(Verdict.Fail, EngineSettings.AbortTestPlanType.Step_Fail, 1)]
        [TestCase(Verdict.Fail,
            EngineSettings.AbortTestPlanType.Step_Error | EngineSettings.AbortTestPlanType.Step_Fail, 1)]
        [TestCase(Verdict.Error,
            EngineSettings.AbortTestPlanType.Step_Error | EngineSettings.AbortTestPlanType.Step_Fail, 1)]
        [TestCase(Verdict.Pass,
            EngineSettings.AbortTestPlanType.Step_Error | EngineSettings.AbortTestPlanType.Step_Fail, 2)]
        public void EngineInheritedConditions(Verdict verdictOutput, EngineSettings.AbortTestPlanType abortTestPlanType,
            int runCount)
        {
            Verdict finalVerdict = verdictOutput;
            var prev = EngineSettings.Current.AbortTestPlan;
            try
            {
                EngineSettings.Current.AbortTestPlan = abortTestPlanType;
                var l = new PlanRunCollectorListener();
                TestPlan plan = new TestPlan();
                var verdict = new VerdictStep
                {
                    VerdictOutput = verdictOutput
                };
                verdict.SetAbortCondition(TestStepVerdictBehavior.Inherit);
                var verdict2 = new VerdictStep
                {
                    VerdictOutput = Verdict.Pass
                };
                plan.Steps.Add(verdict);
                plan.Steps.Add(verdict2);
                var run = plan.Execute(new[] {l});
                Assert.AreEqual(finalVerdict, run.Verdict);
                Assert.AreEqual(runCount, l.StepRuns.Count);
                Assert.AreEqual(TestStepVerdictBehavior.Inherit, verdict2.GetAbortCondition());
            }
            finally
            {
                EngineSettings.Current.AbortTestPlan = prev;
            }
        }

        [Test]
        public void EngineInheritedConditions2()
        {
            Verdict verdictOutput = Verdict.Fail;
            EngineSettings.AbortTestPlanType abortTestPlanType = EngineSettings.AbortTestPlanType.Step_Fail;
            int runCount = 1;
            Verdict finalVerdict = verdictOutput;
            var prev = EngineSettings.Current.AbortTestPlan;
            try
            {
                EngineSettings.Current.AbortTestPlan = abortTestPlanType;
                var l = new PlanRunCollectorListener();
                TestPlan plan = new TestPlan();
                var verdict = new VerdictStep
                {
                    VerdictOutput = verdictOutput,
                };
                verdict.SetAbortCondition(TestStepVerdictBehavior.Inherit | TestStepVerdictBehavior.BreakOnError);
                var verdict2 = new VerdictStep
                {
                    VerdictOutput = Verdict.Pass
                };
                plan.Steps.Add(verdict);
                plan.Steps.Add(verdict2);
                var run = plan.Execute(new[] {l});
                Assert.AreEqual(finalVerdict, run.Verdict);
                Assert.AreEqual(runCount, l.StepRuns.Count);
                Assert.AreEqual(TestStepVerdictBehavior.Inherit, verdict2.GetAbortCondition());
            }
            finally
            {
                EngineSettings.Current.AbortTestPlan = prev;
            }
        }

        /// <summary>  This step overrides the verdict of the child steps. </summary>
        [AllowAnyChild]
        class VerdictOverrideStep : TestStep
        {
            public Verdict OutputVerdict { get; set; } = Verdict.Pass;
            public override void Run()
            {
                foreach (var step in EnabledChildSteps)
                    RunChildStep(step, throwOnError: false);
                this.Verdict = Verdict.Pass;
            }
        }

        [Test]
        public void StepsCanOverrideVerdicts()
        {
            var plan = new TestPlan();
            var stepCatch = new VerdictOverrideStep { OutputVerdict = Verdict.Pass };
            var verdict = new VerdictStep { VerdictOutput = Verdict.Error };
            plan.ChildTestSteps.Add(stepCatch);
            stepCatch.ChildTestSteps.Add(verdict);

            var run = plan.Execute();
            Assert.AreEqual(Verdict.Pass, run.Verdict);
        }

        public class TestStepWaitInfinite : TestStep
        {
            public Semaphore Sem = new Semaphore(0,1);
        
            public override void Run()
            {
                Sem.Release();
                TapThread.Sleep(TimeSpan.MaxValue);
            }
        }

        public class TestStepAbortPlan : TestStep
        {
            public AbortConditionsTest.TestStepWaitInfinite WaitFor { get; set; }
            public override void Run()
            {
                WaitFor?.Sem.WaitOne();
                PlanRun.MainThread.Abort();
            }
        }

        [Test]
        public void TestStepWaitInfiniteAndAbortTest()
        {
            var plan = new TestPlan();
            var parallel = new ParallelStep();
            var abort = new TestStepAbortPlan(){WaitFor =  new TestStepWaitInfinite()};
            parallel.ChildTestSteps.Add(abort);
            parallel.ChildTestSteps.Add(abort.WaitFor);
            plan.ChildTestSteps.Add(parallel);
            var run = plan.Execute();
            Assert.AreEqual(Verdict.Aborted, run.Verdict);
            foreach (var step in parallel.ChildTestSteps)
            {
                Assert.AreEqual(Verdict.Aborted, step.Verdict);    
            }
        }
    }
}