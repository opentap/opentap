//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Tap.Shared;

namespace OpenTap.Package
{
    public class FilePackageRepository : IPackageRepository
    {
        private static TraceSource log = Log.CreateSource("FilePackageRepository");
        internal const string TapPluginCache = ".TapPackageCache";
        private static object cacheLock = new object();

        private List<string> allFiles = new List<string>();
        private PackageDef[] allPackages;
        
        public FilePackageRepository(string path)
        {
            Url = File.Exists(path) ? Path.GetDirectoryName(path) : path.Trim();
        }
        public void Reset()
        {
            LoadPath(new CancellationToken());
        }
        private void LoadPath(CancellationToken cancellationToken)
        {
            if (File.Exists(Url) || Directory.Exists(Url) == false)
            {
                allPackages = new PackageDef[0];

                if (Url != PackageDef.SystemWideInstallationDirectory) // Let's ignore this error if the repo is the system wide directory.
                    throw new DirectoryNotFoundException(string.Format("File package repository directory not found at: {0}", Url));

                return;
            }

            allFiles = Directory.GetFiles(Url).ToList();
            cancellationToken.ThrowIfCancellationRequested();
            allFiles.AddRange(GetAllFiles(Url, cancellationToken));
            allPackages = GetAllPackages(allFiles).ToArray();

            var caches = PackageManagerSettings.Current.Repositories.Select(p => GetCache(p.Url).CacheFileName);
            foreach (var file in allFiles)
            {
                var filename = Path.GetFileName(file);
                if (filename.StartsWith(TapPluginCache) == false) continue;
                if (caches.Any(r => r == filename)) continue;
                try
                {
                    File.Delete(file);
                }
                catch { }
            }
        }

        #region IPackageRepository Implementation
        public string Url { get; set; }
        public void DownloadPackage(IPackageIdentifier package, string destination, CancellationToken cancellationToken)
        {
            if (allPackages == null)
                LoadPath(cancellationToken);
            
            bool finished = false;
            try
            {
                var packageDef = allPackages.FirstOrDefault(p => p.Equals(package));
                if (packageDef == null)
                    throw new Exception($"Could not download '{package.Name}', because it does not exists");
                if (PathUtils.AreEqual(packageDef.Location, destination))
                {
                    finished = true;
                    return; // No reason to copy..
                }
                if (Path.GetExtension(packageDef.Location).ToLower() == ".tappackages") // If package is a .TapPackages file, unpack it.
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var packagesFiles = PluginInstaller.UnpackPackage(packageDef.Location, Path.GetTempPath());
                    string path = null;
                    foreach (var packageFile in packagesFiles)
                    {
                        var internalPackage = PackageDef.FromPackage(packageFile);
                        if (internalPackage.Name == packageDef.Name && internalPackage.Version == packageDef.Version)
                        {
                            path = packageFile;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(path) == false)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        File.Copy(path, destination, true);
                        finished = true;
                    }

                    foreach (var packageFile in packagesFiles)
                    {
                        if (File.Exists(packageFile))
                            File.Delete(packageFile);
                    }
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    File.Copy(packageDef.Location, destination, true);
                    finished = true;
                }
            }
            catch (Exception ex)
            {
                if (!(ex.InnerException is TaskCanceledException))
                {
                    throw;
                }
            }
            finally
            {
                if ((!finished || cancellationToken.IsCancellationRequested) && File.Exists(destination))
                    File.Delete(destination);
            }
        }
        public string[] GetPackageNames(CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
            if (allPackages == null)
                LoadPath(cancellationToken);
            
            if (this.allPackages == null || this.allFiles == null) return null;
            var packages = this.allPackages.ToList();

            // Check if package dependencies are compatible
            compatibleWith = CheckCompatibleWith(compatibleWith);
            if (compatibleWith != null)
                packages = packages.Where(p => p.Dependencies.All(d => compatibleWith.All(r => IsCompatible(d, r)))).ToList();
            
            return packages
                .Select(p => p.Name)
                .Distinct()
                .ToArray();
        }
        public PackageVersion[] GetPackageVersions(string packageName, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
            if (allPackages == null)
                LoadPath(cancellationToken);
            
            if (this.allPackages == null || this.allFiles == null) return null;
            var packages = this.allPackages.ToList();
            
            // Check if package dependencies are compatible
            compatibleWith = CheckCompatibleWith(compatibleWith);
            if (compatibleWith != null)
                packages = packages.Where(p => p.Dependencies.All(d => compatibleWith.All(r => IsCompatible(d, r)))).ToList();
            
            return packages
                .Where(p => p.Name == packageName)
                .Select(p => new PackageVersion(packageName, p.Version, p.OS, p.Architecture, p.Date, 
                    p.Files.Where(f => string.IsNullOrWhiteSpace(f.LicenseRequired) == false).Select(f => f.LicenseRequired).ToList()))
                .Distinct()
                .ToArray();
        }
        public PackageDef[] GetPackages(PackageSpecifier pid, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
            if (allPackages == null)
                LoadPath(cancellationToken);
            
            if (this.allPackages == null || this.allFiles == null) return null;
            
            // Filter packages
            var openTapIdentifier = new PackageIdentifier("OpenTAP", PluginManager.GetOpenTapAssembly().SemanticVersion.ToString(), CpuArchitecture.Unspecified, null);
            var packages = new List<PackageDef>();
            compatibleWith = CheckCompatibleWith(compatibleWith);

            foreach (var package in allPackages)
            {
                if (string.IsNullOrWhiteSpace(pid.Name) == false && (pid.Name != package.Name))
                    continue;
                if (!pid.Version.IsCompatible(package.Version))
                    continue;
                if (package.IsPlatformCompatible(pid.Architecture, pid.OS) == false)
                    continue;

                // Check if package dependencies are compatible
                if (package.Dependencies.All(d => compatibleWith.All(r => IsCompatible(d, r))) == false)
                    continue;

                packages.Add(package);
            }

            // If we should not check compatibility, take the one that is most compatible.
            if (compatibleWith?.Length == 0)
            {
                List<Tuple<int, PackageDef>> filteredPackages = new List<Tuple<int, PackageDef>>();
                foreach (var item in packages)
                {
                    filteredPackages.Add(Tuple.Create(item.Dependencies.Count(d => IsCompatible(d, openTapIdentifier)), item));
                }

                // Find most compatible packages
                packages = filteredPackages
                    .OrderByDescending(p => (double) p.Item1 / p.Item2.Dependencies.Count)
                    .ThenByDescending(p => p.Item2.Version).Select(p => p.Item2).ToList();
            }

            // Select the latest of each packagename left
            return packages
                .GroupBy(p => p.Name).Select(g => g.First())
                .ToArray();
        }
        public  PackageDef[] CheckForUpdates(IPackageIdentifier[] packages, CancellationToken cancellationToken)
        {
            if (allPackages == null)
                LoadPath(cancellationToken);
            
            if (allPackages == null || allFiles == null) return null;
            
            List<PackageDef> latestPackages = new List<PackageDef>();
            var openTapIdentifier = new PackageIdentifier("OpenTAP", PluginManager.GetOpenTapAssembly().SemanticVersion.ToString(), CpuArchitecture.Unspecified, null);

            // Find updated packages
            foreach (var packageIdentifier in packages)
            {
                if (packageIdentifier == null)
                    continue;
                
                var package = new PackageIdentifier(packageIdentifier);

                // Try finding a OpenTAP package
                var latest = allPackages
                    .Where(p => package.Equals(p))
                    .Where(p => p.Dependencies.All(dep => IsCompatible(dep, openTapIdentifier))).FirstOrDefault(p => p.Version != null && p.Version.CompareTo(package.Version) > 0);

                if (latest != null)
                    latestPackages.Add(latest);
            }

            return latestPackages.ToArray();
        }
        #endregion
        
        #region file system
        private void CreatePackageCache(IEnumerable<PackageDef> packages, FileRepositoryCache cache)
        {
            // Serialize all packages
            string xmlText;
            using (Stream stream = new MemoryStream())
            {
                PackageDef.SaveManyTo(stream, packages);
                stream.Position = 0;
                xmlText = new StreamReader(stream).ReadToEnd();
            }

            string currentDir = FileSystemHelper.GetCurrentInstallationDirectory();
            // Delete existing cache
            List<string> caches = Directory.GetFiles(currentDir, $"{TapPluginCache}.{cache.Hash}*").ToList();
            caches.ForEach(File.Delete);
            
            // Save cache
            string fullPath = Path.Combine(currentDir, cache.CacheFileName);
            if (File.Exists(fullPath))
                File.SetAttributes(fullPath, FileAttributes.Normal);
            File.WriteAllText(fullPath, xmlText);
            File.SetAttributes(fullPath, FileAttributes.Hidden);
        }
        private List<string> GetAllFiles(string path, CancellationToken cancellationToken)
        {
            List<string> files = new List<string>();

            if (new DirectoryInfo(path).Name.ToLower() == "obj")
                return files;

            foreach (var directory in Directory.GetDirectories(path))
            {
                var dirFiles = Directory.GetFiles(directory);
                foreach (var file in dirFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    files.Add(file);
                }

                cancellationToken.ThrowIfCancellationRequested();
                files.AddRange(GetAllFiles(directory, cancellationToken));
            }

            return files;
        }
        private PackageDef[] loadPackagesFromFile(IEnumerable<FileInfo> allFiles)
        {
            var allPackages = new List<PackageDef>();
            var packagesFiles = allFiles.Where(f => f.Extension.ToLower() == ".tappackages").ToHashSet();

            foreach (var packagesFile in packagesFiles)
            {
                List<PackageDef> packages;
                try
                {
                    packages = PackageDef.FromPackages(packagesFile.FullName);
                    if (packages == null) continue;
                }
                catch
                {
                    continue;
                }

                packages.ForEach(p => p.Location = packagesFile.FullName);
                allPackages.AddRange(packages);
            }
            
            foreach (var pluginFile in allFiles)
            {
                if (packagesFiles.Contains(pluginFile))
                    continue;

                PackageDef package;
                try
                {
                    package = PackageDef.FromPackage(pluginFile.FullName);
                    if (package == null) continue;
                }
                catch
                {
                    continue;
                }
                package.Location = pluginFile.FullName;
                allPackages.Add(package);
            }

            return allPackages.ToArray();
        }
        private List<PackageDef> GetAllPackages(List<string> allFiles)
        {
            // Get cache
            var cache = GetCache();

            // Find TapPackages in repo
            var allFileInfos = allFiles.Select(f => new FileInfo(f)).Where(f => f.Extension.ToLower() == ".tapplugin" || f.Extension.ToLower() == ".tappackage" || f.Extension.ToLower() == ".tappackages").ToList();

            List<PackageDef> allPackages = null;
            lock (cacheLock)
            {
                try
                {
                    if (File.Exists(cache.CacheFileName))
                    {
                        if (allFileInfos.Count == cache.CachePackageCount)
                        {
                            // Load cache
                            using (var str = File.OpenRead(cache.CacheFileName))
                                allPackages = PackageDef.ManyFromXml(str).ToList();
    
                            // Check if any files has been replaced
                            if (allPackages.Any(p => !allFiles.Any(f => p.Location == f)))
                                allPackages = null;
    
                            // Check if the cache is the newest file
                            if (allPackages != null && allFileInfos.Any() && (allFileInfos.Max(f => f.LastWriteTimeUtc) > new FileInfo(cache.CacheFileName).LastWriteTimeUtc))
                                allPackages = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Warning("Error while reading package cache from '{0}'. Rebuilding cache.", cache.CacheFileName);
                    log.Debug(ex);
                }

                if (allPackages == null)
                {
                    // Get all packages
                    allPackages = loadPackagesFromFile(allFileInfos).ToList();
                    cache.CachePackageCount = allFileInfos.Count;

                    // Create cache
                    CreatePackageCache(allPackages, cache);
                }
            }

            // Order packages
            allPackages = allPackages.OrderByDescending(p => p.Version).ToList();

            return allPackages;
        }
        #endregion
        
        #region helper
        private static bool IsCompatible(PackageDependency dep, IPackageIdentifier packageIdentifier)
        {
            try
            {
                if (dep.Name == packageIdentifier.Name)
                {
                    return dep.Version.IsCompatible(packageIdentifier.Version);
                }
            }
            catch
            {
                log.Warning("Dependency '{0}' is not compatible with '{1}'.", dep.Name, packageIdentifier.Name);
                throw;
            }

            return true;
        }
        private IPackageIdentifier[] CheckCompatibleWith(IPackageIdentifier[] compatibleWith)
        {
            var list = compatibleWith?.ToList();

            var openTap = list?.FirstOrDefault(p => p.Name == "OpenTAP");
            if (openTap != null)
            {
                list.AddRange(new []
                {
                    new PackageIdentifier("Tap", openTap.Version, openTap.Architecture, openTap.OS),
                    new PackageIdentifier("TAP Base", openTap.Version, openTap.Architecture, openTap.OS)
                });
            }
            
            return list?.ToArray();
        }

        private FileRepositoryCache GetCache(string url = null)
        {
            var hash = MurMurHash3.Hash(url ?? Url).ToString();
            var files = Directory.GetFiles(FileSystemHelper.GetCurrentInstallationDirectory(), $"{TapPluginCache}*");
            var filePath = files.FirstOrDefault(f => f.Contains(hash));

            if (File.Exists(filePath))
            {
                var matches = Regex.Split(Path.GetFileName(filePath), "\\.");

                if (int.TryParse(matches[3], out int count))
                    return new FileRepositoryCache() {Hash = hash, CachePackageCount = count};
            }

            return new FileRepositoryCache() {Hash = hash};
        }
        #endregion
    }

    class FileRepositoryCache
    {
        public int CachePackageCount { get; set; }
        public string Hash { get; set; }

        public string CacheFileName => $"{FilePackageRepository.TapPluginCache}.{Hash}.{CachePackageCount}.xml";
    }
}
