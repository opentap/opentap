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
using System.Globalization;
using System.Xml.Serialization;
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment
{
    // This shows how to implement a custom collection of components, similar to Instruments or DUTs.
    // This will appear under Settings -> Bench.
    // See AccessingComponentSettings.cs to see how to use these, and other settings, in a Test Step.

    // First a high level collection class must be created.
    // To do this define the Settings Group that will be used.
    // The high level "Instruments" group is an example of this collection.
    [SettingsGroupAttribute("Bench", Profile: true)]
    [Display("Example Component Settings", Description: "A collection of different instances of settings.")]
    [XmlInclude(typeof(CustomBenchSettings))]
    public class CustomBenchSettingsList : ComponentSettingsList<CustomBenchSettingsList, CustomBenchSettings>
    {
        // No code necessary.
    }

    // Each implementation with inherit from Resource.
    // An abstract class is defined for the similar properties within each instance. 
    // From the defined list above, these individual instances can be added.
    [Display("Custom Bench Settings", Description: "A collection of different instances of settings.")]
    // This will appear in the Settings menu in the GUI.
    public abstract class CustomBenchSettings : Resource
    {
        // Define custom properties.
        public string MyProperty { get; set; }
        public DateTime MyTime { get; set; }

        protected CustomBenchSettings()
        {
            MyProperty = "Key06131121";
            MyTime = DateTime.Now;
        }

        public override string ToString()
        {
            return MyTime.ToString(CultureInfo.InvariantCulture);
        }
    }

    // An individual instance of the Custom Bench Setting.  
    // These are added through the '+' in the Example Component Settings menu.
    [Display("Example Component A", Description: "An instance of Example Component Setting.")]    
    public class ExampleComponentA : CustomBenchSettings
    {
        public ExampleComponentA()
        {
            // Define a Name to describe how the component will appear.
            Name = "CompA";
        }

        public string SomeUniqueProperty { get; set; }
    }

    // Similar to Instruments and DUTs, custom components can extend each other.
    [Display("SimilarB", Description: "An instance of Example Component Setting. Extends SimilarA.")]
    public class SimilarSettingsB : ExampleComponentA
    {
        public SimilarSettingsB()
        {
            // Define a Name to describe how the component will appear.
            Name = "SmlrB";
        }
        public string SomeOtherProperty { get; set; }
    }
}
