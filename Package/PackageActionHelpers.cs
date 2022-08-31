//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenTap.Package
{
    internal static class PackageActionHelpers
    {
        readonly static TraceSource log = OpenTap.Log.CreateSource("PackageAction");

        private enum DepResponse
        {
            Add,
            Ignore
        }

        [System.Reflection.Obfuscation(Exclude = true)]
        private class DepRequest
        {
            [Browsable(true)]
            [Layout(LayoutMode.FullRow)]
            public string Message => message;
            internal string message;
            internal string PackageName { get; set; }
            [Submit]
            [Layout(LayoutMode.FullRow | LayoutMode.FloatBottom)]
            public DepResponse Response { get; set; } = DepResponse.Add;
        }

        static List<PackageDef> TriviallyResolvePackage(IEnumerable<PackageSpecifier> packages,
            ICollection<IPackageRepository> repositories, ICollection<PackageDef> directlyReferencedPackages)
        {
            directlyReferencedPackages = directlyReferencedPackages ?? new List<PackageDef>();
            List<PackageDef> forcePackages = new List<PackageDef>();
            foreach (var pkgSpec in packages)
            {
                PackageDef pkgDef = null;
                foreach (var repo in repositories)
                {
                    pkgDef = repo.GetPackages(pkgSpec).FirstOrDefault();
                    if (pkgDef != null) break;
                }

                if (pkgDef == null)
                {
                    pkgDef = directlyReferencedPackages.FirstOrDefault(x => x.Name == pkgSpec.Name && pkgSpec.Version.IsCompatible(x.Version));
                }
                

                if (pkgDef == null)
                {
                    throw new Exception($"Could not find package exactly matching {pkgSpec} (--force specified).");
                }

                forcePackages.Add(pkgDef);

            }
            return forcePackages;
            
        }
        
        
        internal static List<PackageDef> GatherPackagesAndDependencyDefs(Installation installation, PackageSpecifier[] pkgRefs, string[] packageNames, string Version, CpuArchitecture arch, string OS, List<IPackageRepository> repositories,
            bool force, bool includeDependencies, bool ignoreDependencies, bool askToIncludeDependencies, bool noDowngrade)
        {
            List<PackageDef> directlyReferencesPackages = new List<PackageDef>();
            

            List<PackageSpecifier> packages = new List<PackageSpecifier>();
            if (pkgRefs != null)
                packages = pkgRefs.ToList();
            else
            {
                if (packageNames == null)
                    throw new Exception("No packages specified.");
                foreach (string packageName in packageNames)
                {
                    var version = Version;
                    if (Path.GetExtension(packageName).ToLower().EndsWith("tappackages"))
                    {
                        if(!File.Exists(packageName))
                            throw new FileNotFoundException($"Unable to find the file {packageName}.");
                        var tempDir = Path.GetTempPath();
                        var bundleFiles = PluginInstaller.UnpackPackage(packageName, tempDir);
                        var packagesInBundle = bundleFiles.Select(PackageDef.FromPackage);

                        // A packages file may contain the several variants of the same package, try to select one based on OS and Architecture
                        foreach (IGrouping<string, PackageDef> grp in packagesInBundle.GroupBy(p => p.Name))
                        {
                            var selected = grp.ToList();
                            if (selected.Count == 1)
                            {
                                var pkg = selected.First();
                                directlyReferencesPackages.Add(pkg);
                                packages.Add(pkg.GetSpecifier());
                                continue;
                            }
                            if (!string.IsNullOrEmpty(OS))
                            {
                                selected = selected.Where(p => p.OS.ToLower().Split(',').Any(OS.ToLower().Contains)).ToList();
                                if (selected.Count == 1)
                                {
                                    var pkg = selected.First();
                                    directlyReferencesPackages.Add(pkg);
                                    packages.Add(pkg.GetSpecifier());
                                    log.Debug("TapPackages file contains packages for several operating systems. Picking only the one for {0}.", OS);
                                    continue;
                                }
                            }
                            if (arch != CpuArchitecture.Unspecified)
                            {
                                selected = selected.Where(p => ArchitectureHelper.CompatibleWith(arch, p.Architecture)).ToList();
                                if (selected.Count == 1)
                                {
                                    var pkg = selected.First();
                                    directlyReferencesPackages.Add(pkg);
                                    packages.Add(pkg.GetSpecifier());
                                    log.Debug("TapPackages file contains packages for several CPU architectures. Picking only the one for {0}.", arch);
                                    continue;
                                }
                            }
                            throw new Exception("TapPackages file contains multiple variants of the same package. Unable to auto-select a suitable one.");
                        }
                    }
                    else if (Path.GetExtension(packageName)
                             .Equals(".Tappackage", StringComparison.InvariantCultureIgnoreCase) || File.Exists(packageName))
                    {
                        
                        var pkg = PackageDef.FromPackage(packageName);
                        directlyReferencesPackages.Add(pkg);
                        packages.Add(pkg.GetSpecifier());
                    }
                    else if (string.IsNullOrWhiteSpace(packageName) == false)
                    {
                        packages.Add(new PackageSpecifier(packageName, VersionSpecifier.Parse(version ?? ""), arch, OS));
                    }
                }
            }

            if (force)
            {
                // when --force is specified, exact package specifiers has to be used.
                // there is no need to resolve the image in this case.
                var packagesToInstall = TriviallyResolvePackage(packages, repositories, directlyReferencesPackages);
                
                if (noDowngrade)
                {
                    packagesToInstall = packagesToInstall.Where(x =>
                    {
                        var installed = installation.FindPackage(x.Name);
                        if (installed != null && installed.Version.CompareTo(x.Version) > 0)
                            return false;
                        return true;
                    }).Select(x => directlyReferencesPackages.FirstOrDefault(y => y.Name == x.Name && y.Version == x.Version) ?? x)
                        .ToList();
                }
                // make sure to use the TapPackage if one was directly referenced
                packagesToInstall = packagesToInstall.Select(x => directlyReferencesPackages.FirstOrDefault(y => y.Name == x.Name && y.Version == x.Version) ?? x)
                    .ToList();

                return packagesToInstall;

            }

            if (noDowngrade)
            {
                // if --no-downgrade is specified, none of the already installed packages are allowed to get downgraded
                // hence they can be added as extra constraints for the dependency resolver.
                var existingSpec = installation.GetPackages().Select(pkg =>
                    new PackageSpecifier(pkg.Name, pkg.Version.AsCompatibleSpecifier(), pkg.Architecture, pkg.OS));
                packages = packages.Concat(existingSpec).ToList();
            }

            var img = ImageSpecifier.FromAddedPackages(installation, packages);
            img.Repositories = repositories.Select(x => x.Url).ToList();
            img.AdditionalPackages.AddRange(directlyReferencesPackages);
            var result = img.Resolve(TapThread.Current.AbortToken);

            // missing dependencies are those which are not installed

            List<PackageDef> installedAsDependencies = new List<PackageDef>();
            List<PackageDef> gatheredPackages = new List<PackageDef>();
            foreach (var pkg in result.Packages)
            {
                var installed = img.InstalledPackages.FirstOrDefault(x => x.Name == pkg.Name && x.Version == pkg.Version);
                if (installed != null) continue; // this package is already provided by the installation.
                var gathered = packages.FirstOrDefault(x => x.Name == pkg.Name);
                gatheredPackages.Add(pkg);
                if (gathered == null)
                    installedAsDependencies.Add(pkg);
            }

            foreach (var additional in installedAsDependencies)
            {
                if (img.InstalledPackages.Any(x => x.Name == additional.Name))
                {  
                    // This implies that the version is newer.
                    log.Info("Updating dependency {0} {1}", additional.Name, additional.Version);    
                }
                else
                {
                    log.Info("Adding dependency {0} {1}", additional.Name, additional.Version);
                }
            }
            
            // make sure to use the TapPackage if one was directly referenced
            gatheredPackages = gatheredPackages
                .Select(x => directlyReferencesPackages.FirstOrDefault(y => y.Name == x.Name && y.Version == x.Version) ?? x)
                .ToList();
            return gatheredPackages.ToList();
        }

        internal static List<string> DownloadPackages(string destinationDir, List<PackageDef> PackagesToDownload, List<string> filenames = null, Action<int, string> progressUpdate = null, bool ignoreCache = false)
        {
            progressUpdate = progressUpdate ?? ((i, s) => { });

            List<string> downloadedPackages = new List<string>();

            for (int i = 0; i < PackagesToDownload.Count; i++)
            {
                Stopwatch timer = Stopwatch.StartNew();

                var pkg = PackagesToDownload[i];
                // Package names can contain slashes and backslashes -- avoid creating subdirectories when downloading packages
                var packageName = GetQualifiedFileName(pkg).Replace('/', '.');
                string filename = filenames?.ElementAtOrDefault(i) ??
                                  Path.Combine(destinationDir, packageName);

                TapThread.ThrowIfAborted();

                var i1 = i;

                void innerProgress(string header, long pos, long len)
                {
                    var downloadProgress = 100.0 * pos / len;

                    var thisProgress = downloadProgress / PackagesToDownload.Count;
                    var otherProgress = (100.0 * i1) / PackagesToDownload.Count;

                    var progress = thisProgress + otherProgress;

                    var progressString = $"({downloadProgress:0.00}% | {Utils.BytesToReadable(pos)} of {Utils.BytesToReadable(len)})";
                    progressUpdate((int)progress, $"Downloading '{pkg}' {progressString}");
                }


                try
                {
                    PackageDef existingPkg = null;
                    try
                    {
                        // If the package we are installing is from a file, we should always use that file instead of a cached package.
                        // During development a package might not change version but still have different content.
                        if (pkg.PackageSource is FilePackageDefSource == false && File.Exists(filename) && !ignoreCache)
                            existingPkg = PackageDef.FromPackage(filename);
                    }
                    catch (Exception e)
                    {
                        log.Warning("Could not open OpenTAP Package. Redownloading package.", e);
                        File.Delete(filename);
                    }

                    if (existingPkg != null)
                    {
                        if (existingPkg.Version == pkg.Version && existingPkg.OS == pkg.OS && existingPkg.Architecture == pkg.Architecture)
                        {
                            if (!PackageCacheHelper.PackageIsFromCache(existingPkg))
                                log.Info("Package '{0}' already exists in '{1}'.", pkg.Name, destinationDir);
                            else
                                log.Info("Package '{0}' already exists in cache '{1}'.", pkg.Name, destinationDir);
                        }
                        else
                        {
                            throw new Exception($"A package already exists but it is not the same as the package that is being downloaded.");
                        }
                    }
                    else
                    {
                        IPackageRepository rm = null;
                        switch (pkg.PackageSource)
                        {
                            case HttpRepositoryPackageDefSource repoSource:
                                rm = new HttpPackageRepository(repoSource.RepositoryUrl);
                                break;
                            case FileRepositoryPackageDefSource repoSource:
                                rm = new FilePackageRepository(repoSource.RepositoryUrl);
                                break;
                            case IFilePackageDefSource fileSource:
                                rm = new FilePackageRepository(System.IO.Path.GetDirectoryName(fileSource.PackageFilePath));
                                break;
                            default:
                                throw new Exception($"Unable to determine repositoy type for package source {pkg.PackageSource.GetType()}.");
                        }
                        if (rm is IPackageDownloadProgress r)
                        {
                            r.OnProgressUpdate = innerProgress;
                        }
                        if (PackageCacheHelper.PackageIsFromCache(pkg) && !ignoreCache)
                        {
                            rm.DownloadPackage(pkg, filename);
                            log.Info(timer, "Found package '{0}' in cache. Copied to '{1}'.", pkg.Name, Path.GetFullPath(filename));
                        }
                        else
                        {
                            log.Debug("Downloading '{0}' version '{1}' from '{2}'", pkg.Name, pkg.Version, rm.Url);
                            rm.DownloadPackage(pkg, filename);
                            log.Info(timer, "Downloaded '{0}' to '{1}'.", pkg.Name, Path.GetFullPath(filename));
                            PackageCacheHelper.CachePackage(filename);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException)
                        throw;
                    log.Error("Failed to download OpenTAP package.");
                    log.Debug(ex);
                    throw;
                }

                downloadedPackages.Add(filename);
                float progress_f = (float)(i + 1) / PackagesToDownload.Count;
                progressUpdate((int)(progress_f * 100), $"Acquired '{pkg}'.");
            }

            progressUpdate(100, "Finished downloading packages.");

            return downloadedPackages;
        }

        internal static string GetQualifiedFileName(PackageDef pkg)
        {
            List<string> filenameParts = new List<string> { pkg.Name };
            if (pkg.Version != null)
                filenameParts.Add(pkg.Version.ToString());
            if (pkg.Architecture != CpuArchitecture.AnyCPU)
                filenameParts.Add(pkg.Architecture.ToString());
            if (!String.IsNullOrEmpty(pkg.OS) && pkg.OS != "Windows")
                filenameParts.Add(pkg.OS);
            filenameParts.Add("TapPackage");
            return String.Join(".", filenameParts);
        }
    }
}
