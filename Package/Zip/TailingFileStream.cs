//            Copyright Keysight Technologies 2012-2026
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Package.Zip;

/// <summary>
/// Shared between a thread writing a file and a <see cref="TailingFileStream"/> reading it. The writer signals when
/// data has been written, and when it has completed or failed. The reader waits on the signal when it has caught up.
/// </summary>
internal sealed class FileWriteSignal
{
    readonly ManualResetEventSlim changed = new(false);
    Exception error;
    volatile bool completed;

    /// <summary> Called by the writer after data has been written to the file. </summary>
    public void NotifyWritten() => changed.Set();

    /// <summary>
    /// Called by the writer when it will not write any more data. All data must be in the file at this point.
    /// If <paramref name="error"/> is set, the reader throws it when it reaches the end of the written data.
    /// </summary>
    public void Complete(Exception error = null)
    {
        this.error = error;
        completed = true; // volatile write: publishes error to readers that see completed == true
        changed.Set();
    }

    /// <summary>
    /// Called by the reader before reading. Returns true if the writer has completed, in which case a read
    /// returning 0 bytes means the real end of the file.
    /// </summary>
    internal bool PrepareRead()
    {
        /* Reset before reading, so a write that happens after the read sets the signal again and is not missed. */
        changed.Reset();
        return completed;
    }

    /// <summary> Waits until more data has been written, or the writer has completed. </summary>
    internal void Wait(CancellationToken token) => changed.Wait(token);

    internal void ThrowIfFailed()
    {
        if (error != null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}

/// <summary> Writes to a stream and calls <see cref="FileWriteSignal.NotifyWritten"/> after every write. </summary>
/// <param name="inner">The file. Should be opened with a buffer size of 1, so each write goes straight to the file.</param>
/// <param name="signal">The signal to notify.</param>
internal sealed class SignalingWriteStream(FileStream inner, FileWriteSignal signal) : Stream
{
    public override void Write(byte[] buffer, int offset, int count)
    {
        inner.Write(buffer, offset, count);
        signal.NotifyWritten();
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await inner.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        signal.NotifyWritten();
    }

    public override void Flush()
    {
        inner.Flush();
        signal.NotifyWritten();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose(); // flushes any buffered data before the writer completes
        base.Dispose(disposing);
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => inner.Length;
    /// <summary> Used by the download to resume from the end of the file. </summary>
    public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}

/// <summary>
/// Reads a file while another thread is still writing it, e.g. a package that is being downloaded.
/// When the reader catches up with the writer, <see cref="Read(byte[], int, int)"/> waits for the writer to signal
/// more data instead of returning early. It only returns fewer bytes than requested at the real end of the file,
/// after the writer has completed. If the writer failed, the read rethrows its exception.
///
/// Small reads are served from an internal buffer, which is filled with whatever is available in a single read
/// from the file.
/// </summary>
/// <param name="file">The file being written. Must be opened with <see cref="FileShare.ReadWrite"/>, and should be
/// opened with a buffer size of 1, so that it does not cache the end of the file.</param>
/// <param name="signal">Signaled by the writer. See <see cref="SignalingWriteStream"/>.</param>
/// <param name="token">Cancels waiting for the writer.</param>
internal sealed class TailingFileStream(FileStream file, FileWriteSignal signal, CancellationToken token) : Stream
{
    const int BufferSize = 64 * 1024;

    readonly byte[] buffer = new byte[BufferSize];
    int start, end;

    public override int Read(byte[] dest, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            if (start == end)
            {
                /* Large reads bypass the buffer, like BufferedStream. */
                if (count - total >= buffer.Length)
                {
                    int direct = ReadAvailable(dest, offset + total, count - total);
                    if (direct == 0) break;
                    total += direct;
                    continue;
                }
                start = 0;
                end = ReadAvailable(buffer, 0, buffer.Length);
                if (end == 0) break;
            }
            int n = Math.Min(count - total, end - start);
            Buffer.BlockCopy(buffer, start, dest, offset + total, n);
            start += n;
            total += n;
        }
        return total;
    }

    /// <summary> Reads whatever is available, waiting only if nothing is. Returns 0 only at the real end of the file. </summary>
    int ReadAvailable(byte[] dest, int offset, int count)
    {
        while (true)
        {
            /* Check for completion before reading. If the writer had already completed, a read returning 0 is the real
             * end of the file. If it is checked after, data written between the read and the check would be lost. */
            bool completed = signal.PrepareRead();
            int n = file.Read(dest, offset, count);
            if (n > 0) return n;
            if (completed)
            {
                signal.ThrowIfFailed();
                return 0;
            }
            signal.Wait(token);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            file.Dispose();
        base.Dispose(disposing);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
