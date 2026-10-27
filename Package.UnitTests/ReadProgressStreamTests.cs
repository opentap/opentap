using NUnit.Framework;
using OpenTap.Package.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace OpenTap.Package.UnitTests
{
    public class ReadProgressStreamTests
    {
        string path;

        [SetUp]
        public void SetUp() => path = Path.GetTempFileName();

        [TearDown]
        public void TearDown() => File.Delete(path);

        [Test]
        public void ReportsIncreasingProgressEndingAtFileLength()
        {
            var data = new byte[100_000];
            new Random(1).NextBytes(data);
            File.WriteAllBytes(path, data);

            var reports = new List<(long read, long length)>();
            using (var stream = new ReadProgressStream(File.OpenRead(path), (read, length) => reports.Add((read, length))))
            {
                var buffer = new byte[7];
                while (stream.Read(buffer, 0, buffer.Length) > 0) { }
            }

            Assert.That(reports.Select(r => r.read), Is.Ordered.And.Unique);
            Assert.That(reports.Select(r => r.length), Is.All.EqualTo((long)data.Length));
            Assert.AreEqual((long)data.Length, reports[^1].read);
        }

        [Test]
        public void ReportsOncePerPercent()
        {
            File.WriteAllBytes(path, new byte[100_000]);
            int reports = 0;
            using (var stream = new ReadProgressStream(File.OpenRead(path), (_, _) => reports++))
            {
                // 50,000 small reads, like the zip reader's header reads.
                var buffer = new byte[2];
                while (stream.Read(buffer, 0, buffer.Length) > 0) { }
            }
            Assert.That(reports, Is.InRange(100, 101), "Expected one report per whole percent (1..100), plus at most one at 0%.");
        }

        [Test]
        public void EmptyFileReportsComplete()
        {
            var reports = new List<(long read, long length)>();
            using (var stream = new ReadProgressStream(File.OpenRead(path), (read, length) => reports.Add((read, length))))
                Assert.AreEqual(0, stream.Read(new byte[16], 0, 16));
            CollectionAssert.AreEqual(new[] { (0L, 0L) }, reports);
        }

        [Test]
        public void ZipReaderProgressReachesEndOfArchive()
        {
            using (var zip = new ZipArchive(File.Create(path), ZipArchiveMode.Create))
                for (int i = 0; i < 200; i++)
                {
                    using var w = new StreamWriter(zip.CreateEntry($"dir/file{i}.txt").Open());
                    w.Write(new string((char)('a' + i % 26), 1000 + i * 50));
                }

            var reports = new List<(long read, long length)>();
            using (var reader = new StreamingZipReader(new ReadProgressStream(File.OpenRead(path), (read, length) => reports.Add((read, length)))))
            {
                foreach (var entry in reader)
                    entry.Stream.CopyTo(Stream.Null);
                reader.Validate();
            }

            Assert.That(reports.Count, Is.GreaterThan(10), "Progress should be reported throughout extraction.");
            Assert.That(reports.Select(r => r.read), Is.Ordered);
            Assert.AreEqual(new FileInfo(path).Length, reports[^1].read, "The zip reader reads the whole archive, including the end of central directory record.");
        }
    }
}
