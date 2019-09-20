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
    [Display("Flags Attribute Example", Groups: new[] { "Examples", "Plugin Development", "Attributes" },
        Description: "Shows examples of how to use the Flags attribute.")]
    public class FlagsAttributeExample : TestStep
    { 
        // The enum values are used as a bitmask to define enabled and disabled.
        [Flags]
        public enum FixturePositions
        {
            // Display names can be added to individual enum values.
            [Display("My Fixture Position A")]
            A = 1,
            B = 2,
            C = 4,
            D = 8,
            [Display("My Fixture Position E")]
            E = 16,
            F = 32,
            G = 64
        }

        // To use the flags create a property of the enum type.
        [Display("Fixture Positions", "A setting with potentially multiple selections.")]
        public FixturePositions MyFixturePositions { get; set; }

        public FlagsAttributeExample()
        {
            // The '|' operator can be used to define multiple selections
            MyFixturePositions = FixturePositions.A | FixturePositions.B;
        }

        public override void Run()
        {
            // Use the HasFlag method to check which flags have been selected. 
            if (MyFixturePositions.HasFlag(FixturePositions.A))
            {
                UpgradeVerdict(Verdict.Pass);
            }
        }
    }
}
