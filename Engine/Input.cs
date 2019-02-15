//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>   Interface for TestStep input properties. </summary>
    public interface IInput
    {
        /// <summary>   Gets or sets the TestStep that has the output property to which this Input is connected. </summary>
        ITestStep Step { get; set; }

        /// <summary>   Describes the <see cref="OutputAttribute"/> property on the <see cref="Step"/> to which this Input is connected.   </summary>
        PropertyInfo Property { get; set; }
    }

    /// <summary>
    /// Input type restriction for IInput.
    /// </summary>
    public interface IInputTypeRestriction : IInput
    {
        /// <summary> returns true if the concrete type is supported. </summary>
        /// <param name="concreteType"></param>
        /// <returns></returns>
        bool SupportsType(Type concreteType);
    }

    /// <summary>   
    /// A generic type that specifies input properties for a TestStep. The user can link this property to properties on other TestSteps that are marked with the <see cref="OutputAttribute"/>
    /// When used in a TestStep, Input value should always be set in the constructor.
    /// </summary>
    /// <typeparam name="T"> Generic type parameter. </typeparam>
    public class Input<T> : ValidatingObject, IInput, IInputTypeRestriction
    {
        /// <summary> 
        /// Describes the output property on the <see cref="Step"/> to which this Input is connected.  
        /// </summary>
        [XmlIgnore]
        public PropertyInfo Property { get; set; }

        /// <summary>   
        /// Gets or sets the name of the property to which this Input is connected. Used for serialization.  
        /// </summary>
        public string PropertyName
        {
            get { return Property != null ? Property.DeclaringType.AssemblyQualifiedName + "|" + Property.Name : null; }
            set
            {
                if (string.IsNullOrEmpty(value))
                    Property = null;
                else
                {
                    string[] parts = value.Split('|');
                    Type stepType = PluginManager.LocateType(parts.First().Split(',').First());

                    Property = stepType.GetProperty(value.Split('|').Last());
                }
            }
        }

        Action unbindStep = () => { };

        ITestStep step;
        /// <summary>   Gets or sets the TestStep that has the output property to which this Input is connected. </summary>
        public ITestStep Step {
            get { return step; }
            set
            {
                if(step != value)
                {
                    step = value;
                    
                    if (step == null)
                    {   
                        return;
                    }
                    unbindStep();
                    List<ITestStepParent> parents = new List<ITestStepParent>();
                    var parent = step.Parent;
                    while(parent != null)
                    {
                        parents.Add(parent);
                        parent.ChildTestSteps.CollectionChanged += ChildTestSteps_CollectionChanged;
                        parent = parent.Parent;
                    }
                    
                    unbindStep = () => parents.ForEach(p => p.ChildTestSteps.CollectionChanged -= ChildTestSteps_CollectionChanged);
                    OnPropertyChanged("Step");
                }
            }
        }

        ITestStep prevStep;
        void ChildTestSteps_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if(Step != null && e.OldItems != null && e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                if (e.OldItems.Contains(step) || Step.GetParents().Any(e.OldItems.Contains))
                {
                    prevStep = step;
                    Step = null;
                }
            }else if(prevStep != null && e.NewItems != null && e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                if (e.NewItems.Contains(prevStep) || prevStep.GetParents().Any(e.NewItems.Contains))
                { // if prevStep or one of its parents has been re-added it means the step was moved and not removed.
                    Step = prevStep;
                    prevStep = null;
                }
            }
        }

        /// <summary>   Gets the value of the connected output property. </summary>
        /// <exception cref="Exception">    Thrown when this Input does not contain a reference to a TestStep output. </exception>
        [XmlIgnore]
        public T Value
        {
            get
            {
                if (Step == null || Property == null)
                    throw new Exception("Step input requires reference to a TestStep output.");

                // Wait for the step to complete
                var run = Step.StepRun;
                var planRun = step.PlanRun;
                if (run != null && planRun != null) run.WaitForCompletion(planRun.AbortToken);

                return (T)Property.GetValue(Step, null);
            }
        }
        
        /// <summary>Constructor for the Input class.</summary>
        public Input()
        {
            Rules.Add(() => Step != null, "No input selected.", "Step");
        }
        /// <summary> Compares one Input to another. </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            var other = obj as IInput;
            if (other == null) return false;
            return other.Step == Step && other.Property == Property;
        }

        /// <summary> Gets the hash code.</summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return (Step?.GetHashCode() ?? 0) ^ (Property?.GetHashCode() ?? 0);
        }

        /// <summary> Returns true if this input supports the concrete type. </summary>
        /// <param name="concreteType"></param>
        /// <returns></returns>
        public virtual bool SupportsType(Type concreteType)
        {
            return concreteType.DescendsTo(typeof(T));
        }

        /// <summary> Compares two Input for equality.</summary>
        /// <returns></returns>
        public static bool operator==(Input<T> a, Input<T> b)
        {
            return a.Equals(b);
        }

        /// <summary> Compares two Input for inequality.</summary>
        /// <returns></returns>
        public static bool operator !=(Input<T> a, Input<T> b)
        {
            return !a.Equals(b);
        }
    }
}
