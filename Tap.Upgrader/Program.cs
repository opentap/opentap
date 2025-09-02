using System;
using System.IO;

namespace Tap.Upgrader
{
    public static class Program
    {
        // It would be preferable to move the files to the temp directory, but
        // File.Move only works on open files if the source and destination are on the same volume.
        // The easiest way to ensure this is to move files to be deleted to a subdirectory of the same installation.
        private static string UninstallPath => Path.Combine(Installation, ".uninstall");
        private static string PackageDir => Path.GetDirectoryName(Environment.ProcessPath);
        private static string Installation => new DirectoryInfo(PackageDir).Parent.Parent.FullName;

        private static void Uninstall(string filename)
        {
            if (File.Exists(filename))
            {
                Directory.CreateDirectory(UninstallPath);
                var dest = Path.Combine(UninstallPath, Guid.NewGuid().ToString());
                File.Move(filename, dest);
            }
        }

        public static void Main()
        {
            // Try to clean up any files left over from previous upgrades
            if (Directory.Exists(UninstallPath))
            {
                try 
                {
                    Directory.Delete(UninstallPath, true);
                }
                catch 
                {
                    // ignore
                }
            }

            var tapExe = Path.Combine(Installation, "tap.exe");
            var newTapExe = Path.Combine(PackageDir, "tap.exe.new");
            var tapDll = Path.Combine(Installation, "tap.dll");
            var newTapDll = Path.Combine(PackageDir, "tap.dll.new");

            Uninstall(tapExe);
            File.Copy(newTapExe, tapExe);
            Uninstall(tapDll);
            File.Copy(newTapDll, tapDll);
        }
    }
}
