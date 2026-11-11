using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace OpenTap.Package;

/// <summary> Moves files to a temporary location during uninstall. </summary>
internal class UninstallContext
{
    readonly struct Move(string originalFile, string deletedFile)
    {
        public string OriginalFile { get; } = originalFile;
        public string DeletedFile { get; } = deletedFile;
    }

    public static UninstallContext Create(Installation installation)
    {
        // Try to delete files from previous uninstall operations.
        // These files could still be in use, so we just delete whatever we can.
        DeletePreviouslyUninstalledFiles(installation);
        return new UninstallContext(installation);
    }
    
    private static readonly TraceSource log = Log.CreateSource("Uninstall");
    private readonly Installation install;
    private readonly string Target;
    private readonly List<Move> Moves = [];

    private UninstallContext(Installation installation)
    {
        install = installation;

        var uninstallDirectory = GetUninstallDir(install);
        Target = Path.Combine(uninstallDirectory, Guid.NewGuid().ToString());
        Directory.CreateDirectory(Target);
        // Ensure plugins are not loaded from the uninstall directory.
        var ignoreFile = Path.Combine(uninstallDirectory, ".OpenTapIgnore");
        // a buffersize of 0 is invalid in .NET Framework
        int bufferSize = 1;
        if (!File.Exists(ignoreFile))
            File.Create(ignoreFile, bufferSize).Close();
    }

    // Previous iterations attempted to uninstall by moving files to the temp directory, but 
    // File.Move has different semantics when moving files between storage volumes (e.g. C:\ to D:\ drive)
    // In such cases, it will copy the file and then attempt to delete it normally. This doesn't work
    // on Windows if the file is in use. To work around this limitation, we uninstall files to a subdirectory of
    // the OpenTAP installation; this should ensure the file can always be moved.
    private static string GetUninstallDir(Installation install) => Path.Combine(install.Directory, ".uninstall");

    public static void DeletePreviouslyUninstalledFiles(Installation install)
    {
        var dir = GetUninstallDir(install);
        if (!Directory.Exists(dir)) return;
        var subdirs = Directory.GetDirectories(dir);
        foreach (var subdir in subdirs)
        {
            try
            {
                FileSystemHelper.DeleteDirectory(subdir);
            }
            catch 
            {
                // ignore
            }
        }
    }

    internal bool Delete(PackageFile file)
    {
        return Delete(file.RelativeDestinationPath);
    }

    /// <summary>
    /// Moves all package files, sweeping every file before retrying failures. Shared files may be excluded.
    /// Returns false after exhausting retries and restores all moves on a best-effort basis.
    /// Cancellation also restores moves before propagating. Total backoff is at most ten seconds.
    /// </summary>
    public bool Delete(PackageDef package, ISet<string> filesToRemain = null)
        => Delete(package, filesToRemain, TapThread.Current.AbortToken);

    internal bool Delete(PackageDef package, ISet<string> filesToRemain, CancellationToken cancellationToken)
    {
        if (package == null) throw new ArgumentNullException(nameof(package));
        try
        {
            const int timeout = 10000;
            var pending = package.Files.Where(f => filesToRemain?.Contains(f.RelativeDestinationPath) != true).ToList();
            var sw = Stopwatch.StartNew();
            int delay = 100;
            List<PackageFile> failed = [];
            while (true)
            {
                failed = [];
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var file in pending)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Delete(file.RelativeDestinationPath, false))
                        failed.Add(file);
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (failed.Count == 0) return true;
                int remaining = timeout - (int)sw.ElapsedMilliseconds;
                if (remaining <= 0)
                    break;

                pending = failed;
                // Every wait is followed by a sweep, including the final wait at the deadline.
                int wait = Math.Min(delay, remaining);
                log.Debug("Retrying {0} package files in {1} ms.", pending.Count, wait);
                if (cancellationToken.WaitHandle.WaitOne(wait))
                    cancellationToken.ThrowIfCancellationRequested();
                delay = Math.Min(delay * 2, 1000);
            }

            /* failure */
            foreach (var file in failed)
                log.Error("Unable to delete file '{0}' after retrying.", file.RelativeDestinationPath);
            UndoAllDeletions();
            return false;
        }
        catch
        {
            UndoAllDeletions();
            throw;
        }
    }

    internal bool Delete(string path) => Delete(path, true);

    private bool Delete(string path, bool reportError)
    {
        var fullname = Path.Combine(install.Directory, path);
        if (File.Exists(fullname))
        {
            var destination = Path.Combine(Target, path);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Move(fullname, destination);
                Moves.Add(new Move(fullname, destination));
                log.Debug("File {0} moved to {1}.", fullname, destination);
            }
            catch(Exception e)
            {
                if (reportError)
                    log.Error("Unable to move file {0} to {1}: {2}", fullname, destination, e.Message);
                else
                    log.Debug("Unable to move file {0} to {1}: {2}", fullname, destination, e.Message);
                log.Debug(e);
                return false;
            }
        }

        return true;
    }

    public void UndoAllDeletions()
    {
        // Retain unsuccessful moves so a caller may attempt recovery again. Never retry or wait here.
        for (int i = Moves.Count - 1; i >= 0; i--)
        {
            var move = Moves[i];
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(move.OriginalFile));
                File.Move(move.DeletedFile, move.OriginalFile);
                Moves.RemoveAt(i);
                log.Debug("File {0} reverted to {1}.", move.DeletedFile, move.OriginalFile);
            }
            catch (Exception e)
            {
                log.Error("Unable to restore file {0} from {1}: {2}", move.OriginalFile, move.DeletedFile, e.Message);
                log.Debug(e);
            }
        }
    }
}
