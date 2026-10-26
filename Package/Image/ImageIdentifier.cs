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

        static bool Passthrough(Exception ex) => ex is OperationCanceledException or ExitCodeException;

        static Stream GetPackageStream(PackageDef package, CancellationToken token, out string filepath)
        {
            if (package.PackageSource is IFilePackageDefSource fd)
            {
                filepath = fd.PackageFilePath;
                return File.OpenRead(fd.PackageFilePath);
            }

            if (CachedLocation(package) is string cachedLocation)
            {
                filepath = cachedLocation;
                return File.OpenRead(cachedLocation);
            }

            // TODO: Update this so the download stream can be consumed in-flight.
            // When that happens, read errors while staging from the stream may also be network errors.
            try
            {
                filepath = Download(package, null, token);
            }
            catch (Exception ex) when (package.PackageSource is IRepositoryPackageDefSource repo && !Passthrough(ex))
            {
                throw DeploymentError(ExitCodes.NetworkError,
                    $"Failed to download {Describe(package)} from '{repo.RepositoryUrl}': {ex.Message}", ex);
            }
            return File.OpenRead(filepath);
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
                // Corrupt, truncated or otherwise unreadable package archives.
                // UnstreamableZipException is not listed: it never reaches here, because staging falls back to
                // ZipArchive when it is thrown.
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
        static Task<StagingResult>[] StagePackages(StagingInstaller installer, IReadOnlyList<PackageDef> packages, CancellationToken token)
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
                    int i;
                    while ((i = Interlocked.Increment(ref next)) < packages.Count)
                    {
                        var package = packages[i];
                        if (token.IsCancellationRequested)
                        {
                            sources[i].TrySetCanceled(token);
                            continue;
                        }
                        StagingResult result = new() { Package = package };
                        try
                        {
                            using Stream packageStream = GetPackageStream(package, token, out result.FilePath);
                            try 
                            {
                                result.Ctx = installer.StagePackageFromStream(result.FilePath, packageStream, token);
                            }
                            catch (UnstreamableZipException)
                            {
                                /* File cannot be streamed. Fall back to native ZipArchive.
                                 * By exhausting the stream we can be sure the download operation is complete.
                                 * The file should now be locally available. */
                                packageStream.CopyTo(Stream.Null);
                                for (int retry = 0; retry < 10; retry++)
                                {
                                    /* The downloaded file may be incrementally downloaded to a temp path.
                                     * Wait for it to exist before proceeding */
                                    if (File.Exists(result.FilePath)) break;
                                    /* sleep for 1, 2, 4, ... 1024 ms (total of ~2s)
                                     * This should be more than enough since we already exhausted the stream. */
                                    TapThread.Sleep(1 << retry);
                                }
                                if (!File.Exists(result.FilePath))
                                    throw new FileNotFoundException("The package file does not exist.", result.FilePath);
                                result.Ctx = installer.StagePackageFromFile(result.FilePath, token);
                            }
                            sources[i].SetResult(result);
                        }
                        catch (Exception) when (token.IsCancellationRequested)
                        {
                            // Staging was cancelled, either by the user or because the deployment already failed.
                            // Errors raised as a side effect of cancellation (e.g. an aborted download) are not
                            // the cause of the failure, so they are not reported.
                            sources[i].TrySetCanceled(token);
                        }
                        catch (Exception ex)
                        {
                            sources[i].SetException(StagingError(package, result.FilePath, ex));
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

            // Start staging first, so it runs while existing files are moved aside below.
            var StagedPackages = StagePackages(installer, packagesInOrder, cts.Token);

            int[] StagingProgress = new int[packagesInOrder.Count];
            int[] InstallProgress = new int[packagesInOrder.Count];

            void AnnounceProgress(string message)
            {
                double stg = (double)StagingProgress.Sum() / StagingProgress.Length;
                double inst = (double)InstallProgress.Sum() / InstallProgress.Length;
                progress((int)(stg * 0.8 + inst * 0.2), message);
            }

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
