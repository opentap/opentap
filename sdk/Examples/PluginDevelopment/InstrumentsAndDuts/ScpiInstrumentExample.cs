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

// Note this template assumes that you have a SCPI based instrument, and accordingly
// extends the ScpiInstrument base class.

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Scpi Instrument Example", Groups: new[] { "Examples", "Plugin Development" }, Description: "Example of a SCPI Instrument implementation.")]
    // If the instrument is SCPI based, inheriting from the ScpiInstrument base class will simplify implementation.
    public class ScpiInstrumentExample : ScpiInstrument
    {
        // ScpiInstuments provides useful settings for VisaAddress, IdnString and others.
        // Anything not already included in ScpiInstrument can be added here.

        public ScpiInstrumentExample()
        {
            // Set the name of the Scpi Instrument Example
            Name = "ScpiEx";
            // Set default values for properties / settings.
            VisaAddress = "Simulate";
        }

        /// <summary>
        /// Open procedure for the instrument.
        /// </summary>
        public override void Open()
        {
            base.Open();

            // Use this to ensure the correct instrument is being connected to.
            if (!IdnString.Contains("Instrument ID"))
            {
                Log.Error("This instrument driver does not support the connected instrument.");
                throw new ArgumentException("Wrong instrument type.");
            }
        }

        // Add wrapper methods as needed.
        public void Configure(double centerFrequency, double frequencySpan, int points)
        {
            // Use ScpiCommand to send SCPI strings to the instrument.
            ScpiCommand("SENSE:FREQ:CENT " + centerFrequency);
            ScpiCommand("SENSE: FREQ:SPAN " + frequencySpan);
            ScpiCommand("SENSE:SWE:POIN " + points);
        }

        public double[] SweepMeasurement()
        {
            // Use ScpiQuery to read back from the device.
            return ScpiQuery<double[]>("READ:SAN1?");
        }

        /// <summary>
        /// Close procedure for the instrument.
        /// </summary>
        public override void Close()
        {
            // Add code to close the connection to the instrument here.
            base.Close();
        }
    }
}
