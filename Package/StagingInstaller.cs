using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using OpenTap.Package.Zip;

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
        readonly struct ExtractedFile(string target, string stage, byte[] data)
        {
            public string Target { get; } = target;
            public string Stage { get; } = stage;
            public byte[] Data { get; } = data;
        }

        readonly List<ExtractedFile> ExtractedFiles = [];

        /// <summary> The package definition, read from the package's metadata while staging. </summary>
        public PackageDef Package { get; private set; }

        class ZipEntry(string fullname, Stream stream, long length)
        {
            public string FullName => fullname;
            public Stream Stream => stream;
            public long Length => length;
        }

        static IEnumerable<ZipEntry> IterateStream(Stream stream)
        {
            using var zip = new StreamingZipReader(stream, leaveOpen: true);
            foreach (var file in zip)
            {
                yield return new ZipEntry(file.Fullname, file.Stream, file.Length);
                file.Stream.Dispose();
            }
            // The entries have only been checked against their local headers. Check them against the central
            // directory before the package is used. Throws UnstreamableZipException on mismatch.
            zip.Validate();
        }

        static IEnumerable<ZipEntry> IterateZipFile(string fullname)
        {
            using var zip = new ZipArchive(File.OpenRead(fullname), ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                var e = new ZipEntry(entry.FullName, entry.Open(), entry.Length);
                yield return e;
                e.Stream.Dispose();
            }
        }

        static StagedPackage StageFromIterable(string packagePath, string stagingDir, MemoryBudget budget, IEnumerable<ZipEntry> entries, CancellationToken token)
        {
            StagedPackage ctx = new();
            var metadataCandidates = new List<(string path, byte[] data)>();
            foreach (var file in entries)
            {
                token.ThrowIfCancellationRequested();
                var path = file.FullName.Replace('\\', '/');
                if (PluginInstaller.ZipExclude(path)) continue;

                Stream source = file.Stream;
                bool metadata = IsMetadataCandidate(path);
                bool inMemory = file.Length <= MemoryStagingLimit && budget.TryReserve(file.Length);
                if (metadata || inMemory)
                {
                    var data = ReadAll(file.Stream, file.Length);
                    if (metadata)
                        metadataCandidates.Add((path, data));
                    if (inMemory)
                    {
                        ctx.ExtractedFiles.Add(new ExtractedFile(path, null, data));
                        continue;
                    }
                    source = new MemoryStream(data);
                }

                // Staged files are stored flat under a unique name. This needs no directory structure, cannot collide
                // with files of the same name from other packages, and has no extension, so plugin and assembly
                // searches never pick up a staged file.
                var target = Path.Combine(stagingDir, Guid.NewGuid().ToString("N"));
                using (var fs = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    source.CopyTo(fs);
                }
                ctx.ExtractedFiles.Add(new ExtractedFile(path, target, null));
            }
            ctx.Package = ReadPackageDef(metadataCandidates, packagePath);
            return ctx;
        }

        public static StagedPackage StageFromFile(string packagePath, string stagingDir, MemoryBudget budget, CancellationToken token)
        {
            return StageFromIterable(packagePath, stagingDir, budget, IterateZipFile(packagePath), token);
        }

        public static StagedPackage StageFromStream(Stream packageStream, string packagePath, string stagingDir, MemoryBudget budget, CancellationToken token)
        {
            return StageFromIterable(packagePath, stagingDir, budget, IterateStream(packageStream), token);
        }

        static byte[] ReadAll(Stream stream, long length)
        {
            var data = new byte[length];
            int read = 0;
            while (read < data.Length)
            {
                int n = stream.Read(data, read, data.Length - read);
                if (n == 0) throw new EndOfStreamException("Package entry is shorter than its declared size.");
                read += n;
            }
            if (stream.ReadByte() != -1) throw new InvalidDataException("Package entry is longer than its declared size.");
            return data;
        }

        static bool IsMetadataCandidate(string path) =>
            (path.StartsWith(PackageDef.PackageDefDirectory + '/') && path.EndsWith('/' + PackageDef.PackageDefFileName))
            || ((path.Contains("package/") || path.Contains("Package Definitions/")) && path.EndsWith("package.xml"));

        /// <summary> Equivalent of <see cref="PackageDef.FromPackage"/>, using metadata captured while streaming. </summary>
        static PackageDef ReadPackageDef(List<(string path, byte[] data)> candidates, string packagePath)
        {
            // Same selection rule as PackageDef.GetMetadataFromPackage: the top-most Packages/*/package.xml,
            // falling back to the TAP 8.x locations.
            var metadata = candidates
                .Where(c => c.path.StartsWith(PackageDef.PackageDefDirectory + '/') && c.path.EndsWith('/' + PackageDef.PackageDefFileName))
                .OrderBy(c => c.path.Length)
                .Select(c => c.data)
                .FirstOrDefault()
                ?? candidates.Select(c => c.data).FirstOrDefault()
                ?? throw new IOException("No metadata found in package " + packagePath);

            PackageDef package;
            using (var stream = new MemoryStream(metadata))
                package = PackageDef.FromXml(stream);

            // Set the same location fields as PackageDef.FromPackage. EulaAcceptanceDialog relies on Location.
            var fullPath = Path.GetFullPath(packagePath);
#pragma warning disable 618
            package.Location = fullPath;
#pragma warning restore 618
            package.PackageSource = new FilePackageDefSource { PackageFilePath = fullPath };
            return package;
        }

        public void InstallStagedFilesToDestination(string target, DirectoryCache directories, MemoryBudget budget, ICollection<string> installedFiles)
        {
            foreach (var file in ExtractedFiles)
            {
                var path = Path.Combine(target, file.Target);
                directories.EnsureExists(Path.GetDirectoryName(path));
                InstallFile(file, path);
                installedFiles.Add(path);
                if (file.Data != null)
                    budget.Release(file.Data.Length);
            }
            // Drop the file contents. The staged package stays referenced until the deployment finishes, so
            // without this the memory would not be reclaimed even though the budget was released.
            ExtractedFiles.Clear();
        }

        static void InstallFile(ExtractedFile file, string path)
        {
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
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(file.Stage, path);
            }
        }
    }

    internal StagedPackage StagePackageFromFile(string packagePath, CancellationToken token)
    {
        return StagedPackage.StageFromFile(packagePath, StagingDir, memoryBudget, token);
    }

    internal StagedPackage StagePackageFromStream(string packagePath, Stream packageStream, CancellationToken token)
    {
        return StagedPackage.StageFromStream(packageStream, packagePath, StagingDir, memoryBudget, token);
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
        new Installation(TapDir).AnnouncePackageChange();
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
