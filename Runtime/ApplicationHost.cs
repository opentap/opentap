using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace OpenTap.Hosting;

// Engine configuration and shutdown use reflection against the private engine copy.
public static class ApplicationHost
{
    /// <summary>Starts application code in a private assembly graph using builder-created options.</summary>
    public static ApplicationHostContext Run(ApplicationHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var enginePath = Path.Combine(options.BaseDirectory, "OpenTap.dll");
        var applicationPath = options.AssemblyPath;
        var typeName = options.TypeName;
        var methodName = options.MethodName;
        var argument = options.Argument;
        if (!File.Exists(enginePath)) throw new FileNotFoundException("The installation does not contain OpenTap.dll.", enginePath);
        if (!File.Exists(applicationPath)) throw new FileNotFoundException("The entry assembly does not exist.", applicationPath);
        var context = new OpenTapLoadContext(Path.GetDirectoryName(enginePath));
        var lifetime = new Lifetime(context);
        object result;
        try
        {
            var engine = context.LoadFromAssemblyPath(Path.GetFullPath(enginePath));
            lifetime.shutdown = engine.GetType("OpenTap.ApplicationContext", true)
                .GetMethod("DisposeInstance", BindingFlags.NonPublic | BindingFlags.Static)
                .CreateDelegate<Action>();
            var application = string.Equals(enginePath, applicationPath, StringComparison.Ordinal)
                ? engine : context.LoadFromAssemblyPath(Path.GetFullPath(applicationPath));
            engine.GetType("OpenTap.ApplicationContext", true)
                .GetMethod("ConfigureEnvironment", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, [application, Path.GetDirectoryName(enginePath)]);
            using (context.EnterContextualReflection())
            {
                var type = application.GetType(typeName, true);
                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly,
                    null, [typeof(object)], null);
                if (method == null || (!method.IsPublic && !method.IsAssembly) || method.IsGenericMethod || method.ReturnType != typeof(object))
                    throw new ArgumentException("Entry point must be a public or internal static object method accepting exactly object.", nameof(methodName));
                context.ConfigureEngine(engine);
                result = method.Invoke(null, [argument]);
            }
            return new ApplicationHostContext(lifetime, result);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            lifetime.Dispose();
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
        catch
        {
            lifetime.Dispose();
            throw;
        }
    }

    sealed class Lifetime : IDisposable
    {
        OpenTapLoadContext context;
        internal Action shutdown;
        internal Lifetime(OpenTapLoadContext context) { this.context = context; }
        public void Dispose()
        {
            var unloading = Interlocked.Exchange(ref context, null);
            if (unloading == null) return;
            var notify = Interlocked.Exchange(ref shutdown, null);
            try
            {
                using (unloading.EnterContextualReflection())
                    notify?.Invoke();
            }
            finally
            {
                unloading.ReleaseEngine();
                unloading.Unload();
            }
            // Non-cooperative work and escaped objects may still prevent collection.
        }
    }
}
