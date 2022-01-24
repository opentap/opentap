using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpenTap.Package
{
    internal class ImageDeployer
    {
        static TraceSource log = Log.CreateSource("OpenTAP");
        internal static Dictionary<PackageDef, string> cacheFileLookup = new Dictionary<PackageDef, string>();

        internal static void Deploy(Installation currentInstallation, List<PackageDef> dependencies, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Deployment operation cancelled by user");

            var currentPackages = currentInstallation.GetPackages();

            var skippedPackages = dependencies.Where(s => currentPackages.Any(p => p.Name == s.Name && p.Version.ToString() == s.Version.ToString())).ToHashSet();
            var modifyOrAdd = dependencies.Where(s => !skippedPackages.Contains(s)).ToList();
            var packagesToUninstall = currentPackages.Where(pack => !dependencies.Any(p => p.Name == pack.Name) && pack.Class.ToLower() != "system-wide").ToList(); // Uninstall installed packages which are not part of image
            var versionMismatch = currentPackages.Where(pack => dependencies.Any(p => p.Name == pack.Name && p.Version != pack.Version)).ToList(); // Uninstall installed packages where we're deploying another version

            if (!packagesToUninstall.Any() && !modifyOrAdd.Any())
            {
                log.Info($"Target installation is already up to date.");
                return;
            }

            if (!currentPackages.Any())
                log.Info($"Deploying installation in {currentInstallation.Directory}:");
            else
                log.Info($"Modifying installation in {currentInstallation.Directory}:");

            foreach (var package in skippedPackages)
                log.Info($"- Skipping {package.Name} version {package.Version} ({package.Architecture}-{package.OS}) - Already installed.");
            foreach (var package in packagesToUninstall)
                log.Info($"- Removing {package.Name} version {package.Version} ({package.Architecture}-{package.OS})");
            foreach (var package in versionMismatch)
                log.Info($"- Modifying {package.Name} version {package.Version} to {dependencies.FirstOrDefault(s => s.Name == package.Name).Version}");
            foreach (var package in modifyOrAdd.Except(packagesToUninstall))
                log.Info($"- Installing {package.Name} version {package.Version}");

            packagesToUninstall.AddRange(versionMismatch);

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Deployment operation cancelled by user");

            if (packagesToUninstall.Any())
                Uninstall(packagesToUninstall, currentInstallation.Directory, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Deployment operation cancelled by user");

            if (modifyOrAdd.Any())
                Install(modifyOrAdd, currentInstallation.Directory, cancellationToken);
        }

        private static void Install(IEnumerable<PackageDef> modifyOrAdd, string target, CancellationToken cancellationToken)
        {
            Installer installer = new Installer(target, cancellationToken) { DoSleep = false };
            var packagesInOrder = OrderPackagesForInstallation(modifyOrAdd);
            List<string> paths = new List<string>();
            foreach (var package in packagesInOrder)
            {
                if (!cacheFileLookup.ContainsKey(package))
                    Download(package);
                paths.Add(cacheFileLookup[package]);
            }
            installer.PackagePaths.Clear();
            installer.PackagePaths.AddRange(paths);


            List<Exception> installErrors = new List<Exception>();
            installer.Error += ex => installErrors.Add(ex);

            try
            {
                installer.InstallThread();
            }
            catch (Exception ex)
            {
                installErrors.Add(ex);
            }

            if (installErrors.Any())
                throw new AggregateException("Image deployment failed to install packages.", installErrors);
        }

        private static void Uninstall(IEnumerable<PackageDef> packagesToUninstall, string target, CancellationToken cancellationToken)
        {
            var orderedPackagesToUninstall = OrderPackagesForInstallation(packagesToUninstall);
            orderedPackagesToUninstall.Reverse();

            List<Exception> uninstallErrors = new List<Exception>();
            var newInstaller = new Installer(target, cancellationToken) { DoSleep = false };

            newInstaller.Error += ex => uninstallErrors.Add(ex);
            newInstaller.DoSleep = false;

            newInstaller.PackagePaths.AddRange(orderedPackagesToUninstall.Select(x => (x.PackageSource as InstalledPackageDefSource)?.PackageDefFilePath).ToList());
            int exitCode = newInstaller.RunCommand("uninstall", false, true);

            if (uninstallErrors.Any() || exitCode != 0)
                throw new AggregateException("Image deployment failed to uninstall existing packages.", uninstallErrors);
        }

        private static List<PackageDef> OrderPackagesForInstallation(IEnumerable<PackageDef> packages)
        {
            var toInstall = new List<PackageDef>();

            var toBeSorted = packages.ToList();

            while (toBeSorted.Count() > 0)
            {
                var packagesWithNoRemainingDepsInList = toBeSorted.Where(pkg => pkg.Dependencies.All(dep => !toBeSorted.Any(p => p.Name == dep.Name))).ToList();
                toInstall.AddRange(packagesWithNoRemainingDepsInList);
                toBeSorted.RemoveAll(p => packagesWithNoRemainingDepsInList.Contains(p));
            }

            return toInstall;
        }

        internal static void Download(PackageDef package)
        {
            string filename = PackageCacheHelper.GetCacheFilePath(package);

            if (File.Exists(filename))
            {
                log.Info($"Package {package.Name} exists in cache: {filename}");
                cacheFileLookup.Add(package, filename);
                return;
            }

            if (package.PackageSource is IFilePackageDefSource fileSource)
            {
                if (string.Equals(Path.GetPathRoot(fileSource.PackageFilePath), Path.GetPathRoot(PackageCacheHelper.PackageCacheDirectory), StringComparison.InvariantCultureIgnoreCase) && string.IsNullOrEmpty(fileSource.PackageFilePath) == false)
                    File.Copy(fileSource.PackageFilePath, filename);
            }
            else if (package.PackageSource is IRepositoryPackageDefSource repoSource)
            {
                IPackageRepository rm = PackageRepositoryHelpers.DetermineRepositoryType(repoSource.RepositoryUrl);
                log.Info($"Downloading {package.Name} version {package.Version} from {rm.Url}");
                rm.DownloadPackage(package, filename, CancellationToken.None);
            }
            cacheFileLookup.Add(package, filename);
        }
    }
}
