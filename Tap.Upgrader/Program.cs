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
        internal static void Install(string installDir, string version)
        {
            var packageCaches = new[]
            {
                // Pre-9.17 package cache location
                Path.Combine(installDir, "PackageCache"),
                // Post-9.17 package cache location
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenTap",
                    "PackageCache")
            }.Where(Directory.Exists);

            var ms = new MemoryStream();
            bool foundTap = false;

            // Look for an OpenTAP package in the package caches which matches the version we are currently installing
            var files = new List<string>();
            foreach (var cache in packageCaches)
            {
                try
                {
                    files.AddRange(Directory.EnumerateFiles(cache));
                }
                catch
                {
                    // There was an error enumerating files, just skip this cache
                }
            }

            foreach (var file in files)
            {
                try
                {
                    using var fs = File.OpenRead(file);
                    using var arch = new ZipArchive(fs);
                    var xml = arch.GetEntry("Packages/OpenTAP/package.xml");
                    if (xml == null) continue;
                    var doc = XElement.Load(xml.Open());

                    if (doc.Attribute("Name")?.Value != "OpenTAP" ||
                        doc.Attribute("Version")?.Value != version) continue;

                    var tapExe = arch.GetEntry("tap.exe");
                    if (tapExe == null) continue;
                    tapExe.Open().CopyTo(ms);
                    foundTap = true;
                    break;
                }
                catch
                {
                    // Some error occurred, likely either permissions or because the package was not a zip archive
                }
            }

            // Didn't find tap -- we can't really do anything
            if (!foundTap) return;

            var sw = Stopwatch.StartNew();
            // Retry in a loop for until tap.exe is no longer locked
            while (sw.Elapsed < TimeSpan.FromMinutes(10))
            {
                try
                {
                    var oldTapExe = Path.Combine(installDir, "tap.exe");
                    File.WriteAllBytes(oldTapExe, ms.ToArray());
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
        static string ExeDir;

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
            var parent = ProcessBasicInformation.GetParentProcess();
            var tempdir = Path.GetTempPath();
            while (true)
            {
                try
                {
                    var next = ProcessBasicInformation.GetParentProcess(parent.Handle);
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
            if (argTable.ContainsKey("p"))
            {
                ExeDir = argTable["p"];
                var version = argTable["v"];
                Installer.Install(ExeDir, version);
            }
            else
            {
                try
                {
                    var parent = GetNonTempAncestor();
                    if (parent?.MainModule == null) return;

                    ExeDir = Path.GetDirectoryName(parent.MainModule.FileName);
                    // Start this process and immediately exit
                    // This subprocess will then wait for the tap.exe instance which started this program to exit
                    // so it can overwrite tap.exe
                    var thisExe = typeof(Program).Assembly.Location;
                    Process.Start(new ProcessStartInfo(thisExe)
                    {
                        // Specify the OpenTAP version we are installing and the directory it is being installed to
                        Arguments = $"p=\"{ExeDir}\" v={argTable["v"]}",
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
        }
    }
}