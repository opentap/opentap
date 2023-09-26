using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class TeeStreamTest
    {
        [TestCase(100)]
        [TestCase(100000)]
        [TestCase(1000000)]
        public void TestReadWriteTeeStream(int len)
        {
            
            var rnd = new Random(123);
            var bytes = new byte[len];
            rnd.NextBytes(bytes);
            var str = new MemoryStream(bytes);
            var tee = new TeeStream(str);
            var streams = tee.CreateClientStreams(2);
            var s1 = streams[0];
            var s2 = streams[1];
            byte[] h1 = null;
            byte[] h2 = null;
            byte[] readStream(Stream s)
            {
                using var sh1 = SHA1.Create();
                return sh1.ComputeHash(s);
            }
            var t1 = TapThread.StartAwaitable(() =>
            {
                h1 = readStream(s1);
            });
            var t2 = TapThread.StartAwaitable(() =>
            {
                h2 = readStream(s2);
            });
            t1.Wait();
            t2.Wait();
            Assert.IsTrue(h1.SequenceEqual(h2));
            
            using var sh2 = SHA1.Create();
            var r2 = sh2.ComputeHash(bytes);
            Assert.IsTrue(r2.SequenceEqual(h1));


        }
        
        [TestCase(0)]
        [TestCase(100)]
        [TestCase(10000)]
        [TestCase(10001)]
        [TestCase(100001)]
        [TestCase(1000001)]
        public void TestReadWriteTeeStream1(int len)
        {
            
            var rnd = new Random(123);
            var bytes = new byte[len];
            rnd.NextBytes(bytes);
            var str = new MemoryStream(bytes);
            var tee = new TeeStream(str);
            var streams = tee.CreateClientStreams(1);
            var s1 = streams[0];
            byte[] h1 = null;
            byte[] readStream(Stream s)
            {
                using var sh1 = SHA1.Create();
                return sh1.ComputeHash(s);
            }
            var t1 = TapThread.StartAwaitable(() =>
            {
                h1 = readStream(s1);
            });
            t1.Wait();
            
            using var sh2 = SHA1.Create();
            var r2 = sh2.ComputeHash(bytes);
            Assert.IsTrue(r2.SequenceEqual(h1));
        }
        
    }
}
