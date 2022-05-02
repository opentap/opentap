using System;
using System.IO;
using System.Reflection;
using OpenTap;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    class OpenTapContext : IDisposable
    {
        OpenTapContext() { }

        /// <summary>
        /// Help the runtime resolve OpenTAP. The correct OpenTAP dll is in a subdirectory and will not be found by
        /// the default resolver. Also, the resolver will look for the debug version (9.4.0.0) because that's what this
        /// assembly was compiled against. This is sort of a hack, but it should be fine.
        /// </summary>
        public static IDisposable Create()
        {
            var openTapDll = Path.Combine(thisAsmDir, "payload", "OpenTap.dll");
            var openTapPackageDll = Path.Combine(thisAsmDir, "payload", "OpenTap.Package.dll");
            
            Assembly resolve(object sender, ResolveEventArgs args)
            {
                if (args.Name.StartsWith("OpenTap.Package"))
                    return Assembly.LoadFile(openTapPackageDll);
                if (args.Name.StartsWith("OpenTap"))
                    return Assembly.LoadFile(openTapDll);
                return null;
            }
            
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += resolve;
                var ctx = new OpenTapContext();
                ctx.InitializeLogs();
                return ctx;
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolve;
            }
        }
        
        static string thisAsmDir => Path.GetDirectoryName(typeof(OpenTapContext).Assembly.Location);
        /// <summary>
        /// Calling this method forces the runtime to resolve OpenTAP because SessionLogs is from the OpenTAP assembly.
        /// </summary>
        private void InitializeLogs()
        {
            SessionLogs.Initialize();
        }
        
        /// <summary>
        /// Ensure the session log is closed when this context is disposed.
        /// </summary>
        public void Dispose()
        {
            SessionLogs.Deinitialize();
        }
    }
}