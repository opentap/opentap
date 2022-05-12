//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTap.Cli;

namespace OpenTap.Package
{
    /// <summary>
    /// Base class for ICliActions that makes a copy of the installation to a temp dir before executing. Useful for making changes to the installation. 
    /// </summary>
    public abstract class IsolatedPackageAction : LockingPackageAction
    {
        /// <summary>
        /// Try to force execution in spite of errors. When true the action will execute even when isolation cannot be achieved.
        /// </summary>
        [CommandLineArgument("force", Description = "Try to run in spite of errors.", ShortName = "f")]
        public bool Force { get; set; }

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
                if (File.Exists(Target))
                {
                    log.Error("Destination directory \"{0}\" is a file.", Target);
                    return (int)ExitCodes.ArgumentError;
                }
                FileSystemHelper.EnsureDirectory(Target);
            }

            return base.Execute(cancellationToken);
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
    }
}
