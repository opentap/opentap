using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace Tap.Upgrader
{
    internal static class Installer
    {
        internal static void Install(string installDir)
        {
            var target = Path.Combine(installDir, "tap.exe");
            var source = Path.Combine(installDir, "Packages", "OpenTap", "tap.exe.new");

            var sw = Stopwatch.StartNew();
            // Retry in a loop for until tap.exe is no longer locked
            while (sw.Elapsed < TimeSpan.FromMinutes(10))
            {
                try
                {
                    if (File.Exists(target))
                        File.Delete(target);
                    if (File.Exists(source))
                        File.Move(source, target);
                    break;
                }
                catch
                {
                    // Retry again in a few
                    Thread.Sleep(100);
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

        /// <summary>
        /// Return the first parent process which was not started from some temp path
        /// </summary>
        /// <returns></returns>
        private static Process GetNonTempAncestor()
        {
            var parent = ProcessUtils.GetParentProcess();
            var tempdir = Path.GetTempPath();
            while (true)
            {
                try
                {
                    var next = ProcessUtils.GetParentProcess(parent.Handle);
                    if (next == null) return parent;
                    parent = next;
                    if (parent.MainModule.FileName.StartsWith(tempdir) == false) return parent;
                }
                catch
                {
                    return parent;
                }
            }
        }

        public static void Main(string[] args)
        {
            var argTable = parseArgs(args);
            if (!argTable.ContainsKey("p"))
            {
                try
                {
                    var parent = GetNonTempAncestor();
                    if (parent?.MainModule == null) return;

                    var exeDir = Path.GetDirectoryName(parent.MainModule.FileName);
                    // Start this process and immediately exit
                    // This subprocess will then wait for the tap.exe instance which started this program to exit
                    // so it can overwrite tap.exe
                    var thisExe = typeof(Program).Assembly.Location;
                    Process.Start(new ProcessStartInfo(thisExe)
                    {
                            // Specify the OpenTAP version we are installing and the directory it is being installed to
                            Arguments = $"p=\"{exeDir}\"",
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
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
                var exeDir = argTable["p"];
                Installer.Install(exeDir);
            }
        }
    }
}