using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using OpenTap;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    class SessionLogContext : IDisposable
    {
        private Action _onDispose;

        /// <summary>
        /// Calling this method forces the runtime to resolve OpenTAP because SessionLogs is from the OpenTAP assembly.
        /// </summary>
        public SessionLogContext(Action OnDispose)
        {
            PluginManager.Search();
            SessionLogs.Initialize();
            _onDispose = OnDispose;
        }

        /// <summary>
        /// Ensure the session log is closed when this context is disposed.
        /// </summary>
        public void Dispose()
        {
            SessionLogs.Deinitialize();
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
            var bitness = IntPtr.Size == 8 ? "x64" : "x86";
            var platformDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" : "linux";
            return Path.Combine(thisAsmDir, "payload", $"{platformDir}-{bitness}");
        }

        /// <summary>
        /// Help the runtime resolve OpenTAP. The correct OpenTAP dll is in a subdirectory and will not be found by
        /// the default resolver. Also, the resolver will look for the debug version (9.4.0.0) because that's what this
        /// assembly was compiled against. This is sort of a hack, but it should be fine.
        /// </summary>
        public static IDisposable Create()
        {
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
                var ctx = new SessionLogContext(() =>
                {
                    AppDomain.CurrentDomain.AssemblyResolve -= resolve;
                });
                return ctx;
            }

            return defer();
        }
    }
}