//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace OpenTap.Plugins
{
    /// <summary> Serializer implementation for TestStep. </summary>
    public class TestStepSerializer : TapSerializerPlugin
    {
        /// <summary> The order of this serializer. </summary>
        public override double Order { get { return 1; } }

        Dictionary<Guid, ITestStep> stepLookup = new Dictionary<Guid, ITestStep>();
        Dictionary<Guid, List<Action<object>>> setters = new Dictionary<Guid, List<Action<object>>>();
        /// <summary>
        /// Guids where duplicate guids should be ignored. Useful when pasting to test plan.
        /// </summary>
        HashSet<Guid> ignoredGuids = new HashSet<Guid>();

        /// <summary>
        /// Adds known steps to the list of tests used for finding references in deserialization.
        /// </summary>
        /// <param name="stepParent"></param>
        public void AddKnownStepHeirarchy(ITestStepParent stepParent)
        {
            var step = stepParent as ITestStep; // could also be TestPlan.

            if (step != null)
            {
                stepLookup[step.Id] = step;
                ignoredGuids.Add(step.Id);
            }
            
            foreach (var step2 in stepParent.ChildTestSteps)
                AddKnownStepHeirarchy(step2);
        }

        /// <summary>
        /// Ensures that duplicate step IDs are not present in the test plan and updates an ID->step mapping.
        /// </summary>
        /// <param name="step">the step to fix.</param>
        /// <param name="recurse"> true if child steps should also be 'fixed'.</param>
        public void FixupStep(ITestStep step, bool recurse)
        {
            if (stepLookup.ContainsKey(step.Id) && !ignoredGuids.Contains(step.Id))
            {
                step.Id = Guid.NewGuid();
                if (step is IDynamicStep)
                {   // if newStep is an IDynamicStep, we just print in debug.
                    Log.Debug("Duplicate test step ID found in dynamic step. The duplicate ID has been changed for step '{0}'.", step.Name);
                }
                else
                {
                    Log.Warning("Duplicate test step ID found. The duplicate ID has been changed for step '{0}'.", step.Name);
                }
            }
            stepLookup[step.Id] = step;

            if (recurse == false) return;
            foreach(var step2 in step.ChildTestSteps)
            {
                FixupStep(step2, true);
            }
        }
        
        /// <summary> Deserialization implementation. </summary>
        public override bool Deserialize( XElement elem, Type t, Action<object> setResult)
        {

            if(t.DescendsTo(typeof(ITestStep)))
            {
                if (elem.HasElements == false)
                {
                    Guid stepGuid;
                    if (Guid.TryParse(elem.Value, out stepGuid))
                    {
                        Serializer.DeferLoad(() =>
                        {
                            if (stepLookup.ContainsKey(stepGuid))
                                setResult(stepLookup[stepGuid]);
                        });
                        return true;
                    }
                }
                else
                {
                    if (currentNode.Contains(elem)) return false;
                    ITestStep step = null;
                    currentNode.Add(elem);
                    try
                    {
                        if (Serializer.Deserialize(elem, x => step = (ITestStep)x, t))
                        {
                            setResult(step);
                            FixupStep(step, false);
                            return true;
                        }
                    }finally
                    {
                        currentNode.Remove(elem);
                    }
                    
                    return false;

                }
            }
            return false;
        }
        HashSet<XElement> currentNode = new HashSet<XElement>();
        
        /// <summary> Serialization implementation. </summary>
        public override bool Serialize( XElement elem, object obj, Type expectedType)
        {
            if (false == obj is ITestStep) return false;
            if (currentNode.Contains(elem)) return false;
            
            var objp = Serializer.SerializerStack.OfType<ObjectSerializer>().FirstOrDefault();

            
            if (objp != null && objp.Object != null && objp.Property.PropertyType.DescendsTo(typeof(ITestStep)))
            {
                elem.Value = ((ITestStep)obj).Id.ToString();
                return true;
            }

            currentNode.Add(elem);
            try
            {
                Serializer.Serialize(elem, obj);
            }
            finally
            {
                currentNode.Remove(elem);
            }

            return true;
        }
    }

}
