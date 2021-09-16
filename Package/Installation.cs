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
        string directory { get; }

        /// <summary>
        /// Initialize an instance of a OpenTAP installation.
        /// </summary>
        /// <param name="directory"></param>
        public Installation(string directory)
        {
            this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
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

            // Compute the absolute path in order to ensure the file exists, and normalize the path so it matches the format in package.xml files
            var abs = Path.GetFullPath(file);

            var installDir = ExecutorClient.ExeDir;

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
            
            // Get the path relative to the install directory by removing the install path + the leading '/'
            var relative = assemblyPath.Substring(installPath.Length + 1);

            return FindPackageContainingFile(relative);
        }

        private List<PackageDef> PackageCache;
        private long previousChangeId = -1;

        /// <summary>
        /// Invalidate caches if the installation has changed.
        /// </summary>
        private void InvalidateIfChanged()
        {
            long changeId = IsolatedPackageAction.GetChangeId(directory);
            
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
        /// <returns></returns>
        public List<PackageDef> GetPackages()
        {
            InvalidateIfChanged();

            if (PackageCache == null || invalidate)
            {
                List<PackageDef> plugins = new List<PackageDef>();
                List<string> package_files = new List<string>();

                // Add normal package from OpenTAP folder
                package_files.AddRange(PackageDef.GetPackageMetadataFilesInTapInstallation(directory));

                // Add system wide packages
                package_files.AddRange(PackageDef.GetSystemWidePackages());

                foreach (var file in package_files)
                {
                    var package = installedPackageMemorizer.Invoke(file);
                    if (package != null && !plugins.Any(s => s.Name == package.Name))
                    {
#pragma warning disable 618
                        package.Location = file;
#pragma warning restore 618
                        package.PackageSource = new InstalledPackageDefSource
                        {
                            Installation = this,
                            PackageDefFilePath = file
                        };
                        plugins.Add(package);
                    }
                }

                invalidate = false;
                PackageCache = plugins;
            }

            return new List<PackageDef>(PackageCache);
        }

        /// <summary>
        /// Get a package definition of OpenTAP engine.
        /// </summary>
        /// <returns></returns>
        public PackageDef GetOpenTapPackage()
        {
            var opentap = GetPackages()?.FirstOrDefault(p => p.Name == "OpenTAP");
            if (opentap == null)
                log.Warning($"Could not find OpenTAP in {directory}.");

            return opentap;
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
            using (var changeId = new ChangeId(this.directory))
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
