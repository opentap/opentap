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
    [Display("Handle Input", Groups: new[] { "Examples", "Plugin Development", "Input Output" }, 
        Description: "Handles a double input value.")]
    public class HandleInput : TestStep
    {
        [Display("Input Value")]
        // Properties defined using the Input generic class will accept value from other 
        // test steps with properties that have been marked with the Output attribute.
        public Input<double> InputValue { get; set; }      

        public HandleInput()
        {
            InputValue = new Input<double>();
        }

        public override void Run()
        {
            if (InputValue == null) throw new ArgumentException();

            Log.Info("Input Value: " + InputValue.Value);
            UpgradeVerdict(Verdict.Pass);
        }
    }
}
