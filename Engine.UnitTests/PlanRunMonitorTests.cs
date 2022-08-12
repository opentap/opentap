using System;
using System.ComponentModel;
using System.Linq;
using NUnit.Framework;
using OpenTap.Engine.UnitTests.TestTestSteps;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class PlanRunMonitorTests
    {
        [Browsable(false)]
        public class TestTestPlanRunMonitor : ComponentSettings<TestTestPlanRunMonitor>, ITestPlanRunMonitor
        {
            public bool IsEnabled { get; set; }
            public IResultListener ListenerToAdd { get; set; }
            public bool Entered { get; set; }
            public bool Exited { get; set; }
            public bool ThrowOnEnter { get; set; }

            public void EnterTestPlanRun(TestPlanRun plan)
            {
                if (!IsEnabled) return;
                Entered = true;
                if (ThrowOnEnter)
                    throw new Exception("Intended exception");
                if (ListenerToAdd != null)
                    plan.AddResultListener(ListenerToAdd);
            }

            public void ExitTestPlanRun(TestPlanRun plan)
            {
                if (!IsEnabled) return;
                Exited = true;
                if (ListenerToAdd != null)
                    plan.RemoveResultListener(ListenerToAdd);
            }
        }

        [Test]
        public void TestAddResultListenerInPlanRunMonitor()
        {
            // Verify that a result listener can be added by an ITestPlanRunMonitor.
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(new SineResultsStep());
            using (Session.Create(SessionOptions.OverlayComponentSettings))
            {
                var listener = new PlanRunCollectorListener {CollectResults = true};
                TestTestPlanRunMonitor.Current.IsEnabled = true;
                TestTestPlanRunMonitor.Current.ListenerToAdd = listener;
                var executed = plan.Execute();
                Assert.IsFalse(executed.ResultListeners.Contains(listener));
                Assert.IsTrue(listener.WasOpened);
                Assert.IsFalse(listener.IsConnected);
                Assert.IsTrue(listener.Results.Any());
                Assert.IsFalse(ResultSettings.Current.Any());
            }
        }

        [Test]
        public void PlanRunMonitorGeneralTest()
        {
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(new SineResultsStep());
            using (Session.Create(SessionOptions.OverlayComponentSettings))
            {
                // Verify that run monitor is both entered and exited.
                TestTestPlanRunMonitor.Current.IsEnabled = true;
                plan.Execute();
                Assert.IsTrue(TestTestPlanRunMonitor.Current.Entered);
                Assert.IsTrue(TestTestPlanRunMonitor.Current.Exited);
            }

            using (Session.Create(SessionOptions.OverlayComponentSettings))
            {
                // Verify that run monitor is both entered and exited, even if the plan failed to start.
                TestTestPlanRunMonitor.Current.IsEnabled = true;
                TestTestPlanRunMonitor.Current.ThrowOnEnter = true;
                var run = plan.Execute();
                Assert.IsTrue(run.FailedToStart);
                Assert.IsTrue(TestTestPlanRunMonitor.Current.Entered);
                Assert.IsTrue(TestTestPlanRunMonitor.Current.Exited);
            }
            
            // verify that the overlaid session works.
            Assert.IsFalse(TestTestPlanRunMonitor.Current.ThrowOnEnter);
        }
        
        [Test]
        public void PlanRunMonitorOpenTestTest()
        {
            var plan = new TestPlan();
            plan.ChildTestSteps.Add(new SineResultsStep());
            using (Session.Create(SessionOptions.OverlayComponentSettings))
            {
                // Verify that run monitor is both entered and exited.
                TestTestPlanRunMonitor.Current.IsEnabled = true;
                var listener = new PlanRunCollectorListener {CollectResults = true};
                TestTestPlanRunMonitor.Current.ListenerToAdd = listener;
                plan.Open();
                var executed = plan.Execute();
                var listener2 = new PlanRunCollectorListener {CollectResults = true};
                TestTestPlanRunMonitor.Current.ListenerToAdd = listener2;
                var executed2 = plan.Execute();
                Assert.IsFalse(executed.ResultListeners.Contains(listener));
                Assert.IsFalse(executed2.ResultListeners.Contains(listener2));
                Assert.IsFalse(executed2.ResultListeners.Contains(listener));
                Assert.IsTrue(listener.WasOpened);
                Assert.IsTrue(listener.IsConnected);
                Assert.IsTrue(listener.Results.Any());
                Assert.IsTrue(listener2.Results.Any());
                Assert.IsTrue(listener2.IsConnected);
                Assert.IsFalse(ResultSettings.Current.Any());
                Assert.IsTrue(TestTestPlanRunMonitor.Current.Entered);
                Assert.IsTrue(TestTestPlanRunMonitor.Current.Exited);
                plan.Close();
                Assert.IsFalse(listener.IsConnected);
            }

        }
    }
}