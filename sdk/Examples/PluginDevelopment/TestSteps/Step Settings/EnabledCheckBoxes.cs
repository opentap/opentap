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

/*
This file shows the use of the EnableIf attribute and the Enable templates.
At a high level, these two features give the test step author flexibility
in defining when property editing should be allowed.

Note to avoid the overuse of the word “Enabled”,  
the word “Checked” is used to indicate those properties which
implement a checkbox (implemented via the Enabled template).

The EnabledIfExample template enables the editor, based on some other property value.
The Enabled          template inserts a check box in from of each editor.
EnabledIfExample is higher level logic, and will impact check boxes.
*/

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Enabled Check Boxes", Groups: new[] { "Examples", "Plugin Development", "Step Settings" }, 
        Description: "Shows different Enabled check boxes.")]
    public class EnabledCheckBoxes : TestStep
    {
        [Display("Checked Double", Order: 1)]
        public Enabled<double>  CheckedDouble { get; set; }

        [Display("Checked Double Array", Order: 2)]
        public Enabled<double[]> CheckedDoubleArray { get; set; }

        [Display("Checked Instrument", Order: 3)]
        public Enabled<Instrument> CheckedInstrument { get; set; }

        public EnabledCheckBoxes()
        {
            // A property that is enabled (either via EnableIf, or via a CheckBox)
            // must be populated with an object. Consequently, these properties are defined in the constructor.
            // Conversely properties that are NOT enabled do not require an object,
            // and will not result in errors at run time.
            
            CheckedDouble = new Enabled<double>() { IsEnabled = true, Value = 3.0};
            CheckedDoubleArray = new Enabled<double[]>();                        
            CheckedInstrument = new Enabled<Instrument>();
        }
        
        public override void Run()
        {
            Log.Info("CheckDouble is enabled: " + CheckedDouble.IsEnabled);
            Log.Info("CheckedDoubleArray is enabled: " + CheckedDoubleArray.IsEnabled);
            Log.Info("CheckedInstrument is enabled: " + CheckedInstrument.IsEnabled);

            UpgradeVerdict(Verdict.Pass);
        }
    }
}
