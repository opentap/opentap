//            Copyright Keysight Technologies 2012-2026
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace OpenTap.Package.Zip;

internal class ThreadSafeZipReader : IDisposable
{
    readonly Stream f;
    readonly bool leaveOpen;
    private readonly object flock = new();

    public ThreadSafeZipReader(Stream stream, bool leaveOpen)
    {
        f = stream;
        this.leaveOpen = leaveOpen;
        byte[] magic = "PK\x03\x04"u8.ToArray();
        byte[] header = new byte[4];
        if (f.Read(header, 0, 4) != 4 || !header.SequenceEqual(magic))
            throw new ArgumentException("Stream is not a zip archive.", nameof(stream));

        byte[] eocd = new byte[22];
        f.Seek(f.Length - eocd.Length, SeekOrigin.Begin);
        if (f.Read(eocd, 0, eocd.Length) != eocd.Length)
            throw new InvalidOperationException("Failed to read eocd");
        Eocd = new(eocd);
        f.Seek(Eocd.OffsetOfStartOfCentralDirectory, SeekOrigin.Begin);
        using var reader = new BinaryReader(f, Encoding.Default, true);
        for (int i = 0; i < Eocd.TotalNumberOfCentralDirectoryRecords; i++)
        {
            CentralDirectory cd = new(reader);
            Entries.Add(new(cd, this));
        }
    }

    private readonly EndOfCentralDirectory Eocd;
    private readonly List<Part> _entries = [];
    List<Part> GetEntries()
    {
        if (_entries != null) return _entries;
        return null;
    }

    public List<Part> Entries => GetEntries();

    public class Part(CentralDirectory cd, ThreadSafeZipReader reader)
    {
        private readonly string path = Encoding.UTF8.GetString(cd.Filename);

        public string Name => Path.GetFileName(path);
        public string FullName => path;

        public Stream Open()
        {
            lock (reader.flock)
            {
                reader.f.Seek(cd.RelativeOffsetOfLocalFileHeader, SeekOrigin.Begin);
                using var binreader = new BinaryReader(reader.f, Encoding.Default, true);
                var header = new LocalFileHeader(binreader);
                if (header.compression == 0)
                {
                    return new MemoryStream(binreader.ReadBytes((int)header.compressedSize));
                }
                else
                {
                    var ms = new MemoryStream(binreader.ReadBytes((int)header.compressedSize));
                    return new DeflateStream(ms, CompressionMode.Decompress);
                }
            }
        }
        public long Length => cd.UncompressedSize;
    }

    public void Dispose()
    {
        if (!leaveOpen)
        {
            lock (flock)
            {
                f.Dispose();
            }
        }
    }

    class LocalFileHeader
    {
        public ushort version;
        public ushort generalPurpose;
        public ushort compression;
        public ushort lastModificationTime;
        public ushort lastModificationDate;
        public uint crc32;
        public uint compressedSize;
        public uint uncompressedSize;
        public ushort fileNameLength;
        public ushort extraFieldLength;
        public byte[] filename;
        public byte[] extra;

        public LocalFileHeader(BinaryReader reader)
        {
            var magic = new byte[] { (byte)'P', (byte)'K', 3, 4 };
            if (!magic.SequenceEqual(reader.ReadBytes(4))) throw new InvalidOperationException("Bad file header");
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
        private readonly static byte[] EocdSignature = [0x50, 0x4b, 0x05, 0x06];
        public ushort NumberOfThisDisk;
        public ushort DiskWhereCentralDirectoryStarts;
        public ushort NumberOfCentralDirectoryRecordsOnThisDisk;
        public ushort TotalNumberOfCentralDirectoryRecords;
        public uint SizeOfCentralDirectoryInBytes;
        public uint OffsetOfStartOfCentralDirectory;
        public ushort CommentLength = 0;
        public byte[] Comment = [];

        public EndOfCentralDirectory(byte[] content)
        {
            using var reader = new BinaryReader(new MemoryStream(content));
            if (!reader.ReadBytes(4).SequenceEqual(EocdSignature))
                throw new InvalidOperationException("Invalid eocd header");
            NumberOfThisDisk = reader.ReadUInt16();
            DiskWhereCentralDirectoryStarts = reader.ReadUInt16();
            NumberOfCentralDirectoryRecordsOnThisDisk = reader.ReadUInt16();
            TotalNumberOfCentralDirectoryRecords = reader.ReadUInt16();
            SizeOfCentralDirectoryInBytes = reader.ReadUInt32();
            OffsetOfStartOfCentralDirectory = reader.ReadUInt32();
            CommentLength = reader.ReadUInt16();
        }
    }
}

class CentralDirectory
{
    private static readonly byte[] cdSignature = [0x50, 0x4b, 0x01, 0x02];
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
        if (!reader.ReadBytes(4).SequenceEqual(cdSignature))
            throw new InvalidOperationException("Invalid central directory signature.");
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
