//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Linq;
using System.Xml.Linq;

namespace OpenTap.Plugins
{
    /// <summary> Serializer for MacroString values. </summary>
    public class MacroStringSerializer : TapSerializerPlugin
    {
        /// <summary> Tries to deserialize a MacroString. This is just a simple string value XML element, but it tries to find the step context for the MacroString.</summary>
        public override bool Deserialize( XElement node, ITypeInfo t, Action<object> setter)
        {
            if (t.IsA(typeof(MacroString)) == false) return false;
            string text = node.Value;
            var obj = Serializer.SerializerStack.OfType<ObjectSerializer>().FirstOrDefault();
            if (obj != null)
            {
                setter(new MacroString(obj.Object as ITestStep) { Text = text });
            }
            else
            {
                setter(new MacroString { Text = text });
            }
            
            return true;
        }

        /// <summary> Serializes a MacroString. it just sets the text as the value. MacroString should be compatible with string in XML.</summary>
        public override bool Serialize( XElement node, object obj, ITypeInfo expectedType)
        {
            if (obj is MacroString == false) return false;
            node.SetValue(((MacroString)obj).Text);
            return true;
        }
    }

}
