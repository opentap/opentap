//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Package
{
    /// <summary>
    /// Base class for ICliActions related to installing or removing packages. Useful for making changes to the installation.  Previously this made a copy of the installation to a temp dir before executing. 
    /// </summary>
    public abstract class IsolatedPackageAction : LockingPackageAction
    {
        /// <summary>
        /// Try to force execution in spite of errors. When true the action will execute even when isolation cannot be achieved.
        /// </summary>
        [CommandLineArgument("force", Description = "Try to run in spite of errors.", ShortName = "f")]
        public bool Force { get; set; }
        
        /// <summary>
        /// Avoid starting an isolated process. This can cause installations to fail if the DLLs that must be overwritten are loaded.
        /// </summary>
        [Browsable(false)]
        [CommandLineArgument("no-isolation", Description = "Avoid starting an isolated process.")]
        [Obsolete("Package actions are no longer starting isolated.")]
        public bool NoIsolation { get; set; }
        

        /// <summary>
        /// Executes this the action. Derived types should override LockedExecute instead of this.
        /// </summary>
        /// <returns>Return 0 to indicate success. Otherwise, return a custom error code that will be set as the exitcode from the CLI.</returns>
        public override int Execute(CancellationToken cancellationToken)
        {
            if (String.IsNullOrEmpty(Target))
                Target = GetLocalInstallationDir();
            else
                Target = Path.GetFullPath(Target.Trim()); 
            if (!Directory.Exists(Target))
            {
                if (File.Exists(Target))
                {
                    log.Error("Destination directory \"{0}\" is a file.", Target);
                    return (int)ExitCodes.ArgumentError;
                }
                FileSystemHelper.EnsureDirectoryOf(Target);
            }

            return base.Execute(cancellationToken);
        }

        internal static bool TryFindParentInstallation(string targetDirectory, out string parent)
        {
            var dir = new DirectoryInfo(targetDirectory).Parent;
            while (dir != null)
            {
                if (dir.EnumerateFiles("OpenTap.dll").Any())
                {
                    parent = dir.FullName;
                    return true;
                }
                dir = dir.Parent;
            }
            parent = null;
            return false;
        }


        private static string GetChangeFile(string target) => Path.Combine(target, "Packages", ".changeId");
        internal static long GetChangeId(string target)
        {
            var filePath = GetChangeFile(target);
            if (File.Exists(filePath))
                if (long.TryParse(File.ReadAllText(filePath), out var changeId))
                    return changeId;
            return 0;
        }

        private static void EnsureDirectory(string filePath) => Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        
        /// <summary> This notifies the rest of the system that the package configuration has changed. </summary>
        internal static void IncrementChangeId(string target)
        {
            var filePath = GetChangeFile(target);
            long changeId = GetChangeId(target);
            changeId += 1;

            try
            {
                EnsureDirectory(filePath);
                File.WriteAllText(filePath, changeId.ToString());
            }
            catch (Exception ex)
            {
                log.Warning($"Failed writing Change ID to {filePath}");
                log.Debug(ex);
            }
        }

        // This method is only called by the static function LockedPackageAction.RunIsolated(), which has been obsolete since 9.0
        // It is unlikely to ever be called.
        internal static void RunIsolated(string application = null, string target = null, IsolatedPackageAction action = null)
        {
            // public static bool IsExecutorMode => Environment.GetEnvironmentVariable(ExecutorSubProcess.EnvVarNames.TpmInteropPipeName) != null;
            var cmd = application ?? Assembly.GetEntryAssembly().Location;
            if (cmd.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))

            {  //.netcore wierdness.
                cmd = Path.ChangeExtension(cmd, "exe");
                if (File.Exists(cmd) == false)
                    cmd = cmd.Substring(0, cmd.Length - ".exe".Length);
            }

            // now that we start from a different dir, we need to supply a --target argument 
            string args = "";
            if (target != null)
                args = $"--target \"{target}\"";

            var startInfo = new ProcessStartInfo()
            {
                FileName = cmd, 
                Arguments = args,
                UseShellExecute = false,
            };
            var proc = Process.Start(startInfo);
            proc?.WaitForExit();
        }
    }
}
