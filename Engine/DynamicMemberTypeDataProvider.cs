using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>  This interface speeds up accessing dynamic members as it avoids having to access a global table to store the information. </summary>
    interface IDynamicMembersProvider
    {
        IMemberData[] DynamicMembers { get; set; }
    }

    /// <summary>  Extensions for parameter operations. </summary>
    public static class ParameterExtensions
    {
        /// <summary> Parameterizes a member from one object unto another.
        /// If the name matches an existing parameter, the member will be added to that. </summary>
        /// <param name="target"> The object on which to add a new member. </param>
        /// <param name="member"> The member to forward. </param>
        /// <param name="source"> The owner of the forwarded member. </param>
        /// <param name="name"> The name of the new property. If null, the name of the source member will be used.</param>
        /// <returns>The parameterization of the member..</returns>
        public static ParameterMemberData Parameterize(this IMemberData member, object target, object source, string name)
        {
            if (member.GetParameter(target, source) != null)
                throw new Exception("Member is already parameterized.");
            return DynamicMember.ParameterizeMember(target, member, source, name);
        }

        /// <summary> Removes a parameterization of a member. </summary>
        /// <param name="parameterizedMember"> The parameterized member owned by the source. </param>
        /// <param name="parameter"> The parameter to remove it from.</param>
        /// <param name="source"> The source of the member. </param>
        public static void Unparameterize(this IMemberData parameterizedMember, ParameterMemberData parameter, object source)
        {
            DynamicMember.UnparameterizeMember(parameter, parameterizedMember, source);
        }

        /// <summary>
        /// Finds the parameter that parameterizes this member on 'source'. If no parameter is found null is returned.
        /// </summary>
        /// <param name="target"> The object owning the parameter.</param>
        /// <param name="source"> The source of the member. </param>
        /// <param name="parameterizedMember"> The parameterized member owned by the source. </param>
        /// <returns></returns>
        internal static ParameterMemberData GetParameter(this IMemberData parameterizedMember, object target, object source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (parameterizedMember == null)
                throw new ArgumentNullException(nameof(parameterizedMember));

            var parameterMembers = TypeData.GetTypeData(target).GetMembers().OfType<ParameterMemberData>();
            foreach (var fwd in parameterMembers)
            {
                if (fwd.ParameterizedMembers.Contains((source, parameterizedMember)))
                    return fwd;
            }
            return null;
        }
    }

    /// <summary>
    /// A member that represents a parameter. The parameter controls the value of a set of parameterized members.
    /// Parameterized members can be added/removed using IMemberData.Parameterize() and IMemberData.Unparameterize() 
    /// </summary>
    /// <remarks>
    /// The first member have special meaning since it decides which attributes the parameter will have.
    /// If the member is later removed from the parameter (unparameterized), the first additional member will take its place.
    /// </remarks>
    public class ParameterMemberData : IParameterMemberData
    {
        internal ParameterMemberData(object target, object source, IMemberData member, string name)
        {
            var names = name.Split('\\');
            Target = target;
            DeclaringType = TypeData.GetTypeData(target);
            this.source = source;
            this.member = member;
            Name = name;

            var disp = member.GetDisplayAttribute();

            var displayName = names[names.Length - 1].Trim();
            var displayGroup = names.Take(names.Length - 1).Select(x => x.Trim()).ToArray();

            displayAttribute = new DisplayAttribute(displayName, disp.Description, Order: -5, Groups: displayGroup);
        }

        readonly DisplayAttribute displayAttribute;
        object[] __attributes__;
        /// <summary> Gets the attributes on this member. </summary>
        public IEnumerable<object> Attributes
        {
            get
            {
                if (__attributes__ != null) return __attributes__;
                bool anyDisplayAttribute = false;
                var m = member.Attributes.Select(x =>
                {
                    if (x is DisplayAttribute)
                    {
                        anyDisplayAttribute = true;
                        return displayAttribute;
                    }

                    return x;
                }).ToArray();
                if (!anyDisplayAttribute)
                    m = member.Attributes.Append(displayAttribute).ToArray();
                __attributes__ = m;
                return m;
            }
        }

        /// <summary> The target object to which this member is added.
        /// This should always be the same as the argument to GetValue/SetValue. </summary>
        internal object Target { get; }
        object source;
        IMemberData member;
        List<(object Source, IMemberData Member)> additionalMembers;
        
        /// <summary>  Gets the value of this member. </summary>
        public object GetValue(object owner) =>  member.GetValue(source);

        /// <summary> Sets the value of this member on the owner. </summary>
        public void SetValue(object owner, object value)
        {
            // this gets a bit complicated now.
            // we have to ensure that the value is not just same object type, but not the same object
            // in some cases. Hence we need special cloning of the value.
            bool strConvertSuccess = false;
            string str = null;
            strConvertSuccess = StringConvertProvider.TryGetString(value, out str);

            ICloneable cloneable = value as ICloneable;

            TapSerializer serializer = null;
            string serialized = null;
            if (cloneable == null && !strConvertSuccess && value != null)
            {
                serializer = new TapSerializer();
                try
                {
                    serialized = serializer.SerializeToString(value);
                }
                catch
                {
                }
            }

            int count = 0;
            if (additionalMembers != null)
                count = additionalMembers.Count;

            for (int i = -1; i < count; i++)
            {
                var context = i == -1 ? source : additionalMembers[i].Source;
                var _member = i == -1 ? member : additionalMembers[i].Member;
                try
                {
                    object setVal = value;
                    if (i >= 0 || TypeData.GetTypeData(setVal).DescendsTo(TypeDescriptor) == false) // let's just set the value on the first property.
                    {
                        if (strConvertSuccess)
                        {
                            if (StringConvertProvider.TryFromString(str, TypeDescriptor, context, out setVal) == false)
                                setVal = value;
                        }
                        else if (cloneable != null)
                        {
                            setVal = cloneable.Clone();
                        }
                        else if (serialized != null)
                        {
                            try
                            {
                                setVal = serializer.DeserializeFromString(serialized);
                            }
                            catch
                            {
                            }
                        }
                    }

                    _member.SetValue(context, setVal); // This will throw an exception if it is not assignable.
                }
                catch

                {
                    object _value = value;
                    if (_value != null)
                        _member.SetValue(context, _value); // This will throw an exception if it is not assignable.
                }
            }
        }

        /// <summary>  The members and objects that make up the aggregation of this parameter. </summary>
        public IEnumerable<(object Source, IMemberData Member)> ParameterizedMembers
        {
            get
            {
                yield return (source, member);
                if (additionalMembers != null)
                    foreach (var item in additionalMembers)
                        yield return item;
            }
        }

        /// <summary> The target object type. </summary>
        public ITypeData DeclaringType { get; }
        
        /// <summary>  The declared type of this property. This is the type of the first member added to the parameter.
        /// Subsequent members does not need to have the same type, but they should be conversion compatible. e.g
        /// if the first member is an int, subsequent members can be other numeric types or string as well. </summary>
        public ITypeData TypeDescriptor => member.TypeDescriptor;
        
        /// <summary> If this member is writable. Usually true for parameters.</summary>
        public bool Writable => member.Writable;
        /// <summary> If this member is readable. Usually true for parameters. </summary>
        public bool Readable => member.Readable;
        /// <summary> The declared name of this parameter. This parameter can be referred to by this name. It may contain spaces etc. </summary>
        public string Name { get; }

        internal void AddAdditionalMember(object newSource, IMemberData newMember)
        {
            if (additionalMembers == null)
                additionalMembers = new List<(object, IMemberData)>();
            additionalMembers.Add((newSource, newMember));
        }

        /// <summary>
        /// Removes a forwarded member. If it was the original member, the first additional member will be used.
        /// If no additional members are present, then true will be returned, signalling that the forwarded member no longer exists.
        /// </summary>
        /// <param name="delMember">The forwarded member.</param>
        /// <param name="delSource">The object owning 'delMember'</param>
        /// <returns>True if the last member/source pair has been removed. If this happens the parameter should be removed
        /// from the target object.</returns>
        internal bool RemoveMember(IMemberData delMember, object delSource)
        {
            if (delSource == source && Equals(delMember, member))
            {
                if (additionalMembers == null || additionalMembers.Count == 0)
                {
                    source = null;
                    return true;
                }
                (source, member) = additionalMembers[0];
                additionalMembers.RemoveAt(0);
            }
            else
            {
                additionalMembers?.Remove((delSource, delMember));
            }

            return false;
        }
    }

    class DynamicMember : IMemberData
    {
        public virtual IEnumerable<object> Attributes { get; set; } = Array.Empty<object>();
        public string Name { get; set; }
        public ITypeData DeclaringType { get; set; }
        public ITypeData TypeDescriptor { get; set; }
        public bool Writable { get; set; }
        public bool Readable { get; set; }

        public object DefaultValue;

        readonly ConditionalWeakTable<object, object> dict = new ConditionalWeakTable<object, object>();

        public DynamicMember()
        {
            
        }
        /// <summary> This overload allows two DynamicMembers to share the same Get/Set value backing field.</summary>
        /// <param name="base"></param>
        public DynamicMember(DynamicMember @base)
        {
            dict = @base.dict;
        }
        
        public virtual void SetValue(object owner, object value)
        {
            dict.Remove(owner);
            if (Equals(value, DefaultValue) == false)
                dict.Add(owner, value);
        }

        public virtual object GetValue(object owner)
        {
            // TODO: use IDynamicMembersProvider
            if (dict.TryGetValue(owner, out object value))
                return value;
            return DefaultValue;
        }

        public static void AddDynamicMember(object target, IMemberData member)
        {
            var members =
                (IMemberData[]) DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.GetValue(target) ?? new IMemberData[0];
            
            
            Array.Resize(ref members, members.Length + 1);
            members[members.Length - 1] = member;
            DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.SetValue(target, members);
        }

        public static void RemovedDynamicMember(object target, IMemberData member)
        {
            var members =
                (IMemberData[]) DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.GetValue(target);
            members = members.Where(x => !Equals(x,member)).ToArray();
            DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.SetValue(target, members);
        }
        
        public static ParameterMemberData ParameterizeMember(object target, IMemberData member, object source, string name)
        {
            if(target == null) throw new ArgumentNullException(nameof(target));
            if(member == null) throw new ArgumentNullException(nameof(member));
            if(source == null) throw new ArgumentNullException(nameof(source));
            if (name == null) throw new ArgumentNullException(nameof(name));
            {
                var sourceType = TypeData.GetTypeData(source);
                if (!sourceType.GetMembers().Contains(member))
                    throw new ArgumentException("The does not belong to the source object type");
            }
            var targetType = TypeData.GetTypeData(target);
            var existingMember = targetType.GetMember(name);
            
            

            if (existingMember  == null)
            {
                var newMember = new ParameterMemberData(target, source, member, name);
                
                AddDynamicMember(target, newMember);
                return newMember;
            }
            if (existingMember is ParameterMemberData fw)
            {
                fw.AddAdditionalMember(source, member);
                return fw;
            }
            throw new Exception("A member by that name already exists.");
        }

        public static void UnparameterizeMember(ParameterMemberData parameterMember, IMemberData delMember, object delSource)
        {
            if (parameterMember == null) throw new ArgumentNullException(nameof(parameterMember));
            if (parameterMember == null)
                throw new Exception($"Member {parameterMember.Name} is not a forwarded member.");
            if (parameterMember.RemoveMember(delMember, delSource))
                RemovedDynamicMember(parameterMember.Target, parameterMember);
        }
    }

    internal class DynamicMemberTypeDataProvider : IStackedTypeDataProvider
    {
        class BreakConditionDynamicMember : DynamicMember
        {
            public BreakConditionDynamicMember(DynamicMember breakConditions) : base(breakConditions)
            {
                
            }

            public BreakConditionDynamicMember()
            {
                
            }

            public override void SetValue(object owner, object value)
            {
                if (owner is IBreakConditionProvider bc)
                {
                    bc.BreakCondition = (BreakCondition) value;
                    return;
                }

                base.SetValue(owner, value);
            }

            public override object GetValue(object owner)
            {
                if (owner is IBreakConditionProvider bc)
                    return bc.BreakCondition;
                return base.GetValue(owner);
            }
        }


        class DescriptionDynamicMember : DynamicMember
        {
            public override void SetValue(object owner, object value)
            {
                if (owner is IDescriptionProvider bc)
                {
                    bc.Description = (string) value;
                    return;
                }

                base.SetValue(owner, value);
            }

            public override object GetValue(object owner)
            {
                string result;
                if (owner is IDescriptionProvider bc)
                    result = bc.Description;
                else
                    result = (string) base.GetValue(owner);
                if (result == null)
                    result = TypeData.GetTypeData(owner).GetDisplayAttribute().Description;
                return result;
            }
        }

        class DynamicMembersMember : DynamicMember
        {
            public override void SetValue(object owner, object value)
            {
                if (owner is IDynamicMembersProvider bc)
                {
                    bc.DynamicMembers = (IMemberData[]) value;
                    return;
                }

                base.SetValue(owner, value);
            }

            public override object GetValue(object owner)
            {
                IMemberData[] result;
                if (owner is IDynamicMembersProvider bc)
                    result = bc.DynamicMembers;
                else
                    result = (IMemberData[]) base.GetValue(owner);

                return result;
            }
        }

        class DynamicTestStepTypeData : ITypeData
        {
            public DynamicTestStepTypeData(TestStepTypeData innerType, object target)
            {
                BaseType = innerType;
                Target = target;
            }

            readonly object Target;

            public IEnumerable<object> Attributes => BaseType.Attributes;
            public string Name => BaseType.Name;
            public ITypeData BaseType { get; }
            public IEnumerable<IMemberData> GetMembers()
            {
                var extra = (IMemberData[])TestStepTypeData.DynamicMembers.GetValue(Target);
                if (extra != null)
                    return BaseType.GetMembers().Concat(extra);
                return BaseType.GetMembers();
            }

            public IMemberData GetMember(string name)
            {
                var extra = (IMemberData[])TestStepTypeData.DynamicMembers.GetValue(Target);
                return extra.FirstOrDefault(x => x.Name == name) ?? BaseType.GetMember(name);
            }

            public object CreateInstance(object[] arguments) => BaseType.CreateInstance(arguments);

            public bool CanCreateInstance => BaseType.CanCreateInstance;
        }

        internal class TestStepTypeData : ITypeData
        {
            internal static readonly DynamicMember BreakConditions = new BreakConditionDynamicMember
            {
                Name = nameof(BreakConditions),
                DefaultValue = BreakCondition.Inherit,
                Attributes = new Attribute[]
                {
                    new DisplayAttribute("Break Conditions",
                        "When enabled, specify new break conditions. When disabled conditions are inherited from the parent test step, test plan, or engine settings.",
                        "Common", 20001.1),
                    new UnsweepableAttribute()
                },
                DeclaringType = TypeData.FromType(typeof(TestStepTypeData)),
                Readable = true,
                Writable = true,
                TypeDescriptor = TypeData.FromType(typeof(BreakCondition))
            };
            
            /// <summary>
            /// This is slightly different from normal BreakConditions as the Display attribute is different.
            /// </summary>
            internal static readonly DynamicMember TestPlanBreakConditions = new BreakConditionDynamicMember(BreakConditions)
            {
                Name = nameof(BreakConditions),
                DefaultValue = BreakCondition.Inherit,
                Attributes = new Attribute[]
                {
                    new DisplayAttribute("Break Conditions",
                        "When enabled, specify new break conditions. When disabled conditions are inherited from the engine settings.", Order: 1),
                    new UnsweepableAttribute()
                },
                DeclaringType = TypeData.FromType(typeof(TestStepTypeData)),
                Readable = true,
                Writable = true,
                TypeDescriptor = TypeData.FromType(typeof(BreakCondition))
            };


            internal static readonly DynamicMember DescriptionMember = new DescriptionDynamicMember
            {
                Name = "Description",
                DefaultValue = null,
                Attributes = new Attribute[]
                {
                    new DisplayAttribute("Description", "A short description of this test step.", "Common",
                        20001.2),
                    new LayoutAttribute(LayoutMode.Normal, 3, 5),
                    new UnsweepableAttribute()
                },
                DeclaringType = TypeData.FromType(typeof(TestStepTypeData)),
                Readable = true,
                Writable = true,
                TypeDescriptor = TypeData.FromType(typeof(string))
            };
            
            internal static readonly DynamicMember DynamicMembers = new DynamicMembersMember()
            {
                Name = "ForwardedMembers",
                DefaultValue = null,
                DeclaringType = TypeData.FromType((typeof(TestStepTypeData))),
                Attributes = new Attribute[]{new XmlIgnoreAttribute(), new AnnotationIgnoreAttribute()},
                Writable = true,
                Readable = true,
                TypeDescriptor = TypeData.FromType(typeof((Object,IMemberData)[]))
            };

            static IMemberData[] extraMembers = {BreakConditions, DynamicMembers}; //, DescriptionMember // Future: Include Description Member
            static IMemberData[] extraMembersTestPlan = {TestPlanBreakConditions, DynamicMembers}; //, DescriptionMember // Future: Include Description Member

            IMemberData[] members;
            public TestStepTypeData(ITypeData innerType)
            {
                this.innerType = innerType;
                if (innerType.DescendsTo(typeof(TestPlan)))
                    members = extraMembersTestPlan;
                else
                    members = extraMembers;
            }

            public override bool Equals(object obj)
            {
                if (obj is TestStepTypeData td2)
                    return td2.innerType.Equals(innerType);
                return base.Equals(obj);
            }

            public override int GetHashCode() => innerType.GetHashCode() * 157489213;

            readonly ITypeData innerType;
            public IEnumerable<object> Attributes => innerType.Attributes;
            public string Name => innerType.Name;
            public ITypeData BaseType => innerType;

            public IEnumerable<IMemberData> GetMembers()
            {
                return innerType.GetMembers().Concat(members);
            }

            public IMemberData GetMember(string name)
            {
                if (name == BreakConditions.Name) return BreakConditions;
                return innerType.GetMember(name);
            }

            public object CreateInstance(object[] arguments)
            {
                return innerType.CreateInstance(arguments);
            }

            public bool CanCreateInstance => innerType.CanCreateInstance;
        }

        // memorize for reference equality.
        static ConditionalWeakTable<ITypeData, TestStepTypeData> dict =
            new ConditionalWeakTable<ITypeData, TestStepTypeData>();
        static TestStepTypeData getStepTypeData(ITypeData subtype) =>
            dict.GetValue(subtype, x => new TestStepTypeData(x));

        public ITypeData GetTypeData(string identifier, TypeDataProviderStack stack)
        {
            var subtype = stack.GetTypeData(identifier);
            if (subtype.DescendsTo(typeof(ITestStep)))
            {
                var result = getStepTypeData(subtype);
                return result;
            }

            return subtype;
        }

        public ITypeData GetTypeData(object obj, TypeDataProviderStack stack)
        {
            if (obj is ITestStep || obj is TestPlan)
            {
                var subtype = stack.GetTypeData(obj);
                var result = getStepTypeData(subtype);
                if (TestStepTypeData.DynamicMembers.GetValue(obj) is IMemberData[])
                    return new DynamicTestStepTypeData(result, obj);
                return result;
            }
            return null;
        }
        public double Priority { get; } = 10;
    }
    
    /// <summary> An IMemberData that represents a parameter. The parameter controls the value of a set of parameterized members.</summary>
    public interface IParameterMemberData : IMemberData
    {
        /// <summary> The members controlled by this parameter. </summary>
        IEnumerable<(object Source, IMemberData Member)> ParameterizedMembers { get; }
    }
}