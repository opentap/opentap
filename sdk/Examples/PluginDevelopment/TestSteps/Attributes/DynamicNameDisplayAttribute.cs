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
    // This example is a demonstration of a test step name being dynamically modified by values of a setting.  
    // This is useful when test steps of the same type are added or looped, but have different settings values.
    // For example, a test might be run at identical frequencies within a sweep loop.
    
    // Strings in braces {} in the Display.Name parameter are replaced with the value of the property of the same name.
    
    // Dynamic naming only applies to test steps.
    // Because of the default setting, when first added this Test Step will be displayed as:
    //      Dynamic Name - 10
    // Not that in the Add New Step window, this value will not be expanded yet.
    [Display(Name: "Dynamic Name - {MyValue}",
        Groups: new[] {"Examples", "Plugin Development", "Attributes"},
        Description: "An example of how a test step name can dynamically reflect step values.")]
    public class DynamicName : TestStep
    {
        public double MyValue { get; set; }

        public DynamicName()
        {
            MyValue = 10;
        }
        
        public override void Run()
        {
            // Do Nothing
        }
    }
}
