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

// An example a parent that has certain resources available (the DUT and Instrument).
// Children of this parent may "reach up" and use those resources.
// This allows resources to be defined once, for a set of sibling children.

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Parent With Resources", Groups: new[] { "Examples", "Plugin Development", "Parent Child" }, 
        Description: "A parent step, that only allows ChildSeekingResources child steps, with resources.")]
    // This parent only allows children of the specified type.
    [AllowChildrenOfType(typeof(ChildSeekingResources))]
    public class ParentWithResources : TestStep
    {
        // The DUT and Instrument resources can be accessed via GetParent in child steps.
        public Dut SomeDut { get; set; }
        public Instrument SomeInstrument { get; set; }

        public override void PrePlanRun()
        {
            base.PrePlanRun(); 
        }

        public override void Run()
        {            
            RunChildSteps(); // If step has child steps.
            UpgradeVerdict(Verdict.Pass);
        }

        public override void PostPlanRun()
        {
            base.PostPlanRun();
        }
    }
}
