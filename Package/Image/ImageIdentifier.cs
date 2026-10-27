using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers;
using OpenTap.Cli;
using OpenTap.Package.Zip;

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
                /* Always include the base hash in the hash calculation.
                 * The values the base hash is derived from are normally included in pkg.Hash and pkg.ComputeHash().
                 * This guards against hash collisions if a package contains an incorrect hash.
                 *
                 * The main way this can happen is if a user unzips a package,
                 * modifies the content, and then zips the package again and uploads a new version.
                 * If the user neglected to update the package hash, this can lead to weird
                 * downstream bugs if consumers expect image hashes to be distinct.
                 */
                var baseHash = $"{pkg.Name} {pkg.Version} {pkg.Architecture} {pkg.OS}";
                if (pkg.Hash != null)
                {
                    packageHashes.Add($"{baseHash}|{pkg.Hash}");
                }
                else
                {
                    // This can happen if the package was created with OpenTAP < 9.16 that did not set the Hash property.
                    // We can just try to compute the hash now.
                    try
                    {
                        packageHashes.Add($"{baseHash}|{pkg.ComputeHash()}");
                    }
                    catch
                    {
                        // This might happen if the PackageDef does not contain <Hash> elements for each file (for packages crated with OpenTAP < 9.5).
                        // In this case, just use the fields from IPackageIdentifier, they should be unique in most cases.
                        packageHashes.Add(baseHash);
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

        struct StagingResult
        {
            public PackageDef Package;
            public string FilePath;
            public StagingInstaller.StagedPackage Ctx;
        }

        static bool PassthroughException(Exception ex) => ex is OperationCanceledException or ExitCodeException;

        static Stream GetPackageStream(PackageDef package, Action<long, long> downloadProgress, CancellationToken token, out string filepath)
        {
            filepath = null;
            if (package.PackageSource is IFilePackageDefSource fd)
            {
                filepath = fd.PackageFilePath;
            }
            else if (CachedLocation(package) is string cachedLocation)
            {
                filepath = cachedLocation;
            }
            else if (package.PackageSource is IRepositoryPackageDefSource repoSource
                && PackageRepositoryHelpers.DetermineRepositoryType(repoSource.RepositoryUrl) is HttpPackageRepository http)
            {
                filepath = PackageCacheHelper.GetCacheFilePath(package);
                return StartStreamingDownload(http, package, filepath, downloadProgress, token);
            }

            if (filepath != null)
            {
                /* The package is already on disk. The zip reader reads it from start to end, so the amount read
                 * tracks extraction, and is reported in place of download progress. */
                return new ReadProgressStream(File.OpenRead(filepath), downloadProgress);
            }

            try
            {
                /* In this case, we don't know the actual download size, and the progress callback
                 * only gives us the percentage completion.
                 * Just report percentage/100. */
                downloadProgress(0, 100);
                filepath = Download(package, (pct, _) => downloadProgress(pct, 100), token);
            }
            catch (Exception ex) when (package.PackageSource is IRepositoryPackageDefSource repo && !PassthroughException(ex))
            {
                throw NetworkError(package, repo.RepositoryUrl, ex);
            }

            /* The download already reported its progress. Reporting the read progress here would restart at 0%. */
            var stream = File.OpenRead(filepath);
            downloadProgress(stream.Length, stream.Length);
            return stream;
        }

        static ExitCodeException NetworkError(PackageDef package, string repositoryUrl, Exception ex)
        {
            /* Some repository errors have no message, e.g. an error response without a body. */
            var reason = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
            return DeploymentError(ExitCodes.NetworkError,
                $"Failed to download {Describe(package)} from '{repositoryUrl}': {reason}", ex);
        }

        /// <summary>
        /// Starts downloading a package into the package cache, and returns a stream that reads the package while it is
        /// being downloaded. The download is written to a temporary file next to <paramref name="cachePath"/>, which is
        /// moved into the cache when the download completes. Reading the returned stream to the end therefore
        /// guarantees that the package is available at <paramref name="cachePath"/>.
        /// Download errors are rethrown by the stream as <see cref="ExitCodes.NetworkError"/>.
        /// </summary>
        static Stream StartStreamingDownload(HttpPackageRepository repo, PackageDef package, string cachePath,
            Action<long, long> downloadProgress, CancellationToken token)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
            var tmpPath = cachePath + "." + Guid.NewGuid().ToString("N");
            /* FileShare.Delete allows the temporary file to be moved into the cache while it is still being read. */
            const FileShare share = FileShare.ReadWrite | FileShare.Delete;
            /* bufferSize: 1 disables FileStream's buffers. Each write goes straight to the file before the reader is
             * signaled, and the reader does not cache the end of the file. TailingFileStream does its own buffering. */
            var file = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, share, bufferSize: 1);
            var signal = new FileWriteSignal();
            var writer = new SignalingWriteStream(file, signal);
            FileStream reader;
            try
            {
                reader = new FileStream(tmpPath, FileMode.Open, FileAccess.Read, share, bufferSize: 1);
            }
            catch
            {
                writer.Dispose();
                File.Delete(tmpPath);
                throw;
            }

            log.Info($"Downloading {package.Name} version {package.Version} from {repo.Url}");
            TapThread.Start(() =>
            {
                try
                {
                    var timer = Stopwatch.StartNew();
                    /* Disposing the writer flushes it, so all data is in the file before the download completes. */
                    using (writer)
                        repo.DoDownloadPackage(package, writer, token, downloadProgress);
                    token.ThrowIfCancellationRequested();
                    /* The file must be moved into the cache before signal.Complete() is called. The reader only reports
                     * the end of the stream after it has seen Complete(), so when a reader has read the stream to the
                     * end, the file is guaranteed to be at cachePath. The ZipArchive fallback in StagePackages relies on
                     * this to open the cached file without waiting for it to appear. */
                    if (File.Exists(cachePath))
                        File.Delete(cachePath);
                    File.Move(tmpPath, cachePath);
                    log.Debug(timer, "Downloaded {0} version {1} ({2}).", package.Name, package.Version,
                        Utils.BytesToReadable(new FileInfo(cachePath).Length));
                    signal.Complete();
                }
                catch (Exception ex) when (token.IsCancellationRequested)
                {
                    signal.Complete(new OperationCanceledException("Download cancelled.", ex, token));
                }
                catch (Exception ex)
                {
                    signal.Complete(PassthroughException(ex) ? ex : NetworkError(package, repo.Url, ex));
                }
                finally
                {
                    /* Does nothing if the file was moved into the cache. */
                    try { File.Delete(tmpPath); }
                    catch (Exception ex) { log.Debug("Unable to delete temporary download '{0}': {1}", tmpPath, ex.Message); }
                }
            }, $"Download {package.Name}");

            return new TailingFileStream(reader, signal, token);
        }

        static string Describe(PackageDef package) => $"package '{package.Name}' version {package.Version}";

        static ExitCodeException DeploymentError(ExitCodes code, string message, Exception inner) =>
            DeploymentError((int)code, message, inner);

        static ExitCodeException DeploymentError(PackageExitCodes code, string message, Exception inner) =>
            DeploymentError((int)code, message, inner);

        /// <summary>
        /// Creates the exception used to report a deployment failure. <see cref="ExitCodeException"/> cannot carry
        /// an inner exception, so the original exception is logged at debug level to keep the stack trace.
        /// </summary>
        static ExitCodeException DeploymentError(int code, string message, Exception inner)
        {
            if (inner != null)
                log.Debug(inner);
            return new ExitCodeException(code, message);
        }

        /// <summary>
        /// Maps an exception thrown while staging (extracting) a package to an <see cref="ExitCodeException"/>.
        /// Exceptions that already carry an exit code, and cancellations, are returned unchanged.
        /// </summary>
        static Exception StagingError(PackageDef package, string filePath, Exception ex)
        {
            switch (ex)
            {
                case ExitCodeException:
                case OperationCanceledException:
                    return ex;
                case InvalidDataException:
                case EndOfStreamException:
                    return DeploymentError(PackageExitCodes.InvalidPackageArchive,
                        $"Failed to extract {Describe(package)} from '{filePath}': the package file is invalid or corrupt. {ex.Message}", ex);
                default:
                    return DeploymentError(PackageExitCodes.PackageInstallError,
                        $"Failed to extract {Describe(package)} from '{filePath ?? "<unknown>"}': {ex.Message}", ex);
            }
        }

        const int StagingWorkerCount = 8;

        /// <summary>
        /// Stages all packages using a bounded pool of worker threads. Workers pick packages in
        /// install order, so the packages the sequential install loop needs first are staged first.
        /// </summary>
        static Task<StagingResult>[] StagePackages(StagingInstaller installer, IReadOnlyList<PackageDef> packages, CancellationToken token, Action<int, int, string> onProgress)
        {
            var sources = new TaskCompletionSource<StagingResult>[packages.Count];
            for (int i = 0; i < sources.Length; i++)
                sources[i] = new TaskCompletionSource<StagingResult>();

            int next = -1;
            int workers = Math.Min(StagingWorkerCount, packages.Count);
            for (int w = 0; w < workers; w++)
            {
                TapThread.Start(() =>
                {
                    int pkgIndex;
                    while ((pkgIndex = Interlocked.Increment(ref next)) < packages.Count)
                    {
                        var package = packages[pkgIndex];
                        if (token.IsCancellationRequested)
                        {
                            sources[pkgIndex].TrySetCanceled(token);
                            continue;
                        }
                        StagingResult result = new() { Package = package };
                        try
                        {
                            void downloadProgress(long count, long total)
                            {
                                if (total == 0) return;
                                double percentage = 100 * (double)count / total;
                                onProgress(pkgIndex, (int)percentage, "Downloading");
                            }
                            using Stream packageStream = GetPackageStream(package, downloadProgress, token, out result.FilePath);
                            try
                            {
                                result.Ctx = installer.StagePackageFromStream(result.FilePath, packageStream, token);
                            }
                            catch (UnstreamableZipException)
                            {
                                /* File cannot be streamed. Fall back to native ZipArchive.
                                 * For a streaming download, reading to the end does not race with the download thread
                                 * moving the file into the cache: the stream only ends after the download has signaled
                                 * completion, which happens after the move (see StartStreamingDownload). The file is
                                 * therefore at result.FilePath once CopyTo returns. */
                                packageStream.CopyTo(Stream.Null);
                                result.Ctx = installer.StagePackageFromFile(result.FilePath, token);
                            }
                            sources[pkgIndex].SetResult(result);
                        }
                        catch (Exception) when (token.IsCancellationRequested)
                        {
                            // Staging was cancelled, either by the user or because the deployment already failed.
                            // Errors raised as a side effect of cancellation (e.g. an aborted download) are not
                            // the cause of the failure, so they are not reported.
                            sources[pkgIndex].TrySetCanceled(token);
                        }
                        catch (Exception ex)
                        {
                            sources[pkgIndex].SetException(StagingError(package, result.FilePath, ex));
                        }
                    }
                });
            }
            return [.. sources.Select(s => s.Task)];
        }

        private static void Install(IEnumerable<PackageDef> modifyOrAdd, string target,
            ProgressUpdateDelegate progress, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            progress ??= (percent, message) => { };
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var packagesInOrder = OrderPackagesForInstallation(modifyOrAdd);
            var installer = new StagingInstaller(target);

            int[] StagingProgress = new int[packagesInOrder.Count];
            int[] InstallProgress = new int[packagesInOrder.Count];

            void AnnounceProgress(string message)
            {
                try
                {
                    double stg = (double)StagingProgress.Sum() / StagingProgress.Length;
                    double inst = (double)InstallProgress.Sum() / InstallProgress.Length;
                    progress((int)(stg * 0.8 + inst * 0.2), message);
                }
                catch
                {
                    /* ignore */
                }
            }

            // Start staging first, so it runs while existing files are moved aside below.
            var StagedPackages = StagePackages(installer, packagesInOrder, cts.Token, (package, percentage, what) =>
            {
                StagingProgress[package] = percentage;
                AnnounceProgress($"{what} {packagesInOrder[package].Name}");
            });

            for (int i = 0; i < StagedPackages.Length; i++)
            {
                int index = i;
                StagedPackages[i].ContinueWith(t =>
                {
                    StagingProgress[index] = 100;
                    AnnounceProgress($"Staged {packagesInOrder[index].Name}");
                }, cts.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
            }

            try
            {
                // Move existing files aside for all packages at once. Uses only the package definitions, so it runs
                // on this thread while packages are staged, and must finish before the first package is installed.
                installer.PrepareDestination(packagesInOrder);

                for (int i = 0; i < packagesInOrder.Count; i++)
                {
                    // GetResult() rethrows the worker's exception directly. Task.Result would wrap it in an
                    // AggregateException, losing the exit code and the error message.
                    var ctx = StagedPackages[i].GetAwaiter().GetResult();
                    try
                    {
                        installer.InstallStagedPackage(sw, ctx.Ctx);
                    }
                    catch (Exception ex) when (ex is not ExitCodeException && ex is not OperationCanceledException)
                    {
                        throw DeploymentError(PackageExitCodes.PackageInstallError,
                            $"Failed to install {Describe(ctx.Package)}: {ex.Message}", ex);
                    }
                    InstallProgress[i] = 100;
                    AnnounceProgress($"Installed {ctx.Package.Name}");
                }
            }
            catch (Exception ex)
            {
                // Stop staging and wait for the workers to finish, so nothing is written to the staging directory
                // while it is being deleted.
                cts.Cancel();
                try { Task.WaitAll(StagedPackages); } catch { /* inspected below */ }

                // Staging runs in parallel, so other packages may have failed independently of the error that
                // stopped the deployment. Report them as well, so the user sees every problem at once.
                foreach (var task in StagedPackages)
                {
                    if (task.IsFaulted && task.Exception?.InnerException is Exception stagingError && stagingError != ex)
                        log.Error(stagingError.Message);
                }

                installer.Rollback();

                if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException("Deployment operation cancelled by user", ex, cancellationToken);
                if (ex is ExitCodeException || ex is OperationCanceledException)
                    throw;
                throw DeploymentError(PackageExitCodes.PackageInstallError, $"Image deployment failed: {ex.Message}", ex);
            }

            installer.FinalizeInstall();
        }

        private static void Uninstall(IEnumerable<PackageDef> packagesToUninstall, string target,
            ProgressUpdateDelegate progress, CancellationToken cancellationToken)
        {
            var orderedPackagesToUninstall = OrderPackagesForInstallation(packagesToUninstall);
            orderedPackagesToUninstall.Reverse();

            List<Exception> uninstallErrors = new List<Exception>();
            var newInstaller = new Installer(target, cancellationToken);
            newInstaller.ProgressUpdate += (percent, message) => progress?.Invoke(percent, message);

            newInstaller.Error += uninstallErrors.Add;

            newInstaller.PackagePaths.AddRange(orderedPackagesToUninstall.Select(x => (x.PackageSource as XmlPackageDefSource)?.PackageDefFilePath).ToList());

            int exitCode = newInstaller.RunCommand(Installer.PrepareUninstall, false, false);
            ThrowOnUninstallError(exitCode, uninstallErrors, cancellationToken);

            exitCode = newInstaller.RunCommand(Installer.Uninstall, false, true);
            ThrowOnUninstallError(exitCode, uninstallErrors, cancellationToken);
        }

        static void ThrowOnUninstallError(int exitCode, List<Exception> errors, CancellationToken cancellationToken)
        {
            if (exitCode == (int)ExitCodes.Success && errors.Count == 0)
                return;
            if (exitCode == (int)ExitCodes.UserCancelled || cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Deployment operation cancelled by user", cancellationToken);

            foreach (var error in errors)
            {
                log.Error(error.Message);
                log.Debug(error);
            }
            throw new ExitCodeException((int)PackageExitCodes.PackageUninstallError,
                "Image deployment failed to uninstall existing packages.");
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

        private static string Download(PackageDef package, ProgressUpdateDelegate progress,
            CancellationToken token)
        {
            progress ??= (percent, message) => { };
            if (CachedLocation(package) is string cachedLocation)
            {
                log.Info($"Package {package.Name} exists in cache: {cachedLocation}");
                return cachedLocation;
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
            return filename;
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
