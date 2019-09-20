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
using System;
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment
{
    // This appears as a new option under the Settings tab in the TAP GUI.  
    // These settings can be used to define properties common across the Test Plan or Bench configuration.
    // See AccessingComponentSettings.cs to see how to use these, and other settings, in a Test Step.
    [Display("Example Settings", Description: "A collection of different settings.")]
    public class ExampleSettings : ComponentSettings<ExampleSettings>
    {
        // Properties work the same way here as in Test Steps.
        // Public properties with get and set methods are configurable by the TAP GUI user.
        [Display("Creation Time", Description: "This is the time. It's default value is the time at which the settings file was first created.")]
        public DateTime DemoTime { get; set; }

        [Display("My Integer", Description: "Some integer.")]
        public int MyInt { get; set; }

        public ExampleSettings()
        {
            DemoTime = DateTime.Now;
            MyInt = 12345;
        }

        public override string ToString()
        {
            return DemoTime.ToShortTimeString() + " " + MyInt;
        }
    }
}
