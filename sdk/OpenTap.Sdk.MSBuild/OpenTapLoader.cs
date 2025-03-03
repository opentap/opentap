using System;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenTap;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    /// <summary>
    /// Build actions which require OpenTAP should enter this context before referencing any OpenTAP types.
    /// This is necessary because the OpenTAP dll is not in the same directory as this dll.
    /// In addition, this context initializes session logs with a specific name, and with
    /// a flag that allows them to be deleted so e.g. bin/Debug can be deleted without errors. 
    /// </summary>
    class OpenTapContext : IDisposable
    {
        // The static portion of this type MUST NOT reference OpenTAP.
        #region Static Members

        private static object loadLock = new object();
        private static void loadOpenTap(string tapDir, string runtimeDir)
        {
            // This alters the value returned by 'ExecutorClient.ExeDir' which would otherwise return the location of
            // OpenTap.dll which in an MSBuild context would be the nuget directory which leads to unexpected behavior
            // because the expected location is the build directory in all common use cases.
            Environment.SetEnvironmentVariable("OPENTAP_INIT_DIRECTORY", tapDir, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("OPENTAP_NO_UPDATE_CHECK", "true");
            Environment.SetEnvironmentVariable("OPENTAP_DEBUG_INSTALL", "true");
            
            lock (loadLock)
            {
                var loaded = AppDomain.CurrentDomain.GetAssemblies().Select(l => l.GetName().Name).ToArray();
                string[] assemblies = { "OpenTap", "OpenTap.Package" };
                foreach (var asmName in assemblies)
                {
                    if (loaded.Contains(asmName))
                        continue; // already loaded - continue

                    Assembly.LoadFrom(Path.Combine(runtimeDir, $"{asmName}.dll"));
                }
            }
        }

        /// <summary>
        /// Help the runtime resolve OpenTAP. The correct OpenTAP dll is in a subdirectory and will not be found by
        /// the default resolver. Also, the resolver will look for the debug version (9.4.0.0) because that's what this
        /// assembly was compiled against. This is sort of a hack, but it should be fine.
        /// </summary>
        /// <param name="runtimeDir"></param>
        public static IDisposable Create(string tapDir, string runtimeDir)
        {
            loadOpenTap(tapDir, runtimeDir);
            return new OpenTapContext(runtimeDir);
        }
        
        /// <summary>
        /// Increment a number until we find an available filename.
        /// </summary>
        /// <param name="logName"></param>
        /// <returns></returns>
        static string numberedFileName(string logName)
        {
            var logNameNoExt = Path.Combine(Path.GetDirectoryName(logName), Path.GetFileNameWithoutExtension(logName));
            var num = 1;
            while (File.Exists(logName))
            {
                logName = $"{logNameNoExt} ({num}).txt";
                num += 1;
            }

            return logName;
        }
        
        #endregion

        #region Instance Members
        
        /// <summary>
        /// Calling this method forces the runtime to resolve OpenTAP because SessionLogs is from the OpenTAP assembly.
        /// </summary>
        public OpenTapContext(string tapDir)
        {
            var buildProc = System.Diagnostics.Process.GetCurrentProcess();
            var timestamp = buildProc.StartTime.ToString("yyyy-MM-dd HH-mm-ss");
            var pid = buildProc.Id;

            string pathEnding = $"SessionLog.{pid} {timestamp}";

            if (Assembly.GetEntryAssembly() != null && !String.IsNullOrWhiteSpace(Assembly.GetEntryAssembly().Location))
            {
                string exeName = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
                // Path example: <TapDir>/SessionLogs/tap/tap <timestamp>.txt
                pathEnding = $"{exeName}.{pid} {timestamp}";
            }

            var logName = Path.GetFullPath(Path.Combine(tapDir, "SessionLogs", pathEnding + ".txt"));
            logName = numberedFileName(logName);

            SessionLogs.Initialize(logName, true);
        }

        /// <summary>
        /// Ensure the session log is flushed when this context is disposed.
        /// </summary>
        public void Dispose()
        {
            Log.Flush();
            SessionLogs.Flush();
        }
        #endregion
    }
}
