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
    [Display("Generate Output", Groups: new[] { "Examples", "Plugin Development", "Input Output" },
        Description: "Generates a double output setting.")]
    public class GenerateOutput : TestStep
    {
        [Display("Initial Value")]
        public double InitialValue { get; set; }

        // Output values can be used as inputs to other test steps (see HandleInputs.cs).
        // These are useful when test step depend on output from other steps.
        [Output]
        [Display("Output Value")]
        public double OutputValue { get; private set; }

        public GenerateOutput()
        {
            OutputValue = 1.0;
            InitialValue = 2.0;
        }

        public override void Run()
        {
            OutputValue = 2 * InitialValue;

            UpgradeVerdict(Verdict.Pass);
        }
    }
}
