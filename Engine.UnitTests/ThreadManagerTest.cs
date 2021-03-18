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
    }
}