using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace OpenTap.NetCoreAssemblyLoader
{
    public class AssemblyLoadSession
    {
        public static Session CreateSession()
        {
            var parentContext = PluginManager.assemblyLoader.Value;
            var s = Session.Create(SessionOptions.None);
            PluginManager.assemblyLoader.Value = new SessionLoadContext(s.Id.ToString(), parentContext, true);
            return s;
        }
    }

    public class SessionLoadContext : IAssemblyLoader, IDisposable
    {
        private static TraceSource log = Log.CreateSource(nameof(SessionLoadContext));
        private AssemblyLoadContext ctx;
        public WeakReference refCtx;
        private string Name { get; }

        internal SessionLoadContext(string name, IAssemblyLoader parent, bool isCollectible)
        {
            Name = name;
            ctx = new AssemblyLoadContext(name, isCollectible);
            refCtx = new WeakReference(ctx);
        }

        public string GetAssemblyLocation(Assembly asm)
        {
            if (asm.IsDynamic) return null;
            return asm.Location;
        }

        public Assembly LoadAssembly(string path)
        {
            if (ctx == null)
                throw new ObjectDisposedException($"Load context '{Name}' has been disposed.");

            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetLocation() == path);
            if (loaded != null) return loaded;

            return ctx.LoadFromAssemblyPath(path);
        }

        public void Dispose()
        {
            var sw = Stopwatch.StartNew();
            // The unload needs to happen inside of a nested method. Otherwise the intermediate reference
            // to the AssemblyLoadContext from 'ctx.Target()' will sit on the stack and hold a reference to the load// context
            [MethodImpl(MethodImplOptions.NoInlining)]
            void unload(out WeakReference weak)
            {
                weak = new WeakReference(ctx);
                ctx.Unload();
                ctx = null;
            }

            unload(out var wr);

            int i = 0;
            for (i = 0; wr.IsAlive && i < 100; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (wr.IsAlive)
                log.Warning($"Load context '{Name}' was not unloaded: Strong GC handles are preventing the unload. " +
                            $"The load context will be unloaded once the handles have been released and the garbage collector runs again.");
            else
            {
                log.Info($"Load context '{Name}' was unloaded.");
                log.Debug(sw, $"Unload succeeded after {i} GC iterations");
            }
        }
    }
}