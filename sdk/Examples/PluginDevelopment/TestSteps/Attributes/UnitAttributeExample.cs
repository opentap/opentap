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
    [Display("Unit Attribute Example", Groups: new[] { "Examples", "Plugin Development", "Attributes" }, 
        Description: "Example step that uses the Unit attribute.")]
    public class UnitAttributeExample : TestStep
    {
        [Unit("Hz", UseEngineeringPrefix: false)]
        public double FrequencyWithHz { get; set; }

        [Unit("Hz", UseEngineeringPrefix: true)]
        public double FrequencyWithHzAndEngPrefix { get; set; }

        [Unit("Hz", UseEngineeringPrefix: true, PreScaling: 1000)]
        public double FrequencyWithHzAndEngPrefixAndPreScaling { get; set; }

        [Unit(" Hz", UseRanges: true)]
        public int[] MyIntArrayUnits { get; set; }

        public UnitAttributeExample()
        {
            FrequencyWithHz = 1234567.89;
            FrequencyWithHzAndEngPrefix = 1234567.89;
            FrequencyWithHzAndEngPrefixAndPreScaling = 1234567.89;
            MyIntArrayUnits = new int[] {1, 2, 4, 5, 6, 7};
        }

        public override void Run()
        {
            // Do nothing
        }
    }
}
