using System;
using System.IO;
using System.Threading;
namespace OpenTap
{
    /// <summary> TeeStream allows many reader-streams to be created from one.
    /// If one reader is slower than the others, it will block until the slowest catches up. </summary>
    class TeeStream
    {
        class TeeStreamClient : Stream
        {
            int offset;
            long globalOffset = 0;
            readonly TeeStream teeStream;
            public TeeStreamClient(TeeStream teeStream)
            {
                this.teeStream = teeStream;
            }

            public override void Flush()
            {
            
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = teeStream.Read(buffer, globalOffset, offset, count);
                globalOffset += read;
                return read;
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }
            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => teeStream.Length;
            public override long Position { get; set; }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                this.CopyTo(Stream.Null);
            }
        }

        public long Length => str.Length;
        public long Position => str.Position - block_length;
        public long block_length;
            
        readonly Stream str;
        byte[] buffer;

        public TeeStream(Stream str) => this.str = str;
        
        Stream[] subStreams;
        Stream CreateClientStream() => new TeeStreamClient(this);

        public Stream[] CreateClientStreams(int count)
        {
            if (count == 0)
            {
                str.Dispose();
                return Array.Empty<Stream>();
            }
            buffer = new byte[4096 * count];
            clientCount = count;
            var result = new Stream[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = CreateClientStream();
            }
            return result;
        }
        bool done;
        
        void ReadBlock()
        {
            int len = str.Read(buffer, 0, buffer.Length);
            if (len == 0)
            {
                done = true;
                str.Close();
                str.Dispose();
            }
            block_length = len;
            var oldEvt = evt;
            var w2 = waiting;
            evt = new SemaphoreSlim(0);
            oldEvt.Release(w2);
            
            waiting = 0;
        }
        
        SemaphoreSlim evt = new SemaphoreSlim(0);
        int waiting;
        int clientCount;
        public int Read(byte[] bytes, long offset2, int offset, int count)
        {
            if (done) return 0;
            long innerOffset = offset2 - (str.Position - block_length);
            if (innerOffset < 0) 
                throw new Exception("!!");
            if (innerOffset >= block_length)
            {
                if (Interlocked.Increment(ref waiting) == clientCount)
                {
                    ReadBlock();
                }
                else
                {
                    evt.Wait();
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    long o2 = offset2 - (str.Position - block_length) + i;
                    if (o2 >= block_length)
                    {
                        int r = Read(bytes, offset2 + i, offset + i, count - i);
                        if (r == 0) return i;
                        return r + i;
                    }
                    bytes[i] = buffer[o2];
                }
                return count;
            }
            
            return Read(bytes, offset2, offset, count);
        }
    }
}
