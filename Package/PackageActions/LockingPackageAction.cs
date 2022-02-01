//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenTap.Cli;

namespace OpenTap.Package
{
    /// <summary>
    /// Base class for ICliActions that use a mutex to lock the Target directory for the duration of the command. 
    /// </summary>
    public abstract class LockingPackageAction : PackageAction
    {
        internal const string CommandLineArgumentRepositoryDescription =
            "Override the package repository.\n" +
            "The default is http://packages.opentap.io.";
        internal const string CommandLineArgumentVersionDescription =
            "Semantic version (semver) of the package.\n" +
            "The default is to select only non pre-release packages for the current OS and CPU architecture.\n" +
            "'x'     select all (non pre-release) versions >= x.0.0 and < (x+1).0.0,\n" +
            "'x.y'   select all (non pre-release) versions >= x.y.0 and < x.(y+1).0,\n" +
            "'^x.y'  select all (non pre-release) versions >= x.y.0 and < (x+1).0.0,\n" +
            "'x.y.z' only match the exact version.\n" +
            "Use 'any', 'beta', or 'rc' to match 'any', 'beta', or 'rc' pre-release versions and above.";
        internal const string CommandLineArgumentOsDescription =
            "Override the OS (Linux, Windows, MacOS) to target.\n" +
            "The default is the current OS.";
        internal const string CommandLineArgumentArchitectureDescription =
            "Override the CPU architecture (x86, x64, AnyCPU) to target.\n" +
            "The default is the current CPU architecture.";

        /// <summary>
        /// Unlockes the package action to allow multiple running at the same time.
        /// </summary>
        public bool Unlocked { get; set; }

        /// <summary>
        /// The location to apply the command to. The default is the location of OpenTap.PackageManager.exe
        /// </summary>
        [CommandLineArgument("target", Description = "Override the location where the command is applied.\nThe default is the OpenTAP installation directory.", ShortName = "t")]
        public string Target { get; set; }


        internal static string GetLocalInstallationDir()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        /// <summary>
        /// Get the named mutex used to lock the specified OpenTAP installation directory while it is being changed.
        /// </summary>
        /// <param name="target">The OpenTAP installation directory</param>
        /// <returns></returns>
        public static Mutex GetMutex(string target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var fullDir = Path.GetFullPath(target).Replace('\\', '/');
            var hasher = SHA256.Create();
            var hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(fullDir));

            return new Mutex(false, "Keysight.Tap.Package InstallLock " + BitConverter.ToString(hash).Replace("-", ""));
        }
        
        /// <summary>
        /// Executes this the action. Derived types should override LockedExecute instead of this.
        /// </summary>
        /// <returns>Return 0 to indicate success. Otherwise return a custom errorcode that will be set as the exitcode from the CLI.</returns>
        public override int Execute(CancellationToken cancellationToken)
        {
            if (String.IsNullOrEmpty(Target))
                Target = GetLocalInstallationDir();
            else
                Target = Path.GetFullPath(Target.Trim());
            if (!Directory.Exists(Target))
            {
                log.Error("Destination directory \"{0}\" does not exist.", Target);
                return (int)ExitCodes.ArgumentError;
            }

            using (Mutex state = GetMutex(Target))
            {
                bool useLocking = Unlocked == false;
                if (useLocking && !state.WaitOne(0))
                {
                    log.Info("Waiting for other package manager operation to complete.");
                    try
                    {
                        switch (WaitHandle.WaitAny(new WaitHandle[] { state, cancellationToken.WaitHandle }, 120000))
                        {
                            case 0: // we got the mutex
                                break;
                            case 1: // user cancelled
                                throw new ExitCodeException(5, "User aborted while waiting for other package manager operation to complete.");
                            case WaitHandle.WaitTimeout:
                                throw new ExitCodeException(6, "Timeout after 2 minutes while waiting for other package manager operation to complete.");
                        }
                    }
                    catch (AbandonedMutexException)
                    {
                        // Another package manager exited without releasing the mutex. We can should be able to take it now.
                        if (!state.WaitOne(0))
                            throw new ExitCodeException(7, "Unable to run while another package manager operation is running.");
                    }
                }

                try
                {
                    return LockedExecute(cancellationToken);
                }
                finally
                {
                    if(useLocking)
                        state.ReleaseMutex();
                }
            }
        }

        /// <summary>
        /// The code to be executed by the action while the Target directory is locked.
        /// </summary>
        /// <returns>Return 0 to indicate success. Otherwise return a custom errorcode that will be set as the exitcode from the CLI.</returns>
        protected abstract int LockedExecute(CancellationToken cancellationToken);

        
        /// <summary>
        /// Only here for compatibility. Use IsolatedPackageAction instead of calling this.
        /// </summary>
        [Obsolete("Inherit from IsolatedPackageAction instead.")]
        public static bool RunIsolated(string application = null, string target = null)
        {
            try
            {
                IsolatedPackageAction.RunIsolated(application,target);
                return true;
            }
            catch(InvalidOperationException)
            {
                return false;
            }
        }
    }

}

