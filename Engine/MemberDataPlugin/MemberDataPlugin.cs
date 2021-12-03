using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTap.MemberDataPlugin
{
    public class ProvidedMember
    {
        public IEnumerable<Attribute> Attributes = Array.Empty<Attribute>();
        private Guid id = Guid.NewGuid();
        public ProvidedMember(string memberName, ITypeData memberType, Func<object> constructor = null)
        {
            this.memberType = memberType;
            MemberName = memberName;
            if (constructor == null && !memberType.CanCreateInstance)
                throw new ArgumentException(
                    $"The type '{memberType}' cannot be instantiated, and no constructor was provided. Please provide a constructor.",
                    nameof(constructor));
            this.ctor = constructor ?? (() => this.memberType.CreateInstance());


        }

        public object CreateInstance()
        {
            return ctor();
        }

        public readonly ITypeData memberType;
        public readonly string MemberName;
        private readonly Func<object> ctor;
        public bool Writable { get; set; } = true;
        public bool Readable { get; set; } = true;

        protected virtual void OnAssigned(object assignedTo)
        {

        }

        public virtual void SetValue(object obj, object value)
        {
            var m = ProvidedMemberTypeData.FromObject(obj).GetMember(MemberName);
            m.SetValue(obj, value);
        }

        public virtual object GetValue(object obj)
        {
            var m = ProvidedMemberTypeData.FromObject(obj).GetMember(MemberName);
            return m.GetValue(obj);
        }

        private static ConcurrentDictionary<string, ProvidedMember> cache = new ConcurrentDictionary<string, ProvidedMember>();

        public ProvidedMember WithAttributes(params Attribute[] attributes)
        {
            this.Attributes = new List<Attribute>(this.Attributes.Concat(attributes));
            return this;
        }
        public static ProvidedMember Register<T>(Func<T> constructor = null, [CallerMemberName] string name = null)
        {
            var memberType = TypeData.FromType(typeof(T));
            Func<object> f = null;
            if (constructor != null) f = () => constructor();
            return new ProvidedMember(name, memberType, f);
        }
    }

    public interface IMemberProvider
    {
        ITypeData[] SupportedTypes { get; }
        ProvidedMember[] GetMembers(object owner);
    }

    class ProvidedMemberData : IMemberData
    {
        private readonly ProvidedMember prov;

        ProvidedMemberData(ProvidedMember p, ITypeData declaringType)
        {
            prov = p;
            DeclaringType = declaringType;
        }

        private static ConditionalWeakTable<ProvidedMember, ProvidedMemberData> cache =
            new ConditionalWeakTable<ProvidedMember, ProvidedMemberData>();

        public static ProvidedMemberData FromMember(ProvidedMember providedMember, ITypeData declaringType)
        {
            return cache.GetValue(providedMember, p => new ProvidedMemberData(p, declaringType));
        }

        public IEnumerable<object> Attributes => prov.Attributes;
        public string Name => prov.MemberName;
        public ITypeData DeclaringType { get; }
        public ITypeData TypeDescriptor => prov.memberType;
        public bool Writable => prov.Writable;
        public bool Readable => prov.Readable;

        private ConditionalWeakTable<object, object> valueTable = new ConditionalWeakTable<object, object>();
        public void SetValue(object owner, object value)
        {
            valueTable.Remove(owner);
            valueTable.Add(owner, value);
        }
        public object GetValue(object owner)
        {
            return valueTable.GetValue(owner, o => prov.CreateInstance());
        }
    }

    class ProvidedMemberTypeData : ITypeData
    {
        private static ConditionalWeakTable<object, ProvidedMemberTypeData> cache =
            new ConditionalWeakTable<object, ProvidedMemberTypeData>();

        public static ProvidedMemberTypeData FromObject(object owner, ITypeData innerType = null)
        {
            if (innerType == null)
            {
                innerType = TypeData.GetTypeData(owner);
            }

            return cache.GetValue(owner, t => new ProvidedMemberTypeData(innerType, owner));
        }

        ProvidedMemberTypeData(ITypeData innerType, object owner)
        {
            this.innerType = innerType;
            this.owner = owner;
        }

        private ITypeData innerType;
        private readonly object owner;
        public IEnumerable<object> Attributes => innerType.Attributes;

        public string Name => innerType.Name;

        public ITypeData BaseType => innerType.BaseType;

        private static TraceSource log = Log.CreateSource(nameof(ProvidedMemberTypeData));

        private List<IMemberData> getMembersFromProviders()
        {
            var providers = TypeData.GetDerivedTypes<IMemberProvider>()
                .Where(p => p.CanCreateInstance)
                .TrySelect(p => p.CreateInstance<IMemberProvider>(), (e, p) =>
                {
                    log.Warning($"Error instantiating '{nameof(IMemberProvider)}' '{p.Name}'.");
                    log.Debug(e);
                })
                .Where(p => p != null)
                .Where(p => p.SupportedTypes.Any(t => t.DescendsTo(innerType)))
                .ToArray();

            var lst = new List<IMemberData>();
            var members = providers.Select(p => p.GetMembers(owner));
            foreach (var memberArr in members)
            {
                foreach (var member in memberArr)
                {
                    var p = ProvidedMemberData.FromMember(member, this);
                    lst.Add(p);
                }

            }

            return lst;
        }

        public IEnumerable<IMemberData> GetMembers()
        {
            var result = new List<IMemberData>();
            result.AddRange(getMembersFromProviders());
            result.AddRange(innerType.GetMembers());
            return result;
        }

        public IMemberData GetMember(string name)
        {
            return GetMembers().FirstOrDefault(m => m.Name == name);
        }

        public object CreateInstance(object[] arguments)
        {
            return innerType.CreateInstance(arguments);
        }

        public bool CanCreateInstance => innerType.CanCreateInstance;
    }

    class ProvidedMemberTypeDataStack : IStackedTypeDataProvider
    {
        public ITypeData GetTypeData(string identifier, TypeDataProviderStack stack)
        {
            return stack.GetTypeData(identifier);
        }

        public ITypeData GetTypeData(object obj, TypeDataProviderStack stack)
        {
            return ProvidedMemberTypeData.FromObject(obj, stack.GetTypeData(obj));
        }

        public double Priority { get; } = 0;
    }
}