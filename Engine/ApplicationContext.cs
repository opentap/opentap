using System;
using System.Reflection;

namespace OpenTap
{
    /// <summary>Provides context-local application environment information.</summary>
    public static class ApplicationContext
    {
        static Assembly entryAssembly;
        static string baseDirectory;
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
