//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Xml.Linq;

namespace OpenTap
{
    /// <summary>
    /// Species a OpenTAP Serializer plugin.
    /// </summary>
    [Display("Serializer")]
    public interface ITapSerializerPlugin : ITapPlugin
    {
        
        /// <summary>
        /// Called as part for the deserialization chain. Returns false if it cannot serialize the XML element.  
        /// </summary>
        /// <param name="node"></param>
        /// <param name="t"></param>
        /// <param name="setter"></param>
        /// <returns></returns>
        bool Deserialize(XElement node, ITypeData t, Action<object> setter);
        /// <summary>
        /// Called as part for the serialization chain. Returns false if it cannot serialize the XML element.  
        /// </summary>
        /// <param name="node">The output XML element.</param>
        /// <param name="obj">The object being deserialized.</param>
        /// <param name="expectedType">The expected type from deserialization.</param>
        /// <returns>return true if the object could be serialized.</returns>
        bool Serialize(XElement node, object obj, ITypeData expectedType);

        /// <summary>
        /// Priority of the serializer. Defines the order in which the serializers are used. Default is 0.  
        /// </summary>
        double Order { get; }
    }

}
