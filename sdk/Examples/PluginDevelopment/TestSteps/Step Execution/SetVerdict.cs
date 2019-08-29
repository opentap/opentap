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
    [Display("Set Verdict", Groups: new[] { "Examples", "Plugin Development", "Step Execution" }, 
        Description: "Shows how verdict of step is set using UpgradeVerdict.")]
    public class SetVerdict : TestStep
    {
        // The verdict value is how the overall result of the TestStep and TestPlan execution is described.
        // Verdict can be set to six different values, sorted from least to most severe:
        //   NotSet - the default value.
        //   Pass - Execution went as expected.
        //   Inconclusive - Result could not be determined.
        //   Fail - Required results were not met.
        //   Aborted - Test was stopped, and did not complete.
        //   Error - An exception or procedural error occurred.
        public Verdict MyVerdict { get; set; }

        public double LowerLimit { get; set; }

        public double UpperLimit { get; set; }

        public SetVerdict()
        {
            MyVerdict = Verdict.NotSet;
            LowerLimit = 0;
            UpperLimit = 5;
        }

        public override void Run()
        {
            // UpgradeVerdict is a method on all TestSteps. It can be set at any time during TestStep.Run.
            // If the previous Verdict is less severe (described above) than the new Verdict, it will be Upgraded, 
            // otherwise it will remain the same.
            UpgradeVerdict(MyVerdict);

            // Perform limit check.
            var result = 2.5;
            if (result > LowerLimit && result < UpperLimit)
            {
                UpgradeVerdict(Verdict.Pass);
            }
            else UpgradeVerdict(Verdict.Fail);

            // The overall TestPlan Verdict follows the same principles.
        }
    }
}
