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
    [Display("Abort Example", Groups: new[] { "Examples", "Plugin Development", "Step Execution" })]
    public class AbortsTheTestPlan : TestStep
    {
        public override void Run()
        {
            // Any exception will result in the debug message in the log.
            // Make sure that debug messages are visible in the log.
            PlanRun.MainThread.Abort();
        }
    }
}
