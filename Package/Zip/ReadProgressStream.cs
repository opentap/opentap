//            Copyright Keysight Technologies 2012-2026
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.IO;

namespace OpenTap.Package.Zip;

/// <summary>
/// Reports how much of a file has been read, as (bytes read, file length). Used to report extraction progress for
/// packages that are already on disk, which the zip reader reads from start to end.
/// Only reports when the whole percentage changes, so at most about 100 times per file, even though the zip reader
/// makes many small reads.
/// </summary>
/// <param name="inner">The file to read. Disposed with this stream.</param>
/// <param name="progress">Called with the number of bytes read and the file length.</param>
internal sealed class ReadProgressStream(FileStream inner, Action<long, long> progress) : Stream
{
    /* FileStream.Length queries the file system on every call, so it is read once. */
    readonly long length = inner.Length;
    long read;
    int lastPercent = -1;

    public override int Read(byte[] buffer, int offset, int count)
    {
        int n = inner.Read(buffer, offset, count);
        Report(n);
        return n;
    }

    void Report(int n)
    {
        read += n;
        int percent = length == 0 ? 100 : (int)(Math.Min(read, length) * 100 / length);
        if (percent == lastPercent) return;
        lastPercent = percent;
        progress(read, length);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();
        base.Dispose(disposing);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => length;
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
