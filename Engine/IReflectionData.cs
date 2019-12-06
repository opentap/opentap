//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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

    /// <summary>
    /// Type info provider. Provides type info for a given object. 
    /// </summary>
    [Display("TypeInfo Provider")]
    public interface ITypeDataProvider : ITapPlugin
    {
        /// <summary>
        /// Gets the type info from an identifier.
        /// </summary>
        ITypeData GetTypeData(string identifier);
        /// <summary>
        /// Gets the type info from an object.
        /// </summary>
        ITypeData GetTypeData(object obj);

        /// <summary>
        /// The priority of this type info provider. Note, this decides the order in which tyhe type info is resolved.
        /// </summary>
        double Priority { get; }
    }

    /// <summary> Can resolve a type info. </summary>
    internal class TypeInfoResolver
    {
        internal TypeInfoResolver(List<ITypeDataProvider> providers) => this.providers = providers;

        readonly List<ITypeDataProvider> providers;
        int offset = 0;
        /// <summary> Looks for a type info provider to provide the type for obj. </summary>
        public void Iterate(object obj)
        {
            while (offset < providers.Count && FoundType == null)
            {
                var provider = providers[offset];
                offset++;
                try
                {
                    FoundType = provider.GetTypeData(obj);
                }
                catch(Exception error)
                {
                    logProviderError(provider, error);
                }
            }
        }

        static void logProviderError(ITypeDataProvider provider, Exception error)
        {
            var log = Log.CreateSource(provider.GetType().Name);
            log.Error("Unhandled error occured in type resolution: {0}", error.Message);
            log.Debug(error);
        }
        /// <summary> Looks for a type info provider to provide the type for the obj string. </summary>
        public void Iterate(string obj)
        {
            while (offset < providers.Count && FoundType == null)
            {
                var provider = providers[offset];
                offset++;
                try
                {
                    FoundType = provider.GetTypeData(obj);
                }
                catch (Exception error)
                {
                    logProviderError(provider, error);
                }
            }
        }

        /// <summary> The found type info instance. </summary>
        public ITypeData FoundType { get; private set; }
    }

    /// <summary>
    /// Factories for ITypeData
    /// </summary>
    public partial class TypeData
    {
        static List<ITypeDataProvider> providers = new List<ITypeDataProvider>();
        static List<ITypeDataProvider> Providers
        {
            get
            {
                var _providers = PluginManager.GetPlugins<ITypeDataProvider>();
                if (providers.Count == _providers.Count) return providers;
                providers = _providers.Select(x => Activator.CreateInstance(x)).OfType<ITypeDataProvider>().ToList();
                providers.Sort((x, y) => y.Priority.CompareTo(x.Priority));
                return providers;
            }
        }

        /// <summary> Get the type info of an object. </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        static public ITypeData GetTypeData(object obj)
        {
            if (obj == null) return TypeData.FromType(typeof(object));
            var resolver = new TypeInfoResolver(Providers);
            resolver.Iterate(obj);
            return resolver.FoundType;
        }

        /// <summary> Gets the type info from a string. </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        static public ITypeData GetTypeData(string name)
        {
            var resolver = new TypeInfoResolver(Providers);
            resolver.Iterate(name);
            return resolver.FoundType;
        }

        private static List<ITypeDataSearcher> searchers = new List<ITypeDataSearcher>();
        private static Dictionary<ITypeData, IEnumerable<ITypeData>> derivedTypesCache = new Dictionary<ITypeData, IEnumerable<ITypeData>>();

        /// <summary> Get all known types that derive from a given type.</summary>
        /// <typeparam name="BaseType">Base type that all returned types descends to.</typeparam>
        /// <returns>All known types that descends to the given base type.</returns>
        public static IEnumerable<ITypeData> GetDerivedTypes<BaseType>()
        {
            return GetDerivedTypes(TypeData.FromType(typeof(BaseType)));
        }

        /// <summary> Get all known types that derive from a given type.</summary>
        /// <param name="baseType">Base type that all returned types descends to.</param>
        /// <returns>All known types that descends to the given base type.</returns>
        static public IEnumerable<ITypeData> GetDerivedTypes(ITypeData baseType)
        {
            var searcherTypes = TypeData.FromType(typeof(ITypeDataSearcher)).DerivedTypes;
            if(derivedTypesCache.ContainsKey(baseType) && searchers.Count == searcherTypes.Count())
            {
                return derivedTypesCache[baseType];
            }
            if (searchers.Count() != searcherTypes.Count())
            {
                var searchTasks = new List<Task>();
                foreach (var st in searcherTypes)
                {
                    if (!searchers.Any(s => TypeData.GetTypeData(s) == st))
                    {
                        var searcher = (ITypeDataSearcher)st.CreateInstance(Array.Empty<object>());
                        searchTasks.Add(Task.Run(() => searcher.Search()));
                        searchers.Add(searcher);
                    }
                }
                try
                {
                    Task.WaitAll(searchTasks.ToArray());
                }
                catch (Exception ex)
                {
                    log.Debug(ex);
                }
            }
            List<ITypeData> DerivedTypes = new List<ITypeData>();
            foreach (ITypeDataSearcher searcher in searchers)
            {
                if (searcher is DotNetTypeDataSearcher && baseType is TypeData td)
                {
                    // This is a performance shortcut.
                    DerivedTypes.AddRange(td.DerivedTypes);
                    continue;
                }
                if (searcher != null && searcher.Types != null)
                {
                    foreach (ITypeData type in searcher.Types)
                    {
                        if (type.DescendsTo(baseType))
                            DerivedTypes.Add(type);
                    }
                }
            }
            derivedTypesCache[baseType] = DerivedTypes;
            return DerivedTypes;
        }

    }

    /// <summary>
    /// Searches for "types" and returns them as ITypeData objects. The OpenTAP type system calls all implementations of this.
    /// </summary>
    public interface ITypeDataSearcher
    {
        /// <summary> Get all types found by the search. </summary>
        IEnumerable<ITypeData> Types { get; }
        /// <summary>
        /// Performs an implementation specific search for types. Generates ITypeData objects for all types found Types property.
        /// </summary>
        void Search();
    }

    internal class DotNetTypeDataSearcher : ITypeDataSearcher
    {
        /// <summary>
        /// Get all types found by the search. 
        /// </summary>
        public IEnumerable<ITypeData> Types { get; private set; }

        /// <summary>
        /// Performs an implementation specific search for types. Generates ITypeData objects for all types found Types property.
        /// </summary>
        public void Search()
        {
            Types = PluginManager.GetSearcher().AllTypes.Values;
        }
    }

    /// <summary> Helpers for work with ITypeInfo objects. </summary>
    public static class ReflectionDataExtensions
    {
        /// <summary>
        /// Creates an instance of this type using the default constructor.
        /// </summary>
        public static object CreateInstance(this ITypeData type)
        {
            return type.CreateInstance(Array.Empty<object>());
        }

        /// <summary> returns tru if 'type' is a descendant of 'basetype'. </summary>
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
            if(typeof(T) == typeof(DisplayAttribute) && mem is TypeData td)
            {
                return (T)((object)td.Display);
            }
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

        internal static bool IsBrowsable(this IReflectionData mem)
        {
            if (mem is TypeData td)
            {
                return td.IsBrowsable;
            }
            var attr = mem.GetAttribute<BrowsableAttribute>();
            if (attr is null)
                return true;
            return attr.Browsable;
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
