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
    public interface IReflectionInfo
    {
        /// <summary> The attributes of it. </summary>
        IEnumerable<object> Attributes { get; }
        /// <summary>
        /// The name of it.
        /// </summary>
        string Name { get; }
    }

    /// <summary> A member of an object type. </summary>
    public interface IMemberInfo : IReflectionInfo
    {
        /// <summary> The declaring type of this member. </summary>
        ITypeInfo DeclaringType { get; }
        /// <summary> The underlying type of this member. </summary>
        ITypeInfo TypeDescriptor { get; }
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
    public interface ITypeInfo : IReflectionInfo
    {
        /// <summary> The base type of this type. </summary>
        ITypeInfo BaseType { get; }
        /// <summary> Gets the members of this object. </summary>
        /// <returns></returns>
        IEnumerable<IMemberInfo> GetMembers();
        /// <summary> Gets a member of this object by name.  </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        IMemberInfo GetMember(string name);
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

    /// <summary>
    /// Type info provider. Provides type info for a given object. 
    /// </summary>
    [Display("TypeInfo Provider")]
    public interface ITypeInfoProvider : ITapPlugin
    {
        /// <summary>
        /// Gets the type info from an identifier.
        /// </summary>
        /// <param name="res"></param>
        /// <param name="identifier"></param>
        void GetTypeInfo(TypeInfoResolver res, string identifier);
        /// <summary>
        /// Gets the type info from an object.
        /// </summary>
        /// <param name="res"></param>
        /// <param name="obj"></param>
        void GetTypeInfo(TypeInfoResolver res, object obj);

        /// <summary>
        /// The priority of this type info provider. Note, this decides the order in which tyhe type info is resolved.
        /// </summary>
        double Priority { get; }
    }


    /// <summary> Can resolve a type info. </summary>
    public class TypeInfoResolver
    {

        internal TypeInfoResolver(List<ITypeInfoProvider> providers)
        {
            this.providers = providers;
        }
        List<ITypeInfoProvider> providers;
        int offset = 0;
        /// <summary> Looks for a type info provider to provide the type for obj. </summary>
        /// <param name="obj"></param>
        public void Iterate(object obj)
        {
            while (offset < providers.Count && FoundType == null)
            {
                var provider = providers[offset];
                offset++;
                provider.GetTypeInfo(this, obj);
            }
        }

        /// <summary> Looks for a type info provider to provide the type for the obj string. </summary>
        /// <param name="obj"></param>
        public void Iterate(string obj)
        {
            while (offset < providers.Count && FoundType == null)
            {
                var provider = providers[offset];
                offset++;
                provider.GetTypeInfo(this, obj);
            }
        }

        /// <summary> Can be used to stop iteration from a type info provider. </summary>
        /// <param name="desc"></param>
        public void Stop(ITypeInfo desc)
        {
            FoundType = desc;
        }

        /// <summary>
        /// The found type info instance.
        /// </summary>
        public ITypeInfo FoundType { get; private set; }
    }

    /// <summary>
    /// Helper method for TypeInfo.
    /// </summary>
    public static class TypeInfo
    {
        static List<ITypeInfoProvider> providers = new List<ITypeInfoProvider>();
        static List<ITypeInfoProvider> Providers
        {
            get
            {
                var _providers = PluginManager.GetPlugins<ITypeInfoProvider>();
                if (providers.Count == _providers.Count) return providers;
                providers = _providers.Select(x => Activator.CreateInstance(x)).OfType<ITypeInfoProvider>().ToList();
                providers.Sort((x, y) => y.Priority.CompareTo(x.Priority));
                return providers;
            }
        }

        /// <summary> Get the type info of an object. </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        static public ITypeInfo GetTypeInfo(object obj)
        {
            if (obj == null) return CSharpTypeInfo.Create(typeof(object));
            var resolver = new TypeInfoResolver(Providers);
            resolver.Iterate(obj);
            return resolver.FoundType;
        }

        /// <summary> Gets the type info from a string. </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        static public ITypeInfo GetTypeInfo(string name)
        {
            var resolver = new TypeInfoResolver(Providers);
            resolver.Iterate(name);
            return resolver.FoundType;
        }
    }

    /// <summary> Helpers for work with ITypeInfo objects. </summary>
    public static class ReflectionInfoExtensions
    {
        /// <summary> Returns true if 'type' and 'basetype' are equal. </summary>
        /// <param name="type"></param>
        /// <param name="basetype"></param>
        /// <returns></returns>
        public static bool IsA(this ITypeInfo type, ITypeInfo basetype)
        {
            return object.Equals(type, basetype);
        }
        /// <summary>Returns true if 'type' and 'basetype' are equal. </summary> 
        /// <param name="type"></param>
        /// <param name="basetype"></param>
        /// <returns></returns>
        public static bool IsA(this ITypeInfo type, Type basetype)
        {
            if (type is CSharpTypeInfo cst)
                return cst.Type == basetype;
            return false;
        }

        /// <summary> returns tru if 'type' is a descendant of 'basetype'. </summary>
        /// <param name="type"></param>
        /// <param name="basetype"></param>
        /// <returns></returns>
        public static bool DescendsTo(this ITypeInfo type, ITypeInfo basetype)
        {
            while (type != null)
            {
                if (type.IsA(basetype))
                    return true;
                type = type.BaseType;
            }
            return false;
        }
        /// <summary> returns tru if 'type' is a descendant of 'basetype'. </summary>
        /// <param name="type"></param>
        /// <param name="basetype"></param>
        /// <returns></returns>
        public static bool DescendsTo(this ITypeInfo type, Type basetype)
        {
            while (type != null)
            {
                if (type is CSharpTypeInfo cst)
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
        static public bool HasAttribute<T>(this IReflectionInfo mem)
        {
            return mem.Attributes.OfType<T>().Any();
        }

        /// <summary> Gets the attribute of type T from mem. </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mem"></param>
        /// <returns></returns>
        static public T GetAttribute<T>(this IReflectionInfo mem)
        {
            return mem.GetAttributes<T>().FirstOrDefault();
        }

        /// <summary> Gets all the attributes of type T.</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mem"></param>
        /// <returns></returns>
        static public IEnumerable<T> GetAttributes<T>(this IReflectionInfo mem)
        {
            return mem.Attributes.OfType<T>();
        }

        /// <summary> Gets the display attribute of mem. </summary>
        /// <param name="mem"></param>
        /// <returns></returns>
        public static DisplayAttribute GetDisplayAttribute(this IReflectionInfo mem)
        {
            return mem.GetAttribute<DisplayAttribute>() ?? new DisplayAttribute(mem.Name, null, Order: -10000, Collapsed: false);
        }

        /// <summary>Gets the help link of 'member'</summary>
        /// <param name="member"></param>
        /// <returns></returns>
        internal static string GetHelpLink(this IReflectionInfo member)
        {
            var attr = member.GetAttribute<HelpLinkAttribute>();
            if (attr != null)
                return attr.HelpLink;
            if (member is IMemberInfo meminfo)// Recursively look for class level help.
                return meminfo.DeclaringType.GetHelpLink();
            return null;
        }
    }
}
