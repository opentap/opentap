using System;
using System.IO;
using System.Threading;

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
                var basename = Path.GetFileName(filename);
                Directory.CreateDirectory(UninstallPath);
                var dest = Path.Combine(UninstallPath, $"{basename}.{Guid.NewGuid()}");
                File.Move(filename, dest);
            }
        }

        private static void Retry(Action act, string error)
        {
            Exception ex = null;
            for (int i = 0; i < 10; i++)
            {
                try 
                {
                    act();
                    return;
                }
                catch (Exception e)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(10));
                    ex = e;
                }
            }

            Console.Error.WriteLine($"{error}: {ex.Message}");
        }

        public static void Main()
        {
            // clean up any files left over from previous upgrades
            if (Directory.Exists(UninstallPath))
            {
                foreach (var file in Directory.GetFiles(UninstallPath))
                {
                    try 
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting file '{file}': {ex.Message}");
                    }
                }
            }

            var tapExe = Path.Combine(Installation, "tap.exe");
            var newTapExe = Path.Combine(PackageDir, "tap.exe.new");
            var tapDll = Path.Combine(Installation, "tap.dll");
            var newTapDll = Path.Combine(PackageDir, "tap.dll.new");

            Retry(() => Uninstall(tapExe), $"Error deleting tap.exe");
            Retry(() => File.Copy(newTapExe, tapExe), $"Error updating tap.exe");
            Retry(() => Uninstall(tapDll), $"Error deleting tap.dll");
            Retry(() => File.Copy(newTapDll, tapDll), $"Error updating tap.dll");
        }
    }
}
