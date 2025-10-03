using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace OpenTap.Package
{
    /// <summary>
    /// Image that specifies a list of <see cref="PackageSpecifier"/> to install and a list of repositories to get the packages from.
    /// </summary>
    public class ImageIdentifier 
    {
        /// <summary>
        /// A delegate used by <see cref="ProgressUpdate"/>
        /// </summary>
        /// <param name="progressPercent">Indicates progress from 0 to 100.</param>
        /// <param name="message"></param>
        public delegate void ProgressUpdateDelegate(int progressPercent, string message);
        /// <summary>
        /// Called by the action to indicate how far it has gotten. Will usually be called with a progressPercent of 100 to indicate that it is done.
        /// </summary>
        public event ProgressUpdateDelegate ProgressUpdate;
        
        internal bool Cached => Packages.All(s => CachedLocation(s) != null);
        static TraceSource log = Log.CreateSource("OpenTAP");

        /// <summary>
        /// Image ID created by hashing the <see cref="Packages"/> list
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Package configuration of the Image
        /// </summary>
        public ReadOnlyCollection<PackageDef> Packages { get; }

        /// <summary>
        /// Repositories to retrieve the packages from
        /// </summary>
        public ReadOnlyCollection<string> Repositories { get; }


        /// <summary>
        /// An <see cref="ImageIdentifier"/> is immutable, but can be converted to an <see cref="ImageSpecifier"/> which is mutable.
        /// </summary>
        /// <returns><see cref="ImageSpecifier"/></returns>
        public ImageSpecifier ToSpecifier()
        {
            return new ImageSpecifier()
            {
                Packages = Packages.Select(s => new PackageSpecifier(s)).ToList(),
                Repositories = Repositories.ToList()
            };
        }

        internal ImageIdentifier(IEnumerable<PackageDef> packages, IEnumerable<string> repositories)
        {
            if (packages is null)
            {
                throw new ArgumentNullException(nameof(packages));
            }

            if (repositories is null)
            {
                throw new ArgumentNullException(nameof(repositories));
            }

            var packageList = packages.OrderBy(s => s.Name).ToList();
            Id = CalculateId(packageList);
            Repositories = new ReadOnlyCollection<string>(repositories.ToArray());
            Packages = new ReadOnlyCollection<PackageDef>(packageList);
        }

        private string CalculateId(IEnumerable<PackageDef> packageList)
        {
            List<string> packageHashes = new List<string>();
            foreach (PackageDef pkg in packageList)
            {
                if (pkg.Hash != null)
                    packageHashes.Add(pkg.Hash);
                else
                {
                    // This can happen if the package was created with OpenTAP < 9.16 that did not set the Hash property.
                    // We can just try to compute the hash now.
                    try
                    {
                        packageHashes.Add(pkg.ComputeHash());
                    }
                    catch
                    {
                        // This might happen if the PackageDef does not contain <Hash> elements for each file (for packages crated with OpenTAP < 9.5).
                        // In this case, just use the fields from IPackageIdentifier, they should be unique in most cases.
                        packageHashes.Add($"{pkg.Name} {pkg.Version} {pkg.Architecture} {pkg.OS}");
                    }
                }

            }


            using var algorithm = SHA1.Create();
            var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(string.Join(",", packageHashes)));
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        /// <summary>
        /// Deploy the <see cref="ImageIdentifier"/> as a OpenTAP installation.
        /// </summary>
        /// <param name="targetDir">Directory to deploy OpenTap installation. 
        /// If the directory is already an OpenTAP installation, the installation will be modified to match the image
        /// System-Wide packages are not removed
        /// </param>
        /// <param name="cancellationToken">Cancellation token</param>
        public void Deploy(string targetDir, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Deployment operation cancelled by user");

            Directory.CreateDirectory(targetDir);

            Installation currentInstallation = new Installation(targetDir);

            Deploy(currentInstallation, Packages.ToList(), ProgressUpdate, cancellationToken);
        }

        /// <summary>
        /// Download all packages to the PackageCache. This is an optional step that can speed up deploying later.
        /// </summary>
        public void Cache()
        {
            if (Cached)
                return;
            foreach (var package in Packages)
                Download(package, null, TapThread.Current.AbortToken);
        }

        private static void Deploy(Installation currentInstallation, List<PackageDef> dependencies,
            ProgressUpdateDelegate progress, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Deployment operation cancelled by user");

            var currentPackages = currentInstallation.GetPackages(validOnly: true);

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
                Uninstall(packagesToUninstall, currentInstallation.Directory, progress, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Deployment operation cancelled by user");

            if (modifyOrAdd.Any())
                Install(modifyOrAdd, currentInstallation.Directory, progress, cancellationToken);
        }

        private static void Install(IEnumerable<PackageDef> modifyOrAdd, string target,
            ProgressUpdateDelegate progress, CancellationToken cancellationToken)
        {
            progress ??= (percent, message) => { };
            var packagesInOrder = OrderPackagesForInstallation(modifyOrAdd);
            
            var cnt = packagesInOrder.Count;
            var downloaded = 0;
            
            // Download progress is the cumulative download progress divided by 2 
            void downloadProgress(int percent, string message)
            {
                var completed = Percent(downloaded + (double)percent/100, cnt);
                progress(completed / 2, message);
            }

            List<string> paths = new List<string>();
            for (var i = 0; i < cnt; i++)
            {
                var package = packagesInOrder[i];
                // If the package is a file, download it directory instead of caching it
                if (package.PackageSource is IFilePackageDefSource fd)
                {
                    paths.Add(fd.PackageFilePath);
                }
                else
                {
                    if (CachedLocation(package) is string cachedLocation)
                        log.Info($"Package {package.Name} exists in cache: {cachedLocation}");
                    else
                        Download(package, downloadProgress, cancellationToken); 
                    paths.Add(CachedLocation(package)); 
                }

                downloaded = i + 1;
                downloadProgress(Percent(downloaded, cnt), $"Downloaded {package.Name}");
            }

            // installProgress already accounts for packages that were already downloaded, so the 
            Installer installer = new Installer(target, cancellationToken) { DoSleep = false };
            installer.ProgressUpdate += (percent, message) => progress(percent, message);   
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

        private static void Uninstall(IEnumerable<PackageDef> packagesToUninstall, string target,
            ProgressUpdateDelegate progress, CancellationToken cancellationToken)
        {
            var orderedPackagesToUninstall = OrderPackagesForInstallation(packagesToUninstall);
            orderedPackagesToUninstall.Reverse();

            List<Exception> uninstallErrors = new List<Exception>();
            var newInstaller = new Installer(target, cancellationToken) { DoSleep = false };
            newInstaller.ProgressUpdate += (percent, message) => progress?.Invoke(percent, message); 

            newInstaller.Error += ex => uninstallErrors.Add(ex);
            newInstaller.DoSleep = false;

            newInstaller.PackagePaths.AddRange(orderedPackagesToUninstall.Select(x => (x.PackageSource as XmlPackageDefSource)?.PackageDefFilePath).ToList());
            
            int exitCode = newInstaller.RunCommand(Installer.PrepareUninstall, false, false);
            if (uninstallErrors.Any() || exitCode != 0)
                throw new AggregateException("Image deployment failed to uninstall existing packages.", uninstallErrors);
            
            exitCode = newInstaller.RunCommand(Installer.Uninstall, false, true);

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

        private static int Percent(double n, double of)
        {
            return (int)(n / of * 100);
        }

        private static void Download(PackageDef package, ProgressUpdateDelegate progress,
            CancellationToken token)
        {
            progress ??= (percent, message) => { };
            if (CachedLocation(package) is string cachedLocation)
            {
                log.Info($"Package {package.Name} exists in cache: {cachedLocation}");
                return;
            }

            string filename = PackageCacheHelper.GetCacheFilePath(package);
            Directory.CreateDirectory(Path.GetDirectoryName(filename));
            if (package.PackageSource is IFilePackageDefSource fileSource)
            {
                File.Copy(fileSource.PackageFilePath, filename);
            }
            else if (package.PackageSource is IRepositoryPackageDefSource repoSource)
            {
                IPackageRepository rm = PackageRepositoryHelpers.DetermineRepositoryType(repoSource.RepositoryUrl);
                if (rm is IPackageDownloadProgress p)
                {
                    p.OnProgressUpdate += (message, pos, len) =>
                    {
                        progress(Percent(pos, len), message);
                    };
                }
                log.Info($"Downloading {package.Name} version {package.Version} from {rm.Url}");
                rm.DownloadPackage(package, filename, token);
            }
            else if (package.PackageSource is InstalledPackageDefSource)
            {
                throw new Exception(
                    $"Unable to download package {package.Name} since it is only available as an installed package.");
            }
            else
            {
                throw new Exception(
                    $"Unable to downlaod package {package.Name} because its source type '{package.PackageSource.GetType().Name}' is not supported.");
            }
        }

        private static string CachedLocation(PackageDef package)
        {
            string filename = PackageCacheHelper.GetCacheFilePath(package);

            if (File.Exists(filename))
            {
                return filename;
            }
            return null;
        }

    }
}
