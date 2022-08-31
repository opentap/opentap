using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace OpenTap
{
    /// <summary>
    /// Representation of a C#/dotnet type including its inheritance hierarchy. Part of the object model used in the PluginManager
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class TypeData : ITypeData
    {
        // Used to mark when no display attribute is present.
        static readonly DisplayAttribute noDisplayAttribute = new DisplayAttribute("<<Null>>");

        /// <summary>
        /// Gets the fully qualified name of the type, including its namespace but not its assembly.
        /// </summary>
        public string Name { get;}

        /// <summary>
        /// Gets the TypeAttributes for this type. This can be used to check if the type is abstract, nested, an interface, etc.
        /// </summary>
        public TypeAttributes TypeAttributes { get; internal set; }

        /// <summary>
        /// Gets the Assembly that defines this type.
        /// </summary>
        public AssemblyData Assembly { get; internal set; }


        /// <summary>
        /// Gets.the DisplayAttribute for this type. Null if the type does not have a DisplayAttribute
        /// </summary>
        public DisplayAttribute Display
        {
            get
            {
                if (display is null && attributes != null)
                {
                    display = noDisplayAttribute;
                    foreach (var attr in attributes)
                    {
                        if (attr is DisplayAttribute displayAttr)
                        {
                            display = displayAttr;
                            break;
                        }
                    }
                }

                if (ReferenceEquals(display, noDisplayAttribute))
                    return null;
                return display;
            }
            internal set => display = value;
        }

        /// <summary> Gets a list of base types (including interfaces) </summary>
        internal ICollection<TypeData> BaseTypes => baseTypes;

        /// <summary>
        /// Gets a list of plugin types (i.e. types that directly implement ITapPlugin) that this type inherits from/implements
        /// </summary>
        public IEnumerable<TypeData> PluginTypes => pluginTypes;

        /// <summary>
        /// Gets a list of types that has this type as a base type (including interfaces)
        /// </summary>
        public IEnumerable<TypeData> DerivedTypes => derivedTypes ?? Array.Empty<TypeData>();

        /// <summary>
        /// False if the type has a System.ComponentModel.BrowsableAttribute with Browsable = false.
        /// </summary>
        public bool IsBrowsable { get; internal set; }

        /// <summary> 
        /// The attributes of this type. 
        /// Accessing this property causes the underlying Assembly to be loaded if it is not already.
        /// </summary>
        public IEnumerable<object> Attributes =>
            attributes ?? (attributes = Load()?.GetAllCustomAttributes(false)) ?? Array.Empty<object>();

        /// <summary>
        /// Gets the System.Type that this represents. Same as calling <see cref="Load()"/>.
        /// Accessing this property causes the underlying Assembly to be loaded if it is not already.
        /// </summary>
        public Type Type => Load();

        /// <summary> gets if the type is a value-type. (see Type.IsValueType)</summary>
        public bool IsValueType
        {
            get
            {
                PostLoad();
                return isValueType;
            }
        }

        internal bool IsNumeric
        {
            get
            {
                PostLoad();
                if (type.IsEnum)
                    return false;
                switch (typeCode)
                {
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.UInt64:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.Decimal:
                    case TypeCode.Double:
                    case TypeCode.Single:
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary> The base type of this type. Will return null if there is no base type. If there is no direct base, but instead an interface, that will be returned.</summary>
        public ITypeData BaseType
        {
            get
            {
                if (baseTypeCache != null)
                    return ReferenceEquals(baseTypeCache, NullTypeData.Instance) ? null : baseTypeCache;
                baseTypeCache = BaseTypes?.ElementAtOrDefault(0);
                if (baseTypeCache != null) return baseTypeCache;
                var result2 = Load()?.BaseType;
                if (result2 != null)
                {
                    baseTypeCache = FromType(result2);
                    return baseTypeCache;
                }

                baseTypeCache = NullTypeData.Instance;
                return baseTypeCache;
            }
        }

        /// <summary> If this is a collection type, then this is the element type. Otherwise null. </summary>
        internal TypeData ElementType
        {
            get
            {
                PostLoad();
                return elementType;
            }
        }

        /// <summary> 
        /// returns true if an instance possibly can be created. 
        /// Accessing this property causes the underlying Assembly to be loaded if it is not already.
        /// </summary>
        public bool CanCreateInstance
        {
            get
            {
                if (failedLoad) return false;
                if (canCreateInstance.HasValue) return canCreateInstance.Value;
                if (Load() is Type t)
                {
                    type = t;
                    canCreateInstance = type.IsAbstract == false && type.IsInterface == false &&
                                        type.ContainsGenericParameters == false &&
                                        type.GetConstructor(Array.Empty<Type>()) != null;
                    return canCreateInstance.Value;
                }

                return false; // failed to load
            }
            internal set => canCreateInstance = value;
        }

        internal string AssemblyQualifiedName
        {
            get
            {
                if (failedLoad) return "";
                return assemblyQualifiedName ?? (assemblyQualifiedName = Load().AssemblyQualifiedName);
            }
        }

        /// <summary> The loaded state of the type. </summary>
        internal LoadStatus Status =>
            type != null ? LoadStatus.Loaded : (failedLoad ? LoadStatus.FailedToLoad : LoadStatus.NotLoaded);

        internal bool createInstanceSet => canCreateInstance.HasValue;

        internal bool IsString
        {
            get
            {
                PostLoad();
                return typeCode == TypeCode.String;
            }
        }

        /// <summary>  Invoked when new types has been discovered in an asynchronous fashion. </summary>
        public static event EventHandler<TypeDataCacheInvalidatedEventArgs> TypeCacheInvalidated;

        static readonly TraceSource log = Log.CreateSource("PluginManager");
        static ImmutableArray<ITypeDataSearcher> searchers = ImmutableArray<ITypeDataSearcher>.Empty;

        static readonly ConcurrentDictionary<ITypeData, ITypeData[]> derivedTypesCache =
            new ConcurrentDictionary<ITypeData, ITypeData[]>();

        static int ChangeID = -1; // used for monitoring cache invalidation
        static readonly object lockSearchers = new object();
        static int lastCount;
        static HashSet<ITypeData> warningLogged = new HashSet<ITypeData>();
        static ConditionalWeakTable<Type, TypeData> typeToTypeDataCache = new ConditionalWeakTable<Type, TypeData>();

        // add assembly is not thread safe.
        static object loadTypeDictLock = new object();

        Type type;
        bool? canCreateInstance;
        ICollection<TypeData> baseTypes;
        ICollection<TypeData> derivedTypes;
        ICollection<TypeData> pluginTypes;
        IMemberData[] members;
        bool hasFlags;
        DisplayAttribute display;
        string assemblyQualifiedName;
        bool failedLoad;
        TypeData elementType;
        ITypeData baseTypeCache;
        TypeCode typeCode = TypeCode.Object;
        object[] attributes = null;
        bool postLoaded = false;
        readonly object loadLock = new object();
        bool isValueType;

        internal void FinalizeCreation()
        {
            baseTypes = baseTypes?.ToArray();
            pluginTypes = pluginTypes?.ToArray();
        }

        internal void AddBaseType(TypeData typename)
        {
            if (baseTypes == null)
                baseTypes = new HashSet<TypeData>();
            baseTypes.Add(typename);
        }

        internal void AddPluginType(TypeData typename)
        {
            if (typename == null)
                return;
            if (pluginTypes == null)
                pluginTypes = new HashSet<TypeData>();
            pluginTypes.Add(typename);
        }

        internal void AddPluginTypes(IEnumerable<TypeData> types)
        {
            if (types == null)
                return;
            if (pluginTypes == null)
                pluginTypes = new HashSet<TypeData>();
            foreach (var t in types)
                pluginTypes.Add(t);
        }

        internal void AddDerivedType(TypeData typename)
        {
            if (derivedTypes == null)
                derivedTypes = new HashSet<TypeData>();
            else if (derivedTypes.Contains(typename))
                return;
            derivedTypes.Add(typename);
            if (BaseTypes != null)
            {
                foreach (TypeData b in BaseTypes)
                    b.AddDerivedType(typename);
            }
        }

        internal TypeData(string typeName)
        {
            Name = typeName;
            IsBrowsable = true;
        }

        TypeData(Type type): this(type.FullName)
        {
            this.type = type;
            PostLoad();
            IsBrowsable = this.GetAttribute<BrowsableAttribute>()?.Browsable ?? true;
        }

        /// <summary>
        /// Returns the System.Type corresponding to this. 
        /// If the assembly in which this type is defined has not yet been loaded, this call will load it.
        /// </summary>
        public Type Load()
        {
            if (failedLoad) return null;
            if (type != null) return type;

            try
            {
                var asm = Assembly.Load();
                if (asm == null)
                {
                    failedLoad = true;
                    return null;
                }

                type = asm.GetType(this.Name, true);
                typeToTypeDataCache.GetValue(type, t => this);
            }
            catch (Exception ex)
            {
                failedLoad = true;
                log.Error("Unable to load type '{0}' from '{1}'. Reason: '{2}'.", Name, Assembly.Location,
                    ex.Message);
                log.Debug(ex);
            }

            return type;
        }

        /// <summary>
        /// Returns the DisplayAttribute.Name if the type has a DisplayAttribute, otherwise the FullName without namespace
        /// </summary>
        /// <returns></returns>
        public string GetBestName()
        {
            return Display != null ? Display.Name : Name.Split('.', '+').Last();
        }
        
                /// <summary> Creates a string value of this.</summary>
        public override string ToString() => Name;

        void PostLoad()
        {
            if (postLoaded) return;
            lock (loadLock)
            {
                if (postLoaded) return;
                Load();
                if (type != typeof(string))
                {
                    var elementType = type.GetEnumerableElementType();
                    if (elementType != null)
                        this.elementType = FromType(elementType);
                }

                typeCode = Type.GetTypeCode(type);
                hasFlags = this.HasAttribute<FlagsAttribute>();
                isValueType = type.IsValueType;
                postLoaded = true;
            }
        }


        /// <summary>
        /// Creates a new object instance of this type.
        /// Accessing this property causes the underlying Assembly to be loaded if it is not already.
        /// </summary>
        public object CreateInstance(object[] arguments)
        {
            if (!(Load() is Type t))
                throw new InvalidOperationException(
                    $"Failed to instantiate object of type '{this.Name}'. The assembly failed to load.");
            return Activator.CreateInstance(t, arguments);
        }

        /// <summary>
        /// Gets a member by name.
        /// Causes the underlying Assembly to be loaded if it is not already.
        /// </summary>
        public IMemberData GetMember(string name)
        {
            var members = (IMemberData[])GetMembers();
            foreach (var member in members)
            {
                if (member.Name == name)
                    return member;
            }

            return null;
        }

        /// <summary>
        /// Gets all the members of this type. 
        /// Causes the underlying Assembly to be loaded if it is not already.
        /// </summary>
        public IEnumerable<IMemberData> GetMembers()
        {
            if (members != null) return members;

            if (Load() is Type t)
            {
                var props = t.GetPropertiesTap();
                List<IMemberData> m = new List<IMemberData>(props.Length);
                foreach (var mem in props)
                {
                    try
                    {
                        if (mem.GetMethod != null && mem.GetMethod.GetParameters().Length > 0)
                            continue;

                        if (mem.SetMethod != null && mem.SetMethod.GetParameters().Length != 1)
                            continue;
                    }
                    catch
                    {
                        continue;
                    }

                    m.Add(MemberData.Create(mem));
                }

                foreach (var mem in t.GetMethodsTap())
                {
                    if (mem.GetAttribute<BrowsableAttribute>()?.Browsable ?? false)
                    {
                        var member = MemberData.Create(mem);
                        m.Add(member);
                    }
                }

                members = m.ToArray();
            }
            else
            {
                // The members list cannot be populated because the type could not be loaded.
                members = Array.Empty<IMemberData>();
            }

            return members;
        }

        internal bool HasFlags()
        {
            PostLoad();
            return hasFlags;
        }

        /// <summary> Compares two TypeDatas by comparing their inner Type instances. </summary>
        /// <param name="obj"> Should be a TypeData</param>
        /// <returns>true if the two Type properties are equals.</returns>
        public override bool Equals(object obj)
        {
            if (obj is TypeData td && td.type != null && type != null)
                return td.type == type;
            return ReferenceEquals(this, obj);
        }

        /// <summary> Calculates the hash code based on the .NET Type instance. </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            var asm = Assembly?.GetHashCode() ?? 0;
            return (asm + 586093897) * 759429795 +
                   (Name.GetHashCode() + 836431542) * 678129773;
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
                if (searchers.Length != searcherTypes.Length)
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
                                searchers = searchers.Add(searcher);
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

                searchers = searchers.Sort(PluginOrderAttribute.Comparer);
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

        static readonly ConditionalWeakTable<ITypeData, ITypeDataSource> typeDataSourceLookup =
            new ConditionalWeakTable<ITypeData, ITypeDataSource>();
        /// <summary>
        /// Gets the type data source for an ITypeData. For most types this will return the AssemblyData, but for types for which
        /// an ITypeDataSourceProvider exists it will return that instead. The base AssemblyData will often be associated to one
        /// of the typedatas base classes.
        /// </summary>
        /// <param name="typeData"></param>
        /// <returns></returns>
        public static ITypeDataSource GetTypeDataSource(ITypeData typeData)
        {
            if (typeDataSourceLookup.TryGetValue(typeData, out var src))
                return src;
            var typeData0 = typeData;
            lock (lockSearchers)
            {
                // if 'lock' actually caused a lock and it was that same typeData, we want to use it instead
                // of calculating it again.
                if (typeDataSourceLookup.TryGetValue(typeData, out var src2))
                    return src2;
                GetDerivedTypes<ITypeDataSearcher>(); // update cache.
                while (typeData != null)
                {
                    foreach (var searcher in searchers)
                    {
                        if (searcher is ITypeDataSourceProvider sp)
                        {
                            var source = sp.GetSource(typeData);
                            if (source != null)
                                return typeDataSourceLookup.GetValue(typeData0, td => source);
                        }
                    }
                    typeData = typeData.BaseType;
                }
            }

            return typeDataSourceLookup.GetValue(typeData0, td => null);
        }
        
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

        static void checkCacheValidity()
        {
            if (PluginManager.ChangeID != ChangeID)
            {
                // make sure that ITypeDataSearchers with cache invalidation are demantled.
                foreach (var searcher in searchers.OfType<ITypeDataSearcherCacheInvalidated>())
                    searcher.CacheInvalidated -= CacheInvalidatedOnCacheInvalidated;
                searchers = searchers.Clear();
                MemberData.InvalidateCache();
                typeToTypeDataCache = new ConditionalWeakTable<Type, TypeData>();
                ChangeID = PluginManager.ChangeID;
            }
        }

        static void WarnOnce(string message, ITypeData t)
        {
            if (warningLogged.Add(t))
                log.Warning(message);
        }

        static void CacheInvalidatedOnCacheInvalidated(object sender, TypeDataCacheInvalidatedEventArgs e)
        {
            OnSearcherCacheInvalidated(e);
        }

        
        /// <summary>  Creates a type data cache. Note this should be used with 'using{}' so that it gets removed afterwards. </summary>
        /// <returns> A disposable object removing the cache. </returns>
        internal static IDisposable WithTypeDataCache() => new TypeDataCache();

        /// <summary> Gets the type info from a string. </summary>
        public static ITypeData GetTypeData(string name) => new TypeDataProviderStack().GetTypeData(name);

        /// <summary>
        /// This throws an exception due to the ambiguousness of TypeData.FromType vs TypeData.GetTypeData. To get TypeData representing a type use TypeData.FromType.
        /// Otherwise cast 'type' to an 'object' first.
        /// </summary>
        [Obsolete(
            "This overload of GetTypeData should not be used: To get TypeData representing a type use TypeData.FromType. Otherwise cast 'type' to an 'object' first.",
            true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        static public ITypeData GetTypeData(Type _)
        {
            throw new NotSupportedException(
                @"Ambiguous call to GetTypeData: To get TypeData representing a type use TypeData.FromType."
                + "Otherwise cast 'type' to an 'object' first.");
        }

        /// <summary> Creates a new TypeData object to represent a dotnet type. </summary>
        public static TypeData FromType(Type type)
        {
            checkCacheValidity();
            if (typeToTypeDataCache.TryGetValue(type, out var i))
                return i;
            TypeData td = null;
            lock (loadTypeDictLock)
            {
                var searcher = PluginManager.GetSearcher();

                searcher?.AllTypes.TryGetValue(type.FullName, out td);
                if (td == null && searcher != null)
                {
                    // This can occur for some types inside mscorlib such as System.Net.IPAddress.
                    try
                    {
                        if (type.Assembly != null && type.Assembly.IsDynamic == false &&
                            type.Assembly.Location != null)
                        {
                            searcher.AddAssembly(type.Assembly.Location, type.Assembly);
                            if (searcher.AllTypes.TryGetValue(type.FullName, out td))
                                return td;
                        }
                    }
                    catch
                    {
                    }

                    td = new TypeData(type);
                }
                else
                {
                    // This can occur when using shared projects because the same type is defined in multiple different assemblies with the same fully qualified
                    // name (namespace+typename). In this case, it is possible for the PluginSearcher to resolve an instance of this typedata from a different
                    // type than the input type to this method, which will cause all sorts of reflection errors. We detect this edge case here, and if there
                    // is a type mismatch, we instantiate a new typedata from the correct type.
                    if (td == null || td.Type != type)
                    {
                        td = new TypeData(type);
                    }
                }
            }

            return typeToTypeDataCache.GetValue(type, x => td);
        }


        class TypeDataCache : IDisposable
        {
            static ThreadField<IDictionary<object, ITypeData>> cache =
                new ThreadField<IDictionary<object, ITypeData>>();

            public static IDictionary<object, ITypeData> Current => cache.Value;

            ICacheOptimizer[] caches;

            public TypeDataCache()
            {
                var types = GetDerivedTypes<ICacheOptimizer>().Where(x => x.CanCreateInstance)
                    .Select(x => x.AsTypeData().Type).ToArray();
                caches = types.Select(t => t.CreateInstance()).OfType<ICacheOptimizer>().ToArray();
                foreach (var type in caches)
                    type.LoadCache();
                cache.Value = new ConcurrentDictionary<object, ITypeData>();
            }
            
            public void Dispose()
            {
                cache.Value = null;
                foreach (var cache in caches)
                    cache.UnloadCache();
            }
        }
    }
}