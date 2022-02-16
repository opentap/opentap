using System;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace OpenTap
{
    static class PipeReader
    {
        private static readonly TapSerializer writer = new TapSerializer();
        private static readonly TapSerializer reader = new TapSerializer();
        public static T ReadMessage<T>(this PipeStream stream)
        {
            lock (reader)
            {
                using (var rd = new BinaryReader(stream, Encoding.Default, true))
                {
                    var len = rd.ReadInt32();
                    MemoryStream ms = new MemoryStream(rd.ReadBytes(len));
                    return (T) reader.Deserialize(ms, true, TypeData.FromType(typeof(T)));
                    
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