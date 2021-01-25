using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using OpenTap.Diagnostic;
using OpenTap.Engine.UnitTests.TestTestSteps;
using OpenTap.EngineUnitTestUtils;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    public class SubPlanTest
    {

        class SubPlanResultListener : ResultListener
        {
            ResultSource proxy;
            public SubPlanResultListener(ResultSource proxy) => this.proxy = proxy;

            public override void OnResultPublished(Guid stepRunId, ResultTable result)
            {
                base.OnResultPublished(stepRunId, result);
                proxy.PublishTable(result);
            }
        }

        public class SubDut : Dut
        {
            public override void Open()
            {
                base.Open();
            }

            public override void Close()
            {
                base.Close();
            }
        }

        public class SubPlanStep : TestStep
        {
            
            public TestPlan Plan { get; set; }

            public SubDut  Dut{ get; set; }
            public override void Run()
            {
                var planXml = Plan.SerializeToString();
                using (Session.WithSession(SessionFlag.InheritThreadContext,
                    // Component settings flag added.
                    SessionFlag.OverlayComponentSettings))
                {

                    var plan = Utils.DeserializeFromString<TestPlan>(planXml);
                    var subRun = plan.Execute(new IResultListener[] {new SubPlanResultListener(Results)});
                    UpgradeVerdict(subRun.Verdict);    
                }
            }
        }

        public class DutStep : TestStep
        {
            public Dut Dut { get; set; }
            public override void Run()
            {
                Assert.IsTrue(Dut.IsConnected);
            }
        }

        [Test]
        public void TestRunningSubPlan()
        {
            using (Session.WithSession(SessionFlag.RedirectLogging, SessionFlag.OverlayComponentSettings))
            {
                string knownLogMessage = "asdasdasd";
                var dut = new SubDut();
                DutSettings.Current.Add(dut);
                var subPlan = new TestPlan();
                subPlan.Steps.Add(new LogStep {LogMessage = knownLogMessage});
                subPlan.Steps.Add(new VerdictStep {Verdict = Verdict.Pass});
                subPlan.Steps.Add(new DutStep {Dut = dut});

                var plan = new TestPlan();
                plan.Steps.Add(new SubPlanStep {Plan = subPlan, Dut = dut});
                var listener = new MemoryTraceListener();
                using (Session.WithSession(SessionFlag.RedirectLogging))
                {
                    Log.AddListener(listener);
                    var run = plan.Execute();
                    Assert.IsTrue(run.Verdict == Verdict.Pass);
                }

                Assert.IsTrue(listener.Events.Any(x => x.Message == knownLogMessage));
            }
        }

        class MemoryTraceListener : ILogListener
        {
            public readonly List<Event> Events = new List<Event>();
            public void EventsLogged(IEnumerable<Event> Events)
            {
                this.Events.AddRange(Events);
            }

            public void Flush()
            {
                
            }
        }
        
        [Test]
        public void RedirectedLogTest()
        {
            var listener = new MemoryTraceListener();
            var log = Log.CreateSource("Redirect?");
            string msg1 = "This is redirected";
            string msg2 = "This is redirected and from another thread";
            string msg3 = "This is also redirected";
            
            log.Debug("This is not redirected");

            var trd = TapThread.Start(() =>
            {
                try
                {
                    while (true)
                    {
                        TapThread.Sleep(1);
                        log.Debug("Not redirected!");
                    }
                }
                catch
                {
                    
                }
            });

            using (Session.WithSession(SessionFlag.RedirectLogging, SessionFlag.InheritThreadContext))
            {
                Log.AddListener(listener);
                var sem = new Semaphore(0, 1);
                log.Debug(msg1);
                TapThread.Start(() =>
                {
                    // messages from a different thread should also be redirected.
                    log.Info(msg2);
                    sem.Release();
                });
                sem.WaitOne();
            }

            log.Debug("This is also not redirected");
            using (Session.WithSession(SessionFlag.RedirectLogging, SessionFlag.InheritThreadContext))
            {
                Log.AddListener(listener);
                log.Debug(msg3);
            }

            trd.Abort();
            
            Assert.AreEqual(3, listener.Events.Count);
            Assert.IsTrue(listener.Events[0].Message == msg1);
            Assert.IsTrue(listener.Events[1].Message == msg2);
            Assert.IsTrue(listener.Events[2].Message == msg3);
        }

        [Test]
        public void RedirectedLogTest2()
        {
            var listener = new MemoryTraceListener();
            var listener1 = new MemoryTraceListener();
            var listener2 = new MemoryTraceListener();
            var log = Log.CreateSource("Redirect?");
            string msg1 = "This is redirected";
            string msg2 = "This is redirected and from another thread";

            Log.AddListener(listener);
            log.Debug("This is not redirected0");

            var sem = new Semaphore(0, 1);
            using (Session.WithSession(SessionFlag.RedirectLogging, SessionFlag.InheritThreadContext))
            {
                Log.AddListener(listener1);
                log.Debug(msg1);
                TapThread.Start(() =>
                {
                    Log.AddListener(listener2);
                    Thread.Sleep(50);
                    // messages from a different thread should also be redirected.
                    log.Info(msg2);
                    sem.Release();
                });
            }
            log.Debug("This is not redirected1");
            sem.WaitOne();
            log.Debug("This is not redirected2");
            log.Flush();
            Assert.AreEqual(3, listener.Events.Count);

            Assert.IsTrue(listener1.Events[0].Message == msg1);
            Assert.IsTrue(listener1.Events[1].Message == msg2);
            Assert.IsTrue(listener2.Events[0].Message == msg2);
        }

        [Test]
        public void ComponentSettingSession()
        {
            DutSettings.Current.Clear();
            var dut1 = new SubDut();
            try
            {
                
                DutSettings.Current.Add(dut1);
                var profile1 = EngineSettings.Current.OperatorName;
                using (Session.WithSession(SessionFlag.OverlayComponentSettings))
                {
                    var profile2 = "profile2";
                    EngineSettings.Current.OperatorName = profile2;

                    var dut2 = new SubDut();
                    DutSettings.Current.Add(dut2);
                    Assert.AreEqual(2, DutSettings.Current.Count);
                    using (Session.WithSession(SessionFlag.OverlayComponentSettings))
                    {
                        var profile3 = "profile3";
                        var dut3 = new SubDut();
                        DutSettings.Current.Add(dut3);
                        Assert.AreEqual(3, DutSettings.Current.Count);
                        Assert.AreEqual(profile2, EngineSettings.Current.OperatorName);
                        EngineSettings.Current.OperatorName = profile3;
                    }

                    using (Session.WithSession(SessionFlag.OverlayComponentSettings))
                    {
                        Assert.AreEqual(profile2, EngineSettings.Current.OperatorName);
                    }

                    Assert.AreEqual(2, DutSettings.Current.Count);
                    Assert.AreEqual(profile2, EngineSettings.Current.OperatorName);
                }

                Assert.AreEqual(1, DutSettings.Current.Count);
                Assert.AreEqual(profile1, EngineSettings.Current.OperatorName);
            }
            finally
            {
                DutSettings.Current.Remove(dut1);
            }

            Assert.AreEqual(0, DutSettings.Current.Count);
        }

        [Test]
        public void TestPlanReferenceSubPlanTest()
        {
            int parallelism = 10;
            var planName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".TapPlan");
            string knownLogMessage = "Hello";
            using (Session.WithSession(SessionFlag.OverlayComponentSettings, SessionFlag.RedirectLogging))
            try
            {
                var dut = new SubDut();
                DutSettings.Current.Add(dut);

                {
                    var subPlan = new TestPlan();
                    subPlan.Steps.Add(new LogStep {LogMessage = knownLogMessage});
                    subPlan.Steps.Add(new VerdictStep {Verdict = Verdict.Pass});
                    subPlan.Steps.Add(new DutStep {Dut = dut});
                    subPlan.Save(planName);
                }
                var plan = new TestPlan();
                var par = new ParallelStep();
                plan.ChildTestSteps.Add(par);
                for (int i = 0; i < parallelism; i++)
                {
                    var tpr1 = new TestPlanReference
                    {
                        Filepath = {Text = planName},
                        HideSteps = true
                    };
                    par.ChildTestSteps.Add(tpr1);
                }
                
                var testListener = new TestTraceListener();
                Log.AddListener(testListener);
                var run = plan.Execute();    
                var log = testListener.GetLog();
                Assert.AreEqual(Verdict.Pass, run.Verdict, log);
            }
            finally
            {
                File.Delete(planName);
            }
        }
    }
}