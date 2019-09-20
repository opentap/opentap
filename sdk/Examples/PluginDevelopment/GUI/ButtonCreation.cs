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
    [Display("Button Creation", Groups: new[] { "Examples", "Plugin Development", "GUI" }, 
        Description: "This example shows how a Button is added as a Step Setting.")]
    public class ButtonSetting : TestStep
    {
        // Any public method within a TestStep, Resource, or ComponentSettings class can be displayed as a button in the TAP GUI by adding the [Browsable(true)] attribute.
        // A Display attribute is also recommended. The Display name will be the text displayed within the button.
        [Display("Do Something")]
        [Browsable(true)]
        public void ButtonExample()
        {
            // This example logs a message on button click. However, anything can be done here, such as 
            // opening another application, calling a .bat file, etc.
            Log.Info("Button Clicked");
        }
       
        public override void Run()
        {
            UpgradeVerdict(Verdict.Pass);
        }
    }
}
