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
using System.Collections.Generic;
using System.Linq;
using OpenTap;

namespace OpenTap.Plugins.ExamplePlugin
{
    // As with Test Steps, the Display attribute can also be used with Instruments.
    [Display(Groups: new[] { "Examples", "Example Plugin" }, Name: "Low Pass Filter", Description: "A simulated low pass filter.")]
    // All TAP DUTs inherit from the Dut base class.
    public class LowPassFilterDut : Dut
    {
        // LowPassFilterDut inherits two properties (Comment and Id) from DUT class.
        // Additional DUT properties can be added here.

        // Internal properties are not shown in the GUI.
        internal int WindowSize { get; set; }

        public LowPassFilterDut()
        {
            // Name defines the name that new instances of this class will use.  
            // This is how the DUT will appear in the Resource Bar and in the Step Settings of Test Steps.
            // More than one instance is made unique by automatically appending an integer suffix.
            Name = "Filter";
            // Default settings can be configured in the constructor.
            ID = "KS000000";
        }

        // Open procedure for the DUT.
        public override void Open()
        {
            // Add code needed for opening the resource here.
            // This will be called at the beginning of a TestPlan.
            base.Open();

            // DUTs also have access to a Log object.
            Log.Info(string.Format("The DUT ID is {0}", ID));
        }

        // Close procedure for the DUT.
        public override void Close()
        {
            // Add code needed for closing the resource here.
            // This will be called at the end of a TestPlan execution.
            base.Close();
        }

        // Since this is a simulation of a low pass filter (LPF) the processing is inside the DUT.
        // The LPF is implemented by a moving average.
        internal double[] CalcMovingAverage(double[] inputData)
        {
            int size = inputData.Length;
            List<double> windowValues = new List<double>();

            double[] movingAverage = new double[size];
            for (int i = 0; i < size; i++)
            {
                // Add the newest one.
                windowValues.Add(inputData[i]);

                if (i < WindowSize)
                {
                    movingAverage[i] = 0;
                }
                else
                {
                    movingAverage[i] = windowValues.Average();
                    // Take off the oldest.
                    windowValues.RemoveAt(0);
                }
            }
            return movingAverage;
        }
    }
}
