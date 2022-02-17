using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Tap.Upgrader
{
    static class Installer
    {
        internal static void Install()
        {
            var target = "../../tap.exe";
            var source = "tap.exe.new";

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