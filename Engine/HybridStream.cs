//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap
{
    internal class HybridStream : Stream
    {
        private class MemViewStream : Stream
        {
            private byte[] data;
            private long length, position;

            public MemViewStream(byte[] data, long length)
            {
                this.data = data;
                this.length = length;
            }

            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return true; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { return length; } }

            public override long Position
            {
                get { return position; }
                set
                {
                    if (value != position)
                    {
                        if (value < 0) throw new InvalidOperationException("Invalid position.");
                        if (value > length) throw new InvalidOperationException("Invalid position.");

                        position = value;
                    }
                }
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (count < 0) throw new ArgumentOutOfRangeException("count");
                if (offset < 0) throw new ArgumentOutOfRangeException("count");

                long rest = length - position;
                if (count > rest) count = (int)rest;

                if (count > 0)
                {
                    Array.Copy(data, position, buffer, offset, count);
                    position += count;
                }

                return count;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long newpos = position;

                switch (origin)
                {
                    case SeekOrigin.Begin:
                        newpos = offset;
                        break;
                    case SeekOrigin.Current:
                        newpos = position + offset;
                        break;
                    case SeekOrigin.End:
                        newpos = length + offset;
                        break;
                }

                if (newpos < 0) newpos = 0;
                else if (newpos > length) newpos = length;

                position = newpos;
                return position;
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
        
        #region Private
        private object lockObject = new object();

        private Stream stream = new MemoryStream();

        public HybridStream(string filename, int threshold)
        {
            this.Filename = filename;
            this.MemoryThreshold = threshold;
        }
        #endregion

        public long MemoryThreshold { get; set; }
        public string Filename { get; set; }

        #region Overrides
        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }

        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }

        public override long Length { get { return GetLength(); } }
        public override long Position { get { return GetPosition(); } set { throw new NotSupportedException(); } }
        #endregion

        private long GetPosition()
        {
            lock (lockObject)
                return stream.Length;
        }

        private long GetLength()
        {
            lock (lockObject)
                return stream.Length;
        }

        public override void Flush()
        {
            lock (lockObject)
                stream.Flush();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (lockObject)
            {
                stream.Write(buffer, offset, count);
                if (stream is MemoryStream)
                    checkTrigger();
            }
        }

        private void checkTrigger()
        {
            if (stream.Length >= MemoryThreshold)
                ConvertToFile();
        }

        public void ConvertToFile()
        {
            lock (lockObject)
            {

                var fileStream = new FileStream(Filename, FileMode.Create, FileAccess.Write, FileShare.Read);
                fileStream.Write(((MemoryStream)stream).GetBuffer(), 0, (int)stream.Length);
                stream.Dispose();
                stream = fileStream;
            }
        }

        public Stream GetViewStream()
        {
            lock (lockObject)
            {
                if (stream is FileStream)
                    return new FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                else
                    return new MemViewStream(((MemoryStream)stream).GetBuffer(), stream.Length);
            }
        }

        public override void Close()
        {
            lock (lockObject)
            {
                if (stream != null)
                {
                    stream.Close();
                    stream = null;
                }
            }

            base.Close();
        }
    }
}
