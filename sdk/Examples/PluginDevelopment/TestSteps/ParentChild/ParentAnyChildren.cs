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
    [Display("Parent Any Children", Groups: new[] { "Examples", "Plugin Development", "Parent Child" }, 
        Description: "A parent that allows any children.")]
    // This parent will allow any children.
    [AllowAnyChild]
    public class ParentAnyChildren : TestStep
    {
        public override void PrePlanRun()
        {
            base.PrePlanRun();
            // Delay was added so that behavior could be observed in the Timing Analyzer.
            TapThread.Sleep(100);
        }

        public override void Run()
        {
            // Delay was added so that behavior could be observed in the Timing Analyzer.
            TapThread.Sleep(100);
            RunChildSteps(); // If step has child steps.
            UpgradeVerdict(Verdict.Pass);
        }

        public override void PostPlanRun()
        {
            // Delay was added so that behavior could be observed in the Timing Analyzer.
            TapThread.Sleep(100);
            base.PostPlanRun();
        }
    }
}
