//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Linq;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
using OpenTap;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture()]
    public class TestPlanBreakRequestedTests : EngineTestBase
    {

        [AllowAnyChild]
        public class WithEvents : TestStep
        {
            public override void Run()
            {
                OfferBreak();
                RunChildSteps();
            }
        }


        private int _pausedDetectedCount;
        private int _startingDetectedCount;

        [Test]
        public void RunFlatTestplan()
        {
            TestPlan testplan = new TestPlan();
            WithEvents step1 = new WithEvents();
            WithEvents step2 = new WithEvents();
            WithEvents step3 = new WithEvents();
            WithEvents step4 = new WithEvents();

            testplan.Steps.Add(step1);
            testplan.Steps.Add(step2);
            testplan.Steps.Add(step3);
            testplan.Steps.Add(step4);

            testplan.BreakOffered += _testplan_TestStepPaused;

            _pausedDetectedCount = 0;
            _startingDetectedCount = 0;

            testplan.Execute();
            //Since the plan is flat, the starting and paused count is the same.
            Assert.IsTrue(_startingDetectedCount == TotalEnabledStepsInTestPlan(testplan));
            Assert.IsTrue(_pausedDetectedCount == TotalEnabledStepsInTestPlan(testplan));
        }


        private TestPlan testplanJumpToStep;
        private bool firstBreak = true;
        [Test]
        public void BreakJumpToStepRunNull()
        {
            testplanJumpToStep = new TestPlan();
            SequenceStep step1 = new SequenceStep();
            SequenceStep step2 = new SequenceStep();
            SequenceStep step3 = new SequenceStep();

            testplanJumpToStep.Steps.Add(step1);
            testplanJumpToStep.Steps.Add(step2);
            testplanJumpToStep.Steps.Add(step3);

            testplanJumpToStep.BreakOffered += Testplan_BreakOffered;

            firstBreak = true;
            testplanJumpToStep.Execute();

            foreach (var step in testplanJumpToStep.Steps)
                Assert.IsNull(step.StepRun);
        }

        private void Testplan_BreakOffered(object sender, BreakOfferedEventArgs e)
        {
            if (firstBreak)
            {
                e.JumpToStep = testplanJumpToStep.Steps.Last().Id;
                firstBreak = false;
            }
        }


        private TestPlan testplanAbort;
        [Test]
        public void BreakAbortStepRunNull()
        {
            testplanAbort = new TestPlan();
            SequenceStep step1 = new SequenceStep();
            SequenceStep step2 = new SequenceStep();
            SequenceStep step3 = new SequenceStep();

            testplanAbort.Steps.Add(step1);
            testplanAbort.Steps.Add(step2);
            testplanAbort.Steps.Add(step3);

            testplanAbort.BreakOffered += Testplan_BreakOffered2;

            testplanAbort.Execute();

            foreach (var step in testplanAbort.Steps)
                Assert.IsNull(step.StepRun);
        }

        private void Testplan_BreakOffered2(object sender, BreakOfferedEventArgs e)
        {
            testplanAbort.RequestAbort();
        }

        [Test]
        public void RunHierarchical()
        {
            TestPlan testplan = new TestPlan();

            var grandParent = new WithEvents();
            testplan.Steps.Add(grandParent);

            var parent = new WithEvents();
            grandParent.ChildTestSteps.Add(parent);

            var child1 = new WithEvents();
            parent.ChildTestSteps.Add(child1);

            var child2 = new WithEvents();
            parent.ChildTestSteps.Add(child2);

            testplan.BreakOffered += _testplan_TestStepPaused;

            _pausedDetectedCount = 0;
            _startingDetectedCount = 0;

            testplan.Execute();

            // Since ALL the steps are of type WithEvents, we continue to get 
            // a pausedcount = startingCount = totalcount.
            Assert.IsTrue(_startingDetectedCount == TotalEnabledStepsInTestPlan(testplan));
            Assert.IsTrue(_pausedDetectedCount == TotalEnabledStepsInTestPlan(testplan));
        }

        [Test]
        public void RunHierarchical2()
        {
            TestPlan testplan = new TestPlan();

            var grandParent = new SequenceStep();
            testplan.Steps.Add(grandParent);

            var parent = new SequenceStep();
            grandParent.ChildTestSteps.Add(parent);

            var child1 = new WithEvents();
            parent.ChildTestSteps.Add(child1);

            var child2 = new WithEvents();
            parent.ChildTestSteps.Add(child2);

            testplan.BreakOffered += _testplan_TestStepPaused;

            _pausedDetectedCount = 0;
            _startingDetectedCount = 0;

            testplan.Execute();

            // Since only two of the steps are with events, we get
            // _starting = total
            // _pausedCount = 2; 
            var test = TotalEnabledStepsInTestPlan(testplan);
            Assert.IsTrue(_startingDetectedCount == TotalEnabledStepsInTestPlan(testplan));
            Assert.IsTrue(_pausedDetectedCount == 2);
        }


        [Test]
        public void RunLoop()
        {
            TestPlan testplan = new TestPlan();

            var grandParent = new RepeatStep();
            uint loopCount = 7;
            grandParent.Count = loopCount;
            grandParent.Action = RepeatStep.RepeatStepAction.Fixed_Count;
            testplan.Steps.Add(grandParent);

            var parent = new WithEvents();
            grandParent.ChildTestSteps.Add(parent);

            testplan.BreakOffered += _testplan_TestStepPaused;

            _pausedDetectedCount = 0;
            _startingDetectedCount = 0;

            testplan.Execute();

            //We will actually have loopCount + 1 starts.
            // We get a start for each of the loops around "withevents steps", plus one for the outer loop "FixedCountLoop" step
            Assert.AreEqual(loopCount + 1, _startingDetectedCount, "StartingDetectedCount");

            //Since fixed count loop does NOT have a pause, it will not count in the pause count.
            Assert.AreEqual(loopCount, _pausedDetectedCount, "PausedDetectedCount");
        }

        void _testplan_TestStepPaused(object sender, BreakOfferedEventArgs e)
        {

            if (e.IsTestStepStarting)
                _startingDetectedCount++;
            else
                _pausedDetectedCount++;
        }

        int TotalEnabledStepsInTestPlan(TestPlan testplan)
        {
            int total = testplan.EnabledSteps.Count;
            foreach (ITestStep testStep in testplan.Steps)
            {
                total += testStep.RecursivelyGetChildSteps(TestStepSearch.EnabledOnly).Count();
            }
            return total;
        }

        [Test]
        public void ParmsCheck()
        {
            TestPlan testplan = new TestPlan();
            DelayStep step1 = new DelayStep();
            string myName = "fred";
            step1.Name = myName;
            testplan.Steps.Add(step1);

            testplan.BreakOffered += (s, e) =>
            {
                Assert.IsTrue(e.TestStepRun.Verdict == Verdict.NotSet);
                //Assert.IsTrue(e.TestStepRun.Step.GetStepPath().Contains(myName));
            };
            testplan.Execute();

        }



    }
}
