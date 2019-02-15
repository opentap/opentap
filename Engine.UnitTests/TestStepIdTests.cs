//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TestStepIdTests
    {

        [Test]
        public void TestPlanLoadTestStepIdTest()
        {
            TestPlan plan1 = null;
            TestPlan plan2 = null;
            using (Stream str = File.OpenRead("TestTestPlans\\FiveDelays.TapPlan"))
            {
                plan1 = TestPlan.Load(str, "FiveDelays.TapPlan");
            }
            using (Stream str = File.OpenRead("TestTestPlans\\FiveDelays.TapPlan"))
            {
                plan2 = TestPlan.Load(str, "FiveDelays.TapPlan");
            }

            Assert.AreEqual(plan1.Steps.First().Id, plan2.Steps.First().Id);
        }

        [Test]
        public void TestStepRunChildrenResultListenerTest()
        {
            TestPlan plan1 = null;
            using (Stream str = File.OpenRead("TestTestPlans\\FiveDelays.TapPlan"))
            {
                plan1 = TestPlan.Load(str, "FiveDelays.TapPlan");
            }

            OpenTap.EngineUnitTestUtils.TestTraceListener listener = new OpenTap.EngineUnitTestUtils.TestTraceListener();
            Log.AddListener(listener);
            plan1.Execute(new IResultListener[] { new TestResultListener() });
            listener.AssertErrors();
        }

        [DisplayName("Test\\TestStepId")]
        public class TestResultListener : ResultListener
        {
            public TestResultListener()
            {
                Name = "TestStepId";
            }

            public override void OnTestStepRunStart(TestStepRun stepRun)
            {
                base.OnTestStepRunStart(stepRun);
                //Assert.IsNull(stepRun.PlanRun.Children);
                //Assert.IsNull(stepRun.Children);
            }
            public override void OnTestStepRunCompleted(TestStepRun stepRun)
            {
                base.OnTestStepRunCompleted(stepRun);
                //Assert.IsNull(stepRun.PlanRun.Children); //in some cases this is not null (remember this is executed async)
                //foreach (TestStepRun testStepRun in stepRun.Children)
                //{
                //    Thread.Sleep(100);
                //}
            }

            public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
            {
                base.OnTestPlanRunCompleted(planRun, logStream);
                //Assert.AreEqual(5, planRun.Children.Count());
                //CollectionAssert.AllItemsAreUnique(planRun.Children);
                //foreach (TestStepRun stepRun in planRun.Children)
                //{
                //Assert.IsNotNull(stepRun.Children);
                //Assert.AreEqual(0, stepRun.Children.Count());
                //    Thread.Sleep(100);
                //}
            }
        }
    }
}
