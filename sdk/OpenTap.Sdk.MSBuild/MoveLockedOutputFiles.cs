using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace Keysight.OpenTap.Sdk.MSBuild
{
    /// <summary>
    /// MSBuild task which moves locked files out of the way before they are overwritten by the build.
    /// On Windows, a running process (e.g. an OpenTAP GUI) which has loaded an assembly from the output directory
    /// prevents the build from overwriting it. Such files can still be renamed, so we move them to
    /// '&lt;OutDir&gt;/.uninstall/build-&lt;project&gt;-&lt;guid&gt;/', next to the directories used by OpenTAP's
    /// UninstallContext. The '.uninstall' directory contains a .OpenTapIgnore file.
    ///
    /// To avoid breaking incremental builds, only files which the build is actually about to overwrite are considered.
    /// Files are only ever moved (renamed), which preserves their timestamps. After the build,
    /// <see cref="FinishMovedOutputFiles"/> moves back any file which was not replaced.
    ///
    /// NOTE: This task must not reference any OpenTAP types, as that would require loading OpenTAP into MSBuild.
    /// </summary>
    public class MoveLockedOutputFiles : Task
    {
        /// <summary> Copy mode: the file is written if the destination is missing, or differs in size or timestamp. (Copy with SkipUnchangedFiles=true) </summary>
        public const string ModeIfDifferent = "IfDifferent";
        /// <summary> Copy mode: the file is written if the destination is missing, or older than the source. (Targets with Inputs/Outputs) </summary>
        public const string ModeIfNewer = "IfNewer";
        /// <summary> Copy mode: the file is always written. </summary>
        public const string ModeAlways = "Always";

        /// <summary>
        /// Files the build is about to copy. The item spec is the source file.
        /// Required metadata: 'Destination' (the file which will be overwritten) and 'Mode' (IfDifferent, IfNewer or Always).
        /// </summary>
        [Required]
        public ITaskItem[] Files { get; set; }

        /// <summary> The output directory. Locked files are moved to a subdirectory of this directory. </summary>
        [Required]
        public string OutDir { get; set; }

        /// <summary> The project being built. Used for resolving relative paths and for naming the move directories. </summary>
        [Required]
        public string ProjectFile { get; set; }

        /// <summary>
        /// The files which were moved. The item spec is the original location.
        /// Metadata: 'MovedTo' (the new location) and 'MoveDir' (the directory the file was moved into).
        /// </summary>
        [Output]
        public ITaskItem[] MovedFiles { get; set; }

        internal const string UninstallDirName = ".uninstall";
        internal const string DoneMarker = ".done";
        const string DirPrefix = "build-";

        public override bool Execute()
        {
            var outDir = FullPath(OutDir);
            var uninstallDir = Path.Combine(outDir, UninstallDirName);
            CleanUpPreviousMoves(uninstallDir, outDir);

            var moved = new List<ITaskItem>();
            string moveDir = null;

            foreach (var (_, dst) in FilesToBeWritten())
            {
                if (!IsLocked(dst)) continue;

                moveDir ??= Path.Combine(uninstallDir, $"{DirPrefix}{ProjectId(ProjectFile)}-{Guid.NewGuid()}");
                var destination = Path.Combine(moveDir, RelativeTo(outDir, dst));
                try
                {
                    MoveFile(uninstallDir, dst, destination);
                    var item = new TaskItem(dst);
                    item.SetMetadata("MovedTo", destination);
                    item.SetMetadata("MoveDir", moveDir);
                    moved.Add(item);
                    Log.LogMessage(MessageImportance.Normal, $"Moved locked file '{dst}' to '{destination}'.");
                }
                catch (Exception ex)
                {
                    // Files which are opened without FileShare.Delete cannot be renamed. The copy of this file will
                    // fail with the usual MSB3027 error, so undo the other moves to leave the output directory intact.
                    Log.LogMessage(MessageImportance.High, $"Unable to move locked file '{dst}': {ex.Message}");
                    Undo(moved);
                    moved.Clear();
                    break;
                }
            }

            if (moveDir != null && moved.Count == 0)
                TryDeleteDirectory(moveDir);

            MovedFiles = moved.ToArray();
            return true;
        }

        void Undo(List<ITaskItem> moved)
        {
            foreach (var item in moved)
            {
                try
                {
                    File.Move(item.GetMetadata("MovedTo"), item.ItemSpec);
                }
                catch (Exception ex)
                {
                    Log.LogMessage(MessageImportance.High, $"Unable to move '{item.GetMetadata("MovedTo")}' back to '{item.ItemSpec}': {ex.Message}");
                }
            }
        }

        IEnumerable<(string Source, string Destination)> FilesToBeWritten()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in Files ?? Array.Empty<ITaskItem>())
            {
                var dstMeta = item.GetMetadata("Destination");
                if (string.IsNullOrWhiteSpace(dstMeta)) continue;
                var src = FullPath(item.ItemSpec);
                var dst = FullPath(dstMeta);
                if (!seen.Add(dst)) continue;
                if (WillBeWritten(src, dst, item.GetMetadata("Mode")))
                    yield return (src, dst);
            }
        }

        /// <summary> Mirrors the conditions under which MSBuild overwrites a file. </summary>
        internal static bool WillBeWritten(string source, string destination, string mode)
        {
            var src = new FileInfo(source);
            var dst = new FileInfo(destination);
            // If the destination does not exist, there is nothing to move.
            // If the source does not exist, the copy will either fail or be skipped.
            if (!dst.Exists || !src.Exists) return false;
            // Never move a file onto itself (e.g. content items which already live in the output directory).
            if (string.Equals(src.FullName, dst.FullName, StringComparison.OrdinalIgnoreCase)) return false;

            switch (mode)
            {
                case ModeAlways:
                    return true;
                case ModeIfNewer:
                    return src.LastWriteTimeUtc > dst.LastWriteTimeUtc;
                default: // IfDifferent
                    return src.Length != dst.Length || src.LastWriteTimeUtc != dst.LastWriteTimeUtc;
            }
        }

        /// <summary>
        /// Checks if a file is locked by trying to open it for exclusive write access.
        /// This does not modify the file or its timestamps.
        /// </summary>
        internal static bool IsLocked(string file)
        {
            try
            {
                using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                return false;
            }
            catch (IOException ex)
            {
                const int ERROR_SHARING_VIOLATION = 32;
                const int ERROR_LOCK_VIOLATION = 33;
                var code = ex.HResult & 0xFFFF;
                return code == ERROR_SHARING_VIOLATION || code == ERROR_LOCK_VIOLATION;
            }
            catch
            {
                // e.g. UnauthorizedAccessException for read-only files. This is not a lock, so let MSBuild deal with it.
                return false;
            }
        }

        static void MoveFile(string uninstallDir, string source, string destination)
        {
            // Another build sharing this output directory may be cleaning up the uninstall directory concurrently,
            // so the target directory may disappear between creating it and moving the file into it.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    EnsureIgnoreFile(uninstallDir);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Move(source, destination);
                    return;
                }
                catch (DirectoryNotFoundException) when (attempt < 3)
                {
                }
            }
        }

        static void EnsureIgnoreFile(string uninstallDir)
        {
            Directory.CreateDirectory(uninstallDir);
            // Ensures plugins are not loaded from the uninstall directory.
            var ignoreFile = Path.Combine(uninstallDir, ".OpenTapIgnore");
            if (!File.Exists(ignoreFile))
            {
                try { File.WriteAllBytes(ignoreFile, Array.Empty<byte>()); }
                catch (IOException) { /* created concurrently */ }
            }
        }

        /// <summary>
        /// Best-effort cleanup of files moved by previous builds. Files which are still in use are left alone.
        /// Directories marked as done have been handled by <see cref="FinishMovedOutputFiles"/>, so everything in
        /// them was replaced and can be deleted. Directories which are not marked as done belong to a build which
        /// failed, or which is still running. Those are only handled if they belong to this project (which cannot
        /// be building concurrently), or if they are old. Files in them which were not replaced are moved back.
        /// Directories created by OpenTAP's UninstallContext are left alone.
        /// </summary>
        void CleanUpPreviousMoves(string uninstallDir, string outDir)
        {
            if (!Directory.Exists(uninstallDir)) return;
            var ownPrefix = $"{DirPrefix}{ProjectId(ProjectFile)}-";
            foreach (var dir in SafeEnumerate(() => Directory.GetDirectories(uninstallDir)))
            {
                var name = Path.GetFileName(dir);
                if (!name.StartsWith(DirPrefix, StringComparison.Ordinal)) continue;
                var done = File.Exists(Path.Combine(dir, DoneMarker));
                var own = name.StartsWith(ownPrefix, StringComparison.OrdinalIgnoreCase);
                var stale = SafeGet(() => Directory.GetCreationTimeUtc(dir) < DateTime.UtcNow.AddDays(-1));
                if (!done && !own && !stale) continue;

                foreach (var file in SafeEnumerate(() => Directory.GetFiles(dir, "*", SearchOption.AllDirectories)))
                {
                    var rel = RelativeTo(dir, file);
                    if (rel == DoneMarker) continue;
                    var original = Path.Combine(outDir, rel);
                    try
                    {
                        if (!done && !File.Exists(original))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(original));
                            File.Move(file, original);
                            Log.LogMessage(MessageImportance.Normal, $"Restored '{original}', which was moved by a previous build that did not complete.");
                        }
                        else
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                        // still in use
                    }
                }

                TryDeleteDirectory(dir);
            }
        }

        /// <summary> Deletes the directory if it contains nothing but empty directories and the done marker. </summary>
        internal static void TryDeleteDirectory(string dir)
        {
            try
            {
                foreach (var sub in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                {
                    try { if (!Directory.EnumerateFileSystemEntries(sub).Any()) Directory.Delete(sub); } catch { /* ignore */ }
                }
                var entries = Directory.GetFileSystemEntries(dir);
                if (entries.All(e => Path.GetFileName(e) == DoneMarker))
                {
                    foreach (var e in entries) File.Delete(e);
                    Directory.Delete(dir);
                }
            }
            catch
            {
                // ignore
            }
        }

        static string[] SafeEnumerate(Func<string[]> f)
        {
            try { return f(); } catch { return Array.Empty<string>(); }
        }

        static bool SafeGet(Func<bool> f)
        {
            try { return f(); } catch { return false; }
        }

        string FullPath(string path) =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ProjectFile) ?? Directory.GetCurrentDirectory(), path));

        internal static string ProjectId(string projectFile)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(projectFile ?? "").ToLowerInvariant()));
                return BitConverter.ToString(hash, 0, 4).Replace("-", "").ToLowerInvariant();
            }
        }

        internal static string RelativeTo(string dir, string file)
        {
            var root = dir.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return file.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? file.Substring(root.Length) : Path.GetFileName(file);
        }
    }

    /// <summary>
    /// Runs after the build when <see cref="MoveLockedOutputFiles"/> moved files aside.
    /// Files which were not replaced by the build are moved back. The change itself is announced to running OpenTAP
    /// processes by <see cref="AnnounceOpenTapBuild"/>, which runs after every build.
    /// </summary>
    public class FinishMovedOutputFiles : Task
    {
        /// <summary> The files moved by <see cref="MoveLockedOutputFiles"/>. </summary>
        [Required]
        public ITaskItem[] MovedFiles { get; set; }

        /// <summary> The files which were replaced by the build. </summary>
        [Output]
        public ITaskItem[] ReplacedFiles { get; set; }

        public override bool Execute()
        {
            var replaced = new List<ITaskItem>();
            var restored = new List<string>();

            foreach (var item in MovedFiles ?? Array.Empty<ITaskItem>())
            {
                var original = item.ItemSpec;
                var movedTo = item.GetMetadata("MovedTo");
                if (File.Exists(original))
                {
                    replaced.Add(item);
                    continue;
                }
                // The build did not write the file, so move it back. This is a rename, so the timestamp is preserved
                // and incremental builds are not affected.
                try
                {
                    File.Move(movedTo, original);
                    restored.Add(original);
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"Unable to move '{movedTo}' back to '{original}': {ex.Message}");
                }
            }

            foreach (var dir in (MovedFiles ?? Array.Empty<ITaskItem>()).Select(i => i.GetMetadata("MoveDir")).Distinct())
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                try { File.WriteAllBytes(Path.Combine(dir, MoveLockedOutputFiles.DoneMarker), Array.Empty<byte>()); } catch { /* ignore */ }
                MoveLockedOutputFiles.TryDeleteDirectory(dir);
            }

            if (restored.Count > 0)
                Log.LogMessage(MessageImportance.Normal, $"Moved back {restored.Count} file(s) which were not replaced by the build: {string.Join(", ", restored.Select(Path.GetFileName))}.");

            ReplacedFiles = replaced.ToArray();
            if (replaced.Count > 0)
                Log.LogWarning(null, "OPENTAP0001", null, null, 0, 0, 0, 0,
                    $"{replaced.Count} output file(s) were in use and have been replaced: " +
                    $"{string.Join(", ", replaced.Select(i => Path.GetFileName(i.ItemSpec)))}. " +
                    "Running OpenTAP processes will keep using the previous version until they are restarted.");
            return true;
        }
    }

    /// <summary>
    /// Announces a package change in the output directory after a build, so running OpenTAP processes using it
    /// (e.g. Editor) can detect that plugins may have been updated and decide whether a restart is needed.
    /// This runs after every build on all platforms; it is up to the listener to determine what changed.
    /// </summary>
    public class AnnounceOpenTapBuild : Task
    {
        /// <summary> The output directory, i.e. the OpenTAP installation to announce the change in. </summary>
        [Required]
        public string OutDir { get; set; }

        /// <summary> The runtime directory in the OpenTAP NuGet package. OpenTAP is loaded from here to announce the change. </summary>
        [Required]
        public string OpenTapRuntimeDir { get; set; }

        /// <summary> For testing. Replaces the announcement, which loads OpenTAP. </summary>
        internal Action<string> Announce { get; set; }

        public override bool Execute()
        {
            var installDir = CanonicalPath(OutDir);
            try
            {
                if (Announce != null)
                    Announce(installDir);
                else
                {
                    // Only load OpenTAP. Creating an OpenTapContext would also create a session log on every build.
                    OpenTapContext.LoadOpenTap(installDir, OpenTapRuntimeDir);
                    AnnounceChange(installDir);
                }
                Log.LogMessage(MessageImportance.Normal, $"Announced package change in '{installDir}'.");
            }
            catch (Exception ex)
            {
                // Never fail the build because of this.
                Log.LogMessage(MessageImportance.High, $"Unable to announce package change in '{installDir}': {ex.Message}");
            }
            return true;
        }

        /// <summary>
        /// Notifies OpenTAP processes using the installation that it has changed. Both change counters are incremented:
        /// the shared memory counter (Installation.PackageChangedEvent) and the file based counter (Packages/.changeId,
        /// which invalidates the package cache of Installation objects).
        /// OpenTAP must be loaded before calling this, and it must not be inlined, since it references OpenTAP types.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static void AnnounceChange(string installDir)
        {
            new global::OpenTap.Package.Installation(installDir).AnnouncePackageChange();
            global::OpenTap.Package.IsolatedPackageAction.IncrementChangeId(installDir);
        }

        /// <summary>
        /// Returns the full path with the casing used on disk. The shared memory counter is identified by a
        /// case-sensitive hash of the installation path, so the path must match the one used by running processes,
        /// which is derived from the location of OpenTap.Package.dll. The casing of OutDir on the other hand depends
        /// on how the project path was specified, e.g. 'dotnet build myplugin'.
        /// </summary>
        internal static string CanonicalPath(string path)
        {
            var full = Path.GetFullPath(path).TrimEnd('\\', '/');
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return full;
            try
            {
                var parts = new Stack<string>();
                var dir = new DirectoryInfo(full);
                while (dir.Parent != null)
                {
                    var actual = dir.Parent.GetFileSystemInfos(dir.Name).FirstOrDefault();
                    parts.Push(actual?.Name ?? dir.Name);
                    dir = dir.Parent;
                }
                var root = dir.FullName;
                if (root.Length >= 2 && root[1] == ':') root = char.ToUpperInvariant(root[0]) + root.Substring(1);
                return parts.Aggregate(root, Path.Combine);
            }
            catch
            {
                return full;
            }
        }
    }
}
