using System;
using System.Xml;
using System.Xml.Linq;

namespace OpenTap
{
    /// <summary> Contains detailed information about what went wrong while loading or saving information to an XML file. </summary>
    public class XmlError : XmlMessage
    {
        /// <summary> The exception that occured - if any.</summary>
        public virtual Exception Exception { get; }
        

        /// <summary> Creates an instance of XmlError. xmlElement may be null. message may be null if exception is set. </summary>
        public XmlError(XElement xmlElement, string message, Exception exception = null) : base(xmlElement, message)
        {
            Exception = exception;
        }

        /// <summary> Prints this error in a readable fashion. </summary>
        public override string ToString()
        {
            string message = Message ?? Exception.Message;
            if (Element is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
                return $"XML Line {lineInfo.LineNumber}: {message}";
            return message;
        }
    }
}