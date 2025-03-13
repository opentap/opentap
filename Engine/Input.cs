//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Linq;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>   Interface for TestStep input properties. </summary>
    public interface IInput
    {
        /// <summary>   Gets or sets the TestStep that has the output property to which this Input is connected. </summary>
        ITestStep Step { get; set; }

        /// <summary>   Describes the <see cref="OutputAttribute"/> property on the <see cref="Step"/> to which this Input is connected.   </summary>
        IMemberData Property { get; set; }
    }

    /// <summary>
    /// Input type restriction for IInput.
    /// </summary>
    public interface IInputTypeRestriction : IInput
    {
        /// <summary> returns true if the concrete type is supported. </summary>
        /// <param name="concreteType"></param>
        /// <returns></returns>
        bool SupportsType(ITypeData concreteType);
    }

    interface IOwnedInput
    {
        object Owner { get; set; }
    }

    /// <summary>   
    /// A generic type that specifies input properties for a TestStep. The user can link this property to properties on other TestSteps that are marked with the <see cref="OutputAttribute"/>
    /// When used in a TestStep, Input value should always be set in the constructor.
    /// </summary>
    /// <typeparam name="T"> Generic type parameter. </typeparam>
    public class Input<T> : IInput, IInputTypeRestriction, ICloneable, IOwnedInput
    {
        /// <summary> 
        /// Describes the output property on the <see cref="Step"/> to which this Input is connected.  
        /// </summary>
        [XmlIgnore]
        public IMemberData Property { get; set; }

        void updatePropertyFromName()
        {
            if (string.IsNullOrEmpty(propertyName))
                return;
            else
            {
                string[] parts = propertyName.Split('|');
                if (parts.Length == 1)
                {
                    if (step != null)
                    {
                        ITypeData stepType = TypeData.GetTypeData(step);
                        Property = stepType.GetMember(parts[0]);    
                    }
                    else
                    {
                        Property = null;
                    }
                }
                else
                {
                    var typename = parts[0];
                    ITypeData stepType = TypeData.GetTypeData(typename);
                    Property = stepType.GetMember(parts[1]);
                }
            }

            if (Property != null)
                propertyName = null;
        }
        
        string propertyName;
        /// <summary> Gets or sets the name of the property to which this Input is connected. Used for serialization. </summary>
        public string PropertyName
        {
            get => Property != null ? Property.DeclaringType.Name + "|" + Property.Name : propertyName;
            set
            {
                propertyName = value;
                updatePropertyFromName();
                
            }
        }

        private ITestStepParent getTopmostParent(ITestStepParent step)
        {
            if (step == null) return null;
            ITestStepParent parent = step;
            while (parent.Parent != null)
                parent = parent.Parent;
            return parent;
        }

        object testStepOwner = null;
        const string OwnerNotFound = nameof(OwnerNotFound);
        ITestStep step;

        /// <summary>
        /// Sometimes we need to know the owner object.
        /// Since we cannot automatically get it from the owning object, we can iterate the entire test plan to find it.
        /// It's a bit of a hack, but works well.
        /// For better performance we set the owner object for all Inputs in the test plan by using the IOwnedInput (internal) interface.
        /// </summary>
        void UpdateAllOwnedInputsInPlan()
        {
            if (step == null) return;
            var topmost = getTopmostParent(step);
            foreach (var step in topmost.ChildTestSteps.RecursivelyGetAllTestSteps(TestStepSearch.All))
            {
                foreach (var inputMember in TypeData.GetTypeData(step).GetMembers().Where(member => member.TypeDescriptor.DescendsTo(typeof(IOwnedInput)) && member.Readable))
                {
                    try
                    {
                        if (inputMember.GetValue(step) is IOwnedInput owned)
                        {
                            owned.Owner = step;
                        }
                    }
                    catch
                    {
                        // Ignore errors in user code.
                    }
                }
            }
        }

        /// <summary>   Gets or sets the TestStep that has the output property to which this Input is connected. </summary>
        public ITestStep Step
        {
            get
            {
                // If the step is part of the test plan heirarchy, return the step.
                // Otherwise, return null.
                if (step == null) return null;

                if (testStepOwner == null)
                {
                    UpdateAllOwnedInputsInPlan();
                    if(testStepOwner == null)
                        testStepOwner = OwnerNotFound;
                }

                if (testStepOwner is ITestStep ownerStep)
                {

                    if (getTopmostParent(ownerStep).ChildTestSteps.GetStep(step.Id) != null)
                        return step;

                    // the step is not in the same plan as the owner step.
                    return null;
                }

                // if we could not find the owner step, then its better to assume that the value of step is ok.
                return step;
            }
            set
            {
                if(step != value)
                {
                    step = value;
                    
                    if (step == null)
                    {   
                        return;
                    }
                    stepId = step.Id; 
                    updatePropertyFromName();
                }
            }
        }

        // the step ID is used for storing the step ID when moving the Step owning the Input.
        // otherwise, after moving the step (taking it out of the test plan and moving it back in) the Step might be null.
        Guid stepId;

        /// <summary>   Gets the value of the connected output property. </summary>
        /// <exception cref="Exception">    Thrown when this Input does not contain a reference to a TestStep output. </exception>
        [XmlIgnore]
        public T Value
        {
            get
            {
                if (step == null || Property == null)
                    throw new Exception("Step input requires reference to a TestStep output.");
                
                // Wait for the step to complete
                return (T)InputOutputRelation.GetOutput(Property, step, Guid.NewGuid()); 
            }
        }

        T GetValueNonBlocking()
        {
            if (step != null && Property?.GetValue(Step) is T v)
                return v;
            return default;
        }
        
        /// <summary> Converts the value of this instance to its equivalent string representation. </summary>
        /// <returns> The string representation of the value of this instance. </returns>
        public override string ToString() =>  StringConvertProvider.GetString(GetValueNonBlocking()) ?? "";

        /// <summary> Compares one Input to another. </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if(obj is Input<T> other)
                return other.step == step && other.Property == Property;
            return false;
        }

        /// <summary> Gets the hash code.</summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return (step?.GetHashCode() ?? 0) ^ (Property?.GetHashCode() ?? 0);
        }

        object ICloneable.Clone() => new Input<T>
        {
            step = step,
            Property = Property
        };

        /// <summary> Returns true if this input supports the concrete type. </summary>
        /// <param name="concreteType"></param>
        /// <returns></returns>
        public virtual bool SupportsType(ITypeData concreteType)
        {
            return concreteType.DescendsTo(typeof(T));
        }

        /// <summary> Compares two Input for equality.</summary>
        /// <returns></returns>
        public static bool operator==(Input<T> a, Input<T> b) => object.Equals(a, b);

        /// <summary> Compares two Input for inequality.</summary>
        /// <returns></returns>
        public static bool operator !=(Input<T> a, Input<T> b) => !(a == b);
        object IOwnedInput.Owner
        {
            get => testStepOwner as ITestStepParent;
            set => testStepOwner = value;
        }
    }
}
