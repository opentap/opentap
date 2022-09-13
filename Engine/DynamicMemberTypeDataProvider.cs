using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace OpenTap
{
    /// <summary>  This interface speeds up accessing dynamic members as it avoids having to access a global table to store the information. </summary>
    interface IDynamicMembersProvider
    {
        IDictionary<string, IMemberData> DynamicMembers { get; set; }
    }
    interface IDynamicMemberValue
    {
        bool TryGetValue(IMemberData member, out object value);
        void SetValue(IMemberData member, object value);
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
        internal static IParameterMemberData GetParameter(this IMemberData parameterizedMember, object target, object source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (parameterizedMember == null)
                throw new ArgumentNullException(nameof(parameterizedMember));

            var parameterMembers = TypeData.GetTypeData(target).GetMembers().OfType<IParameterMemberData>();
            foreach (var fwd in parameterMembers)
            {
                if (fwd.ContainsMember((source, parameterizedMember)))
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
    public class ParameterMemberData : IParameterMemberData, IDynamicMemberData
    {
        internal ParameterMemberData(object target, object source, IMemberData member, string name)
        {
            var names = name.Split('\\');
            Target = target;
            DeclaringType = TypeData.GetTypeData(target);
            this.source = source;
            this.member = member;
            if (member is IDynamicMemberData)
                dynamicMembers += 1;
            Name = name;

            var disp = member.GetDisplayAttribute();

            var displayName = names[names.Length - 1].Trim();
            var displayGroup = names.Take(names.Length - 1).Select(x => x.Trim()).ToArray();

            displayAttribute = new DisplayAttribute(displayName, disp.Description, Order: -5, Groups: displayGroup);
        }

        readonly DisplayAttribute displayAttribute;
        object[] attributes;
        /// <summary> Gets the attributes on this member. </summary>
        public IEnumerable<object> Attributes
        {
            get
            {
                if (attributes != null) return attributes;
                // copy all attributes from first member.
                // if there is no display attribute, create one.
                bool found = false;
                var attrs = member.Attributes.ToArray();
                for (int i = 0; i < attrs.Length; i++)
                {
                    if (false == (attrs[i] is DisplayAttribute))
                        continue;
                    attrs[i] = displayAttribute;
                    found = true;
                    break;
                }

                if(!found)
                    Sequence.Append(ref attrs, displayAttribute);
                return attributes = attrs;
            }
        }

        /// <summary> The target object to which this member is added.
        /// This should always be the same as the argument to GetValue/SetValue. </summary>
        internal object Target { get; }
        
        object source;
        IMemberData member;
        HashSet<(object Source, IMemberData Member)> additionalMembers;
        
        /// <summary>  Gets the value of this member. </summary>
        public object GetValue(object owner) =>  member.GetValue(source);

        /// <summary> Sets the value of this member on the owner. </summary>
        public void SetValue(object owner, object value)
        {
            // this gets a bit complicated now.
            // we have to ensure that the value is not just same object type, but not the same object
            // in some cases. Hence we need special cloning of the value.

            var cloner = new ObjectCloner(value);
            
            member.SetValue(source, cloner.Clone(true, source, member.TypeDescriptor));
            if (additionalMembers != null)
            {
                foreach (var (addContext, addMember) in additionalMembers)
                {
                    var cloned = cloner.Clone(false, addContext, addMember.TypeDescriptor);
                    if(cloned != null)
                        addMember.SetValue(addContext, cloned); // This will throw an exception if it is not assignable.
                }
            }
        }

        /// <summary>  The members and objects that make up the aggregation of this parameter. </summary>
        public IEnumerable<(object Source, IMemberData Member)> ParameterizedMembers
        {
            get
            {
                if (source == null) yield break;
                yield return (source, member);
                if (additionalMembers != null)
                    foreach (var item in additionalMembers)
                        yield return item;
            }
        }

        internal bool ContainsMember((object Source, IMemberData Member) memberKey) =>
            memberKey.Source == source && memberKey.Member == member || (additionalMembers?.Contains(memberKey) == true);

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
            if(source == newSource && newMember == member)
                throw new Exception("Member is already parameterized.");
            if (additionalMembers == null)
                additionalMembers = new HashSet<(object Source, IMemberData Member)>();
            if(!additionalMembers.Add((newSource, newMember)))
                throw new Exception("Member is already parameterized.");
            if (newMember is IDynamicMemberData)
                dynamicMembers += 1;
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
                if (delMember is IDynamicMemberData)
                    dynamicMembers -= 1;
                if (additionalMembers == null || additionalMembers.Count == 0)
                {
                    source = null;
                    DynamicMember.RemovedDynamicMember(Target, this);
                    return true;
                }
                (source, member) = additionalMembers.FirstOrDefault();
                additionalMembers.Remove((source, member));
            }
            else
            {
                if (additionalMembers?.Remove((delSource, delMember)) ?? false)
                {
                    if (delMember is IDynamicMemberData)
                        dynamicMembers -= 1;
                }
            }

            return false;
        }

        bool IDynamicMemberData.IsDisposed => source == null;

        int dynamicMembers = 0;
        
        // it can be useful to know if there are any dynamic members because it
        // can make the sanity checks a lot faster. 
        internal bool AnyDynamicMembers => dynamicMembers > 0;
    }

    class AcceleratedDynamicMember<TAccel> : DynamicMember
    {
        
        public Func<object, object> ValueGetter;
        public Action<object, object> ValueSetter;

        public override void SetValue(object owner, object value)
        {
            if (owner is TAccel)
                ValueSetter(owner, value);
            else
                base.SetValue(owner, value);
        }

        public override object GetValue(object owner)
        {
            if (owner is TAccel)
                return ValueGetter(owner);
            return base.GetValue(owner);
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
            if (owner is IDynamicMemberValue dmv)
            {
                dmv.SetValue(this, value);
                return;
            }
            dict.Remove(owner);
            if (Equals(value, DefaultValue) == false)
                dict.Add(owner, value);
        }

        public virtual object GetValue(object owner)
        {
            if (owner is IDynamicMemberValue dmv)
            {
                if (dmv.TryGetValue(this, out var value2))
                    return value2;
                return DefaultValue;
            }

            if (dict.TryGetValue(owner, out object value))
                return value ?? DefaultValue;
            return DefaultValue;
        }

        public static void AddDynamicMember(object target, IMemberData member)
        {
            var members =
                (Dictionary<string, IMemberData>) DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.GetValue(target);
            if (members == null)
            {
                members = new Dictionary<string, IMemberData>();
                DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.SetValue(target, members);
            }
            members[member.Name] = member;
        }

        public static void RemovedDynamicMember(object target, IMemberData member)
        {
            var members = (Dictionary<string, IMemberData>) DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.GetValue(target);
            members.Remove(member.Name);
            if (members.Count == 0) members = null;
            DynamicMemberTypeDataProvider.TestStepTypeData.DynamicMembers.SetValue(target, members);
        }

        /// <summary> the test plan stores a hashset of all current parameterizations, so this can be used
        /// to check if something is allready parameterized.</summary>
        static TestPlan GetPlanFor(object source)
        {
            while (source is ITestStepParent source2)
            {
                if (source is TestPlan plan) return plan;
                source = source2.Parent;
            }

            return null;
        }

        static void registerParameter(IMemberData member, object source, ParameterMemberData parameter)
        {
            if (source is IParameterizedMembersCache cache)
                cache.RegisterParameterizedMember(member, parameter);
            else
                GetPlanFor(source)?.RegisterParameter(member, source);
        }
        static void unregisterParameter(IMemberData member, object source, ParameterMemberData parameter)
        {
            if (source is IParameterizedMembersCache cache)
                cache.UnregisterParameterizedMember(member, parameter);
            else
                GetPlanFor(source)?.UnregisterParameter(member, source);
        }

        static bool isRegisteredParameter(IMemberData member, object source)
        {
            if (source is IParameterizedMembersCache cache)
                return cache.GetParameterFor(member) != null;
            return GetPlanFor(source)?.IsRegistered(member, source) ?? false;
        }
        
        
        public static ParameterMemberData ParameterizeMember(object target, IMemberData member, object source, string name)
        {
            if(target == null) throw new ArgumentNullException(nameof(target));
            if(member == null) throw new ArgumentNullException(nameof(member));
            if(source == null) throw new ArgumentNullException(nameof(source));
            if(name == null) throw new ArgumentNullException(nameof(name));
            if(name.Length == 0) throw new ArgumentException("Cannot be an empty string.", nameof(name));
            { // Verify that the member belongs to the type.   
                var sourceType = TypeData.GetTypeData(source);
                if (!sourceType.GetMembers().Contains(member))
                    throw new ArgumentException("The member does not belong to the source object type");
            }
            if (IsParameterized(member, source))
            {
                bool bad = true;
                if (source is IParameterizedMembersCache cache)
                {
                    // this is a rare case that can occur if a test step has been
                    // in two different test plans and the old parameters are lingering in the old plan.
                    // in this case try to fix the parameterized state by 
                    // checking for parameter sanity.
                    var param = cache.GetParameterFor(member);
                    ParameterManager.CheckParameterSanity(param, true);
                    bad = IsParameterized(member, source);
                }
                if(bad)
                    throw new Exception("the member is already parameterized");
            }
            
            if (member.HasAttribute<UnparameterizableAttribute>())
                throw new ArgumentException("Member cannot be parameterized", nameof(member));
            
            var targetType = TypeData.GetTypeData(target);
            using (ParameterManager.WithSanityCheckDelayed())
            {
                var existingMember = targetType.GetMember(name);

                if (existingMember == null)
                {
                    var newMember = new ParameterMemberData(target, source, member, name);

                    AddDynamicMember(target, newMember);
                    registerParameter(member, source, newMember);
                    return newMember;
                }

                if (existingMember is ParameterMemberData fw)
                {
                    fw.AddAdditionalMember(source, member);
                    registerParameter(member, source, fw);
                    return fw;
                }

                throw new Exception("A member by that name already exists.");
            }
        }

        public static void UnparameterizeMember(ParameterMemberData parameterMember, IMemberData member, object source)
        {
            if (parameterMember == null) throw new ArgumentNullException(nameof(parameterMember));
            if (parameterMember == null)
                throw new Exception($"Member {parameterMember.Name} is not a forwarded member.");
            parameterMember.RemoveMember(member, source);
            unregisterParameter(member, source , parameterMember);
        }

        /// <summary>
        /// Returns true if the member/object combination is parameterized. Note, this only work if they are child steps of a test plan.
        /// </summary>
        public static bool IsParameterized(IMemberData member, object obj) => isRegisteredParameter(member, obj);
    }

    class DynamicMemberTypeDataProvider : IStackedTypeDataProvider
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
                    bc.DynamicMembers = (IDictionary<string, IMemberData>) value;
                    return;
                }

                base.SetValue(owner, value);
            }

            public override object GetValue(object owner)
            {
                IDictionary<string, IMemberData> result;
                if (owner is IDynamicMembersProvider bc)
                    result = bc.DynamicMembers;
                else
                    result = (Dictionary<string, IMemberData>) base.GetValue(owner);

                return result;
            }
        }

        public class DynamicTestStepTypeData : ITypeData
        {
            public DynamicTestStepTypeData(TestStepTypeData innerType, object target)
            {
                BaseType = innerType;
                this.target = target;
            }

            readonly object target;

            public IEnumerable<object> Attributes => BaseType.Attributes;
            public string Name => BaseType.Name;
            public ITypeData BaseType { get; }

            IDictionary<string,IMemberData> getDynamicMembers()
            {
                var dynamicMembers = (IDictionary<string,IMemberData>)TestStepTypeData.DynamicMembers.GetValue(target);
                // dynamicMembers can be null after the last element is removed.
                if(dynamicMembers == null) return EmptyDictionary<string, IMemberData>.Instance;
                if (target is ITestStepParent step)
                {
                    // if it is a test step type, check that the parameters declared on a parent step
                    // actually comes from a child step.
                    if(!ParameterManager.CheckParameterSanity(step, dynamicMembers.Values))
                    {
                        // members modified, reload.
                        dynamicMembers = (IDictionary<string,IMemberData>) TestStepTypeData.DynamicMembers.GetValue(target);
                    }
                }

                return dynamicMembers ?? EmptyDictionary<string, IMemberData>.Instance;
            }
            
            public IEnumerable<IMemberData> GetMembers()
            {
                var dynamicMembers = getDynamicMembers();
                var members = BaseType.GetMembers();
                if (dynamicMembers.Count > 0)
                    members = members.Concat(dynamicMembers.Values);
                return members;
            }

            public IMemberData GetMember(string name)
            {
                var extra = getDynamicMembers();
                if(extra.TryGetValue(name, out var value))
                    return value;
                return BaseType.GetMember(name);
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
                    new UnsweepableAttribute(),
                    new NonMetaDataAttribute(),
                    new DefaultValueAttribute(BreakCondition.Inherit)
                },
                DeclaringType = TypeData.FromType(typeof(ITestStep)),
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
                        "When enabled, specify new break conditions. When disabled conditions are inherited from the engine settings.", Order: 3),
                    new UnsweepableAttribute(),
                    new EnabledIfAttribute("Locked", false),
                    new NonMetaDataAttribute(),
                    new DefaultValueAttribute(BreakCondition.Inherit)
                },
                DeclaringType = TypeData.FromType(typeof(TestPlan)),
                Readable = true,
                Writable = true,
                TypeDescriptor = TypeData.FromType(typeof(BreakCondition))
            };


            const string descriptionName = "OpenTap.Description";
            internal static DynamicMember DescriptionMember(ITypeData target)
            {
                // TestPlan Description should not be in the Common group.
                var displayAttribute = target.DescendsTo(typeof(TestPlan)) ?
                    new DisplayAttribute("Description", "A short description of this item.", null, 20001.2) :
                    new DisplayAttribute("Description", "A short description of this item.", "Common", 20001.2);

                var descr = new DescriptionDynamicMember
                {
                    Name = descriptionName,
                    DefaultValue = target.GetDisplayAttribute().Description,
                    Attributes = new Attribute[]
                    {
                        displayAttribute,
                        new LayoutAttribute(LayoutMode.Normal, 3, 3),
                        new UnsweepableAttribute(),
                        new UnparameterizableAttribute(),
                        new DefaultValueAttribute(target.GetDisplayAttribute().Description),
                        new NonMetaDataAttribute()
                    },
                    DeclaringType = TypeData.FromType(typeof(TestStepTypeData)),
                    Readable = true,
                    Writable = true,
                    TypeDescriptor = TypeData.FromType(typeof(string))
                };
                return descr;
            } 
            
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

            static readonly IMemberData[] extraTestStepMembers = {BreakConditions, DynamicMembers, ChildItemVisibility.VisibilityProperty};
            static readonly IMemberData[] extraTestPlanMembers = {TestPlanBreakConditions, DynamicMembers};
            
            readonly IMemberData[] members;

            static IMemberData[] GetMembersRaw(ITypeData innerType)
            {
                IMemberData[] members;
                if (innerType.DescendsTo(typeof(TestPlan)))
                    members = extraTestPlanMembers;
                else
                    members = extraTestStepMembers;
                var d = DescriptionMember(innerType);
                members = members.Append(d).ToArray();
                return members;
            }

            // memorize the arrays to avoid generating for each instance of test step.
            static readonly ConditionalWeakTable<ITypeData, IMemberData[]> memberMemorizer =
                new ConditionalWeakTable<ITypeData, IMemberData[]>();
            static IMemberData[] GetMembers(ITypeData innerType) =>  memberMemorizer.GetValue(innerType, GetMembersRaw);
            
            
            readonly IMemberData descriptionMember;
            public TestStepTypeData(ITypeData innerType)
            {
                this.innerType = innerType;
                members = GetMembers(innerType);
                descriptionMember = members.First(m => m.Name == descriptionName);
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
                if (name == DynamicMembers.Name) return DynamicMembers;
                if (name == descriptionMember.Name) return descriptionMember; 
                return innerType.GetMember(name);
            }

            public object CreateInstance(object[] arguments)
            {
                return innerType.CreateInstance(arguments);
            }

            public bool CanCreateInstance => innerType.CanCreateInstance;
        }

        // memorize for reference equality.
        static readonly ConditionalWeakTable<ITypeData, TestStepTypeData> dict =
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
            if (obj is ITestStepParent)
            {
                var subtype = stack.GetTypeData(obj);
                var result = getStepTypeData(subtype);
                if (TestStepTypeData.DynamicMembers.GetValue(obj) is Dictionary<string, IMemberData>)
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
