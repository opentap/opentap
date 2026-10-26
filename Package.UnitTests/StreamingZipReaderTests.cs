using NUnit.Framework;
using OpenTap.Package.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace OpenTap.Package.UnitTests
{
    /// <summary>
    /// Tests for <see cref="StreamingZipReader"/>. Archives the reader cannot safely stream must throw
    /// <see cref="UnstreamableZipException"/>, so staging falls back to <see cref="ZipArchive"/>.
    /// Archives that are definitely broken may throw other exceptions, which fail the deployment.
    /// </summary>
    public class StreamingZipReaderTests
    {
        static byte[] CreateZip(params (string name, string content)[] files) => CreateZip(null, CompressionLevel.Optimal, files);

        static byte[] CreateZip(string comment, CompressionLevel level, params (string name, string content)[] files)
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                if (comment != null) zip.Comment = comment;
                foreach (var (name, content) in files)
                {
                    using var writer = new StreamWriter(zip.CreateEntry(name, level).Open());
                    writer.Write(content);
                }
            }
            return ms.ToArray();
        }

        static readonly (string name, string content)[] ThreeFiles =
        [
            ("a.txt", "hello"),
            ("dir/b.txt", new string('x', 5000)),
            ("c.txt", "world"),
        ];

        /// <summary> Streams the archive from a file, like staging does, and returns the content of every entry. </summary>
        static List<(string name, string content)> Read(byte[] archive)
        {
            var path = Path.GetTempFileName();
            try
            {
                System.IO.File.WriteAllBytes(path, archive);
                using var zip = new StreamingZipReader(System.IO.File.OpenRead(path));
                var result = new List<(string, string)>();
                foreach (var entry in zip)
                {
                    using var reader = new StreamReader(entry.Stream);
                    result.Add((entry.Fullname, reader.ReadToEnd()));
                }
                zip.Validate();
                return result;
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [TestCase(CompressionLevel.Optimal)]
        [TestCase(CompressionLevel.NoCompression)]
        public void ReadsValidArchive(CompressionLevel level)
        {
            var files = Read(CreateZip(null, level, ThreeFiles));
            CollectionAssert.AreEqual(ThreeFiles, files);
        }

        [Test]
        public void ReadsSingleFileArchive()
        {
            var files = Read(CreateZip(("a.txt", "hello")));
            CollectionAssert.AreEqual(new[] { ("a.txt", "hello") }, files);
        }

        [Test]
        public void ReadsEmptyArchive()
        {
            CollectionAssert.IsEmpty(Read(CreateZip()));
        }

        [Test]
        public void ReadsArchiveWithComment()
        {
            var files = Read(CreateZip("an archive comment", CompressionLevel.Optimal, ThreeFiles));
            CollectionAssert.AreEqual(ThreeFiles, files);
        }

        [Test]
        public void ReadsArchiveRewrittenInUpdateMode()
        {
            var ms = new MemoryStream();
            ms.Write(CreateZip(ThreeFiles));
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Update, true))
            {
                zip.GetEntry("a.txt").Delete();
                using var writer = new StreamWriter(zip.CreateEntry("d.txt").Open());
                writer.Write("new");
            }

            var names = Read(ms.ToArray()).Select(f => f.name);
            CollectionAssert.AreEquivalent(new[] { "dir/b.txt", "c.txt", "d.txt" }, names);
        }

        [Test]
        public void RecordCountMismatchIsUnstreamable()
        {
            var archive = CreateZip(ThreeFiles);
            // Without a comment, the end of central directory record is the last 22 bytes.
            // TotalNumberOfCentralDirectoryRecords is at offset 10.
            int eocd = archive.Length - 22;
            Assert.AreEqual(3, BitConverter.ToUInt16(archive, eocd + 10));
            archive[eocd + 10] = 2;

            Assert.Throws<UnstreamableZipException>(() => Read(archive));
        }

        [Test]
        public void DuplicateEntriesAreUnstreamable()
        {
            var archive = CreateZip(("a.txt", "first"), ("a.txt", "second"));
            Assert.Throws<UnstreamableZipException>(() => Read(archive));
        }

        [Test]
        public void DataDescriptorsAreUnstreamable()
        {
            // Writing to a non-seekable stream makes ZipArchive use data descriptors (general purpose bit 3),
            // where the sizes are written after the file data instead of in the local header.
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(new NonSeekableStream(ms), ZipArchiveMode.Create, true))
            {
                using var writer = new StreamWriter(zip.CreateEntry("a.txt").Open());
                writer.Write("hello");
            }
            Assert.Throws<UnstreamableZipException>(() => Read(ms.ToArray()));
        }

        [Test]
        public void TruncatedInFileDataIsUnstreamable()
        {
            // Uncompressed, so 1000 bytes ends inside the 5000 bytes of dir/b.txt.
            var archive = CreateZip(null, CompressionLevel.NoCompression, ThreeFiles);
            Assert.Throws<UnstreamableZipException>(() => Read(archive[..1000]));
        }

        // An archive cut off after the file data cannot be read by ZipArchive either, so it fails without a fallback.
        [TestCase(40, TestName = "TruncatedInCentralDirectoryFails")]
        [TestCase(18, TestName = "TruncatedAfterEndOfCentralDirectorySignatureFails")]
        [TestCase(10, TestName = "TruncatedInEndOfCentralDirectoryFails")]
        public void TruncatedAfterFileDataFails(int bytesRemoved)
        {
            var archive = CreateZip(ThreeFiles);
            Assert.Throws<EndOfStreamException>(() => Read(archive[..(archive.Length - bytesRemoved)]));
        }

        [Test]
        public void NonZipFileIsUnstreamable()
        {
            // ZipArchive gets the final say on files that do not look like a zip archive.
            Assert.Throws<UnstreamableZipException>(() => Read(Encoding.ASCII.GetBytes("this is not a zip file")));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void TooShortFileIsInvalid(int length)
        {
            Assert.Throws<InvalidDataException>(() => Read(new byte[] { 0x50, 0x4b }[..length]));
        }

        [Test]
        public void SkipsUnreadEntryData()
        {
            using var zip = new StreamingZipReader(new MemoryStream(CreateZip(ThreeFiles)));
            var names = zip.Select(f => f.Fullname).ToList(); // entry data is never read
            zip.Validate();
            CollectionAssert.AreEqual(ThreeFiles.Select(f => f.name), names);
        }

        [Test]
        public void ValidateBeforeExhaustedThrows()
        {
            using var zip = new StreamingZipReader(new MemoryStream(CreateZip(ThreeFiles)));
            Assert.Throws<InvalidOperationException>(zip.Validate);

            using var entries = zip.GetEnumerator();
            Assert.IsTrue(entries.MoveNext());
            Assert.Throws<InvalidOperationException>(zip.Validate);

            // A failed Validate call does not affect the reader.
            while (entries.MoveNext()) { }
            zip.Validate();
        }

        [Test]
        public void ValidateAfterFailedEnumerationThrows()
        {
            using var zip = new StreamingZipReader(new MemoryStream(CreateZip(("a.txt", "first"), ("a.txt", "second"))));
            Assert.Throws<UnstreamableZipException>(() => zip.ToList());
            Assert.Throws<InvalidOperationException>(zip.Validate);
        }

        [Test]
        public void EnumeratingTwiceThrows()
        {
            using var zip = new StreamingZipReader(new MemoryStream(CreateZip(ThreeFiles)));
            _ = zip.ToList();
            Assert.Throws<InvalidOperationException>(() => zip.ToList());
        }

        [Test]
        public void EmptyArchiveCanBeValidated()
        {
            using var zip = new StreamingZipReader(new MemoryStream(CreateZip()));
            CollectionAssert.IsEmpty(zip.ToList());
            zip.Validate();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisposeRespectsLeaveOpen(bool leaveOpen)
        {
            var stream = new MemoryStream(CreateZip(("a.txt", "hello")));
            new StreamingZipReader(stream, leaveOpen).Dispose();
            Assert.AreEqual(leaveOpen, stream.CanRead);
        }

        class NonSeekableStream(Stream inner) : Stream
        {
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        }
    }
}
