using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

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

            string[] prefixGroups;
            if (embed.PrefixPropertyName == false && string.IsNullOrWhiteSpace(pre_name))
                prefixGroups = Array.Empty<string>();
            else
                prefixGroups = new[] {pre_name};

            var groups = pre_group1.Concat(prefixGroups).Concat(post_group).ToArray();

            
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
                        list[i] = new EnabledIfAttribute(prefix_name + "." + enabled.PropertyName, enabled.Values)
                            {HideIfDisabled = enabled.HideIfDisabled};
                    }
                }
            }

            return attributes = list.ToArray();
        }

    }

    class EmbeddedTypeData : ITypeData
    {
        IMemberData[] listedEmbeddedMembers;
        
        public ITypeData BaseType { get; set; }
        public bool CanCreateInstance => BaseType.CanCreateInstance;
        public IEnumerable<object> Attributes => BaseType.Attributes;
        public string Name => EmbeddedTypeDataProvider.exp +  BaseType.Name;
        public object CreateInstance(object[] arguments) => BaseType.CreateInstance(arguments);
        public IMemberData GetMember(string name) => GetMembers().FirstOrDefault(x => x.Name == name);
        
        public IEnumerable<IMemberData> GetMembers() => BaseType.GetMembers()
            .Where(x => x.HasAttribute<EmbedPropertiesAttribute>() == false)
            .Concat(listedEmbeddedMembers ??= ListEmbeddedMembers());
        public override int GetHashCode() => BaseType.GetHashCode() * 86966513 + typeof(EmbeddedTypeData).GetHashCode();
        public override bool Equals(object obj)
        {
            if(obj is EmbeddedTypeData e)
                return e.BaseType == BaseType;
            return false;
        }

        [ThreadStatic]
        static HashSet<ITypeData> currentlyListing;

        public IEnumerable<IMemberData> GetEmbeddingMembers()
        {
            foreach (var member in BaseType.GetMembers())
            {
                if (member.HasAttribute<EmbedPropertiesAttribute>())
                {
                    yield return member;
                }
            }
        }
        
        internal IMemberData[] ListEmbeddedMembers()
        {
            if (currentlyListing == null)
                currentlyListing = new HashSet<ITypeData>();            
            List<IMemberData> embeddedMembers = new List<IMemberData>();
            if (currentlyListing.Contains(this))
                return embeddedMembers.ToArray();
            currentlyListing.Add(this);

            foreach (var member in BaseType.GetMembers())
            {
                if (member.GetAttribute<EmbedPropertiesAttribute>() is EmbedPropertiesAttribute)
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
        public double Priority => 10.5;
        internal const string exp = "emb:";
        static ConditionalWeakTable<ITypeData, EmbeddedTypeData> internedValues = new ConditionalWeakTable<ITypeData, EmbeddedTypeData>();
        static EmbeddedTypeData getOrCreate(ITypeData e) => internedValues.GetValue(e, t => t.GetMembers().Any(m => m.HasAttribute<EmbedPropertiesAttribute>()) ? new EmbeddedTypeData{BaseType =  e} : null);

        public static EmbeddedTypeData FromTypeData(ITypeData baseType) => getOrCreate(baseType);

        public ITypeData GetTypeData(string identifier, TypeDataProviderStack stack)
        {
            if (identifier.StartsWith(exp))
            {
                var tp = TypeData.GetTypeData(identifier.Substring(exp.Length));
                if (tp != null)
                {
                    var embTp = FromTypeData(tp);
                    if (embTp != null)
                        return embTp;
                    return tp;
                }
            }
            return null;
        }

        class KeyBox
        {
            public int Key { get; set; } 
        }  
        // this key table is used to keep track of the members of dynamic types.
        // if the key has changed, then it means that the cache should be invalidated.
        static ConditionalWeakTable<object, KeyBox> keyTable = new ConditionalWeakTable<object, KeyBox>();   

        public ITypeData GetTypeData(object obj, TypeDataProviderStack stack)
        {
            var typeData = stack.GetTypeData(obj);
            var nowKey = DynamicMember.GetTypeDataKey(obj);
            bool reload = false;
            if (nowKey != 0)
            {
                if (keyTable.TryGetValue(obj, out var currentKey))
                {
                    if (currentKey.Key != nowKey)
                    {
                        reload = true;
                        currentKey.Key = nowKey;
                    }
                }
                else
                {
                    keyTable.Add(obj, new KeyBox(){Key = nowKey});
                    reload = true;
                }
            }
            if (reload)
            {
                internedValues.Remove(typeData);
            }
            if (internedValues.TryGetValue(typeData, out EmbeddedTypeData val))
            {
                return val ?? typeData;
            }
            
            if (typeData.GetMembers().Any(x => x.HasAttribute<EmbedPropertiesAttribute>()))
                return FromTypeData(typeData);
            // assign null to the value.
            return internedValues.GetValue(typeData, t => null) ?? typeData;
        }
    }
}
