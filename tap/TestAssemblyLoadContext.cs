using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace tap;

class TestAssemblyLoadContext : AssemblyLoadContext
{
    private Assembly engine;
    private object assemblyResolver;
    private Type assemblyResolverType;
    private MethodInfo resolveMethod;
    private FieldInfo loadFromField;

    public TestAssemblyLoadContext() : base(isCollectible: true)
    {
    }
    
    Assembly loadFrom(string filename, bool reflectionOnly)
    {
        return LoadFromAssemblyPath(filename);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void InitializeTapResolver()
    {
        var location = GetType().Assembly.Location!;
        var opentap = Path.Combine(Path.GetDirectoryName(location)!, "OpenTap.dll");
        engine = base.LoadFromAssemblyPath(opentap);
        var pluginManager = engine.GetType("OpenTap.PluginManager")!;
        var assemblyResolverField = pluginManager.GetField("assemblyResolver", BindingFlags.NonPublic | BindingFlags.Static)!;
        assemblyResolver = assemblyResolverField.GetValue(null)!;
        assemblyResolverType = assemblyResolver.GetType();
        resolveMethod = assemblyResolverType.GetMethod("Resolve", BindingFlags.Instance | BindingFlags.Public)!;

        Func<string, bool, Assembly> f = loadFrom;
        
        // internal Func<string, bool, Assembly> loadFrom;
        loadFromField = assemblyResolverType.GetField("loadFrom", BindingFlags.Instance | BindingFlags.NonPublic)!;
        loadFromField.SetValue(assemblyResolver, f);
    }

    private static string? runtimeDirectory = null;
    private static Dictionary<string, string>? fileCache = null;

    protected override Assembly? Load(AssemblyName name)
    {
        try
        {
            if (name.Name == "netstandard")
            {
                return null;
            }
            if (name.Name == "OpenTap") return engine;
            if (resolveMethod == null)
            {
                runtimeDirectory ??= Path.GetDirectoryName(AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "System.Runtime")!.Location)!;
                if (runtimeDirectory != null)
                {
                    if (fileCache == null)
                    {
                        fileCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        var files = Directory.GetFiles(runtimeDirectory, "*.dll", SearchOption.TopDirectoryOnly);
                        files = Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly()!.Location)!,
                            "*.dll",
                            SearchOption.AllDirectories).Concat(files).ToArray();
                        foreach (var f in files)
                        {
                            var n = Path.GetFileNameWithoutExtension(f);
                            if (fileCache.ContainsKey(n) == false)
                                fileCache[n] = f;
                        }
                    }

                    if (fileCache.TryGetValue(name.Name!, out var path))
                        return LoadFromAssemblyPath(path);
                }

                return null;
            }

            var asm = resolveMethod.Invoke(assemblyResolver, new object[] { name.FullName, false }) as Assembly;
            if (asm == null)
            {

            }

            return asm;
        }
        catch
        {
            return null;
        }
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        
        return base.LoadUnmanagedDll(unmanagedDllName);
    }
}