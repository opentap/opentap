using NUnit.Framework;
using OpenTap.Package.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Package.UnitTests
{
    /// <summary>
    /// Tests for reading a file while it is being written, as done when staging a package during its download.
    /// </summary>
    public class TailingFileStreamTests
    {
        const FileShare Share = FileShare.ReadWrite | FileShare.Delete;

        string path;
        FileWriteSignal signal;
        SignalingWriteStream writer;

        [SetUp]
        public void SetUp()
        {
            path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            signal = new FileWriteSignal();
            writer = new SignalingWriteStream(new FileStream(path, FileMode.CreateNew, FileAccess.Write, Share, bufferSize: 1), signal);
        }

        [TearDown]
        public void TearDown()
        {
            writer.Dispose();
            File.Delete(path);
        }

        TailingFileStream OpenReader(CancellationToken token = default) =>
            new(new FileStream(path, FileMode.Open, FileAccess.Read, Share, bufferSize: 1), signal, token);

        /// <summary> Runs <paramref name="write"/> on another thread and completes the signal when it is done. </summary>
        Task StartWriter(Action<SignalingWriteStream> write) => Task.Run(() =>
        {
            try
            {
                using (writer) write(writer);
                signal.Complete();
            }
            catch (Exception ex)
            {
                signal.Complete(ex);
            }
        });

        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Test]
        public void ReadWaitsForPartialSignature()
        {
            using var reader = OpenReader();
            writer.Write("PK"u8.ToArray(), 0, 2);

            var buffer = new byte[4];
            var read = Task.Run(() => reader.Read(buffer, 0, 4));
            Assert.IsFalse(read.Wait(200), "Read must wait for the rest of the signature instead of returning 2 bytes.");

            writer.Write([3, 4], 0, 2);
            Assert.IsTrue(read.Wait(Timeout));
            Assert.AreEqual(4, read.Result);
            CollectionAssert.AreEqual("PK\x03\x04"u8.ToArray(), buffer);
        }

        [Test]
        public void ReadReturnsShortOnlyAtRealEnd()
        {
            using var reader = OpenReader();
            writer.Write([1, 2, 3], 0, 3);

            var buffer = new byte[10];
            var read = Task.Run(() => reader.Read(buffer, 0, 10));
            Assert.IsFalse(read.Wait(200));

            writer.Dispose();
            signal.Complete();
            Assert.IsTrue(read.Wait(Timeout));
            Assert.AreEqual(3, read.Result);
            Assert.AreEqual(0, reader.Read(buffer, 0, 10));
        }

        [Test]
        public void ConcurrentWriterGivesNoShortReads()
        {
            var data = new byte[8 << 20];
            new Random(1).NextBytes(data);
            using var reader = OpenReader();
            var writing = StartWriter(w =>
            {
                var rng = new Random(2);
                for (int pos = 0; pos < data.Length;)
                {
                    int n = Math.Min(rng.Next(1, 70_000), data.Length - pos);
                    w.Write(data, pos, n);
                    pos += n;
                    if (rng.Next(10) == 0) Thread.Sleep(rng.Next(1, 5));
                }
            });

            // Mix 4-byte reads, like zip signatures, with larger reads, including reads larger than the internal buffer.
            var result = new byte[data.Length];
            var sizes = new Random(3);
            int got = 0;
            while (got < data.Length)
            {
                int want = Math.Min(sizes.Next(3) == 0 ? 4 : sizes.Next(1, 100_000), data.Length - got);
                Assert.AreEqual(want, reader.Read(result, got, want), $"Short read at offset {got}.");
                got += want;
            }
            Assert.AreEqual(0, reader.Read(new byte[16], 0, 16));
            CollectionAssert.AreEqual(data, result);
            Assert.IsTrue(writing.Wait(Timeout));
        }

        [Test]
        public void WriterErrorIsRethrownAfterWrittenData()
        {
            using var reader = OpenReader();
            var error = new IOException("Connection lost");
            var writing = StartWriter(w =>
            {
                w.Write([1, 2, 3], 0, 3);
                throw error;
            });

            var buffer = new byte[10];
            var thrown = Assert.Throws<IOException>(() => reader.Read(buffer, 0, 10));
            Assert.AreSame(error, thrown);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, buffer.Take(3));
            Assert.IsTrue(writing.Wait(Timeout));
        }

        [Test]
        public void CancellationStopsWaiting()
        {
            using var cts = new CancellationTokenSource();
            using var reader = OpenReader(cts.Token);
            var read = Task.Run(() => reader.Read(new byte[4], 0, 4));
            Assert.IsFalse(read.Wait(200));

            cts.Cancel();
            var ex = Assert.Throws<AggregateException>(() => read.Wait(Timeout));
            Assert.IsInstanceOf<OperationCanceledException>(ex.InnerException);
        }

        [Test]
        public void ZipReaderReadsArchiveWhileItIsWritten()
        {
            var files = Enumerable.Range(0, 200).Select(i => ($"dir{i % 7}/file{i}.txt", new string((char)('a' + i % 26), 100 + i * 37))).ToList();
            var archive = new MemoryStream();
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, true))
                foreach (var (name, content) in files)
                {
                    using var w = new StreamWriter(zip.CreateEntry(name).Open());
                    w.Write(content);
                }
            var bytes = archive.ToArray();

            // Written in chunks that do not line up with zip records, like a download.
            var writing = StartWriter(w =>
            {
                var rng = new Random(4);
                for (int pos = 0; pos < bytes.Length;)
                {
                    int n = Math.Min(rng.Next(1, 300), bytes.Length - pos);
                    w.Write(bytes, pos, n);
                    pos += n;
                    if (rng.Next(20) == 0) Thread.Sleep(1);
                }
            });

            var read = new List<(string, string)>();
            using (var zip = new StreamingZipReader(OpenReader()))
            {
                foreach (var entry in zip)
                {
                    using var r = new StreamReader(entry.Stream);
                    read.Add((entry.Fullname, r.ReadToEnd()));
                }
                zip.Validate();
            }
            CollectionAssert.AreEqual(files, read);
            Assert.IsTrue(writing.Wait(Timeout));
        }

        [Test]
        public void FileCanBeMovedWhileBeingRead()
        {
            var final = path + ".final";
            try
            {
                using var reader = OpenReader();
                writer.Write([1, 2, 3, 4], 0, 4);
                var buffer = new byte[8];
                Assert.AreEqual(2, reader.Read(buffer, 0, 2));

                writer.Write([5, 6, 7, 8], 0, 4);
                writer.Dispose();
                File.Move(path, final);
                signal.Complete();

                Assert.AreEqual(6, reader.Read(buffer, 2, 6));
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, buffer);
            }
            finally
            {
                File.Delete(final);
            }
        }
    }
}
