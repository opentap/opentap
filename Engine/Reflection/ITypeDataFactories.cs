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
        static readonly List<ITypeDataSearcher> searchers = new List<ITypeDataSearcher>();
        static readonly ConcurrentDictionary<ITypeData, ITypeData[]> derivedTypesCache = new ConcurrentDictionary<ITypeData, ITypeData[]>();

        /// <summary>  Invoked when new types has been discovered in an asynchronous fashion. </summary>
        public static event EventHandler<TypeDataCacheInvalidatedEventArgs> TypeCacheInvalidated; 
        static void OnSearcherCacheInvalidated(TypeDataCacheInvalidatedEventArgs args)
        {
            lastCount = 0;
            TypeCacheInvalidated?.Invoke(null, args);
        }
        
        /// <summary> Get all known types that derive from a given type.</summary>
        /// <typeparam name="BaseType">Base type that all returned types descends to.</typeparam>
        /// <returns>All known types that descends to the given base type.</returns>
        public static IEnumerable<ITypeData> GetDerivedTypes<BaseType>()
        {
            return GetDerivedTypes(FromType(typeof(BaseType)));
        }

        static int ChangeID = -1;

        static void checkCacheValidity()
        {
            if (PluginManager.ChangeID != ChangeID)
            {
                // make sure that ITypeDataSearchers with cache invalidation are demantled.
                foreach (var searcher in searchers.OfType<ITypeDataSearcherCacheInvalidated>())
                    searcher.CacheInvalidated -= CacheInvalidatedOnCacheInvalidated;
                searchers.Clear();
                MemberData.InvalidateCache();
                dict = new System.Runtime.CompilerServices.ConditionalWeakTable<Type, TypeData>();
                ChangeID = PluginManager.ChangeID;
            }
        }

        static readonly object lockSearchers = new object();
        static int lastCount;

        private static HashSet<ITypeData> logged = new HashSet<ITypeData>();
        private static void WarnOnce(string message, ITypeData t)
        {
            if (logged.Add(t))
                log.Warning(message);
        }
        
        /// <summary> Get all known types that derive from a given type.</summary>
        /// <param name="baseType">Base type that all returned types descends to.</param>
        /// <returns>All known types that descends to the given base type.</returns>
        public static IEnumerable<ITypeData> GetDerivedTypes(ITypeData baseType)
        {
            checkCacheValidity();
            bool invalidated = false;
            int count = 0;
            foreach (var s in searchers)
            {
                var items = s.Types;
                if (items is null)
                    invalidated = true;
                else
                    count += items.Count();
            }

            if (lastCount == count && invalidated == false && derivedTypesCache.TryGetValue(baseType, out var result2))
                return result2;
            
            lock (lockSearchers)
            {
                if (lastCount != count || invalidated)
                {
                    derivedTypesCache.Clear();
                    lastCount = count;
                }
                else if (derivedTypesCache.TryGetValue(baseType, out var result3))
                {
                    return result3;
                }
                
                var searchTasks = new List<Task>();
                foreach (var s in searchers)
                {
                    if (s != null && s.Types == null)
                    {
                        searchTasks.Add(TapThread.StartAwaitable(s.Search));
                    }
                }

                var searcherTypes = FromType(typeof(ITypeDataSearcher)).DerivedTypes
                    .Where(x => x.CanCreateInstance)
                    .ToArray();
                if (searchers.Count != searcherTypes.Length)
                {
                    var existing = searchers.Select(GetTypeData).ToHashSet();
                    foreach (var searcherType in searcherTypes)
                    {
                        if (existing.Contains(searcherType)) continue;

                        bool error = false;
                        try
                        {
                            if (searcherType.CreateInstance(Array.Empty<object>()) is ITypeDataSearcher searcher)
                            {
                                // make sure that ITypeDataSearchers with cache invalidation are activated.
                                if (searcher is ITypeDataSearcherCacheInvalidated cacheInvalidated)
                                    cacheInvalidated.CacheInvalidated += CacheInvalidatedOnCacheInvalidated;

                                searchTasks.Add(TapThread.StartAwaitable(searcher.Search));
                                searchers.Add(searcher);
                            }
                            else error = true;
                        }
                        catch
                        {
                            error = true;
                        }

                        if (error)
                            WarnOnce($"Failed to instantiate {nameof(ITypeDataSearcher)} {searcherType}", searcherType);
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

                var derivedTypes = new List<ITypeData>();
                foreach (var searcher in searchers)
                {
                    if (searcher is DotNetTypeDataSearcher && baseType is TypeData td)
                    {
                        // This is a performance shortcut.
                        derivedTypes.AddRange(td.DerivedTypes);
                        continue;
                    }

                    if (searcher?.Types is IEnumerable<ITypeData> types)
                    {
                        foreach (var type in types)
                        {
                            if (type.DescendsTo(baseType))
                                derivedTypes.Add(type);
                        }
                    }
                }

                var result = derivedTypes.ToArray();
                derivedTypesCache[baseType] = result;
                return result;
            }
        }

        static void CacheInvalidatedOnCacheInvalidated(object sender, TypeDataCacheInvalidatedEventArgs e)
        {
            OnSearcherCacheInvalidated(e);
        }

        /// <summary> Get the type info of an object. </summary>
        public static ITypeData GetTypeData(object obj)
        {
            if (obj == null) return NullTypeData.Instance;
            var cache = TypeDataCache.Current;
            if (cache != null && cache.TryGetValue(obj, out var cachedValue))
                return cachedValue;
            checkCacheValidity();
            var resolver = new TypeDataProviderStack();
            var result = resolver.GetTypeData(obj);
            if (result == null)
                // this should never be possible since even GetTypeData(null) returns type of object.
                throw new Exception($"GetTypeData returned null for an object if type {obj.GetType()}");
            if (cache == null)
                return result;
            cache[obj] = result;
            return result;
        }

        class TypeDataCache : IDisposable
        {
            static ThreadField<ConcurrentDictionary<object, ITypeData>> cache = new ThreadField<ConcurrentDictionary<object, ITypeData>>();

            public static ConcurrentDictionary<object, ITypeData> Current => cache.Value;
            
            ICacheOptimizer[] caches;
            public TypeDataCache()
            {
                var types = GetDerivedTypes<ICacheOptimizer>().Where(x => x.CanCreateInstance).Select(x => x.AsTypeData().Type).ToArray();
                caches = types.Select(t => t.CreateInstance()).OfType<ICacheOptimizer>().ToArray();
                foreach (var type in caches)
                    type.LoadCache();
                cache.Value = new ConcurrentDictionary<object, ITypeData>();
            }
        

            public void Dispose()
            {
               cache.Value = null;
              foreach(var cache in caches)
                  cache.UnloadCache();
            }  
        }

        /// <summary>  Creates a type data cache. Note this should be used with 'using{}' so that it gets removed afterwards. </summary>
        /// <returns> A disposable object removing the cache. </returns>
        internal static IDisposable WithTypeDataCache() =>  new TypeDataCache();

        /// <summary> Gets the type info from a string. </summary>
        public static  ITypeData GetTypeData(string name) => new TypeDataProviderStack().GetTypeData(name);

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
    internal interface ICacheOptimizer
    {
        /// <summary> Loads / heats up the cache.</summary>
        void LoadCache();
        /// <summary> Unload / cool down the cache.</summary>
        void UnloadCache();
    }   
}
