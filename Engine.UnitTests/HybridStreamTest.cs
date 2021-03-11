using System.IO;
using NUnit.Framework;

namespace OpenTap.Engine.UnitTests
{
    public class HybridStreamTest
    {
        [Test]
        public void TestHybridStream()
        {
            var hybridStream = new HybridStream();
            while(hybridStream.Length + 1024 < hybridStream.MemoryThreshold)
                hybridStream.Write(new byte[1024], 0, 1024);

            Assert.IsFalse(File.Exists(hybridStream.Filename));
            hybridStream.Write(new byte[1024], 0, 1024);
            Assert.IsTrue(File.Exists(hybridStream.Filename));
            hybridStream.Close();
            Assert.IsFalse(File.Exists(hybridStream.Filename));

        }
    }
}