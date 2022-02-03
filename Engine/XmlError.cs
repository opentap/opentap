using System;
using System.Xml;
using System.Xml.Linq;

namespace OpenTap
{
    /// <summary> Contains detailed information about what went wrong while loading or saving information to an XML file. </summary>
    public class XmlError
    {
        /// <summary> The XML element which caused the error. This may be null if no XML element could be associated with the error. </summary>
        public virtual XElement Element { get; }
        /// <summary> The exception that occured - if any.</summary>
        public virtual Exception Exception { get; }
        
        /// <summary> The message describing the error. </summary>
        public virtual string Message {get; }

        /// <summary> Creates an instance of XmlError. xmlElement may be null. message may be null if exception is set. </summary>
        public XmlError(XElement xmlElement, string message, Exception exception = null)
        {
            Element = xmlElement;
            Exception = exception;
            Message = message ?? exception?.Message;
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