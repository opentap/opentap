using NUnit.Framework;
using OpenTap.EngineUnitTestUtils;
using OpenTap.Plugins.BasicSteps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.UnitTests
{
    [TestFixture]
    class MultiTestPlanRun
    {
        [Test]
        public void SeparateLogs()
        {
            TestPlan plan1 = new TestPlan();
            plan1.Steps.Add(new DelayStep { DelaySecs = 1, Name = "Delay1" });

            TestPlan plan2 = new TestPlan();
            plan2.Steps.Add(new DelayStep { DelaySecs = 1, Name = "Delay2" });

            EngineSettings.Current.OperatorName = "Test";
            TestTraceListener parentListener = new TestTraceListener();
            Log.AddListener(parentListener);
            TestTraceListener listener1 = null;

            ManualResetEvent plan1Complete = new ManualResetEvent(false);
            using (Session.WithSession(SessionFlag.RedirectLogging,SessionFlag.OverlayComponentSettings))
            {
                EngineSettings.Current.OperatorName = "Test1";
                var id = Session.Current.Id;
                TapThread.Start(() =>
                {
                    listener1 = new TestTraceListener();
                    Log.AddListener(listener1);
                    plan1.Execute();
                    Assert.AreEqual(id, Session.Current.Id);
                    Assert.AreEqual("Test1", EngineSettings.Current.OperatorName);
                    plan1Complete.Set();
                });
            }
            Assert.AreEqual("Test", EngineSettings.Current.OperatorName);

            ManualResetEvent plan2Complete = new ManualResetEvent(false);
            var t2 = TapThread.Start(() =>
            {
                var res = plan2.Execute();
                plan2Complete.Set();
            });

            plan1Complete.WaitOne();
            plan2Complete.WaitOne();


            var parentLog = parentListener.GetLog();
            //StringAssert.Contains("\"Delay1\" completed", parentLog);
            StringAssert.Contains("\"Delay2\" completed", parentLog);

            var session1Log = listener1.GetLog();
            StringAssert.Contains("\"Delay1\" completed", session1Log);
            StringAssert.DoesNotContain("\"Delay2\" completed", session1Log);
        }
    }
}
