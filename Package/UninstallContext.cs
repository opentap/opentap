using System;
using System.Collections.Generic;
using System.IO;

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

    public bool Delete(PackageFile file)
    {
        return Delete(file.RelativeDestinationPath);
    }

    public bool Delete(string path)
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
                log.Error("Unable to move file {0} to {1}: {2}", fullname, destination, e.Message);
                log.Debug(e);
                return false;
            }
        }

        return true;
    }

    public void UndoAllDeletions()
    {
        foreach (var move in Moves)
        {
            File.Move(move.DeletedFile, move.OriginalFile);
            log.Debug("File {0} reverted to {1}.", move.DeletedFile, move.OriginalFile);
        }
        Moves.Clear();
    }
}
