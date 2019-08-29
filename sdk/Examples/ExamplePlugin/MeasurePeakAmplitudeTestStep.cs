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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using OpenTap;


/// <summary>
/// This example contains a TAP plugin with simulated instrument and DUT. It shows 
/// the basic interaction between Test Steps, DUTs, and Instruments. More detail 
/// on the features found in the example can be found in the PluginDevelopment directory.

/// The project includes a SIMULATED: 
/// -   GeneratorInstrument (TAP Instrument).
/// -   LowPassFilterDut (TAP DUT).

/// MeasurePeakAmplitudeTestStep (this file) is a TAP Test Step that:
/// 1.	Programs the generator.
/// 2.	Configures the low pass filter.
/// 3.	Collects the results from the low pass filter.  
/// 4.  Publishes results to any configured result listeners. 
/// </summary>

namespace OpenTap.Plugins.ExamplePlugin
{
    // The display attribute defines the grouping, name and description of settings in the Add New Step window.    
    [Display(Groups: new[] { "Examples",  "Example Plugin" }, Name: "Measure Peak Amplitude",  
        Description: "Checks the maximum value of a waveform after passing through a low pass filter.")]
    // All Test Steps inherit from the TestStep base class.
    public class MeasurePeakAmplitudeTestStep : TestStep
    {
        #region Settings

        // Public properties with a getter and a setter will be shown in the TAP GUI.
        // Non public properties are not shown in the GUI.
        public double[] InputData { get; set; }

        // The display attribute can be used to define how Step Settings (Properties) appear 
        // in the Step Settings panel.
        [Display(Group: "Generator Settings", Name: "Generator", Order: 1.1)]
        // Instruments are associated with a Test Step by adding a property of the desired instrument type.
        // This can be an exact type, an interface, or base type such as ScpiInstrument, or Instrument.
        public GeneratorInstrument MyGenerator { get; set; }

        // DUT associations are added the same way as Instruments.
        [Display(Group: "Filter (DUT) Settings", Name: "Filter", Order: 2.1)]
        public LowPassFilterDut MyFilter { get; set; }

        // Order is used to sort items within a group
        [Display(Group: "Filter (DUT) Settings", Name: "Window Size", Order: 2.2,
            Description: "Defines the size of the moving average window.")]
        public int WindowSize { get; set; }

        // Public properties are serialized upon loading/saving the TestPlan.
        // The XmlIgnore attribute indicates that the data should not be serialized.
        // This can be used when the user is required to specify a value, and there is no default value.
        [XmlIgnore]
        // Properties with private setters are not shown in the GUI. However, you may wish 
        // to have a "read only" view of a property. To achieve that, use the Browsable(true) 
        // attribute, and keep the setter private, as shown below.
        [Browsable(true)]  
        [Display(Group: "Scope Settings", Name: "Output Data", Order: 3.1)]
        public double[] ReadOnlyOutputData { get; private set; }

        // Limit checking is usually done by creating Limit Settings, and then comparing the limits 
        // to the result values in the Run method (see below).
        [Display(Group: "Limit Checking", Name: "Limit Check Enabled", Order: 4.1)]
        public bool LimitCheckEnabled { get; set; }

        [Display(Group: "Limit Checking", Name: "Max Amplitude", Order: 4.2,
            Description: "The maximum amplitude allowed. If exceeded, the test will be marked as failed.")]

        // The EnabledIf attribute allows for disabling/hiding elements based on the value of other items.
        // Here the MaxAmplitude editor is disable if the LimitCheckEnabled property is true.
        // You can find more examples in the Plugin Development\TestSteps\Attributes section.
        [EnabledIf("LimitCheckEnabled", true, HideIfDisabled = true)]
        public double MaxAmplitude { get; set; } 

        #endregion
        
        public MeasurePeakAmplitudeTestStep()
        {
            // Default values for settings should be defined in the constructor.
            LimitCheckEnabled = true;
            MaxAmplitude = 50;
            InputData = new double[] {0, 0, 0, 0, 5, 5, 5, 5, 0, 0, 0, 0, 0, 0};
            WindowSize = 3;
           
            // Validation Rules can be defined in the constructor to restrict the user inputs.
            Rules.Add(() => WindowSize > 0, "Window size must be greater than zero", "WindowSize");
            Rules.Add(SizesAreAppropriate, "Input Data must be larger than window size", "WindowSize", "InputData");
        }
        
        //public override void PrePlanRun()
        //{
        //    // This method is called on all TestSteps at the START of TestPlan execution.
        //    // If no setup code is needed, this method can be removed.
        //    base.PrePlanRun();            
        //}

        public override void Run()
        {   
            // The Run method is where the main execution logic of a TestStep exists.
            // This is a required method.
            try
            {
                // Setup instrument.
                MyGenerator.SetInputData(InputData);

                // Setup DUT.
                MyFilter.WindowSize = WindowSize;

                // Execute logic for DUT and handle data from DUT.
                ReadOnlyOutputData = MyFilter.CalcMovingAverage(InputData);

                // Check to see if limit checking is enabled.  If so, Upgrade the verdict.
                if (LimitCheckEnabled)
                {
                    // The Verdict is used by TAP to convey the general execution result of a Test Step.
                    UpgradeVerdict(ReadOnlyOutputData.Max() >= MaxAmplitude ? Verdict.Fail : Verdict.Pass);
                }
                else
                {
                    // All Test Steps have a standard Log object (inherited from the base class) 
                    // that can be used to write messages to the run log and the session log.
                    Log.Debug("Limit checking was not enabled.  This is why the verdict is inconclusive.");
                    UpgradeVerdict(Verdict.Inconclusive);
                }

                // Different log message types can be used based on what is being written.
                Log.Info("The DUT comment is {0}", MyFilter.Comment);

                // All Test Steps also contain a Results object. Results are store by calling Publish or PublishTable.
                Results.PublishTable("Inputs Versus Moving Average", new List<string>() {"Input Values", "Output Values"}, 
                    InputData, ReadOnlyOutputData);
            }
            catch (Exception ex)
            {   
                Log.Error(ex.Message);

                // The verdict can be set more than once in a Test Step.
                // UpgradeVerdict sets the current verdict to the more serious verdict.
                // If the new Verdict is not more severe, the original setting will be kept.
                UpgradeVerdict(Verdict.Error);
            }
        }

        //public override void PostPlanRun()
        //{
        //    // This method is called on all TestSteps at the END of TestPlan execution.
        //    // If no shutdown code is needed, this method can be removed.
        //    base.PostPlanRun();
        //}

        // Additional methods can be added to Test Step classes.
        private bool SizesAreAppropriate()
        {
            return InputData.Length > WindowSize;
        }
    }
}
