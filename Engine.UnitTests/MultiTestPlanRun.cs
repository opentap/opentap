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
            using (Session.Create())
            {
                EngineSettings.Current.OperatorName = "Test1";

                var id = Session.Current.Id;
                TapThread.Start(() =>
                {
                    try
                    {
                        listener1 = new TestTraceListener();
                        Log.AddListener(listener1);
                        plan1.Execute();
                        Assert.AreEqual(id, Session.Current.Id);
                        Assert.AreEqual("Test1", EngineSettings.Current.OperatorName);
                    }
                    finally
                    {
                        plan1Complete.Set();
                    }
                });
                //Assert.AreEqual("Test1", EngineSettings.Current.OperatorName);
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

        [Test]
        public void Start()
        {
            TraceSource log = Log.CreateSource("StartTest");
            HashSet<Guid> sessionIds = new HashSet<Guid>();
            List<ManualResetEventSlim> sessionCompleted = new List<ManualResetEventSlim>();
            for (int i = 0; i < 5; i++)
            {
                var completed = new ManualResetEventSlim(false);
                sessionCompleted.Add(completed);
                Session.Start(() =>
                {
                    Thread.Sleep(20);
                    var sessionId = Session.Current.Id;
                    log.Info($"From session {sessionId}.");
                    lock (sessionIds)
                    {
                        CollectionAssert.DoesNotContain(sessionIds, sessionId);
                        sessionIds.Add(sessionId);
                    }
                    completed.Set();
                });
            }
            sessionCompleted.ForEach(s => s.Wait());
            Assert.AreEqual(5, sessionIds.Count);
            CollectionAssert.AllItemsAreUnique(sessionIds);
        }

        class TestDisposable : IDisposable
        {
            public int Id { get; set; }

            public bool IsDisposed { get; set; }

            ManualResetEvent waitForDispose = new ManualResetEvent(false);

            public void WaitForDispose() => waitForDispose.WaitOne();
            public void Dispose()
            {
                if (IsDisposed) throw new Exception("This value was previously disposed");
                IsDisposed = true;
                waitForDispose.Set();
            }
        }
        
        static SessionLocal<TestDisposable> testLocal = new SessionLocal<TestDisposable>(true);
        /// <summary>
        /// This unit test tests what happens with nested sessions and verifies that when session locals are configured
        /// they keep their values, even when the session ends and some threads depends on the values from the threads.
        /// Also checks that the values eventually will be disposed, even tough they  
        /// </summary>
        
        public void NestedStart()
        {
            // start a new thread and return an action that is called to check
            // if the value is still the expected one in that thread
            Action startLocalValueChecker(int expectedLocalValue, CancellationToken cancellationToken)
            {
                var sem = new SemaphoreSlim(0, 1);
                var sem2 = new SemaphoreSlim(0, 1);
                TapThread.Start(() =>
                {
                    try
                    {
                        while(true)
                        {
                            sem.Wait(cancellationToken);
                            try
                            {
                                Assert.IsFalse(testLocal.Value.IsDisposed);
                                Assert.AreEqual(expectedLocalValue, testLocal.Value.Id);
                            }
                            finally
                            {
                                sem2.Release();
                            }
                        }
                    }
                    catch
                    {
                        
                    }
                    finally
                    {
                        sem2.Release();
                    }
                });

                void doCheck()
                {
                    sem.Release();
                    sem2.Wait();
                }

                return doCheck;
            }

            var value1 = new TestDisposable(){Id = 5};
            var value2 = new TestDisposable(){Id = 15};
            var value3 = new TestDisposable(){Id = 25};
            
            
            testLocal.Value = value1;
            CancellationTokenSource c = new CancellationTokenSource();
            Action check = startLocalValueChecker(5, c.Token);
            check();
            using(Session.Create())
            {
                Assert.AreEqual(5, testLocal.Value.Id);
                testLocal.Value = value2;
                check += startLocalValueChecker(15, c.Token);
                check();
                Assert.AreEqual(15, testLocal.Value.Id);
                Assert.AreEqual(value2, testLocal.Value);
                using(Session.Create())
                {
                    Assert.AreEqual(value2, testLocal.Value);
                    Assert.AreEqual(15, testLocal.Value.Id);
                    testLocal.Value = value3;
                    check += startLocalValueChecker(25, c.Token);    
                    Assert.AreEqual(25, testLocal.Value.Id);
                    check();
                }

                check();
                Assert.AreEqual(15, testLocal.Value.Id);
            }
            Assert.AreEqual(5, testLocal.Value.Id);
            check();
            c.Cancel();
            check();
            
            Assert.IsFalse(value1.IsDisposed);
            value2.WaitForDispose();
            value3.WaitForDispose();
            Assert.IsTrue(value2.IsDisposed);
            Assert.IsTrue(value3.IsDisposed);
        }
    }
}
