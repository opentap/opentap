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

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("RuleValidation Example", Groups: new[] { "Examples", "Plugin Development", "Step Settings" }, 
        Description: "An example of how validation works.")]
    // Validation works for ComponentSettings, Resources (DUTs, Instruments, and ResultListeners) and Test Steps,
    // since they all extend ValidatingObject.
    public class RuleValidation : TestStep 
    {
        [Display("Should Be True Property", Description: "This value should be true to pass validation.")]
        public bool ShouldBeTrueProp { get; set; }

        public int MyInt1 { get; set; }
        public int MyInt2 { get; set; }
        public int MyInt3 { get; set; }

        public RuleValidation()
        {
            // Validation rules are usually set up by the constructor.
            // They are soft errors and do not block test plan execution by default.
            // When using the GUI, validation rules are checked when editing.
            // Otherwise, they have to be manually checked (see the Run implementation below).

            // Calls a function that returns a boolean. Use nameof to make sure the property name is correctly specified.
            Rules.Add(CheckShouldBeTrueFunc, "Must be true to run", nameof(ShouldBeTrueProp));  

            // Calls an anonymous function that returns a boolean.
            Rules.Add(() => MyInt1 + MyInt2 == 6, "MyInt1 + MyInt2 must == 6", nameof(MyInt1), nameof(MyInt2));
            
            // The error message can also be dynamically generated using a function.
            Rules.Add(() => MyInt3 > 0, () => $"MyInt3 must be greater than 0, but it is {MyInt3}.", nameof(MyInt3));
            
            // Ensure all rules fail.
            ShouldBeTrueProp = false;
            MyInt1 = 2;
            MyInt2 = 2;
            MyInt3 = -2;
        }

        private bool CheckShouldBeTrueFunc()
        {
            return ShouldBeTrueProp;
        }

        //public override void PrePlanRun()
        //{  
        //    // Block the test plan from being run if there are any validation errors with the current values.
        //    // Note that this might not be desirable if the test step is used with e.g sweep loops and the initial value
        //    // are not ever used. And it also does not protect in case other steps modify the values.
        //    ThrowOnValidationError(true);
        //}

        public override void Run()
        {
            // Block the test step from being run if there are any validation errors with the current values.
            ThrowOnValidationError(true);
        }
    }
}
