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

using System;
using System.ComponentModel;
using OpenTap;

// This example shows dynamic generation of TestStep types (when loading the TestPlan from XML).

// When reading the TestPlan XML file, two passes are done for the IDynamicStep type. First the Factory class is deserialized,
// then GetStep() is called to create the dynamic step. Finally the XML code is deserialized to the dynamic step.
// Hence the XML can contain properties that only apply to the factory class or the dynamic step or both.


namespace OpenTap.Plugins.PluginDevelopment
{
    [Display("Dynamic Step Example",
             Groups: new[] { "Examples", "Plugin Development" },
             Description: "This example shows dynamic generation of TestStep types. The step loads either DynamicMessageStep or DynamicDelayStep depending on settings.")]
    public class DynamicStepExample : TestStep, IDynamicStep
    {
        public enum StepType { MessageStep, DelayStep };

        [Display("Option", Description: "Set type of step to Message or Delay.", Group: "Step Type", Order: 1)]
        public StepType Option { get; set; }

        [Browsable(true)]
        [Display("Load", Description: "Load dynamic step according to selected option.", Group: "Step Type", Order: 2)]
        public void Load()
        {
            if (Parent != null)
            {
                // Replace DynamicStepExample with either DynamicDelayStep or DynamiMessageStep
                var OldParent = Parent;
                int index = OldParent.ChildTestSteps.IndexOf(this);

                if (index != -1)
                {
                    OldParent.ChildTestSteps.RemoveAt(index);
                    OldParent.ChildTestSteps.Insert(index, GetStep());
                }
            }
        }

        public override void Run()
        {
        }

        // Returns the type of the class that can create the dynamic step. The Type returned should implement IDynamicStep.
        public Type GetStepFactoryType()
        {
            return typeof(DynamicStepExample);
        }

        // Returns itself or a new step to be exchanged with itself in the test plan. Must never return null.
        public ITestStep GetStep()
        {
            if (Option == StepType.DelayStep)
            {
                // Return DynamicDelayStep
                var step = new DynamicDelayStep();
                step.Option = StepType.DelayStep;
                return step;
            }
            else
            {
                // Return DynamiMessageStep
                var step = new DynamiMessageStep();
                step.Option = StepType.MessageStep;
                return step;
            }
        }
    }

    class DynamicDelayStep : DynamicStepExample
    {
        // Public properties of dynamic steps are serialized to TestPlan XML file.
        [Display("Delay", Description: "Delay settings.", Group: "Delay Settings", Order: 100)]
        [Unit("Sec")]
        public double Delay { get; set; }

        public override void Run()
        {
            TapThread.Sleep(Convert.ToInt32(Delay * 1000));
        }
    }

    class DynamiMessageStep : DynamicStepExample
    {
        [Display("Message", Description: "Message settings.", Group: "Message Settings", Order: 100)]
        public string Message { get; set; }

        public override void Run()
        {
            Log.Info("Message: {0}", Message);
        }
    }
}
