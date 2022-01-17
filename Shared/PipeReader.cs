using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace OpenTap
{
    internal static class PipeReader
    {
        internal static void WriteShort(this PipeStream pipe, ushort value)
        {
            var buffer = new[]
            {
                // Lower byte
                (byte)(value & 0x00ff),
                // Higher byte
                (byte)(value >> 8)
            };
            pipe.Write(buffer, 0, 2);
        }

        internal static ushort ReadShort(this PipeStream pipe)
        {
            var buffer = new byte[2];
            var numRead = 0;

            while (numRead < 2)
            {
                numRead += pipe.Read(buffer, numRead, 2 - numRead);
                if (numRead == 0) return 0;
            }

            var lower = buffer[0];
            var upper = buffer[1];

            return (ushort)(lower | (upper << 8));
        }

        /// <summary>
        /// Reads a message by interpreting the first byte as the message length
        /// and the concatenating reads until it encounters a terminating message of length '0'.
        /// </summary>
        /// <param name="pipe"></param>
        /// <returns></returns>
        public static MemoryStream ReadMessage(this PipeStream pipe)
        {
            var messageLength = pipe.ReadShort();
            var buffers = new List<byte[]>();

            while (messageLength > 0)
            {
                var buf = new byte[messageLength];
                var numRead = 0;
                while (numRead < messageLength)
                {
                    numRead += pipe.Read(buf, numRead, messageLength - numRead);
                }

                buffers.Add(buf);

                messageLength = pipe.ReadShort();
            }

            var ms = new MemoryStream();

            foreach (var buf2 in buffers)
            {
                ms.Write(buf2, 0, buf2.Length);
            }

            ms.Seek(0, SeekOrigin.Begin);

            return ms;
        }

        private static object writeLock = new object();

        /// <summary>
        /// Sends a message by splitting it into messages of length UInt16.MaxValue
        /// </summary>
        /// <param name="pipe"></param>
        /// <param name="message"></param>
        public static void WriteMessage(this PipeStream pipe, string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            pipe.WriteMessage(bytes);
        }

        public static void WriteMessage(this PipeStream pipe, byte[] bytes)
        {
            lock (writeLock)
            {
                var sent = 0;
                var remaining = bytes.Length;
                while (remaining > 0)
                {
                    var length = (ushort)Math.Min(remaining, ushort.MaxValue);
                    remaining -= length;
                    // first send the message length
                    pipe.WriteShort(length);
                    // then write the message
                    pipe.Write(bytes, sent, length);

                    sent += length;
                }

                // the message is terminated by indicating a 0 length message
                pipe.WriteShort(0);
                pipe.Flush();
            }
        }
    }
}