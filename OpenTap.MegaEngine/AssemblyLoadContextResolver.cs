using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using OpenTap.Cli;
using OpenTap.Package;

namespace OpenTap.MegaEngine;

public class SomethingThatLoadsStuff
{
    public SomethingThatLoadsStuff()
    {
        var steps = TypeData.GetDerivedTypes<ITestStep>();
        var step = steps.FirstOrDefault(s =>
            s.Name.Contains("battery", StringComparison.OrdinalIgnoreCase) && s.CanCreateInstance);
        var asm = PluginManager.assemblyResolver.Resolve("OpenTap", false);
        asm = PluginManager.assemblyResolver.Resolve("OpenTap.Plugins.Demo.Battery", false);
        Type type = asm.GetType(step.AsTypeData().Name)!;
        var instance = (ITestStep)Activator.CreateInstance(type)!;
        Console.WriteLine(instance.GetFormattedName());
    }
}
public class SomeCliAction : ICliAction
{
    public int Execute(CancellationToken cancellationToken)
    {
        void writeLoaded()
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Where(asm => 
                    asm.FullName.Contains("opentap", StringComparison.OrdinalIgnoreCase)
                    || asm.FullName.Contains("demo", StringComparison.OrdinalIgnoreCase));
            foreach (var l in loaded)
            {
                Console.WriteLine(l.FullName);
            }
        }

        Console.WriteLine("===========BEFORE===========");
        writeLoaded();

        using (Session.Create(SessionOptions.OverlayLoadContext))
        {
            new SomethingThatLoadsStuff();
            Console.WriteLine("===========DURING===========");
            writeLoaded();
        }
        
        Console.WriteLine("===========AFTER===========");
        writeLoaded();
        return 0;
    }
}

internal class DisposableLoadContext : IDisposable
{
    private TraceSource _traceSource;
    public AssemblyLoadContext Ctx { get; set; }

    public DisposableLoadContext(string name, bool collectible)
    {
        Ctx = new AssemblyLoadContext(name, false);
        _traceSource = Log.CreateSource($"Resolver {name}");
        Ctx.Unloading += _ =>
        {
            _traceSource.Info($"Unloaded assembly resolver '{name}'.");
        };
        _traceSource.Info($"Entering new load context: '{name}'.");
    }

    public void Dispose()
    {
        if (Ctx.IsCollectible)
            Ctx.Unload();
    }
}

internal class AssemblyLoadContextResolver : IAssemblyResolver
{
    private SessionLocal<TapAssemblyResolver> localResolver = new SessionLocal<TapAssemblyResolver>(false);
    private SessionLocal<DisposableLoadContext> loadContext = new SessionLocal<DisposableLoadContext>(true);
    private SessionLocal<int> nestingLevel = new SessionLocal<int>(1, false);
    private static TraceSource log = Log.CreateSource(nameof(AssemblyLoadContextResolver));

    private readonly string[] _directories;

    private TapAssemblyResolver tapResolver
    {
        get
        {
            // Check the session hierarchy for an assembly resolver.
            // If a session in the hierarchy has the OverlayLoadContext option set,
            // that session should be used to resolve assemblies.
            return localResolver.Value;
            
            for (var session = Session.Current; session != null; session = session.Parent)
            {
                if (session.sessionLocals.TryGetValue(localResolver, out var r) && r is TapAssemblyResolver resolver)
                    return resolver;
                
                if (session.Options.HasFlag(SessionOptions.OverlayLoadContext))
                {
                    var newResolver = new TapAssemblyResolver(_directories)
                    {
                        loadFrom = LoadFrom
                    };
                    session.sessionLocals.TryAdd(localResolver, newResolver);
                    var nesting = nestingLevel.Value + 1;
                    session.sessionLocals.TryAdd(nestingLevel, nesting);
                    var newContext = new DisposableLoadContext($"Level {nesting} resolver", true);
                    session.sessionLocals.TryAdd(loadContext, newContext);
                    return newResolver;
                }
            }
            return localResolver.Value;
        }
    }

    public void Invalidate(IEnumerable<string> directoriesToSearch)
    {
        tapResolver.Invalidate(directoriesToSearch);
    }

    private Assembly LoadFrom(string filename, bool reflectionOnly)
    {
        try
        {
            using var fs = File.OpenRead(filename);
            if (Utils.IsDebugBuild && File.Exists(Path.ChangeExtension(filename, "pdb")))
            {
                using var ss = File.OpenRead(Path.ChangeExtension(filename, "pdb"));
                return loadContext.Value.Ctx.LoadFromStream(fs, ss);
            }
            return loadContext.Value.Ctx.LoadFromStream(fs);
        }
        catch(Exception ex)
        {
            log.Debug("Unable to load {0}. {1}", filename, ex.Message);
            return null;
        }
    }

    public AssemblyLoadContextResolver(IEnumerable<string> directoriesToSearch)
    {
        _directories = directoriesToSearch.ToArray();
        localResolver.Value = new TapAssemblyResolver(_directories)
        {
            loadFrom = LoadFrom
        };
        loadContext.Value = new DisposableLoadContext("Root", false);
    }

    public string[] GetAssembliesToSearch()
    {
        return tapResolver.GetAssembliesToSearch();
    }

    public void AddAssembly(Assembly asm)
    {
        tapResolver.AddAssembly(asm);
    }

    public void AddAssembly(string name, string path)
    {
        tapResolver.AddAssembly(name, path);
    }

    public Assembly Resolve(string name, bool reflectionOnly)
    {
        return tapResolver.Resolve(name, reflectionOnly);
    }
}