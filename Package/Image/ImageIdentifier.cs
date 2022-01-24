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
        internal bool Cached => Packages.All(s => s.PackageSource is FileRepositoryPackageDefSource);

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


            HashAlgorithm algorithm = SHA1.Create();
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

            Installation currentInstallation = new Installation(targetDir);

            ImageDeployer.Deploy(currentInstallation, Packages.ToList(), cancellationToken);
            //var currentPackages = currentInstallation.GetPackages();

            //var skippedPackages = Packages.Where(s => currentPackages.Any(p => p.Name == s.Name && p.Version.ToString() == s.Version.ToString())).ToHashSet();
            //var modifyOrAdd = Packages.Where(s => !skippedPackages.Contains(s)).ToList();
            //var packagesToUninstall = currentPackages.Where(pack => !Packages.Any(p => p.Name == pack.Name) && pack.Class.ToLower() != "system-wide").ToList(); // Uninstall installed packages which are not part of image
            //var versionMismatch = currentPackages.Where(pack => Packages.Any(p => p.Name == pack.Name && p.Version != pack.Version)).ToList(); // Uninstall installed packages where we're deploying another version

            //if (!packagesToUninstall.Any() && !modifyOrAdd.Any())
            //{
            //    log.Info($"Target installation is already up to date.");
            //    return;
            //}

            //if (!currentPackages.Any())
            //    log.Info($"Deploying installation in {targetDir}:");
            //else
            //    log.Info($"Modifying installation in {targetDir}:");

            //foreach (var package in skippedPackages)
            //    log.Info($"- Skipping {package.Name} version {package.Version} ({package.Architecture}-{package.OS}) - Already installed.");
            //foreach (var package in packagesToUninstall)
            //    log.Info($"- Removing {package.Name} version {package.Version} ({package.Architecture}-{package.OS})");
            //foreach (var package in versionMismatch)
            //    log.Info($"- Modifying {package.Name} version {package.Version} to {Packages.FirstOrDefault(s => s.Name == package.Name).Version}");
            //foreach (var package in modifyOrAdd.Except(packagesToUninstall))
            //    log.Info($"- Installing {package.Name} version {package.Version}");

            //packagesToUninstall.AddRange(versionMismatch);

            //if (cancellationToken.IsCancellationRequested)
            //    throw new OperationCanceledException("Deployment operation cancelled by user");

            //if (packagesToUninstall.Any())
            //    Uninstall(packagesToUninstall, targetDir, cancellationToken);

            //if (cancellationToken.IsCancellationRequested)
            //    throw new OperationCanceledException("Deployment operation cancelled by user");

            //if (modifyOrAdd.Any())
            //    Install(modifyOrAdd, targetDir, cancellationToken);
        }

        /// <summary>
        /// Deploy the <see cref="ImageIdentifier"/> as an upgrade to an existing OpenTAP installation.
        /// </summary>
        /// <param name="installation">Installation to deploy OpenTap image onto.
        /// Image packages which are not present in the current installation will be installed.
        /// The existing packages in the installation will get upgraded if the image packages are higher versions.
        /// The existing packages in the installation which are not in the image, will be left untouched.
        /// This can cause existing installed packages to have incompatible dependencies.
        /// To detect issues prior to deployment, use the DependencyAnalyzer
        /// </param>
        /// <param name="cancellationToken">Cancellation token</param>
        //public void Deploy(Installation installation, CancellationToken cancellationToken)
        //{
        //    var currentPackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();

        //    var upgrade = Packages.Where(s => currentPackages.Any(p => s.Name == p.Name && !s.Version.IsCompatible(p.Version))).ToList();
        //    var add = Packages.Where(s => !currentPackages.Any(p => s.Name == p.Name)).ToList();

        //    if (!add.Any() && !upgrade.Any())
        //    {
        //        log.Info($"Target installation is already up to date.");
        //        return;
        //    }

        //    if (!currentPackages.Any())
        //        log.Info($"Deploying installation in {installation.Directory}:");
        //    else
        //        log.Info($"Modifying installation in {installation.Directory}:");

        //    foreach (var package in upgrade)
        //        log.Info($"- Modifying {package.Name} version {package.Version} to {Packages.FirstOrDefault(s => s.Name == package.Name).Version}");
        //    foreach (var package in add)
        //        log.Info($"- Installing {package.Name} version {package.Version}");

        //    var packagesToUninstall = currentPackages.Where(s => upgrade.Any(p => s.Name == p.Name));

        //    if (cancellationToken.IsCancellationRequested)
        //        throw new OperationCanceledException("Deployment operation cancelled by user");

        //    if (packagesToUninstall.Any())
        //        Uninstall(packagesToUninstall, installation.Directory, cancellationToken);

        //    if (cancellationToken.IsCancellationRequested)
        //        throw new OperationCanceledException("Deployment operation cancelled by user");

        //    var modifyOrAdd = add;
        //    modifyOrAdd.AddRange(upgrade);

        //    if (modifyOrAdd.Any())
        //        Install(modifyOrAdd, installation.Directory, cancellationToken);
        //}

        /// <summary>
        /// Download all packages to the PackageCache. This is an optional step that can speed up deploying later.
        /// </summary>
        public void Cache()
        {
            if (Cached)
                return;
            foreach (var package in Packages)
                ImageDeployer.Download(package);
        }
    }
}