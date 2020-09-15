using System.Threading;
using NUnit.Framework;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class TapThreadTest
    {

        static ThreadField<object> tf = new ThreadField<object>();
        static ThreadField<object> tf2 = new ThreadField<object>();
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
        }
    }
}