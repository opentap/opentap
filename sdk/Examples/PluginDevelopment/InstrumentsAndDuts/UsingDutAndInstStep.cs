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

// This example shows how a test step can access methods and properties on DUTs or Instruments.
// DUTs and Instruments are typically configured via the TAP GUI.
// See the User Documentation for more information.

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Dut+Instrument Usage", Groups: new[] { "Examples", "Plugin Development", "InstrumentsAndDuts" }, 
        Description: "Shows how a test step can reference resources such as DUTs and Instruments.")]
    public class UsingDutAndInstExample : TestStep
    {
        [Display("Some Setting")]
        public string SomeSetting { get; set; }

        // Instruments can be added as test step settings, just like any other class.  
        // The TAP GUI will look for an Instrument of the specified type to fill in
        // Instrument or ScpiInstrument can be use to allow any Instrument that inherits from those types.
        // A specific class can be used to restrict which instruments the test step works with.
        [Display("Simple Instrument")]
        public SimpleInstrument ExampleInstrument { get; set; }

        // The same concept for instruments (see above) can be applied to DUTs.
        [Display("Simple Dut")]
        public SimpleDut ExampleDut { get; set; }

        public UsingDutAndInstExample()
        {
            SomeSetting = "Setting Default Value";
        }

        public override void Run()
        {
            Log.Info("SomeSetting = \'{0}\'", SomeSetting);
            Log.Info("DUT MyMetaData = \'{0}\'", ExampleDut.MyMetaData);
            Log.Info("Instrument Name = \'{0}\'", ExampleInstrument.Name);

            // It is not necessary to call the Open or close methods for Instruments and DUTS in a test step.
            // That is handled by the test plan as it starts up, or shuts down.
            // Methods on instruments and DUTs can be called in the Run method, just like calls on any other object.
            ExampleInstrument.DoNothing();
            ExampleDut.DoNothing();

            UpgradeVerdict(Verdict.Pass);
        }
    }
}
