using System;
using System.IO;
using System.Reflection;
using OpenTap;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    class SessionLogContext : IDisposable
    {
        private Action _onDispose;

        /// <summary>
        /// Increment a number until we find a filename which is not in use.
        /// </summary>
        /// <param name="logName"></param>
        /// <returns></returns>
        string numberedFileName(string logName)
        {
            var logNameNoExt = Path.GetFileNameWithoutExtension(logName);
            var num = 1;
            while (File.Exists(logName))
            {
                logName = $"{logNameNoExt} ({num}).txt";
                num += 1;
            }

            return logName;
        }

        /// <summary>
        /// Calling this method forces the runtime to resolve OpenTAP because SessionLogs is from the OpenTAP assembly.
        /// </summary>
        public SessionLogContext(string tapDir, Action OnDispose)
        {
            _onDispose = OnDispose;
            
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
        /// Ensure the session log is closed when this context is disposed.
        /// </summary>
        public void Dispose()
        {
            Log.Flush();
            SessionLogs.Flush();
            _onDispose();
        }
    }

    class OpenTapContext
    {
        /// <summary>
        /// Compute the payload directory based on the current platform
        /// </summary>
        /// <returns></returns>
        private static string CorrectPayloadDir()
        {
            var thisAsmDir = Path.GetDirectoryName(typeof(OpenTapContext).Assembly.Location)!;
            return Path.Combine(thisAsmDir, "payload");
        }

        /// <summary>
        /// Help the runtime resolve OpenTAP. The correct OpenTAP dll is in a subdirectory and will not be found by
        /// the default resolver. Also, the resolver will look for the debug version (9.4.0.0) because that's what this
        /// assembly was compiled against. This is sort of a hack, but it should be fine.
        /// </summary>
        /// <param name="tapDir"></param>
        public static IDisposable Create(string tapDir)
        {
            // This alters the value returned by 'ExecutorClient.ExeDir' which would otherwise return the location of
            // OpenTap.dll which in an MSBuild context would be the nuget directory which leads to unexpected behavior
            // because the expected location is the build directory in all common use cases.
            Environment.SetEnvironmentVariable("OPENTAP_INIT_DIRECTORY", tapDir, EnvironmentVariableTarget.Process);

            var tapInstall = Path.Combine(tapDir, "tap");
            if (File.Exists(tapInstall) == false)
                tapInstall = Path.Combine(tapDir, "tap.exe");
            if (File.Exists(tapInstall) == false)
                throw new Exception($"No tap install found in directory {tapDir}");
            
            Environment.SetEnvironmentVariable("OPENTAP_NO_UPDATE_CHECK", "true");
            Environment.SetEnvironmentVariable("OPENTAP_DEBUG_INSTALL", "true");
            
            var root = CorrectPayloadDir();
            var openTapDll = Path.Combine(root, "OpenTap.dll");
            var openTapPackageDll = Path.Combine(root, "OpenTap.Package.dll");

            Assembly resolve(object sender, ResolveEventArgs args)
            {
                if (args.Name.StartsWith("OpenTap.Package"))
                    return Assembly.LoadFile(openTapPackageDll);
                if (args.Name.StartsWith("OpenTap"))
                    return Assembly.LoadFile(openTapDll);
                return null;
            }

            AppDomain.CurrentDomain.AssemblyResolve += resolve;

            IDisposable defer()
            {
                var ctx = new SessionLogContext(tapDir, () =>
                {
                    AppDomain.CurrentDomain.AssemblyResolve -= resolve;
                });
                return ctx;
            }

            return defer();
        }
    }
}