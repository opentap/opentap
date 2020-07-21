//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace OpenTap
{
    /// <summary>
    /// Factories for ITypeData
    /// </summary>
    public partial class TypeData 
    {
        private static List<ITypeDataSearcher> searchers = new List<ITypeDataSearcher>();
        private static Dictionary<ITypeData, IEnumerable<ITypeData>> derivedTypesCache = new Dictionary<ITypeData, IEnumerable<ITypeData>>();

        /// <summary> Get all known types that derive from a given type.</summary>
        /// <typeparam name="BaseType">Base type that all returned types descends to.</typeparam>
        /// <returns>All known types that descends to the given base type.</returns>
        public static IEnumerable<ITypeData> GetDerivedTypes<BaseType>()
        {
            return GetDerivedTypes(TypeData.FromType(typeof(BaseType)));
        }

        static int ChangeID = -1;

        static void checkCacheValidity()
        {
            if (PluginManager.ChangeID != ChangeID)
            {
                searchers.Clear();
                MemberData.InvalidateCache();
                dict = new System.Runtime.CompilerServices.ConditionalWeakTable<Type, TypeData>();
                ChangeID = PluginManager.ChangeID;
            }
        }

        /// <summary> Get all known types that derive from a given type.</summary>
        /// <param name="baseType">Base type that all returned types descends to.</param>
        /// <returns>All known types that descends to the given base type.</returns>
        static public IEnumerable<ITypeData> GetDerivedTypes(ITypeData baseType)
        {
            checkCacheValidity();
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
                    Log.CreateSource("TypeData").Debug(ex);
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

        /// <summary> Get the type info of an object. </summary>
        static public ITypeData GetTypeData(object obj)
        {
            var cache = TypeDataCache.Current?.cache;
            if (cache != null && cache.TryGetValue(obj, out var cachedValue))
                return cachedValue;
            checkCacheValidity();
            if (obj == null) return FromType(typeof(object));
            var resolver = new TypeDataProviderStack();
            var result = resolver.GetTypeData(obj);
            if (cache == null)
                return result;
            cache[obj] = result;
            return result;
        }

        class TypeDataCache : IDisposable
        {
            static TypeDataCache current;
            public static TypeDataCache Current => current;
            public static IDisposable Load() => current = new TypeDataCache { previousValue = current };
            
            TypeDataCache previousValue;
            public readonly Guid Id;
            ICacheOptimizer[] caches;
            TypeDataCache()
            {
                Id = Guid.NewGuid();
                var types = TypeData.GetDerivedTypes<ICacheOptimizer>().Where(x => x.CanCreateInstance).Select(x => x.AsTypeData().Type).ToArray();
                caches = types.Select(t => t.CreateInstance()).OfType<ICacheOptimizer>().ToArray();
                foreach (var type in caches)
                    type.LoadCache();
            }
        
            public readonly ConcurrentDictionary<object, ITypeData> cache = new ConcurrentDictionary<object, ITypeData>();

            public void Dispose()
            {
              foreach(var cache in caches)
                  cache.UnloadCache();
            }  
        }

        /// <summary>  Creates a type data cache. Note this should be used with 'using{}' so that it gets removed afterwards. </summary>
        /// <returns> A disposable object removing the cache. </returns>
        public static IDisposable WithTypeDataCache() =>  TypeDataCache.Load();

        internal static bool IsCacheInUse => TypeDataCache.Current != null;
        internal static Guid CacheId => TypeDataCache.Current.Id;

        /// <summary> Gets the type info from a string. </summary>
        static public ITypeData GetTypeData(string name) => new TypeDataProviderStack().GetTypeData(name);

        /// <summary>
        /// This throws an exception due to the ambiguousness of TypeData.FromType vs TypeData.GetTypeData. To get TypeData representing a type use TypeData.FromType.
        /// Otherwise cast 'type' to an 'object' first.
        /// </summary>
        [Obsolete("This overload of GetTypeData should not be used: To get TypeData representing a type use TypeData.FromType. Otherwise cast 'type' to an 'object' first.", true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        static public ITypeData GetTypeData(Type _)
        {
            throw new NotSupportedException(@"Ambiguous call to GetTypeData: To get TypeData representing a type use TypeData.FromType."
            + "Otherwise cast 'type' to an 'object' first.");
        }
    }
    
    /// <summary> Interface for classes that can be used for cache optimizations. </summary>
    public interface ICacheOptimizer
    {
        /// <summary> Loads / heats up the cache.</summary>
        void LoadCache();
        /// <summary> Unload / cool down the cache.</summary>
        void UnloadCache();
    }   
}
