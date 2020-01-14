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

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Embed Properties Attribute Example", Groups: new[] { "Examples", "Plugin Development", "Attributes" },
        Description: "Example step that uses the Embedded attribute for embedding properties from other objects.")]
    public class EmbedPropertiesAttributeExample : TestStep
    {
        /// <summary> This class could be used to share settings between multiple types of test steps. </summary>
        public class EmbeddedClass
        {
            // Here are some example settings:
            [Unit("Hz")]
            public double Frequency { get; set; }
            [Unit("dBm")]
            [Display("Power Level")]
            public double Power { get; set; }
        }

        // This causes the properties A and B to be embedded inside this class from the 
        // perspective of serialization and user interfaces.
        [Display("Embedded Group")] // All settings embedded from EmbeddedClass, will get this name as group.
        [EmbedProperties] //Set Prefix or PrefixOverrideProperty name to control the naming of embedded properties.
        public EmbeddedClass Embedded { get; set; } = new EmbeddedClass();
        
        public override void Run()
        {
            Log.Info($"Frequency: {Embedded.Frequency} Hz, Power: {Embedded.Power}");
        }

        // Resulting XML:
        // <EmbeddedAttributeExample type="OpenTap.Plugins.PluginDevelopment.EmbeddedAttributeExample" Version="1.0.0" Id="6e346cf0-bcdf-48a7-89d9-e20d64c7868d">
        //    <Enabled>true</Enabled>
        //    <Name>EmbeddedAttributeExample</Name>
        //    <ChildTestSteps />
        //    <!-- Notice Frequency and Power has been embedded in the test step settings. -->
        //    <Embedded.Frequency>0</Embedded.Frequency>
        //    <Embedded.Power>0</Embedded.Power>
        // </EmbeddedAttributeExample>
    }
}
