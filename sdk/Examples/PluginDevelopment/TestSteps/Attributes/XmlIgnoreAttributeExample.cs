//Copyright 2012-2019 Keysight Technologies
//
//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at
//
//http://www.apache.org/licenses/LICENSE-2.0
//
//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.
using System.ComponentModel;
using System.Xml.Serialization;
using OpenTap;

// This test step shows the use of the XmlIgnore attribute, and how it can interact with the Browsable attribute.
// The XmlIgnore attribute is used when you do not wish the results to be serialized.
// An example would be a desire to always get a default value on startup.

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("XML Ignore Example", Groups: new[] { "Examples", "Plugin Development", "Attributes" }, 
        Description: "Example step that uses the XmlIgnore attribute.")]
    public class XmlIgnoreAttributeExample : TestStep
    {
        // Editable property serialized to XML by default.
        public double SerializedSetting { get; set; }

        // Editable property not serialized to XML. Not shown in GUI.
        [XmlIgnore]       
        public double NotSerializedNotVisible { get; set; }

        // Editable property not serialized to XML. Shown in GUI.
        [Browsable(true)]
        [XmlIgnore]
        public double NotSerializedVisible { get; set; }

        // Read-only property not serialized to XML. Shown in GUI.
        [Browsable(true)]
        [XmlIgnore]
        public double ReadOnlyNotSerializedVisible { get; private set; }

        public XmlIgnoreAttributeExample()
        {
            SerializedSetting = 0.0;
            NotSerializedNotVisible = 1.0;
            NotSerializedVisible = 2.0;
            ReadOnlyNotSerializedVisible = 4.0;
        }

        public override void Run()
        {
            // Do nothing
        }
    }
}
