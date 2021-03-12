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
using System.Linq;

namespace OpenTap.PluginDevelopment.Advanced_Examples
{
    // Icon annotations can be used to provide custom context menu / right-click functionality
    // in settings user interfaces. This can for example be used to clear the value of a property.
    // let's try this.
    // to do this, we need a IMenuModel, a IMenuModelFactory and some custom Icon names.


    /// <summary>
    /// This menu model factory takes an IMemberData and decides if it can generate a model for it.
    /// </summary>
    public class MenuModelExampleFactory : IMenuModelFactory
    {
        public IMenuModel CreateModel(IMemberData member)
        {
            // our MenuModelExample works for all members that defines DefaultValueAttribute.
            if (member.HasAttribute<DefaultValueAttribute>())
            {
                return new MenuModelExample(member);
            }

            return null;
        }
    }
    
    /// <summary>
    /// Shows how to add a menu model. This specific example is about DefaultValueAttribute.
    /// After compiling, try creating a DefaultValueExample test step and right-click it's setting. 
    /// </summary>
    public class MenuModelExample : IMenuModel
    {
        readonly IMemberData member;

        /// <summary> This class requires the member to be specified. </summary>
        public MenuModelExample(IMemberData member)
        {
            this.member = member;   
        }
        
        /// <summary>  When multi-selecting, this holds more than one value. Otherwise, it just contains one step. </summary>
        public object[] Source { get; set; }

        // some code to check that the test step is in a state that is allowed to be modified.
        static bool testStepLocked(ITestStep step)
        {
            if (step.IsReadOnly) return true;
            if (step.Parent is ITestStep step2) return testStepLocked(step2);
            if (step.Parent is TestPlan plan)
                return plan.IsRunning || plan.Locked;
            return true;
        }
        
        public bool CanExecuteRevert => member.Writable && !Source.OfType<ITestStep>().Any(testStepLocked);
        
        public bool IsAlreadyDefaultValues => Source.All(source =>
            Equals(member.GetValue(source), member.GetAttribute<DefaultValueAttribute>()?.Value));
        
        [Display("Revert to default", "Revert the value to it's default.")]
        // The is IconAnnotation is not generally important, it can be used by
        // user interfaces to identify which icon to associate with the action, if applicable.
        [IconAnnotation("MenuModelExample.RevertToDefault")]
        // This is needed because we are using Method annotations (not by default browsable).
        [Browsable(true)]
        // show it as grayed if not writable.
        [EnabledIf(nameof(CanExecuteRevert))] 
        // show as grayed if the current value is the same as the default
        [EnabledIf(nameof(IsAlreadyDefaultValues), false)] 
        public void RevertToDefault() // multiple methods can be added, each corresponding to another menu item.    
        {
            // get the default value and set the property value(s).
            // try multi selecting to understand why we do a 'for-each'.
            var defaultValue = member.GetAttribute<DefaultValueAttribute>().Value;
            foreach (var source in Source)
            {
                member.SetValue(source, defaultValue);
            }
        }
    }
}