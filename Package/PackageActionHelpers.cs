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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    internal static class PackageActionHelpers
    {
        readonly static TraceSource log =  OpenTap.Log.CreateSource("PackageAction");

        private class DepRequest
        {
            [Browsable(true)]
            [Layout(LayoutMode.FullRow)]
            public string Message => message;
            internal string message;
            internal string PackageName { get; set; }
            public bool Response { get; set; }
        }

        internal static PackageDef FindPackage(PackageSpecifier packageReference, bool force, string installDir)
        {
            IPackageIdentifier[] compatibleWith;
            if (!force)
			{
                var installation = new Installation(installDir);
                var tapPackage = installation.GetOpenTapPackage();
                if(tapPackage != null)
                    compatibleWith = new[] { installation.GetOpenTapPackage() };
                else
                    compatibleWith = new[] { new PackageIdentifier("OpenTAP", PluginManager.GetOpenTapAssembly().SemanticVersion.ToString(), CpuArchitecture.Unknown, "") };
            }
            else
                compatibleWith = Array.Empty<IPackageIdentifier>();
            
            var compatiblePackages = PackageRepositoryHelpers.GetPackagesFromAllRepos(packageReference, compatibleWith);

            // Of the compatible packages, pick the one with the highest version number. If that package is available from several repositories, pick the one with the lowest index in the list in PackageManagerSettings
            IEnumerable<PackageManagerSettings.RepositorySettingEntry> repoList = PackageManagerSettings.Current.Repositories;
            PackageDef package = null;
            if (compatiblePackages.Any())
                package = compatiblePackages.GroupBy(p => p.Version).OrderByDescending(g => g.Key).FirstOrDefault()
                                            .OrderBy(p => repoList.IndexWhen(e => NormalizeRepoUrl(e.Url) == NormalizeRepoUrl(p.Location))).FirstOrDefault();

            if (package == null)
            {
                var anyPackage = PackageRepositoryHelpers.GetPackagesFromAllRepos(packageReference);
                var foundAny = anyPackage.Any();
                string message;
                if (!foundAny)
                    message = String.Format("Package '{0}' could not be found in any repository.", packageReference.Name);
                else if (packageReference.Version != VersionSpecifier.Any || packageReference.OS != null || packageReference.Architecture != CpuArchitecture.Unknown)
                    message = String.Format("No '{0}' package {1} was found.", packageReference.Name, string.Join(" and ",
                        new string[] {
                                    packageReference.Version != VersionSpecifier.Any ? "compatible with version " + packageReference.Version : null,
                                    packageReference.OS != null ? "compatible with " + packageReference.OS + " operating system" : null,
                                    packageReference.Architecture != CpuArchitecture.Unknown ? "with \"" + packageReference.Architecture + "\" architechture" : null
                        }.Where(x => x != null).ToArray()));
                else
                    message = String.Format("Could not find any versions of package '{0}' that is compatible.", packageReference.Name);

                throw new ExitCodeException(1,message);
            }
            return package;
        }

        internal static string NormalizeRepoUrl(string path)
        {
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
            else if(path.StartsWith("http"))
                return path.ToUpperInvariant();
            else
                return String.Format("http://{0}",path).ToUpperInvariant();

        }

        internal static List<PackageDef> GatherPackagesAndDependencyDefs(string cacheDir, string installDir, PackageSpecifier[] pkgRefs, string[] packageNames, string Version, CpuArchitecture arch, string OS, IEnumerable<string> repositoryUrls, bool force, bool includeDependencies, bool askToIncludeDependencies)
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
                        var tempDir = Path.GetTempPath();
                        var bundleFiles = PluginInstaller.UnpackPackage(packageName, tempDir);
                        var packagesInBundle = bundleFiles.Select(PackageDef.FromPackage);

                        // A packages file may contain the several variants of the same package, try to select one based on OS and Architechture
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
                            if (arch != CpuArchitecture.Unknown)
                            {
                                selected = selected.Where(p => ArchitectureHelper.CompatibleWith(arch, p.Architecture)).ToList();
                                if (selected.Count == 1)
                                {
                                    gatheredPackages.Add(selected.First());
                                    log.Debug("TapPackages file contains packages for several CPU architechtures. Picking only the one for {0}.", arch);
                                    continue;
                                }
                            }
                            throw new Exception("TapPackages file contains multiple variants of the same package. Unable to autoselect a suitable one.");
                        }
                    }
                    else
                    {
                        packages.Add(new PackageSpecifier(packageName, version != null ? VersionSpecifier.Parse(version) : null, arch, OS));
                    }
                }
            }

            var previousRepoList = PackageManagerSettings.Current.Repositories.Select(rep => new PackageManagerSettings.RepositorySettingEntry { IsEnabled = rep.IsEnabled, Url = rep.Url }).ToList();
            if (repositoryUrls != null)
            {
                PackageManagerSettings.Current.Repositories.Clear();
                PackageManagerSettings.Current.Repositories.AddRange(repositoryUrls.Select(rep => new PackageManagerSettings.RepositorySettingEntry { IsEnabled = true, Url = rep }));
            }
            if (!PackageManagerSettings.Current.Repositories.Any(e => cacheDir.StartsWith(e.Url) && e.IsEnabled))
                PackageManagerSettings.Current.Repositories.Insert(0, new PackageManagerSettings.RepositorySettingEntry { Url = cacheDir, IsEnabled = true });
            else
            {
                int index = PackageManagerSettings.Current.Repositories.IndexWhen(e => cacheDir.StartsWith(e.Url) && e.IsEnabled);
                if(index != 0)
                    PackageManagerSettings.Current.Repositories.Move(index, 0);
            }
            try
            {

                foreach (var packageReference in packages)
                {
                    Stopwatch timer = Stopwatch.StartNew();
                    if (File.Exists(packageReference.Name))
                    {
                        gatheredPackages.Add(PackageDef.FromPackage(packageReference.Name));
                        log.Debug(timer, "Found package {0} locally.", packageReference.Name);
                    }
                    else
                    {
                        PackageDef package = PackageActionHelpers.FindPackage(packageReference, force, installDir);

                        if (PackageCacheHelper.PackageIsFromCache(package))
                            log.Debug(timer, "Found package {0} version {1} in local cache", package.Name, package.Version);
                        else
                            log.Debug(timer, "Found package {0} version {1}", package.Name, package.Version);

                        gatheredPackages.Add(package);
                    }
                }


                log.Debug("Resolving dependencies.");
                var installedPackages = new Installation(installDir).GetPackages();;
                var resolver = new DependencyResolver(gatheredPackages, installedPackages);
                if (resolver.UnknownDependencies.Any())
                {
                    foreach (var dep in resolver.UnknownDependencies)
                    {
                        string message = string.Format("A package dependency named '{0}' with a version compatible with {1} could not be found in any repository.", dep.Name, dep.Version);

                        if (force)
                        {
                            log.Warning(message);
                            log.Warning("Continuing without downloading dependencies. Plugins will likely not work as expected.", dep.Name);
                        }
                        else
                            log.Error(message);
                    }
                    if (!force)
                    {
                        log.Info("To download package dependencies despite the conflicts, use the --force option.");
                        return null;
                    }
                }
                else if (resolver.MissingDependencies.Any())
                {
                    string dependencyArgsHint = "";
                    if (!includeDependencies)
                        dependencyArgsHint = $" (use --dependencies to also get these)";
                    if (resolver.MissingDependencies.Count > 1)
                        log.Info("There are {0} missing dependencies{1}.", resolver.MissingDependencies.Count, dependencyArgsHint);
                    else
                        log.Info("There is 1 missing dependency{0}.", dependencyArgsHint);


                    if (includeDependencies)
                    {
                        //log.Debug($"Currently set to download dependencies quietly.");
                        foreach (var package in resolver.MissingDependencies)
                        {
                            log.Debug("Adding dependency {0} {1}", package.Name, package.Version);
                            gatheredPackages.Insert(0, package);
                        }
                    }
                    else if (askToIncludeDependencies)
                    {
                        var pkgs = new List<DepRequest>();

                        foreach (var package in resolver.MissingDependencies)
                        {
                            // Handle each package at a time.
                            DepRequest req = null;
                            pkgs.Add(req = new DepRequest { PackageName = package.Name, message = string.Format("{0} {1}", package.Name, package.Version), Response = true });
                            UserInput.Request(req, TimeSpan.MaxValue, true);
                        }
                        

                        foreach (var pkg in resolver.MissingDependencies)
                        {
                            var res = pkgs.FirstOrDefault(r => r.PackageName == pkg.Name);

                            if ((res != null) && res.Response)
                            {
                                gatheredPackages.Insert(0, pkg);
                            }
                        }
                    }
                }
            }
            finally
            {
                // Restore the repositories to its original form, as they might have been altered.
                PackageManagerSettings.Current.Repositories.Clear();
                PackageManagerSettings.Current.Repositories.AddRange(previousRepoList);
                PackageManagerSettings.Current.Save();
            }

            return gatheredPackages;
        }

        internal static List<string> DownloadPackages(string destinationDir, List<PackageDef> PackagesToDownload)
        {
            List<string> downloadedPackages = new List<string>();
            foreach (PackageDef pkg in PackagesToDownload)
            {
                Stopwatch timer = Stopwatch.StartNew();
                List<string> filenameParts = new List<string> { pkg.Name };
                if (pkg.Version != null)
                    filenameParts.Add(pkg.Version.ToString());
                if (pkg.Architecture != CpuArchitecture.AnyCPU)
                    filenameParts.Add(pkg.Architecture.ToString());
                if (!String.IsNullOrEmpty(pkg.OS) && pkg.OS != "Windows")
                    filenameParts.Add(pkg.OS);
                filenameParts.Add("TapPackage");
                var filename = Path.Combine(destinationDir, String.Join(".", filenameParts));

                try
                {
                    PackageDef existingPkg = null;
                    try
                    {
                        if (File.Exists(filename))
                            existingPkg = PackageDef.FromPackage(filename);
                    }
                    catch (Exception e)
                    {
                        log.Warning("Could not open TAP Package. Redownloading package.", e);
                        File.Delete(filename);
                    }
                    
                    if (existingPkg != null)
                    {
                        if (existingPkg.Version == pkg.Version && existingPkg.OS == pkg.OS && existingPkg.Architecture == pkg.Architecture)
                        {
                            if(!PackageCacheHelper.PackageIsFromCache(existingPkg))
                                log.Debug("Package {0} already exists in destination {1}.", pkg.Name, destinationDir);
                        }
                        else
                        {
                            throw new Exception($"The file {filename} is interferring.");
                        }
                    }
                    else
                    {
                        IPackageRepository rm = PackageRepositoryHelpers.DetermineRepositoryType(pkg.Location);
                        if (PackageCacheHelper.PackageIsFromCache(pkg))
                        {
                            rm.DownloadPackage(pkg, filename);
                            log.Info(timer, "Found package '{0}' in cache. Copied to '{1}'.", pkg.Name, Path.GetFullPath(filename));
                        }
                        else
                        {
                            log.Debug("Downloading {0} version {1} from {2}", pkg.Name, pkg.Version, pkg.Location);
                            rm.DownloadPackage(pkg, filename);
                            log.Info(timer, "Downloaded '{0}' to '{1}'.", pkg.Name, Path.GetFullPath(filename));
                            PackageCacheHelper.CachePackage(filename);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Failed to download TAP package.");
                    throw ex;
                }

                downloadedPackages.Add(filename);
            }

            return downloadedPackages;
        }

        public static List<PackageDef> FilterPreRelease(this List<PackageDef> packages, string PreRelease)
        {
            if (PreRelease != null)
                packages = packages.Where(p => (p.Version.PreRelease ?? "").ToLower() == (PreRelease ?? "").ToLower()).ToList();
            else
            {
                var filteredPackages = new List<PackageDef>();
                var packageGroups = packages.GroupBy(p => p.Name);
                foreach (var item in packageGroups)
                {
                    if (item.Any(p => string.IsNullOrEmpty(p.Version.PreRelease)))
                        filteredPackages.AddRange(item.Where(p => string.IsNullOrEmpty(p.Version.PreRelease)));
                    else
                        filteredPackages.AddRange(item);
                }

                packages = filteredPackages;
            }

            return packages;
        }
    }
}
