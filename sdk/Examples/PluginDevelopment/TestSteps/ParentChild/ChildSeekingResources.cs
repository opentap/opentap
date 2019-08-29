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

// This example shows how a child can get a parent with of a certain type, and then
// recover some properties from that parent.

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Child Seeking Resources", Groups: new[] { "Examples", "Plugin Development", "Parent Child" }, 
        Description: "A child seeking resources from a parent.")]
    [AllowAsChildIn(typeof(ParentWithResources))]
    // Excluding the AllowAsChildIn attribute would mean this Child TestStep could be used with any parent.
    public class ChildSeekingResources : TestStep
    {
        private Dut _parentsDut;
        private Instrument _parentsInstrument;

        public override void PrePlanRun()
        {
            base.PrePlanRun(); 

            // Find parents of a certain type, and get a resource reference from them.
            // Resources include things like Instruments and DUTs.
            _parentsDut = GetParent<ParentWithResources>().SomeDut;
            _parentsInstrument = GetParent<ParentWithResources>().SomeInstrument;

            string s;
            s = (_parentsDut == null) ? "No DUT Found" : string.Format("Found a DUT named {0}", _parentsDut.Name);
            Log.Info(s);

            s = (_parentsInstrument == null)
                ? "No Instrument"
                : string.Format("Found an Instrument named {0}", _parentsInstrument.Name);
            Log.Info(s);
        }

        public override void Run()
        {
            // Do nothing
        }
    }
}
