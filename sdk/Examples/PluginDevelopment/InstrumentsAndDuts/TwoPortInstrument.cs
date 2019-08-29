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

// This file shows a definition of instrument with multiple ports.
// Ports are used by the Connection manager.
namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Two Port Instrument", Groups: new[] { "Examples", "Plugin Development", "Connections" }, Description: "An example of an instrument with two ports.")]
    public class TwoPortInstrument : Instrument
    {
        public Port PortA { get; private set; }

        public Port PortB { get; private set; }

        public TwoPortInstrument()
        {
            Name = "TwoPortInst";
            PortA = new Port(this, "PortA");
            PortB = new Port(this, "PortB");
        }

        public override void Open()
        {
            base.Open(); 
            Log.Info("Opening TwoPortInstrument.");
        }

        public override void Close()
        {
            Log.Info("Closing TwoPortInstrument.");
            base.Close(); 
        }

        internal void SetupInstrument(object pow)
        {
            Log.Info("Simulating setup of Instrument.");

            // In case of an actual hardware, add IVI driver calls 
            // or SCPI commands for setting output power here.
        }
    }
}
