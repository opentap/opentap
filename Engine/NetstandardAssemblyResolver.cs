using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenTap;

/// <summary>
/// This is the legacy .NET Framework compatible assembly resolver. It exists
/// as a fallback resolver if AssemblyLoadContext is not available.
/// With this resolver, assemblies are loaded into the AppDomain.
/// </summary>
class NetstandardAssemblyResolver : IAssemblyResolver
{
    private TapAssemblyResolver assemblyResolver = null;

    private NetstandardAssemblyResolver()
    {
        assemblyResolver = new TapAssemblyResolver(PluginManager.DirectoriesToSearch);

        // Custom Assembly resolvers.
        AppDomain.CurrentDomain.GetAssemblies().ToList().ForEach(assemblyResolver.AddAssembly);
        AppDomain.CurrentDomain.AssemblyLoad += (s, args) => assemblyResolver.AddAssembly(args.LoadedAssembly);
        AppDomain.CurrentDomain.AssemblyResolve += (s, args) => assemblyResolver.Resolve(args.Name, false);
        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (s, args) => assemblyResolver.Resolve(args.Name, true);
    }

    private static NetstandardAssemblyResolver _resolver = null;
    private static readonly object createlock = new();
    public static NetstandardAssemblyResolver GetResolver()
    {
        if (_resolver == null)
        {
            lock (createlock)
            {
                if (_resolver == null)
                { 
                    _resolver = new();
                }
            }
        }

        return _resolver;
    }

    public void Invalidate(IEnumerable<string> directoriesToSearch) => assemblyResolver.Invalidate(directoriesToSearch);
    public void AddAssembly(string name, string path) => assemblyResolver.AddAssembly(name, path);
    public void AddAssembly(Assembly asm) => assemblyResolver.AddAssembly(asm);
    public string[] GetAssembliesToSearch() => assemblyResolver.GetAssembliesToSearch();
}

