using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OpenTap.Package;

internal class StagingInstaller
{
    public string TapDir { get; }
    string StagingDir => Path.Combine(TapDir, ".install");

    public StagingInstaller(string tapdir)
    {
        TapDir = tapdir;
        SearchBetweenInstalls = SameDirectory(TapDir, Installation.Current.Directory);
        directories.EnsureExists(StagingDir);
    }

    private static readonly TraceSource log = Log.CreateSource("Installer");

    static bool SameDirectory(string d1, string d2)
    {
        var comparer = OperatingSystem.Current == OperatingSystem.Linux ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        d1 = normalize(d1);
        d2 = normalize(d2);
        return d1.Equals(d2, comparer);
        static string normalize(string s) => Path.GetFullPath(s).Replace('\\', '/').TrimEnd('/');
    }

    /// <summary>
    /// It doesn't make sense to search between installs if the target directory is different
    /// from the current installation. This can affect the behavior of the current process
    /// in unpredictable ways, and will potentially make the target installation state dependent
    /// on what is installed in the deploying installation.
    /// </summary>
    bool SearchBetweenInstalls { get; }


    /// <summary>
    /// Cache created directories to avoid calling CreateDirectory multiple times for
    /// the same directory. This saves a significant amount of time when writing many small files.
    /// </summary>
    internal class DirectoryCache
    {
        static StringComparer comparer => OperatingSystem.Current == OperatingSystem.Linux ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        readonly HashSet<string> known = new(comparer);
        public void EnsureExists(string directory)
        {
            if (string.IsNullOrEmpty(directory)) return;
            directory = Path.GetFullPath(directory).Replace('\\', '/').TrimEnd('/');
            if (known.Add(directory))
            {
                Directory.CreateDirectory(directory);
                while (!string.IsNullOrEmpty(directory))
                {
                    directory = Path.GetDirectoryName(directory);
                    if (!known.Add(directory)) break;
                }
            }
        }
    }

    readonly DirectoryCache directories = new();

    /// <summary>
    /// Limits the total size of file contents held in memory by staging, across all packages of a deployment.
    /// Reservations never wait: a file that does not fit is staged on disk instead. Waiting could deadlock, since
    /// the install loop consumes packages in order while later packages could hold the whole budget.
    /// </summary>
    internal class MemoryBudget(long capacity)
    {
        long available = capacity;

        public bool TryReserve(long bytes)
        {
            while (true)
            {
                long current = Volatile.Read(ref available);
                if (current < bytes) return false;
                if (Interlocked.CompareExchange(ref available, current - bytes, current) == current)
                    return true;
            }
        }

        public void Release(long bytes) => Interlocked.Add(ref available, bytes);
    }

    /// <summary>
    /// Files up to this size are staged in memory. Defaults to 1 MiB.
    /// Can probably be increased, but the benefits of in-memory staging quickly diminish for larger files.
    /// </summary>
    internal static readonly long MemoryStagingLimit = 1L << 20;

    /// <summary>
    /// Total in-memory staging is limited to 1 GiB. 
    /// We are unlikely to hit this cap, but it is good to set a limit.
    /// </summary>
    internal static readonly long TotalMemoryStagingLimit = 1L << 30;
    readonly MemoryBudget memoryBudget = new(TotalMemoryStagingLimit);

    internal class StagedPackage
    {
        /// <summary>
        /// A file extracted from the package. Small files are kept in memory (<see cref="Data"/>) and written once,
        /// directly to their destination, at install time. Larger files are written to the staging directory
        /// (<see cref="Stage"/>) and renamed into place. Renaming a freshly written file is expensive on Windows,
        /// where real-time scanners inspect it again, so avoiding the staging write for most files matters there.
        /// </summary>
        readonly struct ExtractedFile(string target, string stage, byte[] data, long length)
        {
            public string Target { get; } = target;
            public string Stage { get; } = stage;
            public byte[] Data { get; } = data;
            public long Length { get; } = length;
        }

        readonly List<ExtractedFile> ExtractedFiles = [];

        public PackageDef Package { get; private set; }

        public static StagedPackage StagePackage(string packagePath, string stagingDir, MemoryBudget budget, CancellationToken token)
        {
            StagedPackage ctx = new();
            using var zip = new ZipArchive(File.OpenRead(packagePath), ZipArchiveMode.Read);
            foreach (var file in zip.Entries)
            {
                token.ThrowIfCancellationRequested();
                var path = file.FullName.Replace('\\', '/');
                if (PluginInstaller.ZipExclude(path)) continue;

                bool inMemory = file.Length <= MemoryStagingLimit && budget.TryReserve(file.Length);
                byte[] data = null;
                string tmpfile = null;
                if (inMemory)
                {
                    var ms = new MemoryStream();
                    using (var source = file.Open())
                        source.CopyTo(ms);
                    data = ms.ToArray();
                }
                else
                {
                    /* file is too large to store in memory, so we instead store it as a temporary file under the .install/ folder. */
                    tmpfile = Path.Combine(stagingDir, Guid.NewGuid().ToString("N"));
                    using var fs = new FileStream(tmpfile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    using var source = file.Open();
                    source.CopyTo(fs);
                }
                ctx.ExtractedFiles.Add(new ExtractedFile(path, tmpfile, data, file.Length));
            }
            ctx.Package = PackageDef.FromPackage(packagePath);
            return ctx;
        }

        static void RetryUntilSuccess(Action a, Func<Exception, bool> retryPredicate)
        {
            while (true)
            {
                try
                {
                    a();
                    return;
                }
                catch (Exception ex)
                {
                    if (!retryPredicate(ex)) throw;
                }
            }
        }

        static bool OutOfSpace(Exception ex) => ex.Message.Contains("There is not enough space on the disk");

        public void InstallStagedFilesToDestination(string target, DirectoryCache directories, MemoryBudget budget, ICollection<string> installedFiles)
        {
            const int maxRetries = 10;
            List<string> installed = [];
            foreach (var file in ExtractedFiles)
            {
                int attempt = 0;
                var path = Path.Combine(target, file.Target);
                directories.EnsureExists(Path.GetDirectoryName(path));

                bool InstallRetryPredicate(Exception ex)
                {
                    attempt++;
                    if (ex is OperationCanceledException) return false;
                    if (OutOfSpace(ex))
                    {
                        log.Error(ex.Message);
                        var req = new AbortOrRetryRequest("Not Enough Disk Space", $"File '{path}' requires {Utils.BytesToReadable(file.Length)} of free space. " +
                                                          $"Please free some space to continue.")
                        { Response = AbortOrRetryResponse.Abort };
                        UserInput.Request(req, true);
                        if (req.Response == AbortOrRetryResponse.Abort)
                            throw new OperationCanceledException("Installation aborted due to missing disk space.");
                        return true;
                    }

                    if (attempt > maxRetries)
                        return false;

                    log.Warning("Unable to unpack file {0}. Retry {1} of {2}.", path, attempt, maxRetries);
                    log.Debug(ex);
                    Thread.Sleep(200);
                    return true;
                }

                RetryUntilSuccess(() => InstallFile(file, path), InstallRetryPredicate);
                installedFiles.Add(path);
                installed.Add(path);
                if (file.Data != null) budget.Release(file.Data.Length);
            }
            /* clear list so garbage collector can reclaim the memory */
            ExtractedFiles.Clear();
            PluginInstaller.SetHiddenAttributes(target, installed);
        }

        static void InstallFile(ExtractedFile file, string path)
        {
            if (File.Exists(path))
                File.Delete(path);
            if (file.Data != null)
            {
                // Existing files were already moved aside by PrepareDestination, so this only overwrites a copy
                // installed earlier in this deployment by another package with the same file.
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                fs.Write(file.Data, 0, file.Data.Length);
            }
            else
            {
                // The staging directory is inside the installation, so this is an atomic rename on the same volume.
                File.Move(file.Stage, path);
            }
        }
    }

    internal StagedPackage StagePackageFromFile(string packagePath, CancellationToken token)
    {
        return StagedPackage.StagePackage(packagePath, StagingDir, memoryBudget, token);
    }

    internal void InstallStagedPackage(Stopwatch timer, StagedPackage ctx)
    {
        PackageDef package = ctx.Package;
        PluginInstaller.CheckEula(package);

        // Existing files were already moved out of the way for all packages by PrepareDestination.
        // Files shared between packages in this deployment are overwritten by later packages, like on main.
        var destination = package.IsSystemWide() ? PackageDef.SystemWideInstallationDirectory : TapDir;
        ctx.InstallStagedFilesToDestination(destination, directories, memoryBudget, installedFiles);

        var pi = new PluginInstaller();
        if (pi.ExecuteAction(package, Installer.Install, false, TapDir) == ActionResult.Error)
        {
            throw new Cli.ExitCodeException((int)PackageExitCodes.PackageInstallError,
                $"Failed to install package '{package.Name}' version {package.Version}: the install action failed.");
        }
        CustomPackageActionHelper.RunCustomActions(package, PackageActionStage.Install, new CustomPackageActionArgs(null, false));

        if (SearchBetweenInstalls && Installer.ShouldSearch(package))
        {
            Installer.Search(timer, package, TapDir);
        }
    }

    // Files placed in the installation by InstallStagedPackage. Used to roll back a failed deployment.
    readonly HashSet<string> installedFiles = new(StringComparer.Ordinal);

    /// <summary>
    /// Undoes a failed deployment: removes every file installed so far, moves the original files back from the
    /// uninstall context, and deletes the staging directory. Staging must have stopped before this is called.
    /// Install actions that already ran for installed packages are not undone.
    /// Best effort: failures are logged and do not stop the rest of the rollback.
    /// </summary>
    internal void Rollback()
    {
        log.Info("Rolling back image deployment.");
        foreach (var file in installedFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                log.Error("Unable to remove installed file '{0}': {1}", file, ex.Message);
                log.Debug(ex);
            }
        }
        installedFiles.Clear();

        if (uninstallContext != null)
        {
            try
            {
                uninstallContext.UndoAllDeletions();
            }
            catch (AggregateException ex)
            {
                // Each file that could not be restored has already been logged by UndoAllDeletions. Their
                // originals are still in the uninstall directory.
                log.Error("Rollback could not restore {0} original file(s). The originals are in '{1}'.",
                    ex.InnerExceptions.Count, Path.Combine(TapDir, ".uninstall"));
            }
            uninstallContext = null;
        }

        DeleteStagingDirectory();
    }

    void DeleteStagingDirectory()
    {
        try
        {
            if (Directory.Exists(StagingDir))
                Directory.Delete(StagingDir, true);
        }
        catch (Exception ex)
        {
            log.Warning("Unable to delete staging directory '{0}': {1}", StagingDir, ex.Message);
            log.Debug(ex);
        }
    }

    internal void FinalizeInstall()
    {
        DeleteStagingDirectory();
        var install = new Installation(TapDir);
        UninstallContext.DeletePreviouslyUninstalledFiles(install);
        install.AnnouncePackageChange();
    }

    /// <summary>
    /// The files a package will write to the installation, as listed by its package definition: the package's files
    /// plus its own metadata file. Matches the archive contents that <see cref="PluginInstaller.FilesInPackage"/> lists.
    /// </summary>
    static List<string> FilesToReplace(PackageDef package) =>
        [
            .. package.Files.Select(f => f.RelativeDestinationPath) ,
            $"{PackageDef.PackageDefDirectory}/{package.Name}/{PackageDef.PackageDefFileName}",
        ];

    /// <summary>
    /// Prepares the installation for the given packages before any of them are installed: 
    ///  * moves every existing file that will be replaced into a single uninstall context. 
    ///  * determines whether files are in use
    ///  * if running interactively, prompts user how to proceed
    ///  * rolls back uninstall context on abort
    /// </summary>
    internal void PrepareDestination(IReadOnlyList<PackageDef> packages)
    {
        // Files shared between packages only need to be moved once.
        var files = packages.SelectMany(FilesToReplace).Distinct(StringComparer.Ordinal).ToList();
        var noninteractive = UserInput.GetInterface() is NonInteractiveUserInputInterface;
        const int maxTries = 10;
        var delaySeconds = 3;
        var tries = 0;
        var warned = false;

        while (true)
        {
            var context = UninstallContext.Create(new Installation(TapDir));
            var inUse = new List<FileInfo>();
            foreach (var file in files)
            {
                if (!context.TryDelete(file, out _))
                    inUse.Add(new FileInfo(Path.Combine(TapDir, file)));
            }

            if (inUse.Count == 0)
            {
                uninstallContext = context;
                return;
            }

            // If some originals cannot be put back, do not retry: a new context would lose track of them.
            // Keep this context so Rollback can try to restore them again, and fail the deployment.
            uninstallContext = context;
            context.UndoAllDeletions();
            uninstallContext = null;

            var inUseString = Installer.BuildString([.. inUse]);
            if (!warned)
            {
                warned = true;
                var allProcesses = Process.GetProcesses().Where(p =>
                    p.ProcessName.ToLowerInvariant().Contains("opentap") &&
                    p.ProcessName.ToLowerInvariant().Contains("vshost") == false &&
                    p.ProcessName != Assembly.GetExecutingAssembly().GetName().Name).ToArray();

                if (allProcesses.Any())
                {
                    // The file could be locked by someone other than OpenTAP processes. We should not assume it's OpenTAP holding the file.
                    log.Warning(Environment.NewLine +
                                "To continue, try closing applications that could be using the files.");
                    foreach (var process in allProcesses)
                        log.Warning("- " + process.ProcessName);
                }

                if (noninteractive)
                    log.Warning(inUseString);
            }

            var req = new AbortOrRetryRequest("Package Files Are In Use", inUseString) { Response = AbortOrRetryResponse.Abort };
            UserInput.Request(req, Installer.waitForFilesTimeout, true);

            if (req.Response == AbortOrRetryResponse.Abort)
            {
                if (noninteractive && tries < maxTries)
                {
                    tries += 1;
                    log.Info($"Package files are in use. Retrying in {delaySeconds} seconds. ({tries} / {maxTries})");
                    TapThread.Sleep(TimeSpan.FromSeconds(delaySeconds));
                    continue;
                }

                throw new IOException(inUseString);
            }
        }
    }

    // Holds the original files moved aside by PrepareDestination, for every package in this deployment.
    UninstallContext uninstallContext;

}
