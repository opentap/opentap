using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace tap;

static class LoadHookInstaller
{
    public static void InstallLoadHook()
    {
        AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
    }
}

class TestAssemblyLoadContext : AssemblyLoadContext
{
    private Assembly engine;
    private object assemblyResolver;
    private Type assemblyResolverType;
    private MethodInfo resolveMethod;
    private FieldInfo loadFromField;

    public TestAssemblyLoadContext() : base(isCollectible: true)
    {
        AddLoadHook();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void AddLoadHook()
    {
        var location = GetType().Assembly.Location!;
        var opentap = Path.Combine(Path.GetDirectoryName(location)!, "OpenTap.dll");
        engine = base.LoadFromAssemblyPath(opentap);
        var pluginManager = engine.GetType("OpenTap.PluginManager")!;
        var assemblyResolverField = pluginManager.GetField("assemblyResolver", BindingFlags.NonPublic | BindingFlags.Static)!;
        assemblyResolver = assemblyResolverField.GetValue(null)!;
        assemblyResolverType = assemblyResolver.GetType();
        resolveMethod = assemblyResolverType.GetMethod("Resolve", BindingFlags.Instance | BindingFlags.Public)!;

        Assembly loadFrom(string filename, bool reflectionOnly)
        {
            return LoadFromAssemblyPath(filename);
        }

        Func<string, bool, Assembly> f = loadFrom;
        
        // internal Func<string, bool, Assembly> loadFrom;
        loadFromField = assemblyResolverType.GetField("loadFrom", BindingFlags.Instance | BindingFlags.NonPublic)!;
        loadFromField.SetValue(assemblyResolver, f);
    }

    protected override Assembly? Load(AssemblyName name)
    {
        if (name.Name == "netstandard") return null;
        if (name.Name == "OpenTap") return engine;
        var asm = resolveMethod.Invoke(assemblyResolver, new object[] { name.FullName, false }) as Assembly;
        return asm;
    }
}