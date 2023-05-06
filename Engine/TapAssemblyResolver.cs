using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenTap
{
    class TapAssemblyResolver : IAssemblyResolver
    {
        static readonly TraceSource log = Log.CreateSource("Resolver");

        private readonly AssemblyFinder FileFinder = new AssemblyFinder();

        /// <summary>
        /// Should be called before each search. Flushes the files found. Also sets up the directories to search.
        /// </summary>
        public void Invalidate(IEnumerable<string> directoriesToSearch)
        {
            FileFinder.Invalidate();
            FileFinder.DirectoriesToSearch = directoriesToSearch;
            lastSearchedDirs = FileFinder.DirectoriesToSearch.ToHashSet();
        }
        
        public void AddAssembly(string name, string path)
        {
            asmLookup[name] = path;
        }

        public void AddAssembly(Assembly asm)
        {
            if (asm.IsDynamic == false)
            {
                var name = Path.GetFileNameWithoutExtension(asm.Location);
                asmLookup[name] = asm.Location;
                if (Utils.IsDebugBuild)
                    log.Debug("Loaded assembly {0} from {1}", asm.FullName, asm.Location);
            }
            else
            {
                if (Utils.IsDebugBuild)
                    log.Debug("Loaded assembly {0}", asm.FullName);
            }

            assemblyResolutionMemorizer.Add(new resolveKey { Name = asm.FullName, ReflectionOnly = asm.ReflectionOnly }, asm);
        }
        HashSet<string> lastSearchedDirs = new HashSet<string>();

        public string[] GetAssembliesToSearch()
        {
            if (false == lastSearchedDirs.SetEquals(FileFinder.DirectoriesToSearch))
            {  // If directories to search has changed.
                lastSearchedDirs = FileFinder.DirectoriesToSearch.ToHashSet();
                FileFinder.Invalidate();
                assemblyResolutionMemorizer.InvalidateWhere((k, v) => v == null);
            }
            return FileFinder.AllAssemblies();
        }

        public Assembly Resolve(string name, bool reflectionOnly)
        {
            if(false == lastSearchedDirs.SetEquals(FileFinder.DirectoriesToSearch))
            {  // If directories to search has changed.
                lastSearchedDirs = FileFinder.DirectoriesToSearch.ToHashSet();
                FileFinder.Invalidate();
                assemblyResolutionMemorizer.InvalidateWhere((k,v) => v == null);
            }
            return assemblyResolutionMemorizer.Invoke(new resolveKey { Name = name, ReflectionOnly = reflectionOnly });
        }

        public TapAssemblyResolver(IEnumerable<string> directoriesToSearch)
        {
            FileFinder.DirectoriesToSearch = directoriesToSearch;
            assemblyResolutionMemorizer = new Memorizer<resolveKey, Assembly>(key => resolveAssembly(key.Name, key.ReflectionOnly))
            {
                MaxNumberOfElements = 10000,
                CylicInvokeResponse = Memorizer.CyclicInvokeMode.ReturnDefaultValue
            };
            loadFrom = DefaultLoadFrom;
        }

        /// <summary>
        /// Look up for assembly locations in case Assembly.Load cannot find it. Used for resolve assembly.
        /// </summary>
        ConcurrentDictionary<string, string> asmLookup = new ConcurrentDictionary<string, string>();

        [DebuggerDisplay("{Name} {ReflectionOnly}")]
        struct resolveKey
        {
            public string Name;
            public bool ReflectionOnly;
        }

        Memorizer<resolveKey, Assembly> assemblyResolutionMemorizer;

        // returns an assembly name or null. (if native asm, etc.)
        static AssemblyName tryGetAssemblyName(string filePath)
        {
            try
            {
                return AssemblyName.GetAssemblyName(filePath);
            }
            catch
            {
                var name = new AssemblyName();
                name.Name = Path.GetFileNameWithoutExtension(filePath);
                return name;
            }
        }

        private static Assembly DefaultLoadFrom(string filename, bool reflectionOnly)
        {
            try
            {
                if (!reflectionOnly)
                    return Assembly.LoadFrom(filename);
                return Assembly.ReflectionOnlyLoadFrom(filename);
            }
            catch(Exception ex)
            {
                log.Debug("Unable to load {0}. {1}", filename, ex.Message);
                return null;
            }
        }

        internal Func<string, bool, Assembly> loadFrom;

        Assembly resolveAssembly(string name, bool reflectionOnly)
        {
            if (name.Contains(".XmlSerializers"))
                return null;
            if (name.Contains(".resources"))
                return null;
            try
            {
                string filename;
                if (asmLookup.TryGetValue(name, out filename))
                {
                    log.Debug("Found match for {0} in {1}", name , filename);
                    return loadFrom(filename, reflectionOnly);
                }
                var requestedAsmName = new AssemblyName(name);
                var requestedStrongNameToken = requestedAsmName.GetPublicKeyToken();
                var asmName = name.Split(',')[0];

                var asmPaths = FileFinder.FindAssemblies(asmName);
                
                if (asmPaths != null && asmPaths.Length > 0)
                {
                    Assembly tryLoad(string filePath)
                    {
                        try
                        {
                            var path = Path.GetFullPath(filePath);
                            log.Debug("Found match for {0} in {1}", name, path);
                            return loadFrom(path, reflectionOnly);
                        }
                        catch (Exception)
                        {
                            //It was unable to load that specific assembly,
                            // but there might be another version that we can load.
                            return null;
                        }
                    }

                    var candidates = asmPaths.Select(p => new MatchingAssembly{Path = p, Name = tryGetAssemblyName(p)}).ToList();
                    if (requestedStrongNameToken != null && requestedStrongNameToken.Length == 8)
                    {
                        // the requested assembly has a strong name, only consider assemblies that has that
                        candidates.RemoveAll(c => false == requestedStrongNameToken.SequenceEqual(c.Name.GetPublicKeyToken()));
                    }

                    // The following logic to assembly loading is based on a significant amount of investigation, be careful when altering this.
                    // The approach is to return exact version if it exists, otherwise return the highest version available.
                    // This approach is consistent with how .NET Core resolves dependencies, if not exact version exists.


                    // Try to find/load an exact match to the requested version:
                    var matchingVersion = candidates.FirstOrDefault(c => c.Name.Version == requestedAsmName.Version);
                    if (matchingVersion.Path != null)
                    {
                        Assembly asm = tryLoad(matchingVersion.Path);
                        if (asm != null)
                            return asm;
                        candidates.Remove(matchingVersion);
                    }


                    // Try to load any remaining candidates from highest version to lowest:
                    var ordered = candidates.OrderByDescending(c => c.Name.Version);
                    foreach (var c in ordered)
                    {
                        Assembly asm = tryLoad(c.Path);
                        if (asm != null)
                            return asm;
                    }
                }
                else
                {
                    if (asmLookup.TryGetValue(asmName, out filename))
                    {
                        log.Debug("Found match for {0} in {1}", name, filename);
                        var asm = loadFrom(filename, reflectionOnly);
                        if (asm != null)
                        {
                            if (requestedStrongNameToken != null && requestedStrongNameToken.Length == 8)
                            {
                                if (requestedStrongNameToken.SequenceEqual(asm.GetName().GetPublicKeyToken()) == false)
                                    log.Warning("Using Assembly '{0}' as '{1}' (strong name mismatch)", name, asm.FullName);
                                if (asm.GetName().Version < requestedAsmName.Version)
                                    log.Warning("Using Assembly '{0}' as '{1}' (version mismatch)", name, asm.FullName);
                            }
                            return asm;
                        }
                    }
                }
            }
            catch
            { // unable to resolve, this is OK.

            }
            log.Debug("Unable to find match for {0}", name);
            return null;
        }

        /// <summary>
        /// This is used instead of a tuple in the above function. Tuples should _not_ be used in the assembly resolving process
        /// as it sometimes requires assembly resolving to load System.ValueTyple.dll.
        /// </summary>
        struct MatchingAssembly
        {
            public string Path;
            public AssemblyName Name;
        }
    }
}