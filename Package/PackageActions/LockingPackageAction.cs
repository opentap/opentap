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
    public abstract class LockingPackageAction : PackageAction
    {
        internal const string CommandLineArgumentRepositoryDescription = "Search this repository for packages instead of using\nsettings from 'Package Manager.xml'.";
        internal const string CommandLineArgumentVersionDescription = "Version of the package. Prepend it with '^' to specify the latest compatible version. E.g. '^9.0.0'. By omitting '^' only the exact version will be matched.";
        internal const string CommandLineArgumentOsDescription = "Override which OS to target.";
        internal const string CommandLineArgumentArchitectureDescription = "Override which CPU to target.";

        /// <summary>
        /// Unlockes the package action to allow multiple running at the same time.
        /// </summary>
        public bool Unlocked { get; set; }

        /// <summary>
        /// The location to apply the command to. The default is the location of OpenTap.PackageManager.exe
        /// </summary>
        [CommandLineArgument("target", Description = "The location where the command is applied. The default is the directory of the application itself.", ShortName = "t")]
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

        public override int Execute(CancellationToken cancellationToken)
        {
            if (String.IsNullOrEmpty(Target))
                Target = GetLocalInstallationDir();
            else
                Target = Path.GetFullPath(Target.Trim());
            if (!Directory.Exists(Target))
            {
                log.Error("Destination directory \"{0}\" does not exist.", Target);
                return -1;
            }

            using (Mutex state = GetMutex(Target))
            {
                if (Unlocked == false && !state.WaitOne(0))
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
                    catch(AbandonedMutexException)
                    {
                        // Another package manager exited without releasing the mutex. We can should be able to take it now.
                        if(!state.WaitOne(0))
                            throw new ExitCodeException(7, "Unable to run while another package manager operation is running.");
                    }
                }

                return LockedExecute(cancellationToken);
            }
        }

        protected abstract int LockedExecute(CancellationToken cancellationToken);

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

