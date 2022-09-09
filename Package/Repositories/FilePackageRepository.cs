//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenTap.Package.PackageInstallHelpers;
using Tap.Shared;

namespace OpenTap.Package
{
    /// <summary>
    /// Implements a IPackageRepository that queries a local directory for OpenTAP packages.
    /// </summary>
    public class FilePackageRepository : IPackageRepository, IPackageDownloadProgress
    {
#pragma warning disable 1591 // TODO: Add XML Comments in this file, then remove this
        private static TraceSource log = Log.CreateSource("FilePackageRepository");
        internal const string TapPluginCache = ".PackageCache";
        private static object cacheLock = new object();
        private static object loadLock = new object();

        private List<string> allFiles = new List<string>();
        private PackageDef[] allPackages;

        /// <summary>
        /// Constructs a FilePackageRepository for a directory
        /// </summary>
        /// <param name="path">Relative or absolute path or URI to a directory or a file. If file, the repository will be the directory containing the file</param>
        /// <exception cref="NotSupportedException">Path is not a valid file package repository</exception>
        public FilePackageRepository(string path)
        {
            if (Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out Uri uri))
            {
                string absolutePath = null;
                if (uri.IsAbsoluteUri)
                {
                    if (uri.Scheme != Uri.UriSchemeFile)
                        throw new NotSupportedException($"Scheme {uri.Scheme} is not supported as a file package repository ({path}).");
                    absolutePath = uri.AbsolutePath;
                }
                else
                {
                    absolutePath = Path.GetFullPath(path);
                }

                if (File.Exists(absolutePath))
                    AbsolutePath = Path.GetFullPath(Path.GetDirectoryName(absolutePath)).TrimEnd('/', '\\');
                else
                    AbsolutePath = Path.GetFullPath(absolutePath).TrimEnd('/', '\\');

                Url = new Uri(AbsolutePath).AbsoluteUri;
            }
            else
                throw new NotSupportedException($"{path} is not supported as a file package repository.");
        }
        public void Reset()
        {
            allPackages = null;
            LoadPath(new CancellationToken());
        }

        private void LoadPath(CancellationToken cancellationToken)
        {
            if (allPackages != null)
                return;

            if (File.Exists(AbsolutePath) || Directory.Exists(AbsolutePath) == false)
            {
                allPackages = Array.Empty<PackageDef>();

                if (AbsolutePath != PackageDef.SystemWideInstallationDirectory) // Let's ignore this error if the repo is the system wide directory.
                    throw new DirectoryNotFoundException($"File package repository directory not found at: {Url}");

                return;
            }

            lock (loadLock)
            {
                if (allPackages != null)
                    return;

                allFiles = GetAllFiles(AbsolutePath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                allPackages = GetAllPackages(allFiles).ToArray();

                var caches = PackageManagerSettings.Current.Repositories.Select(p => GetCache(p.Url).CacheFileName)
                    .ToHashSet();
                foreach (var file in allFiles)
                {
                    var filename = Path.GetFileName(file);
                    if (filename.StartsWith(TapPluginCache) == false) continue;
                    if (caches.Contains(filename)) continue;
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // This is fine
                    }
                }
            }
        }

        Action<string, long, long> IPackageDownloadProgress.OnProgressUpdate { get; set; }

        internal string AbsolutePath;

        #region IPackageRepository Implementation
        public string Url { get; set; }
        public void DownloadPackage(IPackageIdentifier package, string destination, CancellationToken cancellationToken)
        {
            PackageDef packageDef = null;

            // If the requested package is a file we do not want to start searching the entire repo.
            if (package is PackageDef def && File.Exists((def.PackageSource as FilePackageDefSource)?.PackageFilePath))
            {
                log.Debug("Downloading file without searching repository.");
                packageDef = def;
            }
            else
                LoadPath(cancellationToken);

            if (packageDef == null)
                packageDef = allPackages.FirstOrDefault(p => p.Equals(package));

            bool finished = false;
            try
            {
                var packageFilePath = (packageDef?.PackageSource as FilePackageDefSource)?.PackageFilePath;

                if (packageDef == null || packageFilePath == null)
                    throw new Exception($"Could not download '{package.Name}', because it does not exists");

                if (PathUtils.AreEqual(packageFilePath, destination))
                {
                    finished = true;
                    return; // No reason to copy..
                }
                if (Path.GetExtension(packageFilePath).ToLower() == ".tappackages") // If package is a .TapPackages file, unpack it.
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var packagesFiles = PluginInstaller.UnpackPackage(packageFilePath, Path.GetTempPath());
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
                        FileCopy(path, destination);
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
                    FileCopy(packageFilePath, destination);
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

        // Copying files can be very slow if it is from a network location.
        // this file-copy action copies and notifies of progress.
        void FileCopy(string source, string destination)
        {
            var tmpDestination = destination + ".part-" + Guid.NewGuid();
            using (FileLock.Create(destination + ".lock"))
            {
                if (File.Exists(destination) && PathUtils.CompareFiles(source, destination))
                    return;

                using (var readStream = File.OpenRead(source))
                using (var writeStream = File.OpenWrite(tmpDestination))
                {
                    var task = Task.Run(() => readStream.CopyTo(writeStream));
                    ConsoleUtils.ReportProgressTillEnd(task, "Downloading",
                        () => writeStream.Position,
                        () => readStream.Length,
                        (header, pos, len) =>
                        {
                            ConsoleUtils.printProgress(header, pos, len);
                            (this as IPackageDownloadProgress).OnProgressUpdate?.Invoke(header, pos, len);
                        });
                }

                File.Delete(destination);
                // on most operative systems the same folder would be on the same disk, so this is a no-op.
                try
                {
                    File.Move(tmpDestination, destination);
                }
                catch
                {

                }
            }
        }
        public string[] GetPackageNames(CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
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
        public string[] GetPackageNames(string @class, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
            LoadPath(cancellationToken);

            if (this.allPackages == null || this.allFiles == null) return null;
            var packages = this.allPackages.Where(p => p.Class == @class).ToList();

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
                .GroupBy(p => p.Name).Select(g => g.FirstOrDefault(x => x.Architecture == pid.Architecture) ?? g.First())
                .ToArray();
        }
        public  PackageDef[] CheckForUpdates(IPackageIdentifier[] packages, CancellationToken cancellationToken)
        {
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
            var result = new List<string>();
            var dirs = new Queue<DirectoryInfo>();
            dirs.Enqueue(new DirectoryInfo(path));

            while (dirs.Any() && cancellationToken.IsCancellationRequested == false)
            {
                var dir = dirs.Dequeue();
                try
                {
                    var content = dir.EnumerateDirectories();
                    foreach (var subDir in content)
                    {
                        dirs.Enqueue(subDir);
                    }

                    result.AddRange(dir.EnumerateFiles().Select(f => f.FullName));
                }
                catch (Exception)
                {
                    log.Debug($"Access to path {dir.FullName} denied. Ignoring.");
                }
            }

            return result;
        }
        private PackageDef[] loadPackagesFromFile(IEnumerable<FileInfo> allFiles)
        {
            var allPackages = new List<PackageDef>();
            var packagesFiles = allFiles.Where(f => f.Extension.ToLower() == ".tappackages").ToHashSet();

            // Deserializer .TapPackages files
            Parallel.ForEach(packagesFiles, packagesFile =>
            {
                List<PackageDef> packages;
                try
                {
                    packages = PackageDef.FromPackages(packagesFile.FullName);
                    if (packages == null) return;
                }
                catch (Exception e)
                {
                    log.Error($"Could not unpackage '{packagesFile.FullName}'");
                    log.Debug(e);
                    return;
                }

                packages.ForEach(p =>
                {
#pragma warning disable 618
                    p.Location = packagesFile.FullName;
#pragma warning restore 618
                    p.PackageSource = new FileRepositoryPackageDefSource
                    {
                        RepositoryUrl = Url,
                        PackageFilePath = packagesFile.FullName
                    };
                });
                lock (allPackages)
                {
                    allPackages.AddRange(packages);
                }
            });

            // Deserialize all regular packages
            Parallel.ForEach(allFiles, pluginFile =>
            {
                if (packagesFiles.Contains(pluginFile))
                    return;

                PackageDef package;
                try
                {
                    package = PackageDef.FromPackage(pluginFile.FullName);
                    if (package == null) return;
                }
                catch
                {
                    return;
                }

                package.PackageSource = new FileRepositoryPackageDefSource
                {
                    RepositoryUrl = Url,
                    PackageFilePath = pluginFile.FullName
                };

                lock (allPackages)
                {
                    allPackages.Add(package);
                }
            });

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
                            if (allPackages.Any(p => !allFiles.Any(f =>
                            {
                                var packageFilePath = (p.PackageSource as FileRepositoryPackageDefSource)?.PackageFilePath;
                                return string.IsNullOrWhiteSpace(packageFilePath) == false && PathUtils.AreEqual(f, Path.GetFullPath(packageFilePath));
                            })))
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
            var hash = String.Format("{0:X8}", MurMurHash3.Hash(url ?? Url));
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
