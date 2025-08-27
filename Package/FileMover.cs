using System;
using System.Collections.Generic;
using System.IO;

namespace OpenTap.Package;

internal class FileMover
{
    readonly struct Move(string originalFile, string deletedFile)
    {
        public string OriginalFile { get; } = originalFile;
        public string DeletedFile { get; } = deletedFile;
    }
        
    public static FileMover Create(Installation installation)
    {
        return new FileMover(installation);
    }

    private Guid Id = Guid.NewGuid();
    private readonly Installation install;
    private readonly string Target;
    private List<Move> Moves = [];

    private FileMover(Installation installation)
    {
        install = installation;
        Target = Path.Combine(Path.GetTempPath(), "opentap-uninstall", install.Id, Id.ToString());
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
            }
            catch
            {
                // log maybe?
                return false;
            }
        }

        return true;
    }

    public void Rollback()
    {
        foreach (var move in Moves)
        {
            File.Move(move.DeletedFile, move.OriginalFile);
        }
    }
}