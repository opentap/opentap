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
        /// <summary> 
        /// Whether to include the name of the embedded member. Eg, if the name should be 'EmbeddedProperty.X' or just 'X'.
        /// Disabling this can cause name-clashing issues if multiple properties gets the same name. 
        /// </summary>
        public bool IncludeOwnName { get; set; } = true;

        /// <summary> Creates a custom owner name. Only applicable if IncludeOwnName is true. </summary>
        public string NameAs { get; set; } = null;
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
        public string Name { get; }
        public object GetValue(object owner) => innermember.GetValue(ownermember.GetValue(owner));
        public void SetValue(object owner, object value) => innermember.SetValue(ownermember.GetValue(owner), value);
        public EmbeddedMemberData(IMemberData ownermember, IMemberData member, bool includeOwnName, string ownName)
        {
            if (ownName == null)
                ownName = ownermember.Name;
            this.ownermember = ownermember;
            innermember = member;
            if (includeOwnName)
                Name = ownName + "." + innermember.Name; 
            else
                Name = innermember.Name;
        }
        public override string ToString() => $"EmbMem:{Name}";
    }

    class EmbeddedTypeData : ITypeData
    {
        public ITypeData BaseType { get; set; }
        public bool CanCreateInstance => BaseType.CanCreateInstance;
        public IEnumerable<object> Attributes => BaseType.Attributes;
        public string Name => EmbeddedTypeDataProvider.exp +  BaseType.Name;
        public object CreateInstance(object[] arguments) => BaseType.CreateInstance(arguments);
        public IMemberData GetMember(string name) => GetMembers().FirstOrDefault(x => x.Name == name);
        IMemberData[] allMembers;
        public IEnumerable<IMemberData> GetMembers() => allMembers ?? (allMembers = BaseType.GetMembers().Where(x => x.HasAttribute<EmbeddedAttribute>() == false).Concat(list_embedded_members()).ToArray());
        public override int GetHashCode() => BaseType.GetHashCode() ^ typeof(EmbeddedTypeData).GetHashCode();
        public override bool Equals(object obj)
        {
            if(obj is EmbeddedTypeData e)
                return e.BaseType == BaseType;
            return false;
        }

        [ThreadStatic]
        static HashSet<ITypeData> currentlyListing = null;

        IList<IMemberData> list_embedded_members()
        {
            if (currentlyListing == null)
                currentlyListing = new HashSet<ITypeData>();            
            List<IMemberData> embeddedMembers = new List<IMemberData>();
            if (currentlyListing.Contains(this))
                return embeddedMembers;
            currentlyListing.Add(this);

            foreach (var member in BaseType.GetMembers())
            {
                if (member.GetAttribute<EmbeddedAttribute>() is EmbeddedAttribute e)
                {
                    var members = member.TypeDescriptor.GetMembers();
                    foreach(var m in members)
                    {
                        if (m.HasAttribute<EmbeddedAttribute>())
                        {
                            members = EmbeddedTypeDataProvider.FromTypeData(member.TypeDescriptor).GetMembers();
                            break;
                        }
                    }

                    foreach (var innermember in members)
                        embeddedMembers.Add(new EmbeddedMemberData(member, innermember, e.IncludeOwnName, e.NameAs));
                }
            }
            currentlyListing.Remove(this);
            return embeddedMembers.ToArray();
        }
        public override string ToString() => $"EmbType:{Name}";
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
                    return val;
                if (internedValues.TryAdd(e, e))
                    return e;
            }
            throw new InvalidOperationException("Unable to intern type data");
        }

        public static EmbeddedTypeData FromTypeData(ITypeData basetype) => Intern(new EmbeddedTypeData { BaseType = basetype });

        public ITypeData GetTypeData(string identifier)
        {
            if (identifier.StartsWith(exp))
            {
                var tp = TypeData.GetTypeData(identifier.Substring(exp.Length));
                if (tp != null)
                    return FromTypeData(tp);
            }
            return null;
        }

        public ITypeData GetTypeData(object obj)
        {
            var typedata = TypeInfoResolver.ResolveNext(obj);
            if(typedata.GetMembers().Any(x => x.HasAttribute<EmbeddedAttribute>()))
                return FromTypeData(typedata);
            return typedata;
        }
    }
}
