using NUnit.Framework;
namespace OpenTap.UnitTests
{
    [TestFixture]
    public class CacheClassTest
    {
        [Test]
        public void TestInvalidationRemoval()
        {
            int changeId = 0;
            for (int j = 0; j < 3; j++)
            {
                var cache = new Cache<int, int>(() => changeId);
                for (int i = 0; i < 100; i++)
                {
                    cache.AddValue(i, i + 1);
                }
                for (int i = 0; i < 100; i++)
                {
                    Assert.IsTrue(cache.TryGetValue(i, out var i2));
                    Assert.AreEqual(i + 1, i2);
                }
                changeId += 1;
                for (int i = 0; i < 100; i++)
                {
                    Assert.IsFalse(cache.TryGetValue(i, out var i2));
                }
                Assert.AreEqual(0, cache.Count);
            }
        }
    }
}
