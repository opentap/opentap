using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    /// <summary> 
    /// This attribute is used to dynamically embed the properties of an object into another object.
    /// </summary>
    /// <remarks> 
    /// A property of type PT declared on a type DT decorated with this attribute will not be visible in reflection information (ITypeData) for DT.
    /// Instead all properties declared on PT will be visible on DT as though they had been declared there.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public class EmbedPropertiesAttribute : Attribute
    {
        /// <summary> 
        /// When true, property name of the owning property is used as prefix for embedded properties. E.g., the name will be 'EmbeddedProperty.X'.
        /// A prefix can help prevent name-clashing issues if multiple properties gets the same name. 
        /// </summary>
        public bool PrefixPropertyName { get; set; } = true;

        /// <summary> 
        /// Custom prefix for embedded properties. This will overwrite PrefixPropertyName. 
        /// A prefix can help prevent name-clashing issues if multiple properties gets the same name. 
        /// </summary>
        public string Prefix { get; set; } = null;
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
        public EmbeddedMemberData(IMemberData ownermember, IMemberData member, bool prefixPropertyName, string prefix)
        {
            this.ownermember = ownermember;
            innermember = member;
            if (prefix != null)
                Name = prefix + "." + innermember.Name;
            else if (prefixPropertyName)
                Name = ownermember.Name + "." + innermember.Name; 
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
        public IEnumerable<IMemberData> GetMembers() => allMembers ?? (allMembers = BaseType.GetMembers().Where(x => x.HasAttribute<EmbedPropertiesAttribute>() == false).Concat(list_embedded_members()).ToArray());
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
                if (member.GetAttribute<EmbedPropertiesAttribute>() is EmbedPropertiesAttribute e)
                {
                    var members = member.TypeDescriptor.GetMembers();
                    foreach(var m in members)
                    {
                        if (m.HasAttribute<EmbedPropertiesAttribute>())
                        {
                            members = EmbeddedTypeDataProvider.FromTypeData(member.TypeDescriptor).GetMembers();
                            break;
                        }
                    }

                    foreach (var innermember in members)
                        embeddedMembers.Add(new EmbeddedMemberData(member, innermember, e.PrefixPropertyName, e.Prefix));
                }
            }
            currentlyListing.Remove(this);
            return embeddedMembers.ToArray();
        }
        public override string ToString() => $"EmbType:{Name}";
    }

    class EmbeddedTypeDataProvider : IStackedTypeDataProvider
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

        public ITypeData GetTypeData(string identifier, TypeDataProviderStack stack)
        {
            if (identifier.StartsWith(exp))
            {
                var tp = TypeData.GetTypeData(identifier.Substring(exp.Length));
                if (tp != null)
                    return FromTypeData(tp);
            }
            return null;
        }

        public ITypeData GetTypeData(object obj, TypeDataProviderStack stack)
        {
            var typedata = stack.GetTypeData(obj);
            if(typedata.GetMembers().Any(x => x.HasAttribute<EmbedPropertiesAttribute>()))
                return FromTypeData(typedata);
            return typedata;
        }
    }
}
