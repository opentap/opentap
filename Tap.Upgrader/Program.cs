using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Tap.Upgrader
{
    static class Installer
    {
        static string target = "../../tap.exe";
        static string source = "tap.exe.new";

        static bool CompareFiles(string fileA, string fileB)
        {
            // open with FileShare.ReadWrite to allow other files to open them too.
            // with this permission we can compare the files even if tap.exe is running.
            using var handleA = File.Open(fileA, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var handleB = File.Open(fileB, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            
            var bufferA = new byte[1024];
            var bufferB = new byte[1024];
            while (true)
            {
                // load data into the buffers.
                var readBytesA = handleA.Read(bufferA, 0, bufferA.Length);
                var readBytesB = handleB.Read(bufferB, 0, bufferB.Length);
                
                // this should be the same if the files has the same length.
                if (readBytesA != readBytesB) return false; 
                // end of file was reached?
                if (readBytesA == 0) return true; 
                
                // check if this chunk is the same.
                if (bufferA.SequenceEqual(bufferB) == false) return false; 
            }
        }
        public static bool UpgradeNeeded()
        {
            if (!File.Exists(source)) return false; // the source does not exist, we cannot upgrade.
            if (!File.Exists(target)) return true; // the target does not exist, we must upgrade.
            try
            {
                return !CompareFiles(source, target);
            }
            catch
            {
                // of some reason the skip check failed.
                return true;
            }
        }
            
        internal static void Install()
        {
            var sw = Stopwatch.StartNew();
            // Retry in a loop for until tap.exe is no longer locked
            while (sw.Elapsed < TimeSpan.FromMinutes(10))
            {
                try
                {
                    if (File.Exists(target))
                        File.Delete(target);
                    if (File.Exists(source))
                        File.Copy(source, target);
                    break;
                }
                catch
                {
                    // Retry again in a few
                    Thread.Sleep(1000);
                }
            }
        }
    }

    public static class Program
    {
        static Dictionary<string, string> parseArgs(string[] args)
        {
            var result = new Dictionary<string, string>();
            foreach (var arg in args)
            {
                var pivot = arg.IndexOf("=", StringComparison.CurrentCulture);
                var key = arg.Substring(0, pivot);
                var value = arg.Substring(pivot + 1);
                result[key] = value;
            }

            return result;
        }

        public static void Main(string[] args)
        {
            var argTable = parseArgs(args);
            if (!argTable.ContainsKey("p"))
            {
                try
                {
                    var exeDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                    Directory.SetCurrentDirectory(exeDir);
                    if (!Installer.UpgradeNeeded())
                    {
                        Console.WriteLine("Skipping upgrade");
                        return;
                    }
                    // Start this process and immediately exit
                    // This subprocess will then wait for the tap.exe instance which started this program to exit
                    // so it can overwrite tap.exe
                    var thisExe = typeof(Program).Assembly.Location;
                    Process.Start(new ProcessStartInfo(thisExe)
                    {
                            // Specify the OpenTAP version we are installing and the directory it is being installed to
                            Arguments = $"p=\"{exeDir}\"",
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            WorkingDirectory = exeDir
                    });
                }
                catch
                {
                    // This shouldn't happen, but if it does, there isn't really anything we can do about it
                    // Worst case scenario the installation will complete normally without overwriting tap.exe which
                    // shouldn't be necessary anyway in the majority of cases
                }
            }
            else
            {
                Installer.Install();
            }
        }
    }
}