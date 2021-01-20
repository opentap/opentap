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
        private void CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            string noUpdateCheckEnv = Environment.GetEnvironmentVariable("OPENTAP_NO_UPDATE_CHECK");
            bool noUpdateCheck = noUpdateCheckEnv == "true" || noUpdateCheckEnv == "1";
            
            if (Startup && (PackageManagerSettings.Current.CheckForUpdates == false || noUpdateCheck))
                return;
            
            var timer = Stopwatch.StartNew();
            List<PackageDef> updates = new List<PackageDef>();
            TraceSource log = Log.CreateSource("UpdateCheck");
            var installation = new Installation(ExecutorClient.ExeDir);
            IPackageIdentifier[] installedPackages = installation.GetPackages().ToArray();
            Parallel.ForEach(PackageManagerSettings.Current.Repositories, repo =>
            {
                try
                {
                    if (repo.IsEnabled == false || repo.Manager == null)
                        return;
                    updates.AddRange(repo.Manager.CheckForUpdates(installedPackages, cancellationToken));
                }
                catch (Exception ex)
                {

                    log.Warning("Update check against {0} failed. See debug messages for details.", repo.Url);
                    log.Debug(ex);
                }
            });
            if (updates.Any())
            {
                log.Info("Updates available for:");
                foreach (var update in updates.GroupBy(p => p.Name))
                {
                    var currentPackage = installedPackages.FirstOrDefault(p => p.Name == update.Key);
                    var updatedPackage = update.OrderByDescending(p => p.Version).FirstOrDefault();
                    log.Info($" - {update.Key}: {currentPackage?.Version} -> {updatedPackage?.Version}");
                }
            }
            else
            {
                log.Debug(timer, "Update check completed.");
                if (!Startup)
                    log.Info("All installed packages are up to date.");
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
            return 0;
        }
    }
}