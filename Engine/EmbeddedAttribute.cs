using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    /// <summary> This attribute is used to dynamically embed the properties of an object into another object. </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class EmbeddedAttribute : Attribute
    {

    }


    // 
    // The code below implements the embedded member data dynamic reflection.
    // It creats a wrapper around existing types and gives them an extra layer
    // which contains the added properties.
    //

    class EmbeddedMemberData : IMemberData
    {
        IMemberData ownermember;
        IMemberData innermember;
        public ITypeData DeclaringType => ownermember.DeclaringType;
        public ITypeData TypeDescriptor => innermember.TypeDescriptor;
        public bool Writable => innermember.Writable;
        public bool Readable => innermember.Readable;
        public IEnumerable<object> Attributes => innermember.Attributes;
        public string Name => innermember.Name;
        public object GetValue(object owner) => innermember.GetValue(ownermember.GetValue(owner));
        public void SetValue(object owner, object value) => innermember.SetValue(ownermember.GetValue(owner), value);
        public EmbeddedMemberData(IMemberData ownermember, IMemberData member)
        {
            this.ownermember = ownermember;
            this.innermember = member;
        }
    }

    class EmbeddedTypeData : ITypeData
    {
        public ITypeData BaseType { get; set; }
        public bool CanCreateInstance => BaseType.CanCreateInstance;
        public IEnumerable<object> Attributes => BaseType.Attributes;
        public string Name => EmbeddedTypeDataProvider.exp +  BaseType.Name;
        public object CreateInstance(object[] arguments) => BaseType.CreateInstance(arguments);
        public IMemberData GetMember(string name) => BaseType.GetMember(name) ?? getEmbeddedMembers().FirstOrDefault(x => x.Name == name);
        public IEnumerable<IMemberData> GetMembers() => BaseType.GetMembers().Where(x => x.HasAttribute<EmbeddedAttribute>() == false).Concat(getEmbeddedMembers());
        public override int GetHashCode() => BaseType.GetHashCode() ^ typeof(EmbeddedTypeData).GetHashCode();
        public override bool Equals(object obj)
        {
            if(obj is EmbeddedTypeData e)
                return e.BaseType == BaseType;
            return false;
        }

        IMemberData[] _embeddedMembers = null;

        IMemberData[] getEmbeddedMembers()
        {
            if (_embeddedMembers == null)
            {
                List<IMemberData> embeddedMembers = new List<IMemberData>();
                foreach(var member in BaseType.GetMembers())
                {
                    if (member.HasAttribute<EmbeddedAttribute>())
                    {
                        foreach(var innermember in member.TypeDescriptor.GetMembers())
                        {
                            embeddedMembers.Add(new EmbeddedMemberData(member, innermember));
                        }
                    }
                }
                _embeddedMembers = embeddedMembers.ToArray();
            }
            return _embeddedMembers;
        }
    }

    class EmbeddedTypeDataProvider : ITypeDataProvider
    {
        public double Priority => 10;
        internal const string exp = "emb:";
        static ConcurrentDictionary<EmbeddedTypeData, EmbeddedTypeData> internedValues = new ConcurrentDictionary<EmbeddedTypeData, EmbeddedTypeData>();
        static EmbeddedTypeData Intern(EmbeddedTypeData e)
        {
            while (true)
            {
                if (internedValues.TryGetValue(e, out EmbeddedTypeData val))
                {
                    return val;
                }
                if (internedValues.TryAdd(e, e))
                    return e;
            }
            throw new InvalidOperationException("Unable to intern type data");
        }

        public ITypeData GetTypeData(string identifier)
        {
            if (identifier.StartsWith(exp))
            {
                var tp = TypeData.GetTypeData(identifier.Substring(exp.Length));
                if (tp != null)
                {
                    return Intern(new EmbeddedTypeData { BaseType = tp });
                }
            }
            return null;
        }

        public ITypeData GetTypeData(object obj)
        {
            var typedata = TypeInfoResolver.ResolveNext(obj);
            if(typedata.GetMembers().Any(x => x.HasAttribute<EmbeddedAttribute>()))
                return Intern(new EmbeddedTypeData { BaseType = typedata });
            return typedata;
        }
    }
}
