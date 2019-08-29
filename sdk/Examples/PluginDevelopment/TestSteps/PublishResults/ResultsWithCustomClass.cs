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
    [Display("Results with Custom Class", Groups: new[] { "Examples", "Plugin Development", "Publish Results" }, 
        Description: "This example shows how to store results for custom class.")]
    public class ResultsWithCustomClass : TestStep
    {
        // Display attribute is used to override the table name.
        // When adding the display attribute to a class/method that will be used for storing data, you should only use the name parameter.
        // If the group/groups parameter is used, the values will precede the name, 
        // All other parameters to the Display attribute are ignored during Publish calls.
        [Display("Measurement 1")]
        public class MeasurementData
        {
            [Display("Unit #")]
            public Int32 UnitNumber { get; set; }

            [Display("Time [S]")]
            public Double MeasurementTime { get; set; }
            
            [Display("Limit [S]")]
            public Double MeasurementTimeUpperLimit { get; set; }

            [Display("Voltage [mV]")] 
            public Double Voltage { get; set; }

            [Display("Current [mA]")]  
            
            public Double Current { get; set; }

            public MeasurementData()
            {
                MeasurementTimeUpperLimit = 5.0;
            }
        }

        public override void Run()
        {
            Results.Publish(new MeasurementData
            {
                UnitNumber = 1,
                MeasurementTime = 4.8,
                Voltage = 5.0,
                Current = 1.0
            });
        }
    }
}
