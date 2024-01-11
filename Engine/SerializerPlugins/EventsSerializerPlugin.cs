using System;
using System.Xml.Linq;
using OpenTap.Diagnostic;

namespace OpenTap.Plugins
{
    /// <summary> Serializer implementation for default value attributes. </summary>
    class EventsSerializerPlugin : TapSerializerPlugin
    {
        /// <summary> Deserialize an Event object. </summary>
        public override bool Deserialize(XElement node, ITypeData t, Action<object> setter)
        {
            if (t.DescendsTo(typeof(Event)) == false) return false;
            var message = node.Element("Message")?.Value;
            var source = node.Element(nameof(Event.Source))?.Value;
            long.TryParse(node.Element(nameof(Event.Timestamp))?.Value, out var timestamp);
            int.TryParse(node.Element(nameof(Event.EventType))?.Value, out var eventType);
            setter(new Event(0, eventType, message, source, timestamp));
            return true;
        }

        /// <summary> Serializes an Event object. </summary>
        public override bool Serialize(XElement node, object obj, ITypeData expectedType)
        {
            if ((obj is Event evt) == false)
                return false;
            node.SetElementValue(nameof(evt.Timestamp), evt.Timestamp);
            node.SetElementValue(nameof(evt.Message), evt.Message);
            node.SetElementValue(nameof(evt.Source), evt.Source);
            node.SetElementValue(nameof(evt.EventType), evt.EventType);
            return true;
        }
    }
}