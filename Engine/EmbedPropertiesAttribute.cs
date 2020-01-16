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
    // It creates a wrapper around existing types and gives them an extra layer
    // which contains the added properties.
    //

    class EmbeddedMemberData : IMemberData
    {
        public IMemberData OwnerMember => ownerMember;
        public IMemberData InnerMember => innerMember;
        readonly IMemberData ownerMember;
        readonly IMemberData innerMember;
        public ITypeData DeclaringType => ownerMember.DeclaringType;
        public ITypeData TypeDescriptor => innerMember.TypeDescriptor;
        public bool Writable => innerMember.Writable;
        public bool Readable => innerMember.Readable;

        public IEnumerable<object> Attributes  => attributes ?? (attributes = loadAndTransformAttributes());
        public string Name { get; }

        public object GetValue(object owner)
        {
            var target = ownerMember.GetValue(owner);
            if (target == null) return null;
            return innerMember.GetValue(target);
        }

        public void SetValue(object owner, object value)
        {
            var target = ownerMember.GetValue(owner);
            if (target == null) throw new NullReferenceException($"Embedded property object owner ('{ownerMember.Name}') is null.");
            innerMember.SetValue(target, value);   
        }
        public EmbeddedMemberData(IMemberData ownerMember, IMemberData innerMember)
        {
            this.ownerMember = ownerMember;
            this.innerMember = innerMember;
            var embed = ownerMember.GetAttribute<EmbedPropertiesAttribute>();
            if (embed.PrefixPropertyName)
            {
                var prefix_name = embed.Prefix ?? ownerMember.Name;
                Name = prefix_name + "." + this.innerMember.Name;
            }
            else
                Name = this.innerMember.Name;
        }
        public override string ToString() => $"EmbMem:{Name}";
        
        object[] attributes;

        /// <summary>
        /// Loads and transform the list of attributes.
        /// Some attributes are sensitive to naming, the known ones are AvailableValuesAttribute,
        /// SuggestedValueAttribute, and EnabledIf. Others might exist, but they are, for now, not supported.
        /// When NoPrefix is set on EmbedProperties, then there is no issue, but with the prefix, those names also needs
        /// to be transformed.
        /// Additionally, there is some special behavior wanted for Display, for usability and to avoid name clashing:
        ///   - If a Display name/group is set on the owner property, that should turn into a group for the embedded properties.
        ///   - If there is no display name, use the prefix name for the group.
        ///   - If there is no prefix name, don't touch DisplayAttribute.
        /// </summary>
        object[] loadAndTransformAttributes()
        {

            string prefix_name = null;
            
            var list = innerMember.Attributes.ToList();
            
            var embed = ownerMember.GetAttribute<EmbedPropertiesAttribute>();
            if (embed.PrefixPropertyName)
                prefix_name = embed.Prefix ?? ownerMember.Name;
            
            string[] pre_group1;
            string pre_name;
            double ownerOrder = -10000;
            bool collapsed = false;
            {
                var owner_display = ownerMember.GetAttribute<DisplayAttribute>();

                if (owner_display == null)
                {
                    owner_display = new DisplayAttribute(embed.PrefixPropertyName ? ownerMember.Name : "");
                    pre_group1 = owner_display.Group;
                    pre_name = owner_display.Name;
                }
                else
                {
                    collapsed = owner_display.Collapsed;
                    ownerOrder = owner_display.Order;
                    pre_group1 = owner_display.Group;
                    pre_name = owner_display.Name;
                }
            }

            string name;
            string[] post_group;
            var d = list.OfType<DisplayAttribute>().FirstOrDefault();
            if (d != null)
            {
                list.Remove(d); // need to re-add a new DisplayAttribute.
                name = d.Name;
                post_group = d.Group;
                if (d.Collapsed)
                    collapsed = true;
            }
            else
            {
                name = Name;
                post_group = Array.Empty<string>();
            }

            var groups = pre_group1.Concat(pre_name == null ? Array.Empty<string>() : new[] {pre_name}).Concat(post_group)
                .ToArray();

            
            double order = -10000;
            {
                // calculate order:
                // if owner order is set, add it to the display order.
                if (d != null && d.Order != -10000.0)
                {
                    order = d.Order;
                    if (ownerOrder != -10000.0)
                    {
                        // if an order is specified, add the two orders together.
                        // this makes sure that property groups can be arranged.
                        order += ownerOrder;
                    }
                }
                else
                {
                    order = ownerOrder;
                }
            }
            d = new DisplayAttribute(name, Groups: groups,
                Description: d?.Description, Order: order, Collapsed: collapsed);

            list.Add(d);

            if (prefix_name != null)
            {
                // Transform properties that has issues with embedding. 
                for (var i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    if (item is AvailableValuesAttribute avail)
                    {
                        list[i] = new AvailableValuesAttribute(prefix_name + "." + avail.PropertyName);
                    }
                    else if (item is SuggestedValuesAttribute sug)
                    {
                        list[i] = new SuggestedValuesAttribute(prefix_name + "." + sug.PropertyName);
                    }
                    else if (item is EnabledIfAttribute enabled)
                    {
                        list[i] = new EnabledIfAttribute(prefix_name + "." + enabled.PropertyName, enabled.PropertyValues)
                            {HideIfDisabled = enabled.HideIfDisabled};
                    }
                }
            }

            return attributes = list.ToArray();
        }

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
                        embeddedMembers.Add(new EmbeddedMemberData(member, innermember));
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
            if (internedValues.TryGetValue(e, out EmbeddedTypeData val))
                return val;
            if (internedValues.TryAdd(e, e))
                return e;
            internedValues.TryGetValue(e, out val);
            return val; // fallback, not probable.
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
