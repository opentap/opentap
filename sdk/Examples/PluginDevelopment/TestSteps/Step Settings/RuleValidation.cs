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

        public RuleValidation()
        {
            // Validation occurs during the constructor.
            // When using the GUI, validation will occur upon editing.
            // When using the engine without the GUI, validation occurs upon loading the test plan.

            // Calls a function that returns a boolean.
            Rules.Add(CheckShouldBeTrueFunc, "Must be true to run", "ShouldBeTrueProp");

            // Calls an anonymous function that returns a boolean.
            Rules.Add(() => MyInt1 + MyInt2 == 6, "MyInt1 + MyInt2 must == 6", "MyInt1", "MyInt2");

            // Ensure all rules fail.
            ShouldBeTrueProp = false;
            MyInt1 = 2;
            MyInt2 = 2;
        }

        private bool CheckShouldBeTrueFunc()
        {
            return ShouldBeTrueProp;
        }

        public override void Run()
        {
            // Do nothing
        }
    }
}
