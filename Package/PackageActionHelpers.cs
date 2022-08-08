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

        private static int OrderArchitecture(CpuArchitecture architecture)
        {
            if (architecture == ArchitectureHelper.GuessBaseArchitecture)
                return 0;
            switch (architecture)
            {
                case CpuArchitecture.Unspecified:
                    return 10;
                case CpuArchitecture.AnyCPU:
                    return 1;
                case CpuArchitecture.x86:
                    return 3;
                case CpuArchitecture.x64:
                    return 2;
                case CpuArchitecture.arm:
                    return 5;
                case CpuArchitecture.arm64:
                    return 4;
                default:
                    return 10;
            }
        }

        internal static string NormalizeRepoUrl(string path)
        {
            if (path == null)
                return null;
            if (Uri.IsWellFormedUriString(path, UriKind.Relative) && Directory.Exists(path) || Regex.IsMatch(path ?? "", @"^([A-Z|a-z]:)?(\\|/)"))
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    return Path.GetFullPath(path)
                               .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                               .ToUpperInvariant();
                else
                    return Path.GetFullPath(path)
                               .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            else if (path.StartsWith("http"))
                return path.ToUpperInvariant();
            else
                return String.Format("http://{0}", path).ToUpperInvariant();

        }

        internal static List<PackageDef> GatherPackagesAndDependencyDefs(Installation installation, PackageSpecifier[] pkgRefs, string[] packageNames, string Version, CpuArchitecture arch, string OS, List<IPackageRepository> repositories,
            bool force, bool includeDependencies, bool ignoreDependencies, bool askToIncludeDependencies, bool noDowngrade)
        {
            List<PackageDef> gatheredPackages = new List<PackageDef>();

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
                                gatheredPackages.Add(selected.First());
                                continue;
                            }
                            if (!string.IsNullOrEmpty(OS))
                            {
                                selected = selected.Where(p => p.OS.ToLower().Split(',').Any(OS.ToLower().Contains)).ToList();
                                if (selected.Count == 1)
                                {
                                    gatheredPackages.Add(selected.First());
                                    log.Debug("TapPackages file contains packages for several operating systems. Picking only the one for {0}.", OS);
                                    continue;
                                }
                            }
                            if (arch != CpuArchitecture.Unspecified)
                            {
                                selected = selected.Where(p => ArchitectureHelper.CompatibleWith(arch, p.Architecture)).ToList();
                                if (selected.Count == 1)
                                {
                                    gatheredPackages.Add(selected.First());
                                    log.Debug("TapPackages file contains packages for several CPU architectures. Picking only the one for {0}.", arch);
                                    continue;
                                }
                            }
                            throw new Exception("TapPackages file contains multiple variants of the same package. Unable to autoselect a suitable one.");
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(packageName) == false)
                    {
                        packages.Add(new PackageSpecifier(packageName, VersionSpecifier.Parse(version ?? ""), arch, OS));
                    }
                }
            }

            foreach (var packageSpecifier in packages)
            {
                var installedPackages = installation.GetPackages();
                Stopwatch timer = Stopwatch.StartNew();
                if (File.Exists(packageSpecifier.Name))
                {
                    var package = PackageDef.FromPackage(packageSpecifier.Name);

                    if (noDowngrade)
                    {
                        var installedPackage = installedPackages.FirstOrDefault(p => p.Name == package.Name);
                        if (installedPackage != null && installedPackage.Version.CompareTo(package.Version) >= 0)
                        {
                            log.Info($"The same or a newer version of package '{package.Name}' in already installed.");
                            continue;
                        }
                    }

                    gatheredPackages.Add(package);
                    log.Debug(timer, "Found package {0} locally.", packageSpecifier.Name);
                }
                else
                {
                    // assumption: packages ending with .TapPackage are files, not on external repos.
                    // so if this is the case and the file does not exist, throw an exception.
                    if(string.Compare(Path.GetExtension(packageSpecifier.Name), ".TapPackage", StringComparison.InvariantCultureIgnoreCase) == 0)
                        throw new FileNotFoundException($"Unable to find the file {packageSpecifier.Name}");
                    
                    PackageDef package = DependencyResolver.GetPackageDefFromRepo(repositories, packageSpecifier, new List<PackageDef>());

                    if (noDowngrade)
                    {
                        var installedPackage = installedPackages.FirstOrDefault(p => p.Name == package.Name);
                        if (installedPackage != null && installedPackage.Version.CompareTo(package.Version) >= 0)
                        {
                            log.Info($"The same or a newer version of package '{package.Name}' in already installed.");
                            continue;
                        }
                    }

                    if (PackageCacheHelper.PackageIsFromCache(package))
                        log.Debug(timer, "Found package {0} version {1} in local cache", package.Name, package.Version);
                    else
                        log.Debug(timer, "Found package {0} version {1}", package.Name, package.Version);

                    gatheredPackages.Add(package);
                }
            }

            if (gatheredPackages.All(p => p.IsBundle()))
            {
                // If we are just installing bundles, we can assume that dependencies should also be installed
                includeDependencies = true;
            }

            if (force || ignoreDependencies)
            {
                if (force)
                    log.Info($"Ignoring potential depencencies (--force option specified).");
                else
                    log.Debug($"Ignoring potential depencencies (--no-dependencies option specified).");
                return gatheredPackages.ToList();
            }

            log.Debug("Resolving dependencies.");
            var resolver = new DependencyResolver(installation, gatheredPackages, repositories);

            var actualMissingDependencies = resolver.MissingDependencies.Where(s => !gatheredPackages.Any(p => s.Name == p.Name));

            if (resolver.UnknownDependencies.Any())
            {
                foreach (var dep in resolver.UnknownDependencies)
                    log.Error($"A package dependency named '{dep.Name}' with a version compatible with {dep.Version} could not be found in any repository.");

                log.Info("To download package dependencies despite the conflicts, use the --force option.");
                return null;
            }
            else if (actualMissingDependencies.Any())
            {
                if (includeDependencies == false)
                {
                    var dependencies = string.Join(", ",
                        actualMissingDependencies.Select(d => $"{d.Name} {d.Version}"));
                    log.Info($"Use '--dependencies' to include {dependencies}.");
                }

                if (includeDependencies)
                {
                    foreach (var package in actualMissingDependencies)
                    {
                        log.Debug($"Adding dependency {package.Name} {package.Version}");
                        if (!gatheredPackages.Contains(package))
                            gatheredPackages.Insert(0, package);
                    }
                }
                else if (askToIncludeDependencies)
                {
                    var pkgs = new List<DepRequest>();

                    foreach (var package in actualMissingDependencies)
                    {
                        // Handle each package at a time.
                        DepRequest req = null;
                        pkgs.Add(req = new DepRequest { PackageName = package.Name, message = string.Format("Add dependency {0} {1} ?", package.Name, package.Version), Response = DepResponse.Add });
                        UserInput.Request(req, true);
                    }

                    foreach (var pkg in actualMissingDependencies)
                    {
                        var res = pkgs.FirstOrDefault(r => r.PackageName == pkg.Name);

                        if ((res != null) && res.Response == DepResponse.Add)
                        {
                            if (!gatheredPackages.Contains(pkg))
                                gatheredPackages.Insert(0, pkg);
                        }
                        else
                            log.Debug("Ignoring dependent package {0} at users request.", pkg.Name);
                    }
                }
            }

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
