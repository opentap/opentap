using System;
using System.IO;
using System.Threading;
namespace OpenTap
{
    /// <summary> TeeStream allows many reader-streams to be created from one.
    /// If one reader is slower than the others, it will block until the slowest catches up. </summary>
    class TeeStream
    {
        /// <summary> Represents a client stream that reads from a shared TeeStream. </summary>
        class TeeStreamClient : Stream
        {
            /// <summary> Keeps track of the current position in the stream for this client. </summary>
            long globalOffset;
            
            /// <summary>  Reference to the host TeeStream that this client reads from. </summary>
            readonly TeeStream streamHost;
            public TeeStreamClient(TeeStream streamHost) => this.streamHost = streamHost;
            
            /// <summary> This stream is read-only. Flush does nothing. </summary>
            public override void Flush()
            {
                
            }
            
            /// <summary> Reads a sequence of bytes from the current stream and advances the position within the stream. </summary>
            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = streamHost.Read(buffer, globalOffset, offset, count);
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
            public override long Length => streamHost.Length;
            public override long Position 
            { 
                get => globalOffset; 
                set  { } 
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                
                // flush everything. There may be other peers so doing this makes sure that nobody waits for this client to read.
                this.CopyTo(Stream.Null);
            }
        }

        public long Length => mainStream.Length;
        public long Position => mainStreamPosition - blockLength;
        public long blockLength;
            
        readonly Stream mainStream;
        byte[] currentBlock;

        public TeeStream(Stream mainStream) => this.mainStream = mainStream;
        
        Stream CreateClientStream() => new TeeStreamClient(this);

        public Stream[] CreateClientStreams(int count)
        {
            if (count == 0)
            {
                mainStream.Dispose();
                return Array.Empty<Stream>();
            }
            currentBlock = new byte[4096 * count];
            clientCount = count;
            var result = new Stream[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = CreateClientStream();
            }
            return result;
        }
        bool done;
        Exception readException = null;
        
        void ReadNextBlock()
        {
            // at this point everyone is waiting for the next block.
            int len;
            try
            {
                len = mainStream.Read(currentBlock, 0, currentBlock.Length);
            }
            catch(Exception exception)
            {
                this.readException = exception;
                len = 0;
            }
            
            // user interlocked.add to ensure that other threads will get the updated value.
            Interlocked.Add(ref mainStreamPosition, len);
            
            if (len == 0)
            {
                // We are done. let's stop.
                done = true;
                mainStream.Close();
                mainStream.Dispose();
            }
            blockLength = len;
            
            var oldEvt = evt;
            var w2 = waiting;
            evt = new SemaphoreSlim(0);
            Interlocked.Exchange(ref waiting, 0);
            oldEvt.Release(w2);
        }
        
        SemaphoreSlim evt = new SemaphoreSlim(0);
        int waiting;
        int clientCount;
        long mainStreamPosition;
        public int Read(byte[] bytes, long subStreamPosition, int bufferOffset, int count)
        {
            if (done) return 0;
            if (readException != null)
                throw readException;
            
            // Offset into the current block.
            long blockOffset = subStreamPosition - (mainStreamPosition - blockLength);
            if (blockOffset < 0) 
                throw new InvalidOperationException("Unexpected position calculated");
            
            var waitEvent = evt;
            
            // if the block offset is greater than the size of the block, we need to get/wait for the next block. 
            if (blockOffset >= blockLength)
            {
                if (Interlocked.Increment(ref waiting) == clientCount)
                {
                    // All clients are waiting - read the next block.
                    ReadNextBlock();
                }
                else
                {
                    // wait for a new block.
                    waitEvent.Wait();
                }
                // new blocks released. start over.
                return Read(bytes, subStreamPosition, bufferOffset, count);
            }

            // read the block byte-by-byte.
            for (int i = 0; i < count; i++)
            {
                long o2 = subStreamPosition - (mainStreamPosition - blockLength) + i;
                if (o2 >= blockLength)
                {
                    // End of the block reached.
                    // start Read over with new args.
                    int r = Read(bytes, subStreamPosition + i, bufferOffset + i, count - i);
                    if (r == 0) return i;
                    return r + i;
                }
                bytes[i] = currentBlock[o2];
            }
            return count;
        }
    }
}
