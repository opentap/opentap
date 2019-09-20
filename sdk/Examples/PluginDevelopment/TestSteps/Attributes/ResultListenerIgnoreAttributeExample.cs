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

// Most properties are available to the ResultListener via the Parameters collection on the TestStepRun object.
// Using the ResultListenerIgnoreAttribute on a property will result in that property not being made available.
namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Result Listener Ignore Attribute Example",
       Groups: new[] { "Examples", "Plugin Development", "Attributes" },
       Description: "This example shows usage of the ResultListenerIgnore attribute.")]
    
    public class ResultListenerIgnoreAttributeExample : TestStep
    {
        // By default, all Test Step settings are stored by the ResultsListener and available in the ResultsViewer.
        public string MyNormalSetting { get; set; }

        [ResultListenerIgnore]
        public string MyResultListenerIgnoredSetting { get; set; }
        
        public override void Run()
        {
            RunChildSteps(); //If step has child steps.
            UpgradeVerdict(Verdict.Pass);
        }
    }
}
