using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace OpenTap.Plugins.BasicSteps
{
    class ExpandedMemberData : IMemberData, IParameterMemberData, IDynamicMemberData
    {
        public override bool Equals(object obj)
        {
            if (obj is ExpandedMemberData mem)
            {
                return object.Equals(mem.DeclaringType, DeclaringType) && object.Equals(mem.Name, Name);
            }

            return false;
        }

        public override int GetHashCode() =>  DeclaringType.GetHashCode() * 12389321 + Name.GetHashCode() * 7310632;

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

        public  ParameterMemberData ExternalParameter
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
            static bool FilterAttributes(object attr)
            {
                switch (attr)
                {
                    case ColumnDisplayNameAttribute: return false;
                    case ExternalParameterAttribute: return false;
                    default: return true;
                }
            }
            attrs.RemoveIf<object>(static x => FilterAttributes(x) == false);


            Attributes = attrs;
        }

        public IEnumerable<(object, IMemberData)> ParameterizedMembers =>
            ExternalParameter.ParameterizedMembers
                .Select(x => new KeyValuePair<ITestStep, IEnumerable<IMemberData>>((ITestStep)x.Source, new []{x.Member}))
                .SelectMany(x => x.Value.Select(y => ((object)x.Key, y)));
        
        /// <summary> Set to true if the member has been removed from the test plan reference.</summary>
        public bool IsDisposed => ExternalParameter == null || ParameterizedMembers.Any() == false;
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
            if (Object == null) // this can occur during deserialization
                return InnerDescriptor.GetMembers();
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
}
