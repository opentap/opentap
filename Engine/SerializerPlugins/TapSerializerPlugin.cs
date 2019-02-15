//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Xml.Linq;

namespace OpenTap
{
    /// <summary>
    /// Base class for TAP Serializer plugins. Implement this in a public class to extend the TapSerializer with additional functionality.
    /// </summary>
    [Display("Serializer")]
    public abstract class TapSerializerPlugin : ITapPlugin
    {
        /// <summary> Log source for serializer plugins. </summary>
        protected static TraceSource Log = OpenTap.Log.CreateSource("Serializer");
        
        /// <summary> The object facilitating Serialization or Deserialization. </summary>
        protected TapSerializer Serializer { get; private set; }
        
        /// <summary> Creates a new TapSerializerPlugin. </summary>
        public TapSerializerPlugin()
        {
            this.Serializer = TapSerializer.CurrentSerializer.Value;
        }
        /// <summary>
        /// Called as part for the deserialization chain. Returns false if it cannot serialize the XML element.  
        /// </summary>
        /// <param name="node"></param>
        /// <param name="t"></param>
        /// <param name="setter"></param>
        /// <returns></returns>
        public abstract bool Deserialize(XElement node, Type t, Action<object> setter);
        /// <summary>
        /// Called as part for the serialization chain. Returns false if it cannot serialize the XML element.  
        /// </summary>
        /// <param name="node">The output XML element.</param>
        /// <param name="obj">The object being deserialized.</param>
        /// <param name="expectedType">The expected type from deserialization.</param>
        /// <returns>return true if the object could be serialized.</returns>
        public abstract bool Serialize(XElement node, object obj, Type expectedType);

        /// <summary>
        /// Priority of the serializer. Defines the order in which the serializers are used. Default is 0.  
        /// </summary>
        public virtual double Order { get { return 0; } }
    }

    /// <summary>
    /// Helper class for writing warning messages about XML nodes.
    /// </summary>
    static class LogExtension
    {
        /// <summary>
        /// Prints the warning + Line information.
        /// </summary>
        /// <param name="log"></param>
        /// <param name="node"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public static void Warning(this TraceSource log, XElement node, string message, params object[] args)
        {
            var lineinfo = ((System.Xml.IXmlLineInfo)node);
            log.Warning("XML line {0} column {1}: {2}", lineinfo.LineNumber, lineinfo.LinePosition, string.Format(message, args));
        }
    }

}
