//Copyright 2012-2021 Keysight Technologies
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

using System.ComponentModel;

namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Default Value Example", "Shows how DefaultValueAttribute can be used.",  Groups: new[] { "Examples", "Plugin Development", "Attributes" })]
    public class DefaultValueExample : TestStep
    {
        /// <summary> When this property has it's default value, that value will not be saved in the test plan xml.</summary>
        [DefaultValue(1.0)] public double Value { get; set; } = 1.0;
        public override void Run()
        {
            Log.Info("The current value is {0}", Value);
        }
    }
}