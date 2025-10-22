using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTap.Cli;

namespace OpenTap.Package
{
    /// <summary>
    /// Checks packages for updates
    /// </summary>
    [Browsable(false)]
    [Display("check-updates", "Checks for update for installed packages.", "package")]
    internal class UpdateCheck : ICliAction
    {
        /// <summary>
        /// Used to specify if PackageManagerSettings.CheckForUpdates should be checked before executing.
        /// </summary>
        [Browsable(false)]
        [CommandLineArgument("startup")]
        public bool Startup { get; set; }

        static readonly TraceSource Log = OpenTap.Log.CreateSource("UpdateCheck");

        private void CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            string noUpdateCheckEnv = Environment.GetEnvironmentVariable("OPENTAP_NO_UPDATE_CHECK");
            bool noUpdateCheck = noUpdateCheckEnv == "true" || noUpdateCheckEnv == "1";

            if (Startup && (PackageManagerSettings.Current.CheckForUpdates == false || noUpdateCheck))
                return;

            // Since we are deciding to do the update check for the parent process there is no reason to also
            // do it for the child processes.
            Environment.SetEnvironmentVariable("OPENTAP_NO_UPDATE_CHECK", "true");
            string noUpdateMessageEnv = Environment.GetEnvironmentVariable("OPENTAP_NO_UPDATE_MESSAGE");
            bool noUpdateMessage = noUpdateMessageEnv == "true" || noUpdateMessageEnv == "1";

            var timer = Stopwatch.StartNew();
            List<PackageDef> updates = new List<PackageDef>();

            var installation = new Installation(ExecutorClient.ExeDir);
            IPackageIdentifier[] installedPackages = installation.GetPackages().ToArray();
            Parallel.ForEach(PackageManagerSettings.Current.GetEnabledRepositories(null), repo =>
            {
                try
                {
                    updates.AddRange(repo.CheckForUpdates(installedPackages, cancellationToken));
                }
                catch (Exception ex)
                {
                    if (noUpdateMessage)
                        Log.Debug("Update check against {0} failed. See debug messages for details.", repo.Url);
                    else
                        Log.Warning("Update check against {0} failed. See debug messages for details.", repo.Url);
                    Log.Debug(ex);
                }
            });

            if (noUpdateMessage)
                return;


            using var _ = CliUserInputInterface.AcquireUserInputLock();
            if (updates.Any())
            {
                Log.Info("Updates available for:");
                foreach (var update in updates.GroupBy(p => p.Name))
                {
                    var currentPackage = installedPackages.FirstOrDefault(p => p.Name == update.Key);
                    var updatedPackage = update.OrderByDescending(p => p.Version).FirstOrDefault();
                    Log.Info($" - {update.Key}: {currentPackage?.Version} -> {updatedPackage?.Version}");
                }
            }
            else
            {
                Log.Debug(timer, "Update check completed.");
                if (!Startup)
                    Log.Info("All installed packages are up to date.");
            }
        }

        /// <summary>
        /// Runs the check for updates action.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public int Execute(CancellationToken cancellationToken)
        {
            CheckForUpdatesAsync(cancellationToken);
            return (int) ExitCodes.Success;
        }
    }
}
