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
using OpenTap;

namespace OpenTap.Plugins.PluginDevelopment
{
    // An example of a custom instrument.
    [Display("Simple Instrument", Groups: new[] { "Examples", "Plugin Development" }, Description: "An example of a simple instrument.")]
    public class SimpleInstrument : Instrument
    {
        // Using the VisaAddress attribute results in the editor being populated with all available instruments,
        // as discovered by querying VISA.
        [VisaAddress]
        public string VisaAddress { get; set; }

        [Display("Some property")]
        public string SomeProperty { get; set; }

        public SimpleInstrument()
        {
            Name = "SimpleInst";
            SomeProperty = "SomeInitialValue";
        }

        public override void Open()
        {
            base.Open(); 
            Log.Info("Opening SimpleInstrument.");
        }

        public override void Close()
        {
            Log.Info("Closing SimpleInstrument.");
            base.Close(); 
        }

        // Unique to this class.
        public void DoNothing()
        {
            OnActivity();   // Causes the GUI to indicate progress.
            Log.Info("SimpleInstrument called.");
        }
    }
}
