using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace OpenTap.Hosting;

// One shared dependency universe for the CLI, engine and plugins. This is not
// plugin isolation. Unloading is cooperative and requested by the lifetime handle.
sealed class OpenTapLoadContext : AssemblyLoadContext
{
    readonly string appDirectory;
    readonly Dictionary<string, string> platformAssemblies;
    Func<string, Assembly> resolver;

    public OpenTapLoadContext(string appDirectory) : base("OpenTAP", isCollectible: true)
    {
        this.appDirectory = appDirectory;
        platformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            // Only use framework entries as additional candidates. Application dependencies
            // are selected from the installation by the OpenTAP resolver.
            .Where(path => string.Equals(Path.GetDirectoryName(path),
                Path.GetDirectoryName(typeof(object).Assembly.Location), StringComparison.OrdinalIgnoreCase))
            .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    Assembly LoadFile(string path)
    {
        path = Path.GetFullPath(path);
        var name = Path.GetFileNameWithoutExtension(path);
        // A selected runtime file must use the runtime's Default copy. Loading
        // framework infrastructure privately can start permanent background threads
        // (e.g. SocketAsyncEngine) that keep the entire context alive.
        // This does not override selection: newer local package candidates still
        // load privately, even when they have the same name as a runtime assembly.
        if (platformAssemblies.TryGetValue(name, out var platformPath) &&
            string.Equals(path, Path.GetFullPath(platformPath), StringComparison.Ordinal))
            return Default.LoadFromAssemblyPath(path);
        return LoadFromAssemblyPath(path);
    }

    static bool SameIdentity(AssemblyName a, AssemblyName b) =>
        string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.CultureName ?? "", b.CultureName ?? "", StringComparison.OrdinalIgnoreCase) &&
        (a.GetPublicKeyToken() ?? Array.Empty<byte>()).SequenceEqual(b.GetPublicKeyToken() ?? Array.Empty<byte>());

    public Assembly LoadSelectedFile(string path) => LoadFromAssemblyName(AssemblyName.GetAssemblyName(path));

    bool IsVisible(Assembly assembly) => GetLoadContext(assembly) == this;

    string[] AdditionalCandidates(string name) => platformAssemblies.TryGetValue(name, out var path)
        ? new[] { path } : Array.Empty<string>();

    Type ResolveType(Assembly assembly, string name)
    {
        using (EnterContextualReflection())
            return assembly.GetType(name, throwOnError: true);
    }

    // Once unload starts the runtime temporarily roots the context itself. Do
    // not let that context retain a delegate into its collectible engine graph.
    public void ReleaseEngine() => resolver = null;

    public void ConfigureEngine(Assembly engine)
    {
        var bridge = engine.GetType("OpenTap.AssemblyLoading", throwOnError: true);
        var configure = bridge.GetMethod("Configure", BindingFlags.Static | BindingFlags.NonPublic);
        resolver = (Func<string, Assembly>)configure.Invoke(null, new object[]
        {
            (Func<string, Assembly>)LoadFile,
            (Func<string, Assembly>)LoadSelectedFile,
            (Func<Assembly, bool>)IsVisible,
            (Func<string, string[]>)AdditionalCandidates,
            (Func<Assembly, string, Type>)ResolveType
        });
    }

    protected override Assembly Load(AssemblyName name)
    {
        if (resolver != null)
            return resolver(name.FullName); // null intentionally falls back to Default.

        // Only needed while configuring the engine, before its resolver is ready.
        // Keep bootstrap infrastructure BCL-only, and search Dependencies as well.
        var candidates = Directory.EnumerateFiles(appDirectory, name.Name + ".dll", SearchOption.AllDirectories)
            .Concat(platformAssemblies.TryGetValue(name.Name, out var platformPath) ? new[] { platformPath } : Array.Empty<string>())
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Contains("obj"))
            .Select(path => (Path: path, Name: AssemblyName.GetAssemblyName(path)))
            .Where(candidate => SameIdentity(name, candidate.Name) &&
                (name.Version == null || candidate.Name.Version >= name.Version))
            .OrderByDescending(candidate => candidate.Name.Version)
            .ThenBy(candidate => candidate.Path, StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            try { return LoadFile(candidate.Path); }
            catch (Exception ex) when (ex is BadImageFormatException || ex is FileLoadException || ex is FileNotFoundException)
            {
                Console.Error.WriteLine($"Unable to load bootstrap candidate {candidate.Path}: {ex.Message}");
            }
        }
        return null;
    }
}
