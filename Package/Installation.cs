using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTap.Package.Ipc;

namespace OpenTap.Package
{
    /// <summary>
    /// Represents an OpenTAP installation in a specific directory.
    /// </summary>
    public class Installation
    {
        static TraceSource log = Log.CreateSource("Installation");

        /// <summary>
        /// Path to the installation
        /// </summary>
        public string Directory { get; }

        /// <summary>
        /// Initialize an instance of a OpenTAP installation.
        /// </summary>
        /// <param name="directory"></param>
        public Installation(string directory)
        {
            this.Directory = directory ?? throw new ArgumentNullException(nameof(directory));
        }

        /// <summary>
        /// Check if it is an installation folder that contains packages other than system-wide packages
        /// </summary>
        public bool IsInstallationFolder => GetPackages().Any(x => x.IsSystemWide() == false);

        private static Installation _current;
        
        /// <summary>
        /// Get the installation of the currently running tap process
        /// </summary>
        public static Installation Current => _current ?? (_current = new Installation(ExecutorClient.ExeDir));

        /// <summary> Target installation architecture. This could be anything as 32-bit is supported on 64bit systems.</summary>
        internal CpuArchitecture Architecture => GetOpenTapPackage()?.Architecture ?? CpuArchitecture.AnyCPU;

        /// <summary> The target installation OS, should be either Windows, MacOS or Linux. </summary>
        internal string OS
        {
            get
            {
                if (OperatingSystem.Current == OperatingSystem.Windows)
                    return "Windows";
                if (OperatingSystem.Current == OperatingSystem.MacOS)
                    return "MacOS";
                return "Linux";
            }
        }
        

        /// <summary>
        /// Invalidate cached package list. This should only be called if changes have been made to the installation by circumventing OpenTAP APIs.
        /// </summary>
        public void Invalidate()
        {
            fileMap.Clear();
            // Force GetPackages() to repopulate packages next time it is called
            invalidate = true;
        }

        private bool invalidate;
        private ConcurrentDictionary<string, PackageDef> fileMap = new ConcurrentDictionary<string, PackageDef>();
        
        /// <summary>
        /// Get the installed package which provides the file specified by the string.
        /// If multiple packages provide the file the package is chosen arbitrarily.
        /// </summary>
        /// <param name="file">An absolute or relative path to the file</param>
        /// <returns></returns>
        public PackageDef FindPackageContainingFile(string file)
        {
            InvalidateIfChanged();

            try
            {
                var invalid = Path.GetInvalidPathChars();
                if (file.Any(ch => invalid.Contains(ch))) return null;
                var name = Path.GetFileName(file);
                invalid = Path.GetInvalidFileNameChars();
                if (name.Any(ch => invalid.Contains(ch))) return null;
                // The path API is not 100% consistent. In some circumstances 'GetFullPath' will
                // still throw even if there are no illegal characters in the filename or path name.
                _ = Path.GetFullPath(file);
            }
            catch
            {
                // This means the filename is invalid on some way not covered by Path.GetInvalidPathChars.
                // This is fine, and it definitely means the file is not contained in a package
                return null;
            }

            var installDir = ExecutorClient.ExeDir;

            // Compute the absolute path in order to ensure the file exists, and normalize the path so it matches the format in package.xml files
            var abs = Path.IsPathRooted(file)
                ? Path.GetFullPath(file) // If the path is rooted, use the full path
                : Path.GetFullPath(Path.Combine(installDir, file)); // otherwise, append the relative path to the install dir

            // abs must be contained within installDir
            if (abs.Length <= installDir.Length)
                return null;
            
            // Ensure the file is in a subdirectory of the installation. Otherwise it is not contained in a package.
            if (!abs.StartsWith(installDir))
                return null;

            // Compute the relative path and normalize directory separators
            var relative = abs.Substring(installDir.Length + 1).Replace('\\', '/');

            // Fully initialize fileMap as needed whenever it is cleared
            if (fileMap.Count == 0)
            {
                foreach (var package in GetPackages())
                {
                    foreach (var packageFile in package.Files)
                    {
                        var fileName = packageFile.FileName.Replace('\\', '/');
                        fileMap.TryAdd(fileName, package);
                    }
                }                
            }

            if (fileMap.TryGetValue(relative, out var result))
                return result;
            return null;
        }

        /// <summary>
        /// Get the installed package which provides the type specified by pluginType.
        /// If multiple packages provide the type the package is chosen arbitrarily.
        /// </summary>
        /// <param name="pluginType"></param>
        /// <returns></returns>
        public PackageDef FindPackageContainingType(ITypeData pluginType)
        {
            var assembly = pluginType?.AsTypeData()?.Assembly?.Location;
            if (string.IsNullOrWhiteSpace(assembly)) return null;
            
            var assemblyPath = Path.GetFullPath(assembly);
            var installPath = Path.GetFullPath(ExecutorClient.ExeDir);

            // The assembly must be rooted in the installation
            if (assemblyPath.StartsWith(installPath) == false)
                return null;

            return FindPackageContainingFile(assemblyPath);
        }

        private Dictionary<string, PackageDef> packageCache;
        private long previousChangeId = -1;
        
        // Keeps track of which warnings about duplicate packages has been emitted.
        static readonly HashSet<string> duplicateLogWarningsEmitted = new HashSet<string>();

        /// <summary>
        /// Invalidate caches if the installation has changed.
        /// </summary>
        private void InvalidateIfChanged()
        {
            long changeId = IsolatedPackageAction.GetChangeId(Directory);
            
            if (changeId != previousChangeId)
            {
                Invalidate();
                previousChangeId = changeId;
            }
        }
        /// <summary>
        /// Returns package definition list of installed packages in the TAP installation defined in the constructor, and system-wide packages.
        /// Results are cached, and Invalidate must be called if changes to the installation are made by circumventing OpenTAP APIs.
        /// </summary>
        public List<PackageDef> GetPackages() => new List<PackageDef>(GetPackagesLookup().Values);

        /// <summary> Finds an installed package by name. Returns null if the package was not found. </summary>
        public PackageDef FindPackage(string name) => GetPackagesLookup().TryGetValue(name, out var package) ? package : null;
        
        /// <summary>
        /// Returns package definition list of installed packages in the TAP installation defined in the constructor, and system-wide packages.
        /// Results are cached, and Invalidate must be called if changes to the installation are made by circumventing OpenTAP APIs.
        /// </summary>
        /// <returns></returns>
        Dictionary<string, PackageDef> GetPackagesLookup()
        {
            InvalidateIfChanged();

            if (packageCache == null || invalidate)
            {
                Dictionary<string, PackageDef> plugins = new Dictionary<string, PackageDef>();
                List<PackageDef> duplicatePlugins = new List<PackageDef>();
                List<string> package_files = new List<string>();

                // Add normal package from OpenTAP folder
                package_files.AddRange(PackageDef.GetPackageMetadataFilesInTapInstallation(Directory));

                // Add system wide packages
                package_files.AddRange(PackageDef.GetSystemWidePackages());

                foreach (var file in package_files)
                {
                    var package = installedPackageMemorizer.Invoke(file);
                    if (package == null) continue;

#pragma warning disable 618
                    package.Location = file;
#pragma warning restore 618
                    package.PackageSource = new InstalledPackageDefSource
                    {
                        Installation = this,
                        PackageDefFilePath = file
                    };
                    if (!plugins.ContainsKey(package.Name))
                    {
                        plugins.Add(package.Name, package);
                    }
                    else
                    {
                        duplicatePlugins.Add(package);
                        
                    }
                }

                foreach (var p in duplicatePlugins.GroupBy(p => p.Name))
                {
                    if(duplicateLogWarningsEmitted.Add(p.Key))
                        log.Warning(
                        $"Duplicate {p.Key} packages detected. Consider removing some of the duplicate package definitions:\n" +
                        $"{string.Join("\n", p.Append(plugins[p.Key]).Select(x => " - " + ((InstalledPackageDefSource)x.PackageSource).PackageDefFilePath))}");
                }

                invalidate = false;
                packageCache = plugins;
            }

            return packageCache;
        }


        /// <summary>
        /// Get a package definition of OpenTAP engine.
        /// </summary>
        /// <returns></returns>
        public PackageDef GetOpenTapPackage()
        {
            if (GetPackagesLookup().TryGetValue("OpenTAP", out var opentap))
                return opentap;
            
            log.Warning($"Could not find OpenTAP in {Directory}.");
            return null;
        }


        static IMemorizer<string, PackageDef> installedPackageMemorizer = new Memorizer<string, PackageDef, string>(null, loadPackageDef)
        {
            Validator = file => new FileInfo(file).LastWriteTimeUtc.Ticks
        };
        static PackageDef loadPackageDef(string file)
        {
            try
            {
                using (var f = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return PackageDef.FromXml(f);
            }
            catch (Exception e)
            {
                log.Warning("Unable to read package file '{0}'. Moving it to '.broken'", file);
                log.Debug(e);
                var brokenfile = file + ".broken";
                if (File.Exists(brokenfile))
                    File.Delete(brokenfile);
                File.Move(file, brokenfile);
            }
            return null;
        }

        #region Package Change IPC
        /// <summary>
        /// Maintains a running number that increments whenever a plugin is installed.
        /// </summary>
        private class ChangeId : SharedState
        {
            public ChangeId(string dir) : base(".package_definitions_change_ID", dir)
            {

            }

            public long GetChangeId()
            {
                return Read<long>(0);
            }

            public void SetChangeId(long value)
            {
                Write(0, value);
            }

            public static async Task WaitForChange()
            {
                var changeId = new ChangeId(ExecutorClient.ExeDir);
                var id = changeId.GetChangeId();
                while (changeId.GetChangeId() == id)
                    await Task.Delay(500);
            }

            public static void WaitForChangeBlocking()
            {
                var changeId = new ChangeId(ExecutorClient.ExeDir);
                var id = changeId.GetChangeId();
                while (changeId.GetChangeId() == id)
                    Thread.Sleep(500);
            }
        }

        internal void AnnouncePackageChange()
        {
            using (var changeId = new ChangeId(this.Directory))
                changeId.SetChangeId(changeId.GetChangeId() + 1);
        }

        private bool IsMonitoringPackageChange = false;
        private void MonitorPackageChange()
        {
            if (!IsMonitoringPackageChange)
            {
                IsMonitoringPackageChange = true;
                TapThread.Start(() =>
                {
                    while (true)
                    {
                        ChangeId.WaitForChangeBlocking();
                        PackageChanged();
                    }
                });
            }
        }

        private Action PackageChanged;
        /// <summary>
        /// Event invoked when a package is installed/uninstalled from this installation.
        /// </summary>
        public event Action PackageChangedEvent
        {
            add
            {
                MonitorPackageChange();
                PackageChanged += value;
            }
            remove
            {
                PackageChanged -= value;
            }
        }
        #endregion
    }
}
