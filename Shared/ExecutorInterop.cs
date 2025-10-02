//            Copyright Keysight Technologies 2012-2025
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenTap
{
    
    internal static class ExecutorSubProcess
    {
        public class EnvVarNames
        {
            public static string TpmInteropPipeName = "TPM_PIPE";
            public static string ParentProcessExeDir = "TPM_PARENTPROCESSDIR";
            public static string OpenTapInitDirectory = "OPENTAP_INIT_DIRECTORY";
        }
    }


    internal static class ExecutorClient
    {
        /// <summary>
        /// Is this process an isolated sub process of tap.exe
        /// </summary>
        public static bool IsRunningIsolated => Environment.GetEnvironmentVariable(ExecutorSubProcess.EnvVarNames.ParentProcessExeDir) != null;
        /// <summary>
        /// Is this process a sub process of tap.exe
        /// </summary>
        public static bool IsExecutorMode => Environment.GetEnvironmentVariable(ExecutorSubProcess.EnvVarNames.TpmInteropPipeName) != null;
        /// <summary>
        /// The directory containing the OpenTAP installation.
        /// This is usually the value of the environment variable OPENTAP_INIT_DIRECTORY set by tap.exe
        /// If this value is not set, use the location of OpenTap.dll instead
        /// In some cases, when running isolated this is that value but from the parent process.
        /// </summary>
        public static string ExeDir
        {
            get
            {
                if (IsRunningIsolated)
                    return Environment.GetEnvironmentVariable(ExecutorSubProcess.EnvVarNames.ParentProcessExeDir);
                else
                {
                    var exePath = Environment.GetEnvironmentVariable(ExecutorSubProcess.EnvVarNames.OpenTapInitDirectory);
                    if (exePath != null)
                        return exePath;

                    // Referencing OpenTap.dll causes the file to become locked.
                    // Ensure OpenTap.dll is only loaded if the environment variable is not set.
                    // This should only happen if OpenTAP was not loaded through tap.exe.
                    return GetOpenTapDllLocation();
                }
            }
        }
        
        public static string Dotnet => _dotnet ??= mineDotnet();
        private static string _dotnet = null;
        private static string mineDotnet()
        {
            // Ensure dotnet is always assigned. "dotnet' is used as a fallback, in which case the system
            // will try to resolve it from the current PATH

            // Look for dotnet.exe on windows
            var executable = Path.DirectorySeparatorChar == '\\' ? "dotnet.exe" : "dotnet";
            try
            {
                // 1. Try to check which `dotnet` instance launched the current application
                string mainModulePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(mainModulePath) &&
                    Path.GetFileName(mainModulePath).Equals(executable, StringComparison.OrdinalIgnoreCase))
                {
                    return mainModulePath;
                }
            }
            catch
            {
                // ignore potential permission errors
            }

            try
            {
                // 2. Find dotnet based on runtime information
                var runtime = RuntimeEnvironment.GetRuntimeDirectory();
                if (!string.IsNullOrWhiteSpace(runtime) && Directory.Exists(runtime))
                {
                    var dir = new DirectoryInfo(runtime);
                    while (dir != null)
                    {
                        var candidate = System.IO.Path.Combine(dir.FullName, executable);
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }

                        dir = dir.Parent;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return "dotnet";
        } 

        [MethodImpl(MethodImplOptions.NoInlining)]
        static string GetOpenTapDllLocation() => Path.GetDirectoryName(typeof(PluginSearcher).Assembly.Location);
    }
}
