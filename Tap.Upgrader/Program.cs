using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace Tap.Upgrader
{
    class UpgradePair
    {
        private static bool CompareFiles(string fileA, string fileB)
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

        public bool UpgradeNeeded()
        {
            if (DeleteSource && File.Exists(Source)) return true; // The source must be deleted
            if (File.Exists(Source) && File.Exists(Target))
            {
                try
                {
                    return !CompareFiles(Source, Target);
                }
                catch
                {
                    // for some reason the skip check failed.
                    return true;
                }
            }
            if (!File.Exists(Source)) return false; // the source does not exist, we cannot upgrade.
            return true; // the target does not exist, we must upgrade.
        }

        public UpgradePair(string source, string target, bool deleteSource)
        {
            Source = source;
            Target = target;
            DeleteSource = deleteSource;
        }

        public void Install()
        {
            Program.DumbRetry(() =>
            {
                bool mustDeleteTarget = File.Exists(Target) && File.Exists(Source);
                bool mustDeleteSource = File.Exists(Source) && DeleteSource;

                if (mustDeleteTarget)
                    File.Delete(Target);
                if (mustDeleteSource)
                    File.Move(Source, Target);
                else if (File.Exists(Source))
                    File.Copy(Source, Target);
            }, TimeSpan.FromMinutes(10));
        }


        public string Source { get; set; }
        public string Target { get; set; }
        public bool DeleteSource { get; set; }
    }

    public static class Program
    {
        /// <summary>
        /// Retry the action in a loop until it succeeds
        /// </summary>
        /// <param name="act"></param>
        /// <param name="timeout"></param>
        internal static void DumbRetry(Action act, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < timeout)
            {
                try
                {
                    act();
                    return;
                }
                catch
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }

        }
        /// <summary>
        /// Note: The order of the tap.exe files is important here! The first file can be overwritten by the second file.
        /// This is important in niche scenarios where OpenTAP is being upgraded and downgraded between 9.17.0 and releases 9.16.4 and older.
        /// The 'tap.exe.new' file in the root folder should always take precedence over the 'tap.exe.new' in the 'Packages' folder.
        /// </summary>
        internal static readonly UpgradePair[] UpgradePairs =
        {
            // this file is written when installing any version of OpenTAP with OpenTAP 9.17 or later
            new UpgradePair("../../tap.exe.new", "../../tap.exe", true),
            // this file is part of the payload for OpenTAP 9.17 and later
            new UpgradePair("tap.exe.new", "../../tap.exe", false),
            // this file is written when installing any version of OpenTAP with OpenTAP 9.17 or later
            new UpgradePair("../../tap.dll.new", "../../tap.dll", true),
        };

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

        private static void WaitForFileFree(string file)
        {
            DumbRetry(() =>
            {
                using var s = File.OpenWrite(file);
            }, TimeSpan.FromMinutes(10));
        }

        private static bool DeleteTapDll()
        {
            try
            {
                var xmlPath = Path.GetFullPath("package.xml");
                if (File.Exists(xmlPath) == false) return false;
                // Check if tap.dll needs to be deleted
                // We do this by parsing the package xml and checking if tap.dll belongs to the package
                // This is necessary because 'tap.dll' was not part of the OpenTAP package prior to 9.17,
                // and OpenTAP emits a warning in versions prior to 9.17 if it is present
                var xml = XElement.Load(xmlPath);
                var ns = xml.GetDefaultNamespace();
                foreach (var fileEle in xml.Element(ns.GetName("Files"))?.Elements(ns.GetName("File")) ?? Array.Empty<XElement>())
                {
                    if (fileEle.Attribute("Path")?.Value == "tap.dll")
                        return false;
                }
                return true;
            }
            catch
            {
                // this shouldn't happen
                return false;
            }
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
                    var upgradeNeeded = UpgradePairs.Select(p => p.UpgradeNeeded());
                    if (upgradeNeeded.Any() == false)
                    {
                        Console.WriteLine("Skipping upgrade");
                        return;
                    }
                    // Start this process and immediately exit
                    // This subprocess will then wait for the tap.exe instance which started this program to exit
                    // so it can overwrite tap.exe
                    // We also copy this executable to a temporary file which will delete itself after running
                    // Otherwise we will cause OpenTAP reinstalls to hang while waiting for Tap.Upgrader.exe to become free
                    var thisExe = typeof(Program).Assembly.Location;
                    var backup = Path.Combine(exeDir, "Tap.Upgrader.Copy.exe");
                    if (!File.Exists(backup))
                        File.Copy(thisExe, backup);
                    Process.Start(new ProcessStartInfo(backup)
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
                // We need to wait for tap.exe to be free here because the source files might not exist yet
                // This can happen if this process was launched by an uninstall action, and the install actions which
                // create the source files do not exist yet
                WaitForFileFree("../../tap.exe");
                foreach (var p in UpgradePairs)
                {
                    if (p.UpgradeNeeded())
                        p.Install();
                }

                if (DeleteTapDll())
                {
                    var tapDllLoc = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../tap.dll"));
                    DumbRetry(() =>
                    {
                        File.Delete(tapDllLoc);
                    }, TimeSpan.FromMinutes(10));
                }

                var thisExe = typeof(Program).Assembly.Location;
                // Exit and delete self
                Process.Start( new ProcessStartInfo("cmd.exe")
                {
                    // Start a detached 'cmd.exe' command which waits for 3 seconds and deletes this file
                    // This shouldn't fail, but it's not the end of the world if it does.
                    Arguments = $"/C choice /D Y /T 3 & Del \"{thisExe}\"",
                    WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true
                });
            }
        }
    }
}