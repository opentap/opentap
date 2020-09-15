using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace OpenTap.Plugins.BasicSteps
{
    class ExpandedMemberData : IMemberData, IParameterMemberData
    {
        public override bool Equals(object obj)
        {
            if (obj is ExpandedMemberData mem)
            {
                return object.Equals(mem.DeclaringType, DeclaringType) && object.Equals(mem.Name, Name);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return DeclaringType.GetHashCode() ^ Name.GetHashCode();
        }

        public ITypeData DeclaringType { get; set; }

        public IEnumerable<object> Attributes { get; private set; }

        public string Name { get; set; }

        public bool Writable => true;

        public bool Readable => true;

        public ITypeData TypeDescriptor { get; set; }

        public object GetValue(object owner)
        {
            var tpr = owner as TestPlanReference;
            var ep = tpr.ExternalParameters.FirstOrDefault(x => x.Name == epName);

            var Member = ep.ParameterizedMembers.First().Member;
            TypeDescriptor = Member.TypeDescriptor;
            return ep.GetValue(tpr.plan);
        }

        public ParameterMemberData ExternalParameter
        {
            get
            {
                var tpr = (this.DeclaringType as ExpandedTypeData).Object;
                var ep = tpr.ExternalParameters.FirstOrDefault(x => x.Name == epName);
                return ep;
            }
        }

        string epName;

        public void SetValue(object owner, object value)
        {
            var tpr = owner as TestPlanReference;
            var ep = tpr.ExternalParameters.FirstOrDefault(x => x.Name == epName);
            var Member = ep.ParameterizedMembers.First().Member;
            TypeDescriptor = Member.TypeDescriptor;
            ep.SetValue(tpr.plan, value);
        }

        public ExpandedMemberData(ParameterMemberData ep, string name)
        {
            Name = name;
            var Member = ep.ParameterizedMembers.First().Member;
            
            epName = ep.Name;
            TypeDescriptor = Member.TypeDescriptor;
            var attrs = Member.Attributes.ToList();
            attrs.RemoveIf<object>(x => x is DisplayAttribute);
            
            var dis = ep.Attributes.OfType<DisplayAttribute>().FirstOrDefault();
            if (dis == null)
                dis = Member.GetDisplayAttribute();
            
            attrs.Add(new DisplayAttribute(dis.Name, Description: dis.Description, Order: 5, Groups:dis.Group));
            if (attrs.Any(x => x is ColumnDisplayNameAttribute))
            {
                var colAttr = (ColumnDisplayNameAttribute) attrs.FirstOrDefault(x => x is ColumnDisplayNameAttribute);
                attrs.Remove(colAttr);

                var newColAttr = new ColumnDisplayNameAttribute(ep.Name, colAttr.Order, colAttr.IsReadOnly);
                attrs.Add(newColAttr);
            }

            Attributes = attrs;
        }

        public IEnumerable<(object, IMemberData)> ParameterizedMembers =>
            ExternalParameter.ParameterizedMembers
                .Select(x => new KeyValuePair<ITestStep, IEnumerable<IMemberData>>((ITestStep)x.Source, new []{x.Member}))
                .SelectMany(x => x.Value.Select(y => ((object)x.Key, y)));
    }

    class ExpandedTypeData : ITypeData
    {
        private static readonly Regex propRegex = new Regex(@"^prop(?<index>[0-9]+)$", RegexOptions.Compiled);

        public override bool Equals(object obj)
        {
            if (obj is ExpandedTypeData exp)
                return exp.Object == Object;
            return false;
        }

        public override int GetHashCode() => Object.GetHashCode() ^ 0x1111234;

        public ITypeData InnerDescriptor;
        public TestPlanReference Object;

        public string Name => ExpandMemberDataProvider.exp + InnerDescriptor.Name;

        public IEnumerable<object> Attributes => InnerDescriptor.Attributes;

        public ITypeData BaseType => InnerDescriptor;

        public bool CanCreateInstance => InnerDescriptor.CanCreateInstance;

        public object CreateInstance(object[] arguments)
        {
            return InnerDescriptor.CreateInstance(arguments);
        }

        private IMemberData ResolveLegacyName(string memberName)
        {
            ExpandedMemberData result = null; // return null if no valid expanded member data gets set

            // The following code is only for legacy purposes where properties which were not valid would get a valid 
            // name like: prop0, prop1, prop73, where the number after the prefix prop would be the actual index in the
            // ForwardedParameters array.
            Match m = propRegex.Match(memberName);
            if (m.Success)
            {
                int index = 0;
                try
                {
                    index = int.Parse(m.Groups["index"].Value);
                    if (index >= 0 && index < Object.ExternalParameters.Length)
                    {
                        var ep = Object.ExternalParameters[index];
                        // return valid expanded member data
                        result = new ExpandedMemberData(ep, ep.Name) {DeclaringType = this};
                    }
                }
                catch
                {
                }
            }

            return result;
        }

        public IMemberData GetMember(string memberName)
        {
            var mem = GetMembers().FirstOrDefault(x => x.Name == memberName);
            return mem ?? ResolveLegacyName(memberName);
        }

        string names = "";
        IMemberData[] savedMembers = null;

        public IEnumerable<IMemberData> GetMembers()
        {
            var names2 = string.Join(",", Object.ExternalParameters.Select(x => x.Name));
            if (names == names2 && savedMembers != null) return savedMembers;
            List<IMemberData> members = new List<IMemberData>();

            for (int i = 0; i < Object.ExternalParameters.Length; i++)
            {
                var ep = Object.ExternalParameters[i];
                members.Add(new ExpandedMemberData(ep, ep.Name) {DeclaringType = this});
            }

            var innerMembers = InnerDescriptor.GetMembers();
            foreach (var mem in innerMembers)
                members.Add(mem);
            savedMembers = members.ToArray();
            names = names2;
            return members;
        }
    }


    public class ExpandMemberDataProvider : ITypeDataProvider
    {
        public double Priority => 1;
        internal const string exp = "ref@";

        public ITypeData GetTypeData(string identifier)
        {
            if (identifier.StartsWith(exp))
            {
                var tp = TypeData.GetTypeData(identifier.Substring(exp.Length));
                if (tp != null)
                {
                    return new ExpandedTypeData() {InnerDescriptor = tp, Object = null};
                }
            }

            return null;
        }

        static ConditionalWeakTable<TestPlanReference, ExpandedTypeData> types =
            new ConditionalWeakTable<TestPlanReference, ExpandedTypeData>();

        ExpandedTypeData getExpandedTypeData(TestPlanReference step)
        {
            var expDesc = new ExpandedTypeData();
            expDesc.InnerDescriptor = TypeData.FromType(typeof(TestPlanReference));
            expDesc.Object = step;
            return expDesc;
        }

        
        static ConditionalWeakTable<SweepParameterStep, SweepRowTypeData> sweepRowTypes =
            new ConditionalWeakTable<SweepParameterStep, SweepRowTypeData>();
        
        public ITypeData GetTypeData(object obj)
        {
            if (obj is TestPlanReference exp)
                return types.GetValue(exp, getExpandedTypeData);
            
            if (obj is SweepRow row && row.Loop != null)
                return sweepRowTypes.GetValue(row.Loop, e => new SweepRowTypeData(e));
            
            return null;
        }
    }

    class SweepRowMemberData : IMemberData, IParameterMemberData
    {
        SweepRowTypeData declaringType;
        IMemberData innerMember;
        public SweepRowMemberData(SweepRowTypeData declaringType, IMemberData innerMember)
        {
            this.declaringType = declaringType;
            this.innerMember = innerMember;
            ParameterizedMembers = new (object, IMemberData)[]{(declaringType.SweepParameterLoop, innerMember)};
        }

        public IEnumerable<object> Attributes => innerMember.Attributes;
        public string Name => innerMember.Name;
        public ITypeData DeclaringType => declaringType;
        public ITypeData TypeDescriptor => innerMember.TypeDescriptor;
        public bool Writable => innerMember.Writable;
        public bool Readable => innerMember.Readable;
        public void SetValue(object owner, object value)
        {
            var own = (SweepRow)owner;
            own.Values[Name] = cloneIfPossible(value, own.Loop);
        }

        public object GetValue(object owner)
        {
            var own = (SweepRow)owner;
            if(own.Values.TryGetValue(Name, out var value))
                return value;
            var newv = cloneIfPossible(this.innerMember.GetValue(owner), own.Loop);
            own.Values[Name] = newv;
            return newv;
        }
        
        TapSerializer tapSerializer;
        object cloneIfPossible(object value, object context)
        {
            var valType = TypeData.GetTypeData(value);
            var td = valType.AsTypeData();
            if (td.Type.IsValueType)
                return value;
            
            if (StringConvertProvider.TryGetString(value, out string result))
            {
                if (StringConvertProvider.TryFromString(result, valType, context, out object result2))
                    return result2;
            }
            
            if(tapSerializer == null) tapSerializer = new TapSerializer();
            try
            {
                return tapSerializer.DeserializeFromString(tapSerializer.SerializeToString(value), valType) ?? value;
            }
            catch
            {
                return value;
            }
        }

        public IEnumerable<(object Source, IMemberData Member)> ParameterizedMembers { get; }
    }
    
    class SweepRowTypeData : ITypeData
    {
        public IEnumerable<object> Attributes => BaseType.Attributes;
        
        // Sweep row type data cannot be deserialized in a normal sense anyway. Needs sweep step reference
        public string Name => BaseType.Name; 
        public ITypeData BaseType { get; } = TypeData.FromType(typeof(SweepRow));
        public IEnumerable<IMemberData> GetMembers() => BaseType.GetMembers().Concat(GetSweepMembers());

        IEnumerable<IMemberData> GetSweepMembers()
        {
            var selected = SweepParameterLoop.SelectedParameters;
            var loopMembers = TypeData.GetTypeData(SweepParameterLoop).GetMembers()
                .Where(x => selected.Contains(x))
                .OfType<IParameterMemberData>();
            return loopMembers.Select(x => new SweepRowMemberData(this, x));
        } 

        public IMemberData GetMember(string name)
        {
            return BaseType.GetMember(name) ?? GetSweepMembers().FirstOrDefault(x => x.Name == name);
        }

        public SweepParameterStep SweepParameterLoop;

        public SweepRowTypeData(SweepParameterStep sweepParameterLoop)
        {
            this.SweepParameterLoop = sweepParameterLoop;
        }
        
        public object CreateInstance(object[] arguments)
        {
            return new SweepRow {Loop = SweepParameterLoop};
        }

        public bool CanCreateInstance => true;

        public override bool Equals(object obj)
        {
            if (obj is SweepRowTypeData otherSweepRow && otherSweepRow.SweepParameterLoop == SweepParameterLoop)
                return true;
            return false;
        }

        public override int GetHashCode()
        {
            return SweepParameterLoop.GetHashCode() * 37012721 + 1649210;
        }
    };
}