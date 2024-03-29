using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class TapThreadTest
    {

        static ThreadField<object> tf = new ThreadField<object>();
        static ThreadField<object> tf2 = new ThreadField<object>();
        static ThreadField<object> tflat = new ThreadField<object>(ThreadFieldMode.Flat);
        [Test]
        public void TestThreadField()
        {
            tf.Value = 100;
            tf2.Value = 200;
            object value = null;
            Semaphore sem = new Semaphore(0,1);
            TapThread.Start(() =>
            {
                value = tf.Value;
                sem.Release();
            });
            sem.WaitOne();
            Assert.AreEqual(100, value);

            for (int i = 2000; i < 2010; i += 1)
            {
                TapThread.WithNewContext(() =>
                {
                    Assert.AreEqual(100, tf.Value);
                    tf.Value = i;
                    Assert.AreEqual(i, tf.Value);
                    TapThread.WithNewContext(() =>
                    {
                        // verify inherit values from parent thread.
                        Assert.AreEqual(i, tf.Value);
                        tf.Value = i * 2;
                        Assert.AreEqual(i * 2, tf.Value);
                    });
                });
                Assert.AreEqual(100, tf.Value);
            }

            // verify tflat.
            tflat.Value = 100;

            TapThread.WithNewContext(() =>
            {
                Assert.AreEqual(null, tflat.Value);
                tflat.Value = 200;
                TapThread.WithNewContext(() =>
                {
                    // verify it does NOT inherit values from parent thread.
                    Assert.AreEqual(null, tflat.Value);
                    tflat.Value = 300;
                    Assert.AreEqual(300, tflat.Value);

                });
                Assert.AreEqual(200, tflat.Value);
            });
            Assert.AreEqual(100, tflat.Value);

        }

        /// <summary>
        /// Test two levels of children where the last level finishes last
        /// </summary>
        [Test]
        public void TestHierarchyCompleted1()
        {
            ManualResetEventSlim level1Completed = new ManualResetEventSlim();
            ManualResetEventSlim level2Completed = new ManualResetEventSlim();
            ManualResetEventSlim hierarchyCompletedCallbackCalled = new ManualResetEventSlim();
            bool level1CompletedBeforeHierarchyCompletedCallback = false;
            bool level2CompletedBeforeHierarchyCompletedCallback = false;
            TapThread.Start(() =>
            {
                TapThread.Start(() =>
                {
                    Thread.Sleep(100);
                    TapThread.Start(() =>
                    {
                        Thread.Sleep(100);
                        level2Completed.Set();
                    }, "Level2");
                    level1Completed.Set();
                }, "Level1");
            }, () =>
            {
                level1CompletedBeforeHierarchyCompletedCallback = level1Completed.IsSet;
                level2CompletedBeforeHierarchyCompletedCallback = level2Completed.IsSet;
                hierarchyCompletedCallbackCalled.Set();
            }, "Root");
            Thread.Sleep(200);
            Assert.IsTrue(hierarchyCompletedCallbackCalled.Wait(3000), "onHierarchyCompleted callback not called.");
            Assert.IsTrue(level1CompletedBeforeHierarchyCompletedCallback, "Child thread did not complete before onHierarchyCompleted callback.");
            Assert.IsTrue(level2CompletedBeforeHierarchyCompletedCallback, "Second level child thread did not complete before onHierarchyCompleted callback.");
        }

        /// <summary>
        /// Test two levels of children where the last level finishes first
        /// </summary>
        [Test]
        public void TestHierarchyCompleted2()
        {
            ManualResetEventSlim level1Completed = new ManualResetEventSlim();
            ManualResetEventSlim level2Completed = new ManualResetEventSlim();
            ManualResetEventSlim hierarchyCompletedCallbackCalled = new ManualResetEventSlim();
            bool level1CompletedBeforeHierarchyCompletedCallback = false;
            bool level2CompletedBeforeHierarchyCompletedCallback = false;
            TapThread.Start(() =>
            {
                TapThread.Start(() =>
                {
                    TapThread.Start(() =>
                    {
                        level2Completed.Set();
                    }, "Level2");
                    Thread.Sleep(100);
                    level1Completed.Set();
                }, "Level1");
            }, () =>
            {
                level1CompletedBeforeHierarchyCompletedCallback = level1Completed.IsSet;
                level2CompletedBeforeHierarchyCompletedCallback = level2Completed.IsSet;
                hierarchyCompletedCallbackCalled.Set();
            }, "Root");
            Thread.Sleep(200);
            Assert.IsTrue(hierarchyCompletedCallbackCalled.Wait(30 * 1000), "onHierarchyCompleted callback not called.");
            Assert.IsTrue(level1CompletedBeforeHierarchyCompletedCallback, "Child thread did not complete before onHierarchyCompleted callback.");
            Assert.IsTrue(level2CompletedBeforeHierarchyCompletedCallback, "Second level child thread did not complete before onHierarchyCompleted callback.");
        }

        [Test]
        public async Task TestAwaitedThreadThrows([Values(true, false)] bool throws)
        {
            void callback()
            {
                if (throws)
                    throw new Exception("Throws");
            }

            var trd = TapThread.StartAwaitable(callback);

            if (throws)
            {
                try
                {
                    await trd;
                    Assert.Fail("This should have thrown.");
                }
                catch (Exception ex)
                {
                    StringAssert.Contains("Throws", ex.Message);
                }
            }
            else
            {
                await trd;
                Assert.Pass();
            }
        }

        /// <summary>
        /// Test what happens when sibling threads gets aborted - verify that only the right ones are aborted.
        /// </summary>
        [Test]
        public void MultipleThreadAbort()
        {
            TapThread mainThread = null;
            (TapThread, Semaphore)[] internalThreads = null;

            TapThread.WithNewContext(() =>
            {

                (TapThread, Semaphore) createThread()
                {
                    // Semaphores are released when the threads are aborted.
                    var sem = new Semaphore(0, 1);
                    var trd = TapThread.Start(() =>
                    {
                        try
                        {
                            TapThread.Sleep(200000);
                            Assert.Fail("The thread should have been aborted");
                        }
                        catch (OperationCanceledException)
                        {
                            sem.Release(1);
                        }
                    });
                    return (trd, sem);
                }

                var threadSems = Enumerable.Range(0, 20).Select(x => createThread()).ToArray();
                for (int i = 0; i < threadSems.Length; i++)
                {
                    threadSems[i].Item1.Abort();
                    Assert.IsTrue(threadSems[i].Item2.WaitOne(20000));
                    for (int j = i + 1; j < threadSems.Length; j++)
                    {
                        Assert.IsFalse(threadSems[j].Item2.WaitOne(0));
                    }
                }

                // now lets try aborting the parent thread.
                internalThreads = Enumerable.Range(0, 20).Select(x => createThread()).ToArray();
                mainThread = TapThread.Current;
            });

            mainThread.Abort();
            for (int i = 0; i < internalThreads.Length; i++)
            {
                Assert.IsTrue(internalThreads[i].Item2.WaitOne(20000));
            }
        }
    }
}
