using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTap
{
    /// <summary>
    /// Test step break conditions. Can be used to define when a test step should issue a break due to it's own verdict.
    /// </summary>
    [Flags]
    internal enum BreakCondition
    {
        /// <summary> Inherit behavior from parent or engine settings. </summary>
        [Display("Inherit", "Inherit behavior from the parent step. If no parent step exist or specify a behavior, the Engine setting 'Stop Test Plan Run If' is used.")]
        Inherit = 1,
        /// <summary> If a step completes with verdict 'Error', stop execution of any subsequent steps at this level, and return control to the parent step. </summary>
        [Display("Break on Error", "If a step completes with verdict 'Error', skip execution of subsequent steps and return control to the parent step.")]
        BreakOnError = 2,
        /// <summary> If a step completes with verdict 'Fail', stop execution of any subsequent steps at this level, and return control to the parent step. </summary>
        [Display("Break on Fail", "If a step completes with verdict 'Fail', skip execution of subsequent steps and return control to the parent step.")]
        BreakOnFail = 4,
        [Display("Break on Inconclusive", "If a step completes with verdict 'inconclusive', skip execution of subsequent steps and return control to the parent step.")]
        BreakOnInconclusive = 8,
    }

    /// <summary>
    /// Break condition is an 'attached property' that can be attached to any implementor of ITestStep. This ensures that the API for ITestStep does not need to be modified to support the BreakConditions feature.
    /// </summary>
    internal static class BreakConditionProperty
    {
        /// <summary> Sets the break condition for a test step. </summary>
        /// <param name="step"> Which step to set it on.</param>
        /// <param name="condition"></param>
        public static void SetBreakCondition(ITestStep step, BreakCondition condition)
        {
            BreakConditionTypeDataProvider.TestStepTypeData.AbortCondition.SetValue(step, condition);
        }
        
        /// <summary> Gets the break condition for a given test step. </summary>
        /// <param name="step"></param>
        /// <returns></returns>
        public static BreakCondition GetBreakCondition(ITestStep step)
        {
            return (BreakCondition) BreakConditionTypeDataProvider.TestStepTypeData.AbortCondition.GetValue(step);
        }
    }

    internal class BreakConditionTypeDataProvider : IStackedTypeDataProvider
    {
        internal class VirtualMember<T> : IMemberData
        {
            public IEnumerable<object> Attributes { get; set; }
            public string Name { get; set; }
            public ITypeData DeclaringType { get; set; }
            public ITypeData TypeDescriptor { get; set; }
            public bool Writable { get; set; }
            public bool Readable { get; set; }

            public object DefaultValue;
            
            ConditionalWeakTable<object, object> dict = new ConditionalWeakTable<object, object>();

            public void SetValue(object owner, object value)
            {
                dict.Remove(owner);
                if (object.Equals(value, DefaultValue) == false)
                    dict.Add(owner, value);
            }

            public object GetValue(object owner)
            {
                if (dict.TryGetValue(owner, out object value))
                    return value;
                return DefaultValue;
            }
        }
        internal class TestStepTypeData : ITypeData
        {
            internal static readonly VirtualMember<BreakCondition> AbortCondition = new VirtualMember<BreakCondition>
            {
                Name = "BreakConditions",
                DefaultValue = BreakCondition.Inherit,
                Attributes = new Attribute[]{new DisplayAttribute("Break Conditions", "When enabled, specify new break conditions. When disabled conditions are inherited from the parent test step or the engine settings.", "Common", 20001.1), new UnsweepableAttribute() },
                DeclaringType = TypeData.FromType(typeof(TestStepTypeData)),
                Readable = true,
                Writable =  true,
                TypeDescriptor = TypeData.FromType(typeof(BreakCondition))
            };
            
            static IMemberData[] extraMembers =  {AbortCondition};
            public TestStepTypeData(ITypeData innerType)
            {
                this.innerType = innerType;
            }

            public override bool Equals(object obj)
            {
                if (obj is TestStepTypeData td2) 
                    return td2.innerType.Equals(innerType);
                return base.Equals(obj);
            }

            public override int GetHashCode() =>  innerType.GetHashCode() * 157489213;

            readonly ITypeData innerType;
            public IEnumerable<object> Attributes => innerType.Attributes;
            public string Name => innerType.Name;
            public ITypeData BaseType => innerType;
            public IEnumerable<IMemberData> GetMembers()
            {
                return innerType.GetMembers().Concat(extraMembers);
            }

            public IMemberData GetMember(string name)
            {
                if (name == AbortCondition.Name) return AbortCondition;
                return innerType.GetMember(name);
            }

            public object CreateInstance(object[] arguments)
            {
                return innerType.CreateInstance(arguments);
            }

            public bool CanCreateInstance => innerType.CanCreateInstance;
        }
        public ITypeData GetTypeData(string identifier, TypeDataProviderStack stack)
        {
            var subtype = stack.GetTypeData(identifier);
            if (subtype.DescendsTo(typeof(ITestStep)))
            {
                return new TestStepTypeData(subtype);
            }

            return subtype;
        }

        public ITypeData GetTypeData(object obj, TypeDataProviderStack stack)
        {
            if (obj is ITestStep step)
            {
                var subtype = stack.GetTypeData(obj);
                return new TestStepTypeData(subtype);
            }

            return null;
        }

        public double Priority { get; } = 10;
    }
}