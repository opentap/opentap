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

// This file shows a definition of DUT with multiple ports.
// Ports are used by the Connection manager.

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Four Port DUT", Groups: new[] { "Examples", "Plugin Development", "Connections" },  
        Description: "A four port DUT, with a few additional properties.")]
    public class FourPortDut : Dut
    {
        // Private sets are not shown in the GUI.
        public Port PortA { get; private set; }
        public Port PortB { get; private set; }
        public Port PortC { get; private set; }
        public Port PortD { get; private set; }

        [Display("MyString", Group: "DutExtensions")]
        public string MyString { get; set; }
        [Display("MyDouble", Group: "DutExtensions")]
        public double MyDouble { get; set; }
        
        // Initializes a new instance of this DUT class.
        public FourPortDut()
        {
            Name = "FourPortDut";
            PortA = new Port(this, "PortA");
            PortB = new Port(this, "PortB");
            PortC = new Port(this, "PortC");
            PortD = new Port(this, "PortD");
        }

        public override void Open()
        {
            base.Open(); 
            Log.Info("Opening FourPortDut.");
        }

        public override void Close()
        {
            Log.Info("Closing FourPortDut.");
            base.Close(); 
        }

        internal void PerformMeasurement()
        {
            Log.Info("Simulating doing a measurement.");

            // In case of actual DUT hardware, the measurement algorithm can be added here.
        }

        internal void SetupDut(double frequency)
        {
            Log.Info("Simulating setup of DUT (with frequency).");

            // In case of actual hardware, add DUT driver calls to set channel frequency here.
        }
    }
}
