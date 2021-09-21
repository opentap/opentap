using System;
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
        public void AbortThreadError()
        {
            var sem = new Semaphore(0, 1);
            var trd = TapThread.Start(() =>
            {
                // abort token.Register threw an exception.
                using (TapThread.Current.AbortToken.Register(() => throw new Exception("!!")))
                {
                    sem.Release();
                    TapThread.Sleep(100000);
                }
            });
            sem.WaitOne();
            trd.Abort();
        }
    }
}