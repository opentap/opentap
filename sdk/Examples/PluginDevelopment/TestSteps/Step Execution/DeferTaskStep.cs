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
using System.Collections.Generic;

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Defer Task Example", Groups: new[] { "Examples", "Plugin Development", "Step Execution" },
        Description: "This example shows defer actions that run after the step's run method returns.")]
    public class DeferTaskStep : TestStep
    {
        [Display("Run Delay")]
        [Unit("s")]
        public int RunDelay { get; set; }

        [Display("Defer Delay")]
        [Unit("s")]
        public int DeferDelay { get; set; }

        [Display("PrePlanRun Delay")]
        [Unit("s")]
        public int PrePlanDelay { get; set; }

        [Display("PostPlanRun Delay")]
        [Unit("s")]
        public int PostPlanDelay { get; set; }

        public DeferTaskStep()
        {
            RunDelay = 3;
            DeferDelay = 2;
            PostPlanDelay = 1;
            PrePlanDelay = 1;
        }

        public override void PrePlanRun()
        {
            base.PrePlanRun();
            TapThread.Sleep(PrePlanDelay * 1000);
        }

        public override void PostPlanRun()
        {
            TapThread.Sleep(PostPlanDelay * 1000);
            base.PostPlanRun();
        }

        public override void Run()
        {
            TapThread.Sleep(RunDelay * 1000);
            Results.Defer(() =>
            {
                TapThread.Sleep(DeferDelay * 1000);

                // You can also publish results here for example.
                Results.Publish("MyTestResult", new List<string> { "Test" }, 1);
            });
        }

    }
}
