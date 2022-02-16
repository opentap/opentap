using System;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace OpenTap
{
    static class PipeReader
    {
        static int ReadInt(this PipeStream stream)
        {
            var buffer = new byte[4];
            for(int read = 0; read < 4;)
            {
                var r = stream.Read(buffer, read, 4 - read);
                if (r == 0) return -1; // premature end of stream.
                read += r;
            }
            return BitConverter.ToInt32(buffer,0);
        }
        private static readonly TapSerializer writer = new TapSerializer();
        private static readonly TapSerializer reader = new TapSerializer();

        public static T ReadMessage<T>(this PipeStream stream)
        {
            stream.TryReadMessage<T>(out var r);
            return r;
        }
        public static bool TryReadMessage<T>(this PipeStream stream, out T r)
        {
            r = default;
            lock (reader)
            {
                var len = stream.ReadInt();
                if (len == -1) return false;
                using (var rd = new BinaryReader(stream, Encoding.Default, true))
                {
                    MemoryStream ms = new MemoryStream(rd.ReadBytes(len));
                    r = (T) reader.Deserialize(ms, true, TypeData.FromType(typeof(T)));
                    return true;
                }
            }
        }

        public static void WriteMessage(this PipeStream pipe, object obj)
        {
            lock (writer)
            {
                using (var wd = new BinaryWriter(pipe, Encoding.Default, true))
                {
                    var memstr = new MemoryStream();
                    writer.Serialize(memstr, obj);
                    wd.Write((int) memstr.Length);
                    wd.Write(memstr.GetBuffer(), 0, (int) memstr.Length);
                    wd.Flush();
                }
            }
        }
    }
}