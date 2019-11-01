//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    /// <summary>
    /// Base info for reflection objects.
    /// </summary>
    public interface IReflectionData
    {
        /// <summary> The attributes of it. </summary>
        IEnumerable<object> Attributes { get; }
        /// <summary>
        /// The name of it.
        /// </summary>
        string Name { get; }
    }

    /// <summary> A member of an object type. </summary>
    public interface IMemberData : IReflectionData
    {
        /// <summary> The declaring type of this member. </summary>
        ITypeData DeclaringType { get; }
        /// <summary> The underlying type of this member. </summary>
        ITypeData TypeDescriptor { get; }
        /// <summary> Gets if this member is writable. </summary>
        bool Writable { get; }
        /// <summary> Gets if this member is readable.</summary>
        bool Readable { get; }
        /// <summary> Sets the value of this member on the owner. </summary>
        /// <param name="owner"></param>
        /// <param name="value"></param>
        void SetValue(object owner, object value);
        /// <summary>
        /// Gets the value of this member on the owner.
        /// </summary>
        /// <param name="owner"></param>
        /// <returns></returns>
        object GetValue(object owner);
    }

    /// <summary> The type information of an object. </summary>
    public interface ITypeData : IReflectionData
    {
        /// <summary> The base type of this type. </summary>
        ITypeData BaseType { get; }
        /// <summary> Gets the members of this object. </summary>
        /// <returns></returns>
        IEnumerable<IMemberData> GetMembers();
        /// <summary> Gets a member of this object by name.  </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        IMemberData GetMember(string name);
        /// <summary>
        /// Creates an instance of this type. The arguments are used for construction.
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        object CreateInstance(object[] arguments);
        /// <summary>
        /// Gets if CreateInstance will work for this type. For examples, for interfaces and abstract classes it will not work.
        /// </summary>
        bool CanCreateInstance { get; }
    }

    /// <summary> Type info provider. Provides type info for a given object. </summary>
    [Display("TypeInfo Provider")]
    public interface ITypeDataProvider : ITapPlugin
    {
        /// <summary> Gets the type info from an identifier. </summary>
        ITypeData GetTypeData(string identifier);

        /// <summary> Gets the type info from an object. </summary>
        ITypeData GetTypeData(object obj);

        /// <summary> The priority of this type info provider. Note, this decides the order in which tyhe type info is resolved. </summary>
        double Priority { get; }
    }

    /// <summary> Utility class for resolving dynamic type information. </summary>
    public class TypeInfoResolver
    {
        [ThreadStatic]
        static TypeInfoResolver current;

        object theobj;
        List<ITypeDataProvider> providers = GetProviders();
        /// <summary> Used by ITypeDataProviders to continue iteration locally. </summary>
        public static ITypeData ResolveNext(object obj)
        {
            if (current == null || false == object.ReferenceEquals(current.theobj, obj)) throw new InvalidOperationException("TypeInfoResolver.ResolveNext can only be called during TypeResolution.");
            return current.Iterate(obj);
        }

        /// <summary> Used by ITypeDataProviders to continue iteration locally. </summary>
        public static ITypeData ResolveNext(string typename)
        {
            if (current == null || false == object.ReferenceEquals(current.theobj, typename)) throw new InvalidOperationException("TypeInfoResolver.ResolveNext can only be called during TypeResolution.");
            return current.Iterate(typename);
        }
        
        internal TypeInfoResolver(object value) => this.theobj = value;

        int offset = 0;
        
        /// <summary> Resolve the type of an object. </summary>
        internal ITypeData Iterate(object obj)
        {
            TypeInfoResolver prev = current;
            current = this;
            try
            {
                while (offset < providers.Count)
                {
                    var provider = providers[offset];
                    offset++;
                    if (provider.GetTypeData(obj) is ITypeData found)
                        return found;
                }
                return null;
            }
            finally
            {
                current = prev;
            }
        }

        /// <summary> Resolve a type based on string input. </summary>
        internal ITypeData Iterate(string typename)
        {
            TypeInfoResolver prev = current;
            current = this;
            try
            {
                while (offset < providers.Count)
                {
                    var provider = providers[offset];
                    offset++;
                    if (provider.GetTypeData(typename) is ITypeData found)
                        return found;
                }
                return null;
            }
            finally
            {
                current = prev;
            }
        }

        static List<ITypeDataProvider> sproviders = new List<ITypeDataProvider>();
        static List<ITypeDataProvider> GetProviders()
        {
            var _sproviders = TypeData.FromType(typeof(ITypeDataProvider)).DerivedTypes;
            if (sproviders.Count == _sproviders.Count()) return sproviders;
            sproviders = _sproviders.Select(x => x.NewInstanceSafe()).OfType<ITypeDataProvider>().ToList();
            sproviders.Sort((x, y) => y.Priority.CompareTo(x.Priority));
            return sproviders;
        }
    }

    /// <summary> Helpers for work with ITypeInfo objects. </summary>
    public static class ReflectionDataExtensions
    {
        /// <summary> returns true if 'type' is a descendant of 'basetype'. </summary>
        /// <param name="type"></param>
        /// <param name="basetype"></param>
        /// <returns></returns>
        public static bool DescendsTo(this ITypeData type, ITypeData basetype)
        {
            if (basetype is TypeData basetype2)
            {
                return DescendsTo(type, basetype2.Type);
            }
            while (type != null)
            {    
                if (object.Equals(type, basetype))
                    return true;
                type = type.BaseType;
            }
            return false;
        }
        /// <summary> returns tru if 'type' is a descendant of 'basetype'. </summary>
        /// <param name="type"></param>
        /// <param name="basetype"></param>
        /// <returns></returns>
        public static bool DescendsTo(this ITypeData type, Type basetype)
        {
            while (type != null)
            {
                if (type is TypeData cst)
                {
                    return cst.Type.DescendsTo(basetype);
                }

                type = type.BaseType;
            }
            return false;
        }
        /// <summary>
        /// Returns true if a reflection ifno has an attribute of type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mem"></param>
        /// <returns></returns>
        static public bool HasAttribute<T>(this IReflectionData mem) where T: class
        {
            return mem.GetAttribute<T>() != null;
        }

        /// <summary> Gets the attribute of type T from mem. </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mem"></param>
        /// <returns></returns>
        static public T GetAttribute<T>(this IReflectionData mem)
        {
            if (mem.Attributes is object[] array)
            {
                // performance optimization: faster iterations if we know its an array.
                foreach (var thing in array)
                    if (thing is T x)
                        return x;
            }
            else
            {
                foreach (var thing in mem.Attributes)
                    if (thing is T x)
                        return x;
            }

            return default;
        }

        /// <summary> Gets all the attributes of type T.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mem"></param>
        /// <returns></returns>
        static public IEnumerable<T> GetAttributes<T>(this IReflectionData mem)
        {
            return mem.Attributes.OfType<T>();
        }

        /// <summary> Gets the display attribute of mem. </summary>
        /// <param name="mem"></param>
        /// <returns></returns>
        public static DisplayAttribute GetDisplayAttribute(this IReflectionData mem)
        {
            DisplayAttribute attr = null;
            if (mem is TypeData td)
                attr = td.Display;
            else
                attr = mem.GetAttribute<DisplayAttribute>();
            return attr ?? new DisplayAttribute(mem.Name, null, Order: -10000, Collapsed: false);
        }

        /// <summary>Gets the help link of 'member'</summary>
        /// <param name="member"></param>
        /// <returns></returns>
        internal static HelpLinkAttribute GetHelpLink(this IReflectionData member)
        {
            var attr = member.GetAttribute<HelpLinkAttribute>();
            if (attr != null)
                return attr;
            if (member is IMemberData meminfo)// Recursively look for class level help.
                return meminfo.DeclaringType.GetHelpLink();
            return null;
        }
    }
}
