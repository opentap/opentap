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

namespace OpenTap.Plugins.ExamplePlugin
{
    // As with Test Steps, the Display attribute can also be used with Instruments.
    [Display(Groups: new[] { "Examples", "Example Plugin" }, Name: "Generator", Description: "A simulated Arbitrary Waveform Generator.")]    
    // All TAP Instruments inherit from the Instrument base class.
    // If the instrument is SCPI based, the ScpiInstrument base class can be used to simplify implementation.
    public class GeneratorInstrument : Instrument
    {
        // Instrument settings can be added here.

        // The VisaAddress attribute results in an editor that is populated with the available
        // instruments known to VISA.
        [VisaAddress]
        public string VisaAddress { get; set; }

        public GeneratorInstrument()
        {
            // Name defines the name that new instances of this class will use.  
            // This is how the instrument will appear in the Resource Bar and in the Step Settings of Test Steps.
            // More than one instance is made unique by automatically appending an integer suffix.
            Name = "Gen";
            // Default values for settings, and instances of objects should be defined here.
            VisaAddress = "Simulate";
        }

        // Open procedure for the instrument.
        public override void Open()
        {
            // Add code needed for opening the resource here.
            // This will be called at the beginning of TestPlan execution.
            base.Open();

            // Instruments also have access to a Log object.
            Log.Info(string.Format("Connection to {0} successful", VisaAddress));

        }
       
        // Close procedure for the instrument.
        public override void Close()
        {
            // Add code needed for closing the resource here.
            // This will be called at the end of TestPlan execution.
            base.Close();   
        }

        // Public methods can be called from Test Steps.
        public void SetInputData(double[] inputData)
        {
            // This would be some instrument control code to write the input data to generator.

            // ScpiCommand("INPut {0}", inputData);
        }
    }
}
