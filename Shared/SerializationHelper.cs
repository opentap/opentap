using System.IO;
using System.Xml.Serialization;
using OpenTap.Diagnostic;

namespace OpenTap
{
    /// <summary>
    /// <see cref="TapSerializer"/> does not correctly serialize / deserialize <see cref="Event"/> presumably because it is a struct
    /// </summary>
    internal static class SerializationHelper
    {
        private static XmlSerializer serializer = new XmlSerializer(typeof(Event));

        public static byte[] EventToBytes(Event evt)
        {
            var ms = new MemoryStream();
            serializer.Serialize(ms, evt);

            return ms.ToArray();
        }

        public static Event StreamToEvent(Stream stream)
        {
            return (Event)serializer.Deserialize(stream);
        }
    }
}