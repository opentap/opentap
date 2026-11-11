using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
    // TEMP: at most one diagnostic query per file in this uninstall context.
    private readonly HashSet<string> diagnosedLocks = new(StringComparer.OrdinalIgnoreCase);

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
            int attempt = 0;
            List<PackageFile> failed = [];
            while (true)
            {
                attempt++;
                failed = [];
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var file in pending)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Delete(file.RelativeDestinationPath, false))
                        failed.Add(file);
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (failed.Count == 0)
                {
                    if (attempt > 1)
                        log.Debug($"[UNINSTALL-RETRY] package='{package.Name}' recovered after {attempt} sweeps; elapsed={sw.ElapsedMilliseconds}ms; pid={Process.GetCurrentProcess().Id}");
                    return true;
                }
                log.Debug($"[UNINSTALL-RETRY] package='{package.Name}' sweep={attempt}; elapsed={sw.ElapsedMilliseconds}ms; pid={Process.GetCurrentProcess().Id}; failed=[{string.Join(", ", failed.Select(f => f.RelativeDestinationPath))}]");
                int remaining = timeout - (int)sw.ElapsedMilliseconds;
                if (remaining <= 0)
                    break;

                pending = failed;
                // Every wait is followed by a sweep, including the final wait at the deadline.
                int wait = Math.Min(delay, remaining);
                log.Debug("Retrying {0} package files in {1} ms.", pending.Count, wait);
                log.Debug($"[UNINSTALL-RETRY] package='{package.Name}' next-sweep={attempt + 1}; wait={wait}ms; remaining-budget={remaining}ms");
                if (cancellationToken.WaitHandle.WaitOne(wait))
                    cancellationToken.ThrowIfCancellationRequested();
                delay = Math.Min(delay * 2, 1000);
            }

            log.Debug($"[UNINSTALL-RETRY] package='{package.Name}' exhausted retry budget after {attempt} sweeps; elapsed={sw.ElapsedMilliseconds}ms; rolling back");
            /* failure */
            foreach (var file in failed)
                log.Error("Unable to delete file '{0}' after retrying.", file.RelativeDestinationPath);
            UndoAllDeletions();
            return false;
        }
        catch (Exception e)
        {
            log.Debug($"[UNINSTALL-RETRY] package='{package.Name}' interrupted by {e.GetType().Name}: {e.Message}; rolling back");
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
                {
                    log.Debug("Unable to move file {0} to {1}: {2}", fullname, destination, e.Message);
                    log.Debug($"[UNINSTALL-RETRY] move failed: source='{fullname}'; destination='{destination}'; exception={e.GetType().Name}; hresult=0x{e.HResult:X8}; message={e.Message}");
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && (e.HResult & 0xffff) == 32 && diagnosedLocks.Add(fullname))
                        RestartManagerDiagnostics.Report(fullname);
                }
                log.Debug(e);
                return false;
            }
        }

        return true;
    }

    // TEMP: Restart Manager only enumerates possible owners; never request shutdown/restart.
    static class RestartManagerDiagnostics
    {
        [StructLayout(LayoutKind.Sequential)]
        struct UniqueProcess
        {
            public uint ProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME StartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ProcessInfo
        {
            public UniqueProcess Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string AppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string ServiceName;
            public uint AppType;
            public uint AppStatus;
            public uint SessionId;
            [MarshalAs(UnmanagedType.Bool)] public bool Restartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmStartSession(out uint handle, uint flags, StringBuilder key);
        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmRegisterResources(uint handle, uint fileCount,
            [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] files,
            uint appCount, IntPtr apps, uint serviceCount, IntPtr services);
        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        static extern int RmGetList(uint handle, out uint needed, ref uint count,
            [In, Out] ProcessInfo[] processes, ref uint rebootReasons);
        [DllImport("rstrtmgr.dll")]
        static extern int RmEndSession(uint handle);

        public static void Report(string file)
        {
            uint handle = 0;
            bool started = false;
            try
            {
                int error = RmStartSession(out handle, 0, new StringBuilder(33));
                if (error != 0) { log.Debug($"[UNINSTALL-RETRY] RestartManager start failed: code={error}"); return; }
                started = true;
                error = RmRegisterResources(handle, 1, new[] { file }, 0, IntPtr.Zero, 0, IntPtr.Zero);
                if (error != 0) { log.Debug($"[UNINSTALL-RETRY] RestartManager registration failed: code={error}"); return; }
                uint count = 0, reasons = 0;
                error = RmGetList(handle, out uint needed, ref count, null, ref reasons);
                // The process list can change. Bound allocation and retries instead of looping forever.
                for (int attempt = 0; error == 234 && attempt < 3; attempt++)
                {
                    if (needed > 1024) { log.Debug("[UNINSTALL-RETRY] RestartManager owner list exceeds diagnostic limit"); return; }
                    var owners = new ProcessInfo[needed];
                    count = needed;
                    error = RmGetList(handle, out needed, ref count, owners, ref reasons);
                    if (error != 0) continue;
                    if (count == 0) log.Debug($"[UNINSTALL-RETRY] RestartManager found no owners: file='{file}'");
                    using var current = Process.GetCurrentProcess();
                    for (int i = 0; i < count; i++)
                    {
                        var owner = owners[i];
                        string processName = "unavailable", executable = "unavailable";
                        try
                        {
                            using var process = Process.GetProcessById((int)owner.Process.ProcessId);
                            processName = process.ProcessName;
                            try { executable = process.MainModule?.FileName; } catch { }
                        }
                        catch { /* Process exited or access denied. Preserve the Restart Manager record. */ }
                        log.Debug($"[UNINSTALL-RETRY] RestartManager candidate owner: file='{file}'; pid={owner.Process.ProcessId}; testhost-pid={current.Id}; same-process={owner.Process.ProcessId == current.Id}; process='{processName}'; executable='{executable}'; app='{owner.AppName}'; service='{owner.ServiceName}'; session={owner.SessionId}");
                    }
                    return;
                }
                if (error == 0) log.Debug($"[UNINSTALL-RETRY] RestartManager found no owners: file='{file}'");
                else log.Debug($"[UNINSTALL-RETRY] RestartManager query failed: code={error}");
            }
            catch (Exception e)
            {
                log.Debug($"[UNINSTALL-RETRY] RestartManager diagnostic failed: {e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (started)
                {
                    try { RmEndSession(handle); }
                    catch (Exception e) { log.Debug($"[UNINSTALL-RETRY] RestartManager cleanup failed: {e.Message}"); }
                }
            }
        }
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
