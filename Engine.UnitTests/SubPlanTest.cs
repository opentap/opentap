using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
                using (Session.Create(SessionOptions.OverlayComponentSettings))
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
            using (Session.Create())
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
                using (Session.Create(SessionOptions.RedirectLogging))
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
            var rootListener = new MemoryTraceListener();
            var sessionListener = new MemoryTraceListener();
            var log = Log.CreateSource("Redirect?");
            string msg1 = "This is redirected";
            string msg2 = "This is redirected and from another thread";
            string msg3 = "This is also redirected";

            Log.AddListener(rootListener);

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

            using (Session.Create(SessionOptions.RedirectLogging))
            {
                Log.AddListener(sessionListener);
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
            using (Session.Create(SessionOptions.RedirectLogging))
            {
                Log.AddListener(sessionListener);
                log.Debug(msg3);
            }

            trd.Abort();
            
            Assert.AreEqual(3, sessionListener.Events.Count);
            Assert.IsTrue(sessionListener.Events[0].Message == msg1);
            Assert.IsTrue(sessionListener.Events[1].Message == msg2);
            Assert.IsTrue(sessionListener.Events[2].Message == msg3);

            Assert.IsFalse(rootListener.Events.Any(e => e.Message == msg1));
        }

        [Test]
        [Retry(3)]
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
            using (Session.Create(SessionOptions.RedirectLogging))
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
            Assert.AreEqual(3, listener.Events.Count, string.Join(Environment.NewLine, listener.Events.Select(evt => evt.Message)));

            Assert.IsTrue(listener1.Events[0].Message == msg1);
            Assert.IsTrue(listener1.Events[1].Message == msg2);
            Assert.IsTrue(listener2.Events[0].Message == msg2);
        }

        public enum SessionEnum
        {
            Parent,
            SessionA,
            SessionB
        }
        
        public class TestUserInterface : IUserInputInterface
        {
            public SessionEnum SessionEnum { get; set; }

            public TestUserInterface(SessionEnum caller)
            {
                SessionEnum = caller;
            }
            void IUserInputInterface.RequestUserInput(object dataObject, TimeSpan timeout, bool modal)
            {
            
            }
        }
        
        void InterfaceAssert(SessionEnum expected)
        {
            var i = UserInput.GetInterface() as TestUserInterface;
            Assert.IsTrue(i?.SessionEnum == expected,
                $"User interface '{expected}' expected, but was {i?.SessionEnum.ToString() ?? "Not Set"}");
        }
        
        [Test]
        public void RedirectedUserInputTest()
        {
            var previousInterface = UserInput.GetInterface();
            try
            {
                var parentInterface = new TestUserInterface(SessionEnum.Parent);
                var aInterface = new TestUserInterface(SessionEnum.SessionA);
                var bInterface = new TestUserInterface(SessionEnum.SessionB);

                UserInput.SetInterface(parentInterface);
                InterfaceAssert(SessionEnum.Parent);

                // Verify the user input is correctly inherited
                using (Session.Create())
                {
                    InterfaceAssert(SessionEnum.Parent);
                    UserInput.SetInterface(aInterface);
                    InterfaceAssert(SessionEnum.SessionA);
                }

                // Verify the user input is correctly reset
                InterfaceAssert(SessionEnum.Parent);

                // Verify the user input is correctly inherited in nested contexts
                using (Session.Create())
                {
                    InterfaceAssert(SessionEnum.Parent);
                    UserInput.SetInterface(aInterface);
                    InterfaceAssert(SessionEnum.SessionA);

                    using (Session.Create())
                    {
                        InterfaceAssert(SessionEnum.SessionA);
                        UserInput.SetInterface(bInterface);
                        InterfaceAssert(SessionEnum.SessionB);
                    }
                    InterfaceAssert(SessionEnum.SessionA);
                }
                // Verify the user input is correctly reset from nested contexts
                InterfaceAssert(SessionEnum.Parent);
            }
            finally
            {
                UserInput.SetInterface(previousInterface);
            }
        }

        [Test]
        public void RedirectedUserInputParallelTest()
        {
            // 1: Session B sets its interface
            // 2: Session A verifies its interface is unchanged
            var e2 = new ManualResetEventSlim(false);
            // 3: Session A verifies its interface changes
            // 4: Session B verifies it still has the correct interface
            var e4 = new ManualResetEventSlim(false);
            // 5: Session A verifies it still has the correct interface
            var e5 = new ManualResetEventSlim(false);
            // 6: Session A verifies its interface has been reset to 'parent'
            // 7: Session B verifies its interface has been reset to 'parent'
            var e7 = new ManualResetEventSlim(false);
            
            var previousInterface = UserInput.GetInterface();
            try
            {
                var parentInterface = new TestUserInterface(SessionEnum.Parent);
                var aInterface = new TestUserInterface(SessionEnum.SessionA);
                var bInterface = new TestUserInterface(SessionEnum.SessionB);

                UserInput.SetInterface(parentInterface);
                InterfaceAssert(SessionEnum.Parent);

                { // Session A
                    TapThread.Start(() =>
                    {
                        e2.Wait();
                        using (Session.Create())
                        {
                            InterfaceAssert(SessionEnum.Parent); // 2: Session A verifies its interface is unchanged
                            UserInput.SetInterface(aInterface);
                            InterfaceAssert(SessionEnum.SessionA); // 3: Session A verifies its interface changes
                            e4.Set();
                            e5.Wait();
                            InterfaceAssert(SessionEnum.SessionA); // 5: Session A verifies it still has the correct interface
                        }

                        InterfaceAssert(SessionEnum.Parent); // 6: Session A verifies its interface has been reset to 'parent'
                        e7.Set();
                    });
                }

                { // Session B

                    using (Session.Create())
                    {
                        InterfaceAssert(SessionEnum.Parent);
                        UserInput.SetInterface(bInterface); // 1: Session B sets its interface
                        InterfaceAssert(SessionEnum.SessionB);
                        e2.Set();
                        e4.Wait();
                        InterfaceAssert(SessionEnum.SessionB); // 4: Session B verifies it still has the correct interface
                        e5.Set();
                        e7.Wait();
                    }
                    InterfaceAssert(SessionEnum.Parent); // 7: Session B verifies its interface has been reset to 'parent'
                }

                // Verify the user input is correctly reset from nested contexts
                InterfaceAssert(SessionEnum.Parent);

            }
            finally
            {
                UserInput.SetInterface(previousInterface);
            }
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
                using (Session.Create(SessionOptions.OverlayComponentSettings))
                {
                    var profile2 = "profile2";
                    EngineSettings.Current.OperatorName = profile2;

                    var dut2 = new SubDut();
                    DutSettings.Current.Add(dut2);
                    Assert.AreEqual(2, DutSettings.Current.Count);
                    using (Session.Create(SessionOptions.OverlayComponentSettings))
                    {
                        var profile3 = "profile3";
                        var dut3 = new SubDut();
                        DutSettings.Current.Add(dut3);
                        Assert.AreEqual(3, DutSettings.Current.Count);
                        Assert.AreEqual(profile2, EngineSettings.Current.OperatorName);
                        EngineSettings.Current.OperatorName = profile3;
                    }

                    using (Session.Create(SessionOptions.OverlayComponentSettings))
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
        public void TestPlanReferenceDetectChangesTest()
        {
            var planName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".TapPlan");
            var subPlan = new TestPlan();
            var delay = new DelayStep();
            
            delay.DelaySecs = 0.1;
            subPlan.Steps.Add(delay);

            var mainPlan = new TestPlan();
            var tpr = new TestPlanReference() { Filepath = { Text = planName } };
            mainPlan.ChildTestSteps.Add(tpr);

            string saveAndRun()
            {
                subPlan.Save(planName);
                tpr.LoadTestPlan();
                var hash = mainPlan.Execute().Hash;
                Assert.IsFalse(string.IsNullOrWhiteSpace(hash));
                return hash;
            }

            var firstHash = saveAndRun();
            Assert.AreEqual(firstHash, saveAndRun(), "Expected hash to be the same.");
            delay.DelaySecs = 0.01;
            Assert.AreNotEqual(firstHash, saveAndRun(), "Expected hash to be different.");
        }

        [Test]
        public void TestPlanReferenceSubPlanTest()
        {
            int parallelism = 10;
            var planName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".TapPlan");
            string knownLogMessage = "Hello";
            using (Session.Create())
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
                        Filepath = {Text = planName}
                    };
                    var hideSteps = tpr1.GetType().GetProperty("HideSteps", BindingFlags.Instance | BindingFlags.NonPublic);
                    hideSteps.SetValue(tpr1, true);
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

        string MakeSubPlan(int n)
        {
            
            var subPlan = n > 0 ? MakeSubPlan(n - 1) : null;
            string planName = "sub_plan" + n + ".TapPlan";

            var plan = new TestPlan();
            var step = new DelayStep();
            plan.ChildTestSteps.Add(step);
            plan.Save(planName);
            TypeData.GetTypeData(step).GetMember(nameof(step.DelaySecs)).Parameterize(plan, step, "delay");
            if (n > 0)
            {
                var refPlan = new TestPlanReference();
                refPlan.Filepath.Text = subPlan;
                plan.ChildTestSteps.Add(refPlan);
                refPlan.LoadTestPlan();
                TypeData.GetTypeData(refPlan).GetMember("delay").Parameterize(plan, refPlan, "delay");
            }
            plan.Save(planName);
            return planName;
        }

        IEnumerable<IMemberData> UnrollMemberData(IParameterMemberData p)
        {
            List<IMemberData> members = new List<IMemberData>();
            foreach (var mem in p.ParameterizedMembers)
            {
                if (mem.Member is IParameterMemberData p2)
                    members.AddRange(UnrollMemberData(p2));
                else
                    members.Add(mem.Member);
            }

            return members;
        }
        
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(8)]
        public void TestPlanReferenceSubSubPlanTest(int n)
        {
            var planName = MakeSubPlan(n);
            var plan = TestPlan.Load(planName);
            var param = (IParameterMemberData)TypeData.GetTypeData(plan).GetMember("delay");
            var ps = UnrollMemberData(param);
            Assert.AreEqual(n + 1, ps.Count());
            param.SetValue(plan, 0.01);
            var allDelays = plan.ChildTestSteps.RecursivelyGetAllTestSteps(TestStepSearch.All).OfType<DelayStep>();
            foreach (var delay in allDelays)
            {
                Assert.AreEqual(0.01, delay.DelaySecs);
            }
            Assert.AreEqual(n + 1, allDelays.Count());

        }
    }
}
