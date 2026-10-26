//            Copyright Keysight Technologies 2012-2026
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace OpenTap.Package.Zip;

internal class UnstreamableZipException(string message) : Exception(message) { }

/// <summary>
/// Reads a zip archive in a single forward pass, without seeking. Enumerate the entries, then call
/// <see cref="Validate"/> to check them against the central directory. Entries must not be trusted
/// until <see cref="Validate"/> has succeeded.
///
/// Archives that cannot be streamed safely cause an <see cref="UnstreamableZipException"/>. In that case, read the
/// archive with <see cref="ZipArchive"/> instead, which uses the central directory.
/// </summary>
/// <param name="stream">The archive. Must be positioned at the start of the archive. Does not need to be seekable.</param>
/// <param name="leaveOpen">If true, <paramref name="stream"/> is not disposed when the reader is disposed.</param>
internal sealed class StreamingZipReader(Stream stream, bool leaveOpen = false)
    : IEnumerable<StreamingZipReader.File>, IDisposable
{
    class SubStream(Stream inner, long length) : Stream
    {
        readonly long totalSize = length;
        readonly Stream inner = inner;
        long remaining = length;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (remaining <= 0) return 0;
            int n = inner.Read(buffer, offset, (int)Math.Min(count, remaining));
            if (n == 0) throw new UnstreamableZipException($"Zip entry truncated.");
            remaining -= n;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => totalSize;
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    }

    public class File(Stream stream, string fullname, long length)
    {
        public Stream Stream => stream;
        public string Fullname => fullname;
        /// <summary> Uncompressed size from the local file header. </summary>
        public long Length => length;
    }

    static void CheckHeader(LocalFileHeader header)
    {
        /* These exceptions will generally not be surfaced to the user.
         * They will just trigger a fallback to the builtin ZipArchive implementation,
         * which cleanly handles all the below cases, but with the drawback
         * that it requires a seekable stream, whereas this implementation can
         * extract most files in a single forward pass. */
        if ((header.generalPurpose & 8) != 0)
            throw new UnstreamableZipException($"Zip file is not streamable.");
        if (header.compression != 0 && header.compression != 8)
            throw new UnstreamableZipException($"Unknown compression method: {header.compression}");
        if (header.compression == 0 && header.compressedSize != header.uncompressedSize)
            throw new UnstreamableZipException($"Bad local header: File size mismatch");
        if (header.compressedSize == 0xFFFFFFFF || header.uncompressedSize == 0xFFFFFFFF)
            throw new UnstreamableZipException($"ZIP64 not supported.");
    }

    readonly BinaryReader reader = new(stream, Encoding.Default, true);
    /* track streamed files in order to verify CentralDirectory records agree. */
    readonly Dictionary<string, LocalFileHeader> streamedFiles = [];
    /* the signature of the record following the last local file. */
    readonly byte[] magic = new byte[4];
    bool enumerated, exhausted;

    /// <summary>
    /// Enumerates the file entries. Each entry's stream is only valid until the enumeration continues.
    /// The archive can only be enumerated once.
    /// </summary>
    public IEnumerator<File> GetEnumerator()
    {
        // The stream is forward-only, so the entries can only be read once.
        if (enumerated) throw new InvalidOperationException("The archive can only be enumerated once.");
        enumerated = true;
        return ReadLocalFiles();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    IEnumerator<File> ReadLocalFiles()
    {
        if (stream.Read(magic, 0, 4) != 4)
            throw new InvalidDataException($"Not a zip archive.");

        while (magic.SequenceEqual(LocalFileHeader.Signature))
        {
            var header = new LocalFileHeader(reader);
            CheckHeader(header);
            bool compressed = header.compression == 8;
            SubStream sub = new(stream, compressed ? (long)header.compressedSize : (long)header.uncompressedSize);
            Stream s = compressed ? new DeflateStream(sub, CompressionMode.Decompress) : sub;
            yield return new File(s, Encoding.UTF8.GetString(header.filename), (long)header.uncompressedSize);
            /* ensure the base stream is fast-forwarded to the next local file header */
            sub.CopyTo(Stream.Null);

            /* If the archive contains duplicate entries, that likely means the zip
             * was updated by appending. In that case, the central directory
             * will be authoritative and we should stop processing. */
            if (streamedFiles.ContainsKey(header.FullName))
                throw new UnstreamableZipException($"Archive contains duplicate entries.");
            streamedFiles[header.FullName] = header;
            if (stream.Read(magic, 0, 4) != 4)
                break;
        }
        exhausted = true;
    }

    /// <summary>
    /// Reads the central directory and checks that it describes exactly the file entries that were read.
    /// Throws <see cref="UnstreamableZipException"/> if they differ, in which case the archive should be read
    /// with <see cref="ZipArchive"/> instead.
    /// </summary>
    /// <exception cref="InvalidOperationException">Not all file entries have been read.</exception>
    public void Validate()
    {
        if (!exhausted)
            throw new InvalidOperationException("All entries must be read before the archive can be validated.");
        int numFiles = streamedFiles.Count;

        while (magic.SequenceEqual(CentralDirectory.Signature))
        {
            var cd = new CentralDirectory(reader);
            var fullname = Encoding.UTF8.GetString(cd.Filename);
            if (!streamedFiles.ContainsKey(fullname))
                throw new UnstreamableZipException($"File missing from local headers: {fullname}");
            var header = streamedFiles[fullname];
            if (header.compressedSize != cd.CompressedSize || header.uncompressedSize != cd.UncompressedSize)
                throw new UnstreamableZipException($"File size mismatch.");
            streamedFiles.Remove(fullname);
            if (stream.Read(magic, 0, 4) != 4) 
                break;
        }

        /* If there are files unaccounted for in the central directory, then we
         * extracted files which should not be included in the archive. In that case,
         * we should fall back to ZipArchive and use only the files included in the central directory. */
        if (streamedFiles.Count > 0)
            throw new UnstreamableZipException("Local headers contained files unaccounted for in central directory");

        if (!magic.SequenceEqual(EndOfCentralDirectory.Signature))
            throw new UnstreamableZipException("Partial stream detected.");
        var eocd = new EndOfCentralDirectory(reader);
        if (eocd.TotalNumberOfCentralDirectoryRecords != numFiles)
            throw new UnstreamableZipException($"Record count mismatch");
    }

    public void Dispose()
    {
        reader.Dispose();
        if (!leaveOpen)
            stream.Dispose();
    }

    class LocalFileHeader
    {
        public static readonly byte[] Signature = "PK\x03\x04"u8.ToArray();
        public ushort version;
        public ushort generalPurpose;
        public ushort compression;
        public ushort lastModificationTime;
        public ushort lastModificationDate;
        public uint crc32;
        public ulong compressedSize;
        public ulong uncompressedSize;
        public ushort fileNameLength;
        public ushort extraFieldLength;
        public byte[] filename;
        public byte[] extra;

        public string FullName => Encoding.UTF8.GetString(filename);

        public LocalFileHeader(BinaryReader reader)
        {
            version = reader.ReadUInt16();
            generalPurpose = reader.ReadUInt16();
            compression = reader.ReadUInt16();
            lastModificationTime = reader.ReadUInt16();
            lastModificationDate = reader.ReadUInt16();
            crc32 = reader.ReadUInt32();
            compressedSize = reader.ReadUInt32();
            uncompressedSize = reader.ReadUInt32();
            fileNameLength = reader.ReadUInt16();
            extraFieldLength = reader.ReadUInt16();
            filename = reader.ReadBytes(fileNameLength);
            extra = reader.ReadBytes(extraFieldLength);
        }
    }

    class EndOfCentralDirectory
    {
        public readonly static byte[] Signature = [0x50, 0x4b, 0x05, 0x06];
        public ushort NumberOfThisDisk;
        public ushort DiskWhereCentralDirectoryStarts;
        public ushort NumberOfCentralDirectoryRecordsOnThisDisk;
        public ushort TotalNumberOfCentralDirectoryRecords;
        public uint SizeOfCentralDirectoryInBytes;
        public uint OffsetOfStartOfCentralDirectory;
        public ushort CommentLength = 0;
        public byte[] Comment = [];

        public EndOfCentralDirectory(BinaryReader reader)
        {
            NumberOfThisDisk = reader.ReadUInt16();
            DiskWhereCentralDirectoryStarts = reader.ReadUInt16();
            NumberOfCentralDirectoryRecordsOnThisDisk = reader.ReadUInt16();
            TotalNumberOfCentralDirectoryRecords = reader.ReadUInt16();
            SizeOfCentralDirectoryInBytes = reader.ReadUInt32();
            OffsetOfStartOfCentralDirectory = reader.ReadUInt32();
            CommentLength = reader.ReadUInt16();
        }
    }
    class CentralDirectory
    {
        public static readonly byte[] Signature = [0x50, 0x4b, 0x01, 0x02];
        public ushort VersionMadeBy;
        public ushort MinimumVersion;
        public ushort GeneralPurpose;
        public ushort CompressionMethod;
        public ushort FileLastModificationTime;
        public ushort FileLastModificationDate;
        public uint Crc32;
        public uint CompressedSize;
        public uint UncompressedSize;
        public ushort FileNameLength;
        public ushort ExtraFieldLength;
        public ushort FileCommentLength;
        public ushort DiskNumberWhereFileStarts;
        public ushort InternalFileAttributes;
        public uint ExternalFileAttributes;
        public uint RelativeOffsetOfLocalFileHeader;
        public byte[] Filename;
        public byte[] ExtraFieldData;
        public byte[] FileComment;

        public CentralDirectory(BinaryReader reader)
        {
            VersionMadeBy = reader.ReadUInt16();
            MinimumVersion = reader.ReadUInt16();
            GeneralPurpose = reader.ReadUInt16();
            CompressionMethod = reader.ReadUInt16();
            FileLastModificationTime = reader.ReadUInt16();
            FileLastModificationDate = reader.ReadUInt16();
            Crc32 = reader.ReadUInt32();
            CompressedSize = reader.ReadUInt32();
            UncompressedSize = reader.ReadUInt32();
            FileNameLength = reader.ReadUInt16();
            ExtraFieldLength = reader.ReadUInt16();
            FileCommentLength = reader.ReadUInt16();
            DiskNumberWhereFileStarts = reader.ReadUInt16();
            InternalFileAttributes = reader.ReadUInt16();
            ExternalFileAttributes = reader.ReadUInt32();
            RelativeOffsetOfLocalFileHeader = reader.ReadUInt32();
            Filename = reader.ReadBytes(FileNameLength);
            ExtraFieldData = reader.ReadBytes(ExtraFieldLength);
            FileComment = reader.ReadBytes(FileCommentLength);
        }
    }
}
