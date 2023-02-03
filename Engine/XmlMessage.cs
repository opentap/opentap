using System.Xml;
using System.Xml.Linq;

namespace OpenTap
{
    /// <summary> Contains detailed information about something that occured while loading or saving information to an XML file. </summary>
    public class XmlMessage
    {
        /// <summary> The XML element which caused the message. This may be null if no XML element could be associated with the message. </summary>
        public virtual XElement Element { get; }
        /// <summary> The message. </summary>
        public virtual string Message {get; }

        /// <summary> Creates an instance of XmlError. xmlElement may be null. message may be null if exception is set. </summary>
        public XmlMessage(XElement xmlElement, string message)
        {
            Element = xmlElement;
            Message = message;
        }

        /// <summary> Prints this message in a readable fashion. </summary>
        public override string ToString()
        {
            string message = Message;
            if (Element is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
                return $"XML Line {lineInfo.LineNumber}: {message}";
            return message;
        }
    }
}