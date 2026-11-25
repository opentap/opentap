using System;
using System.Reflection;

namespace OpenTap
{
    // Hosts may configure these callbacks before initializing PluginManager. Without
    // explicit bootstrap, every runtime uses the legacy Assembly.LoadFrom behavior.
    // All signatures deliberately use .NET Standard types.
    internal static class AssemblyLoading
    {
        internal static bool IsHosted;
        internal static Func<string, Assembly> LoadFromPath = Assembly.LoadFrom;
        internal static Func<string, Assembly> LoadSelectedPath = Assembly.LoadFrom;
        internal static Func<string, string[]> AdditionalCandidates = name => new string[0];
        internal static Func<Assembly, bool> IsVisible = assembly => true;
        internal static Func<Assembly, string, Type> ResolveType = (assembly, name) => assembly.GetType(name, true);

        internal static Func<string, Assembly> Configure(Func<string, Assembly> loadFromPath,
            Func<string, Assembly> loadSelectedPath, Func<Assembly, bool> isVisible,
            Func<string, string[]> additionalCandidates, Func<Assembly, string, Type> resolveType)
        {
            IsHosted = true;
            LoadFromPath = loadFromPath;
            LoadSelectedPath = loadSelectedPath;
            IsVisible = isVisible;
            AdditionalCandidates = additionalCandidates;
            ResolveType = resolveType;
            PluginManager.Initialize();
            return PluginManager.ResolveAssembly;
        }
    }
}
