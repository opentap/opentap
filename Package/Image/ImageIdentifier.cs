using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Tap.Shared;

namespace OpenTap.Package
{
    /// <summary>
    /// Image that specifies a list of <see cref="PackageSpecifier"/> to install and a list of repositories to get the packages from.
    /// </summary>
    public class ImageIdentifier
    {
        internal bool Cached => cacheFileLookup.Count == Packages.Count();

        /// <summary>
        /// Image ID created from hashing of Packages
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Package configuration of the Image
        /// </summary>
        public ReadOnlyCollection<IPackageIdentifier> Packages { get; }

        /// <summary>
        /// Repositories to retrieve the packages from
        /// </summary>
        public ReadOnlyCollection<string> Repositories { get; }

        internal Dictionary<PackageDef, string> cacheFileLookup = new Dictionary<PackageDef, string>();

        /// <summary>
        /// An Image is immutable, but can be converted to an <see cref="ImageSpecifier"/> which can be manipulated.
        /// </summary>
        /// <returns><see cref="ImageSpecifier"/></returns>
        public ImageSpecifier ToSpecifier()
        {
            return new ImageSpecifier()
            {
                Packages = Packages.Select(s => new PackageSpecifier(s.Name, VersionSpecifier.Parse(s.Version.ToString()), s.Architecture, s.OS)).ToList(),
                Repositories = Repositories.ToList()
            };
        }

        internal ImageIdentifier(IEnumerable<IPackageIdentifier> packages, IEnumerable<string> repositories)
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
            Packages = new ReadOnlyCollection<IPackageIdentifier>(packageList);
        }

        private string CalculateId(List<IPackageIdentifier> packageList)
        {
            List<string> packageStrings = packageList.Select(package => $"{package.Name} {package.Version} {package.Architecture} {package.OS}").ToList();
            HashAlgorithm algorithm = SHA1.Create();
            var bytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(string.Join(",", packageStrings)));
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        /// <summary>
        /// Deploy the <see cref="ImageIdentifier"/> as a OpenTAP installation.
        /// </summary>
        /// <param name="target">Directory to deploy OpenTap installation. 
        /// If the directory is already an OpenTAP installation, the installation will be modified to match the image
        /// System-Wide packages are not removed
        /// </param>
        /// <param name="cancellationToken">Cancellation token</param>
        public void Deploy(string target, CancellationToken cancellationToken)
        {
            if (!Cached)
                Cache();

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Deployment operation cancelled by user");

            Installation currentInstallation = new Installation(target);
            var packagesToUninstall = currentInstallation.GetPackages().Where(s => s.Class.ToLower() != "system-wide" && !Packages.Any(p => p.Name == s.Name));
            var modifyOrAdd = Packages.Where(s => !currentInstallation.GetPackages().Any(p => p.Name == s.Name && p.Version.ToString() == s.Version.ToString())).ToList();

            if (!packagesToUninstall.Any() && !modifyOrAdd.Any())
            {
                log.Info($"Target installation already matches specified image");
                return;
            }

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Deployment operation cancelled by user");

            if (packagesToUninstall.Any())
                Uninstall(packagesToUninstall, target, cancellationToken);


            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Deployment operation cancelled by user");

            if (modifyOrAdd.Any())
                Install(modifyOrAdd, target, cancellationToken);


        }

        private void Install(List<IPackageIdentifier> modifyOrAdd, string target, CancellationToken cancellationToken)
        {
            Installer installer = new Installer(target, cancellationToken) { DoSleep = false };
            List<string> paths = new List<string>();
            foreach (var package in modifyOrAdd)
            {
                paths.Add(cacheFileLookup[(PackageDef)package]);
            }
            var toInstall = ReorderPackages(paths);
            installer.PackagePaths.Clear();
            installer.PackagePaths.AddRange(toInstall);


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
                throw new AggregateException("Image deployment failed due to failiure in installing packages", installErrors);
        }

        private void Uninstall(IEnumerable<PackageDef> packagesToUninstall, string target, CancellationToken cancellationToken)
        {
            log.Info($"Removing packages:");
            foreach (var package in packagesToUninstall)
                log.Info($"- {package.Name} version {package.Version} ({package.Architecture}-{package.OS})");

            List<Exception> uninstallErrors = new List<Exception>();
            var newInstaller = new Installer(target, cancellationToken) { DoSleep = false };

            newInstaller.Error += ex => uninstallErrors.Add(ex);
            newInstaller.DoSleep = false;

            newInstaller.PackagePaths.AddRange(packagesToUninstall.Select(x => (x.PackageSource as InstalledPackageDefSource)?.PackageDefFilePath).ToList());
            newInstaller.UninstallThread();

            if (uninstallErrors.Any())
                throw new AggregateException("Image deployment failed due to failiure in uninstalling existing packages", uninstallErrors);
        }

        private List<string> ReorderPackages(List<string> packagePaths)
        {
            var toInstall = new List<string>();

            var packages = packagePaths.ToDictionary(k => k, k => PackageDef.FromPackage(k));

            while (packages.Count > 0)
            {
                var next = packages.FirstOrDefault(pkg => pkg.Value.Dependencies.All(dep => !packages.Values.Any(p => p.Name == dep.Name)));

                if (next.Value == null) next = packages.First(); // This doesn't matter at this point

                toInstall.Add(next.Key);
                packages.Remove(next.Key);
            }

            return toInstall;
        }

        /// <summary>
        /// Cache the image packages to PackageCache. This action prepares the image to be deployed.
        /// </summary>
        public void Cache()
        {
            if (Cached)
                return;
            foreach (var package in Packages.Cast<PackageDef>())
                Download(package);
        }
        static TraceSource log = Log.CreateSource("Download");
        private void Download(PackageDef package)
        {
            //if (PackageCacheHelper.PackageIsFromCache(package))
            //    return;

            //if (PackageCacheHelper.PackageIsFromCache(package))
            //    return;

            var packageName = PackageActionHelpers.GetQualifiedFileName(package).Replace('/', '.');
            string filename = Path.Combine(PackageCacheHelper.PackageCacheDirectory, packageName);

            if (File.Exists(filename))
            {
                log.Info($"Package {package.Name} exists in cache: {filename}");
                cacheFileLookup.Add(package, filename);
                return;
            }

            string source = (package.PackageSource as IRepositoryPackageDefSource)?.RepositoryUrl;
            if (source == null && package.PackageSource is FilePackageDefSource fileSource)
                source = fileSource.PackageFilePath;
            IPackageRepository rm = PackageRepositoryHelpers.DetermineRepositoryType(source);
            log.Info($"Downloading {package.Name} version {package.Version} from {rm.Url}");
            Directory.CreateDirectory(PackageCacheHelper.PackageCacheDirectory);
            rm.DownloadPackage(package, filename, CancellationToken.None);

            cacheFileLookup.Add(package, filename);
        }
    }
}