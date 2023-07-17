using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTap.Package.Ipc;
using Tap.Shared;

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
        /// Get a unique identifier for this OpenTAP installation.
        /// The identifier is computed from hashing a uniquely generated machine ID combined with the hash of the installation directory.
        /// </summary>
        public string Id 
        {
            get
            {
                if(string.IsNullOrWhiteSpace(id))
                    id = $"{MurMurHash3.Hash(GetMachineId()):X8}{MurMurHash3.Hash(Directory):X8}";
                return id;
            }
        }
        private string id { get; set; }
        internal static string GetMachineId()
        {
            var idPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "OpenTap", "OpenTapGeneratedId");
            string id = default(Guid).ToString(); // 00000000-0000-0000-0000-000000000000

            try
            {
                if (File.Exists(idPath))
                {
                    if (Guid.TryParse(File.ReadAllText(idPath), out Guid parsedGuid)) // In the assumable rare case that a user tampers with the OpenTapGeneratedId file.
                    {
                        id = parsedGuid.ToString();
                        return id;
                    }
                }
                
                id = Guid.NewGuid().ToString();
                if (System.IO.Directory.Exists(Path.GetDirectoryName(idPath)) == false)
                    System.IO.Directory.CreateDirectory(Path.GetDirectoryName(idPath));
                File.WriteAllText(idPath, id);
            }
            catch (Exception e)
            {
                log.Error("Failed to read machine ID. See debug messages for more information");
                log.Debug(e);
            }

            return id;
        }


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

        private static Installation current;

        /// <summary>
        /// Get the installation of the currently running tap process
        /// </summary>
        public static Installation Current
        {
            get
            {
                if (current == null)
                {
                    current = new Installation(ExecutorClient.ExeDir);
                    current.PrintReadable();
                }

                return current;
            }
        }

        // This is included for debugging purposes so we know exactly what plugins are installed when people create issues with session logs.
        private void PrintReadable()
        {
            var packages = GetPackages();
            var longestName = packages.Max(p => p.Name.Length);
            foreach (var pkg in GetPackages())
            {
                var padded = pkg.Name.PadRight(longestName);
                log.Debug($"{padded} - {pkg.Version}");
            }
        }

        /// <summary> Target installation architecture. This could be anything as 32-bit is supported on 64bit systems.</summary>
        internal CpuArchitecture Architecture => GetOpenTapPackage()?.Architecture ?? ArchitectureHelper.GuessBaseArchitecture;

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

        bool invalidate;
        readonly ConcurrentDictionary<string, PackageDef> fileMap = new ConcurrentDictionary<string, PackageDef>();

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
            var source = TypeData.GetTypeDataSource(pluginType);
            if (source == null) return null;
            // sourceFile is normally a .DLL, but may also be other things, e.g .py-file.
            var sourceFile = source.Location;
            if (string.IsNullOrWhiteSpace(sourceFile)) return null;

            var assemblyPath = Path.GetFullPath(sourceFile);
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

                string normalizePath(string s)
                {
                    return Path.GetFullPath(s)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .ToUpperInvariant();
                }

                // Add system wide packages
                if (normalizePath(Directory) != normalizePath(PackageDef.SystemWideInstallationDirectory))
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
                    lock (warningsLock)
                    {
                        if (duplicateLogWarningsEmitted.Add(p.Key))
                            log.Warning(
                                $"Duplicate {p.Key} packages detected. Consider removing some of the duplicate package definitions:\n" +
                                $"{string.Join("\n", p.Append(plugins[p.Key]).Select(x => " - " + ((InstalledPackageDefSource)x.PackageSource).PackageDefFilePath))}");
                    }
                }

                invalidate = false;
                packageCache = plugins;
            }

            return packageCache;
        }

        private static object warningsLock = new object();


        /// <summary>
        /// Get a package definition of OpenTAP engine.
        /// </summary>
        /// <returns></returns>
        public PackageDef GetOpenTapPackage()
        {
            if (GetPackagesLookup().TryGetValue("OpenTAP", out var opentap))
                return opentap;
            return null;
        }


        /// <summary>
        /// Memorizer which returns null when a cyclic memorizer call is detected.
        /// This prevents ugly and misleading error messages from occurring during
        /// calls to Installation.Current.GetPackages() from ITypeDataProvider implementations
        /// </summary>
        class IgnoreCyclicCallMemorizer<T1, T2, T3> : Memorizer<T1, T2, T3>
        {
            public IgnoreCyclicCallMemorizer(Func<T1, T2> func) : base(null, func)
            {

            }

            public override T2 OnCyclicCallDetected(T1 key)
            {
                return default;
            }
        }


        static IMemorizer<string, PackageDef> installedPackageMemorizer = new IgnoreCyclicCallMemorizer<string, PackageDef, string>(loadPackageDef)
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
                var changeId = new ChangeId(Path.GetDirectoryName(typeof(SharedState).Assembly.Location));
                var id = changeId.GetChangeId();
                while (changeId.GetChangeId() == id)
                    await Task.Delay(500);
            }

            public static void WaitForChangeBlocking()
            {
                var changeId = new ChangeId(Path.GetDirectoryName(typeof(SharedState).Assembly.Location));
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
