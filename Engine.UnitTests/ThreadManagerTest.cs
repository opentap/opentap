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
        public void TestReuseThread()
        {
            int id = 0;
            int id2 = 0;
            int id3 = 0;
            int id4 = 0;
            int id5 = 0;
            TapThread.StartAwaitable(() =>
            {
                id = Thread.CurrentThread.ManagedThreadId;
            }).Wait();
            TapThread.StartAwaitable(() =>
            {
                id2 = Thread.CurrentThread.ManagedThreadId;
            }).Wait();
            TapThread.StartAwaitable(() =>
            {
                id3 = Thread.CurrentThread.ManagedThreadId;
                TapThread.StartAwaitable(() =>
                {
                    id4 = Thread.CurrentThread.ManagedThreadId;
                }).Wait();
                TapThread.StartAwaitable(() =>
                {
                    id5 = Thread.CurrentThread.ManagedThreadId;
                }).Wait();
            }).Wait();

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
            
            // Abort the thread that enqueued the work (trd1). 
            // This should not abort the work itself (i.e. jobEndWait should be released) 
            // since the WorkQueue uses another thread (baseThread) as the parent thread for all work
            trd1.Abort();
            trd1AbortWait.Release();

            Assert.IsTrue(jobEndWait.Wait(10000));
            Assert.IsTrue(workEnded1);
            blockBaseThreadWait.Release();
            baseThread.Abort();

            bool workEnded2 = false;
            workQueue.EnqueueWork(() =>
            {
                // Now that the baseThread is aborted, test that the thread running the work is also set to abort.
                Assert.IsTrue(TapThread.Current.AbortToken.IsCancellationRequested);
                workEnded2 = true;
            });
            workQueue.Wait();
            Assert.IsTrue(workEnded2);
        }
    }
}