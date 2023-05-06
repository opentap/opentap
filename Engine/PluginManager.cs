//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Tap.Shared;

namespace OpenTap
{
    /// <summary>
    /// Static class that searches for and loads OpenTAP plugins.
    /// </summary>
    public static class PluginManager
    {
        private static readonly TraceSource log = Log.CreateSource("PluginManager");
        private static ManualResetEventSlim searchTask = new ManualResetEventSlim(true);
        private static PluginSearcher searcher;
        internal static IAssemblyResolver assemblyResolver;

        /// <summary>
        /// Specifies the directories to be searched for plugins. 
        /// Any additional directories should be added before calling <see cref="PluginManager.SearchAsync()"/>,
        /// <see cref="GetAllPlugins"/>, <see cref="GetPlugins{BaseType}"/>, <see cref="GetPlugins(Type)"/>,
        /// <see cref="LocateType(string)"/> or <see cref="LocateTypeData(string)"/>
        /// </summary>
        public static List<string> DirectoriesToSearch { get; private set; }

        /// <summary>
        /// Function signature for assembly load filters.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly (not the full name).</param>
        /// <param name="version">The assembly version.</param>
        /// <returns>true if the assembly should be loaded. False if not.</returns>
        public delegate bool AssemblyLoadFilterDelegate(string assemblyName, Version version);

        /// <summary>
        /// Adds a function that is used to filter whether an assembly should be loaded. This can be used to control which assemblies gets loaded. 
        /// This is for very advanced usage only. The filters are used in the order in which they are added.
        /// </summary>
        /// <param name="filter">The additional filter to use.</param>
        static public void AddAssemblyLoadFilter(AssemblyLoadFilterDelegate filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");
            assemblyLoadFilters.Add(filter);
        }

        static List<AssemblyLoadFilterDelegate> assemblyLoadFilters = new List<AssemblyLoadFilterDelegate> { (asm,version) => true };
        static bool shouldLoadAssembly(string asmName, Version asmVersion)
        {
            foreach (var filter in assemblyLoadFilters)
               if (!filter(asmName, asmVersion))
                   return false;
            return true;
        }

        /// <summary>
        /// Returns a list of types that implement a specified plugin base type.
        /// This will load the assembly containing the type, if not already loaded.
        /// This will search for plugins if not done already (e.g. using <see cref="PluginManager.SearchAsync()"/>)
        /// </summary>
        /// <param name="pluginBaseType">only looks for types descending from pluginBaseType.</param>
        public static ReadOnlyCollection<Type> GetPlugins(Type pluginBaseType)
        {
            return PluginFetcher.GetPlugins(pluginBaseType);
        }

        /// <summary> Class for caching the result of GetPlugins(type).</summary>
        static class PluginFetcher
        {
            public static ReadOnlyCollection<Type> GetPlugins(Type pluginBaseType)
            {
                
                if (searcher == null ||  lastUsedSearcher != searcher)
                {
                    lock (pluginsSelection)
                    {
                        if (searchTask == null)
                            Search(); // if a search has not yet been started, do it now.

                        if (lastUsedSearcher != searcher)
                        {
                            lastUsedSearcher = searcher;
                            pluginsSelection.InvalidateAll();
                        }
                    }
                }
                return pluginsSelection.Invoke(pluginBaseType);
            }
            static readonly ReadOnlyCollection<Type> emptyTypes = Array.Empty<Type>().ToList().AsReadOnly();
            static ReadOnlyCollection<Type> getPlugins(Type pluginBaseType)
            {
                if (pluginBaseType == null)
                    throw new ArgumentNullException("pluginBaseType");

                
                PluginSearcher searcher = GetSearcher(); // Wait for the search to complete (and get the result)
                var unloadedPlugins = PluginManager.GetPlugins(searcher, pluginBaseType.FullName);
                if (unloadedPlugins.Count == 0)
                    return emptyTypes;

                int notLoadedTypesCnt = unloadedPlugins.Count(pl => pl.Status == LoadStatus.NotLoaded);
                if(notLoadedTypesCnt > 0)
                {
                    var notLoadedAssembliesCnt = unloadedPlugins.Select(x => x.Assembly).Distinct().Where(asm => asm.Status == LoadStatus.NotLoaded).ToArray();
                    if (notLoadedAssembliesCnt.Length > 0)
                    {
                        notLoadedAssembliesCnt.AsParallel().ForAll(asm => asm.Load());
                    }
                }
                IEnumerable<TypeData> plugins = unloadedPlugins;
                if (notLoadedTypesCnt > 8)
                {
                    // only find types in parallel if there are sufficiently many.
                    plugins = plugins
                    .AsParallel() // This is around 50% faster when many plugins are loaded in parallel.
                    .AsOrdered(); // ensure the order is the same as before.
                }

                return plugins
                    .Select(td => td.Load())
                    .Where(x => x != null)
                    .ToList()
                    .AsReadOnly();
            }

            static PluginSearcher lastUsedSearcher = null;
            static Memorizer<Type, ReadOnlyCollection<Type>> pluginsSelection = new Memorizer<Type, ReadOnlyCollection<Type>>(getPlugins);
        }

        static ICollection<TypeData> GetPlugins(PluginSearcher searcher, string baseTypeFullName)
        {
            TypeData baseType;
            if (!searcher.AllTypes.TryGetValue(baseTypeFullName, out baseType))
                return Array.Empty<TypeData>();

            if (baseType.DerivedTypes == null)
                return Array.Empty<TypeData>();

            var specializations = new List<TypeData>();

            foreach (TypeData st in baseType.DerivedTypes)
            {
                if (st.TypeAttributes.HasFlag(TypeAttributes.Interface) || st.TypeAttributes.HasFlag(TypeAttributes.Abstract))
                    continue;
                if(shouldLoadAssembly(st.Assembly.Name, st.Assembly.Version))
                    specializations.Add(st);
            }
            return specializations;
        }
        static object startSearcherLock = new object();
        /// <summary>
        /// Returns the <see cref="PluginSearcher"/> used to search for plugins.
        /// This will search for plugins if not done already (i.e. call and wait for <see cref="PluginManager.SearchAsync()"/>)
        /// </summary>
        public static PluginSearcher GetSearcher()
        {
            
            searchTask.Wait();
            if (searcher == null)
            {
                // If the search task has not been started, do it now.
                lock (startSearcherLock)
                {
                    if(searcher == null)
                        Search();
                }
            }
            return searcher; // Wait for the search to complete (and get the result)
        }

        /// <summary>
        /// Gets all plugins. I.e. all types that descend from <see cref="ITapPlugin"/>.
        /// Abstract types and interfaces are not included.
        /// This does not require/cause the assembly containing the type to be loaded.
        /// This will search for plugins if not done already (i.e. call and wait for <see cref="PluginManager.SearchAsync()"/>)
        /// Only C#/.NET types are returned. To also get dynamic types (from custom <see cref="ITypeDataSearcher"/>s) use <see cref="TypeData.GetDerivedTypes(ITypeData)"/> instead.
        /// </summary>
        public static ReadOnlyCollection<TypeData> GetAllPlugins()
        {
            PluginSearcher searcher = GetSearcher();
            return searcher.PluginTypes
                .Where(st =>
                    !st.TypeAttributes.HasFlag(TypeAttributes.Interface)
                    && !st.TypeAttributes.HasFlag(TypeAttributes.Abstract)
                    && st.Status != LoadStatus.FailedToLoad)
                .ToList().AsReadOnly();
        }

        /// <summary>
        /// Returns a list of types that implement a specified plugin base type.
        /// This will load the assembly containing the type, if not already loaded.
        /// This will search for plugins if not done already (i.e. call and wait for <see cref="PluginManager.SearchAsync()"/>)
        /// Only C#/.NET types are returned. To also get dynamic types (from custom <see cref="ITypeDataSearcher"/>s) use <see cref="TypeData.GetDerivedTypes(ITypeData)"/> instead.
        /// </summary>
        /// <remarks>
        /// This is just to provide a more convenient syntax compared to <see cref="GetPlugins(Type)"/>. The funcionallity is identical.
        /// </remarks>  
        /// <typeparam name="BaseType">find types that descends from this type.</typeparam>
        /// <returns>A read-only collection of types.</returns>
        public static ReadOnlyCollection<Type> GetPlugins<BaseType>()
        {
            return StaticPluginTypeCache<BaseType>.Get();
        }

        /// <summary>
        /// Cache structure to lock-free optimize BaseType type lookups. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        class StaticPluginTypeCache<T>
        {
            static ReadOnlyCollection<Type> list;
            static int changeid = 0;

            public static ReadOnlyCollection<Type> Get()
            {
                var changeIdNew = PluginManager.ChangeID;
                if (changeid != changeIdNew)
                {
                    list = null;
                }

                if (list == null)
                {
                    list = GetPlugins(typeof(T));
                    changeid = changeIdNew;
                }

                return list;
            }
        }
        
        /// <summary>
        /// Gets the AssemblyData for the OpenTap.dll assembly.
        /// This will search for plugins if not done already (i.e. call and wait for <see cref="PluginManager.SearchAsync()"/>)
        /// </summary>
        public static AssemblyData GetOpenTapAssembly()
        {
            PluginSearcher searcher = GetSearcher();
            return searcher.PluginMarkerType.Assembly;
        }
        /// <summary>
        /// Start a search task that finds plugins to the platform.
        /// This call is not blocking, some other calls to PluginManager will automatically 
        /// wait for this task to finish (or even start it if it hasn't been already). These calls 
        /// include <see cref="GetAllPlugins"/>, <see cref="GetPlugins{BaseType}"/>, 
        /// <see cref="GetPlugins(Type)"/>, <see cref="LocateType(string)"/> and <see cref="LocateTypeData(string)"/>
        /// </summary>
        public static Task SearchAsync()
        {
            searchTask.Reset();
            searcher = null;
            ChangeID++;
            TapThread.Start(Search);  
            return Task.Run(() => GetSearcher());
        }
        
        ///<summary>Searches for plugins.</summary>
        public static void Search(){
            searchTask.Reset();
            searcher = null;
            assemblyResolver.Invalidate(DirectoriesToSearch);
            ChangeID++;
            try
            {
                IEnumerable<string> fileNames = assemblyResolver.GetAssembliesToSearch();
                searcher = SearchAndAddToStore(fileNames);
            }
            catch (Exception e)
            {
                log.Error("Caught exception while searching for plugins: '{0}'", e.Message);
                log.Debug(e);
                searcher = null;
            }
            finally
            {
                searchTask.Set();
            }
        }

        static bool isLoaded = false;
        static object loadLock = new object();
        /// <summary> Sets up the PluginManager assembly resolution systems. Under normal circumstances it is not needed to call this method directly.</summary>
        internal static void Load()
        {
            lock (loadLock)
            {
                if (isLoaded) return;
                isLoaded = true;

                SessionLogs.Initialize();

                string tapEnginePath = Assembly.GetExecutingAssembly().Location;
                if(String.IsNullOrEmpty(tapEnginePath))
                {
                    // if OpenTap.dll was loaded from memory/bytes instead of from a file, it does not have a location.
                    // This is the case if the process was launched through tap.exe. 
                    // In that case just use the location of tap.exe, it is the same
                    tapEnginePath = Assembly.GetEntryAssembly().Location;
                }
                DirectoriesToSearch = new List<string> { Path.GetDirectoryName(tapEnginePath) };
                if (PluginSearcher.CurrentRuntime == TargetFramework.Net)
                {
                    var assemblyName = "OpenTap.MegaEngine.dll";
                    foreach (var assembly in new[]
                             {
                                 Path.Combine(ExecutorClient.ExeDir, assemblyName),
                                 Path.Combine(ExecutorClient.ExeDir, "Packages", "OpenTAP", assemblyName)
                             })
                    {
                        if (File.Exists(assembly))
                        {
                            try
                            {
                                var symbols = Path.ChangeExtension(assembly, "pdb");
                                Assembly asm = null;
                                var rawAssembly = File.ReadAllBytes(assembly);
                                if (Utils.IsDebugBuild && File.Exists(symbols))
                                {
                                    var rawSymbols = File.ReadAllBytes(symbols);
                                    asm = Assembly.Load(rawAssembly, rawSymbols);
                                }
                                else
                                {
                                    asm = Assembly.Load(rawAssembly);
                                }

                                var resolver = asm.GetTypes().First(t => t.Name == "AssemblyLoadContextResolver");
                                var instance = Activator.CreateInstance(resolver, DirectoriesToSearch);
                                assemblyResolver = instance as IAssemblyResolver;
                                break;
                            }
                            catch
                            {
                                log.Warning($"Failed to load .NET 6 assembly resolver. Using fallback resolver.");
                            }
                        }
                    }
                }


                assemblyResolver ??= new TapAssemblyResolver(DirectoriesToSearch);
                
                log.Info($"Using assembly resolver: {assemblyResolver.GetType().Name}");

                // Custom Assembly resolvers.
                AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assemblyResolver.AddAssembly);
                AppDomain.CurrentDomain.AssemblyLoad += (s, args) => assemblyResolver.AddAssembly(args.LoadedAssembly);
                AppDomain.CurrentDomain.AssemblyResolve += (s, args) => assemblyResolver.Resolve(args.Name, false);
                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (s, args) => assemblyResolver.Resolve(args.Name, true);
            }
        }
        
        /// <summary> Calls PluginManager.Load </summary>
        static PluginManager()
        {
            Load();
        }

        /// <summary>
        /// Searches the files in fileNames for dlls implementing <see cref="ITapPlugin"/>
        /// and puts the implementation in the appropriate list.
        /// </summary>
        /// <param name="_fileNames">List of files to search.</param>
        static PluginSearcher SearchAndAddToStore(IEnumerable<string> _fileNames)
        {
            var fileNames = _fileNames.ToList();
            Stopwatch timer = Stopwatch.StartNew();
            PluginSearcher searcher = new PluginSearcher();
            try
            {
                var w2 = Stopwatch.StartNew();
                IEnumerable<TypeData> foundPluginTypes = searcher.Search(fileNames);
                IEnumerable<AssemblyData> foundAssemblies = foundPluginTypes.Select(p => p.Assembly).Distinct();
                log.Debug(w2, "Found {0} plugin assemblies containing {1} plugin types.", foundAssemblies.Count(), foundPluginTypes.Count());

                foreach (AssemblyData asm in foundAssemblies)
                {
                    assemblyResolver.AddAssembly(asm.Name, asm.Location);
                    
                    if (asm.Location.Contains(AppDomain.CurrentDomain.BaseDirectory))
                        log.Debug("Found version {0,-16} of {1}", asm.SemanticVersion?.ToString() ?? asm.Version?.ToString(), Path.GetFileName(asm.Location));
                    else
                        // log full path of assembly if it was loaded with --search from another directory.
                        log.Debug("Found version {0,-16} of {1} from {2}", asm.SemanticVersion?.ToString() ?? asm.Version?.ToString(), Path.GetFileName(asm.Location), asm.Location);
                }
            }
            catch (Exception ex)
            {
                log.Error("Plugin search failed for: " + String.Join(", ", fileNames));
                log.Debug(ex);
            }
            log.Debug(timer, "Searched {0} Assemblies.", fileNames.Count);


            if (GetPlugins(searcher, typeof(IInstrument).FullName).Count == 0)
                log.Warning("No instruments found.");
            if (GetPlugins(searcher, typeof(ITestStep).FullName).Count == 0)
                log.Warning("No TestSteps found.");
            return searcher;
        }

        private static Type locateType(string typeName)
        {
            PluginSearcher searcher = GetSearcher();

            if (string.IsNullOrWhiteSpace(typeName))
                return null;
            if ((typeName.Length > 2) && (typeName[typeName.Length - 2] == '[') && (typeName[typeName.Length - 1] == ']'))
            {
                var elemType = LocateType(typeName.Substring(0, typeName.Length - 2));
                if (elemType != null)
                    return elemType.MakeArrayType();
                else return null;
            }
            else if (searcher.AllTypes.ContainsKey(typeName))
            {
                TypeData t = searcher.AllTypes[typeName];
                var loaded = t.Load();
                if (loaded != null)
                    return loaded;
            }

            if (typeName.Contains(","))
            {
                var x = locateType(typeName.Split(',')[0]);
                if (x != null) return x;
            }

            return Type.GetType(typeName);
        }

        /// <summary>
        /// Searches through found plugins. Returning the System.Type matching the given name if such a type is found
        /// in any assembly in <see cref="DirectoriesToSearch"/> or mscorelib - otherwise null (e.g. for types located in the GAC)
        /// This will load the assembly containing the type, if not already loaded.
        /// This will search for plugins if not done already (i.e. call and wait for <see cref="PluginManager.SearchAsync()"/>)
        /// </summary>
        public static Type LocateType(string typeName)
        {
            // For compatibility with 8.x testplans in which e.g. OpenTap.BasicSteps.DelayStep was called Keysight.Tap.BasicSteps.DelayStep
            var type = locateType(typeName);
            if (type == null && typeName.StartsWith("Keysight.Tap."))
                type = locateType(typeName.Replace("Keysight.Tap.", "OpenTap."));
            return type;
        }

        /// <summary>
        /// Searches through found plugins. Returns the <see cref="OpenTap.TypeData"/> matching the given name if such a type is found
        /// in any assembly in <see cref="DirectoriesToSearch"/> - otherwise null (e.g. for types located in the GAC).
        /// This does not require/cause the assembly containing the type to be loaded.
        /// This will search for plugins if not done already (i.e. call and wait for <see cref="PluginManager.SearchAsync()"/>)
        /// </summary>
        [Obsolete("This only returns C#/.NET types. Use TypeData.GetDerivedTypes instead.")]
        public static TypeData LocateTypeData(string typeName)
        {
            PluginSearcher searcher = GetSearcher();
            TypeData type;
            searcher.AllTypes.TryGetValue(typeName, out type);
            return type;
        }

        #region Version ResultParameters
        static Memorizer<Assembly, ResultParameter> AssemblyVersions = new Memorizer<Assembly, ResultParameter>(GetVersionResultParameter);
        internal static int ChangeID = 0;

        private static ResultParameter GetVersionResultParameter(Assembly assembly)
        {
            string hash = " - ";

            using (var file = new FileStream(assembly.Location, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4 * 4096))
            {
                byte[] hashValue = BitConverter.GetBytes(MurMurHash3.Hash(file));

                for (int i = 0; i < 4; i++)
                {
                    hash += String.Format("{0:x2}", hashValue[i]);
                }
            }

            System.Reflection.AssemblyName name = assembly.GetName();
            return new ResultParameter("Version", name.Name, name.Version.ToString() + hash);
        }

        internal static IEnumerable<ResultParameter> GetPluginVersions(IEnumerable<object> allPluginTypes)
        {
            //Stopwatch timer = Stopwatch.StartNew();
            var assemblies = allPluginTypes.Select(item => item.GetType()).Distinct().Select(type => type.Assembly).ToHashSet();
            assemblies.Add(Assembly.GetExecutingAssembly());

            List<ResultParameter> parameters = new List<ResultParameter>();
            foreach (var asm in assemblies)
            {
                try
                {
                    if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location))
                    {
                        parameters.Add(AssemblyVersions.Invoke(asm));
                    }
                }
                catch
                {
                    // Silent catch all because we don't care too much about this parameter
                }
            }
            return new ResultParameters(parameters);
        }
        #endregion
    }
}
