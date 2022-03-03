using System.Threading;
using NUnit.Framework;
namespace OpenTap.UnitTests
{
    public class ThreadManagerTest
    {
        [Test]
        public void TestAsyncTest()
        {
            bool completed = false;
            TapThread.StartAwaitable(() => completed = true, "test").Wait();
            Assert.IsTrue(completed);
        }

        [Test]
        public void TestWorkQueueContext()
        {
            var blockBaseThreadWait = new SemaphoreSlim(0);
            var trd1AbortWait = new SemaphoreSlim(0);
            var jobEndWait = new SemaphoreSlim(0);

            var baseThread = TapThread.Start(() => blockBaseThreadWait.Wait());

            var workQueue = new WorkQueue(WorkQueue.Options.None, "testthread", baseThread);
            bool workEnded1 = false;
            var trd1 = TapThread.Start(() =>
            {
                trd1AbortWait.Wait();
                workQueue.EnqueueWork(() =>
                {
                    // verify that since baseThread is not aborted, this does not throw.
                    // at this point only trd1 is aborted.
                    TapThread.ThrowIfAborted();
                    workEnded1 = true;
                    jobEndWait.Release();
                });
            });
            
            trd1.Abort();
            trd1AbortWait.Release();

            Assert.IsTrue(jobEndWait.Wait(10000));
            Assert.IsTrue(workEnded1);
            blockBaseThreadWait.Release();
            baseThread.Abort();

            bool workEnded2 = false;
            workQueue.EnqueueWork(() =>
            {
                Assert.IsTrue(TapThread.Current.AbortToken.IsCancellationRequested);
                workEnded2 = true;
            });
            workQueue.Wait();
            Assert.IsTrue(workEnded2);
        }
    }
}