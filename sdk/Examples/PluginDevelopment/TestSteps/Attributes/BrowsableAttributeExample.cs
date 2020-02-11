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
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Browsable Attribute Example",
        Groups: new[] { "Examples", "Plugin Development", "Attributes" },
        Description: "Shows the use of the Browsable attribute.")]
    public class BrowsableAttributeExample : TestStep
    {
        // By default, any public property with a getter and setter is shown.
        public string Editable { get; set; }

        // The Browsable attribute allows for hiding of Settings from the GUI.
        [Browsable(false)]
        public string HiddenFromGui { get; set; }

        // Combining Browsable(true) with a private set creates a Read-Only Step Setting.
        [Browsable(true)]
        public string ReadOnly { get;  private set; }
        
        public BrowsableAttributeExample()
        {
            Editable = "Some editable string";
            HiddenFromGui = "Non visible string";
            ReadOnly = "A non editable string";
        }

        public override void Run()
        {
           // Do Nothing
        }

    }
}
