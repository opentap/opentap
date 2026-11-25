using System;
using System.Reflection;

namespace OpenTap
{
    /// <summary>Provides context-local application environment and shutdown notifications.</summary>
    public static class ApplicationContext
    {
        static Assembly entryAssembly;
        static string baseDirectory;
        static int disposing;

        /// <summary>Raised once when this hosted instance is disposed, before cooperative thread cancellation.
        /// Handlers run synchronously on the disposing thread, cannot cancel disposal, and should not block indefinitely.
        /// Exceptions are reported without preventing other handlers or engine shutdown. Not raised for ordinary process exit.</summary>
        public static event EventHandler Disposing;

        // Engine services that must remain available during Disposing (notably logging)
        // release their resources in the final phase.
        internal static event EventHandler Disposed;

        internal static void DisposeInstance()
        {
            if (System.Threading.Interlocked.Exchange(ref disposing, 1) != 0) return;
            var handlers = Disposing;
            Disposing = null;
            Notify(handlers);
            try { TapThread.Shutdown(); }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); }
            handlers = Disposed;
            Disposed = null;
            Notify(handlers);
        }

        static void Notify(EventHandler handlers)
        {
            if (handlers == null) return;
            foreach (EventHandler handler in handlers.GetInvocationList())
            {
                try { handler(null, EventArgs.Empty); }
                catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); }
            }
        }

        /// <summary>Gets this instance's logical entry assembly, or the process entry assembly when not bootstrapped.</summary>
        public static Assembly GetEntryAssembly() => entryAssembly ?? Assembly.GetEntryAssembly();

        /// <summary>Gets the installation directory for this instance, or the application domain base directory.</summary>
        public static string BaseDirectory => baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;

        // Called on the new engine copy, before any engine services are initialized.
        internal static void ConfigureEnvironment(Assembly entry, string directory)
        {
            entryAssembly = entry;
            baseDirectory = directory;
        }

    }
}
