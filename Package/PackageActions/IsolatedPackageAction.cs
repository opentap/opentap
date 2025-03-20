//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Package
{
    /// <summary>
    /// Base class for ICliActions that makes a copy of the installation to a temp dir before executing. Useful for making changes to the installation. 
    /// </summary>
    public abstract class IsolatedPackageAction : LockingPackageAction
    {
        /// <summary>
        /// Try to force execution in spite of errors. When true the action will execute even when isolation cannot be achieved.
        /// </summary>
        [CommandLineArgument("force", Description = "Try to run in spite of errors.", ShortName = "f")]
        public bool Force { get; set; }
        
        /// <summary>
        /// Avoid starting an isolated process. This can cause installations to fail if the DLLs that must be overwritten are loaded.
        /// </summary>
        [Browsable(false)]
        [CommandLineArgument("no-isolation", Description = "Avoid starting an isolated process.")]
        public bool NoIsolation { get; set; }
        

        /// <summary>
        /// Executes this the action. Derived types should override LockedExecute instead of this.
        /// </summary>
        /// <returns>Return 0 to indicate success. Otherwise return a custom errorcode that will be set as the exitcode from the CLI.</returns>
        public override int Execute(CancellationToken cancellationToken)
        {
            if (String.IsNullOrEmpty(Target))
                Target = GetLocalInstallationDir();
            else
                Target = Path.GetFullPath(Target.Trim()); 
            if (!Directory.Exists(Target))
            {
                if (File.Exists(Target))
                {
                    log.Error("Destination directory \"{0}\" is a file.", Target);
                    return (int)ExitCodes.ArgumentError;
                }
                FileSystemHelper.EnsureDirectoryOf(Target);
            }

            if (ExecutorClient.IsExecutorMode && NoIsolation == false) // do we support running isolated?
            {
                if (!ExecutorClient.IsRunningIsolated) // are we already running isolated?
                {
                    // Detected Executor, try to run running isolated...
                    try
                    {
                        RunIsolated(target: Target, isolatedAction: this);
                        return (int)ExitCodes.Success; // we succeeded in "recursively" running everything isolated from a different process, we are done now.
                    }
                    catch (Exception ex)
                    {
                        if (this.Force)
                            log.Warning($"Not running isolated because of error: {ex.Message}");
                        else
                            throw new InvalidOperationException($"Error when trying to run isolated (Use --force to try to run anyway): {ex.Message}",ex);
                    }
                }
            }

            return base.Execute(cancellationToken);
        }

        internal static bool TryFindParentInstallation(string targetDirectory, out string parent)
        {
            var dir = new DirectoryInfo(targetDirectory).Parent;
            while (dir != null)
            {
                if (dir.EnumerateFiles("OpenTap.dll").Any())
                {
                    parent = dir.FullName;
                    return true;
                }
                dir = dir.Parent;
            }
            parent = null;
            return false;
        }


        private static string GetChangeFile(string target) => Path.Combine(target, "Packages", ".changeId");
        internal static long GetChangeId(string target)
        {
            var filePath = GetChangeFile(target);
            if (File.Exists(filePath))
                if (long.TryParse(File.ReadAllText(filePath), out var changeId))
                    return changeId;
            return 0;
        }

        private static void EnsureDirectory(string filePath) => Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        
        internal static void IncrementChangeId(string target)
        {
            var filePath = GetChangeFile(target);
            long changeId = GetChangeId(target);
            changeId += 1;

            try
            {
                EnsureDirectory(filePath);
                File.WriteAllText(filePath, changeId.ToString());
            }
            catch (Exception ex)
            {
                log.Warning($"Failed writing Change ID to {filePath}");
                log.Debug(ex);
            }
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(new Uri(path).LocalPath)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       .ToUpperInvariant();
        }


        internal static void RunIsolated(string application = null, string target = null, IsolatedPackageAction isolatedAction = null)
        {
            using (var tpmClient = new ExecutorClient())
            {
                var packages = new Installation(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)).GetPackages();
                PackageDef findPackageWithFile(string file)
                {
                    foreach (var package in packages)
                    {
                        foreach (var pkgfile in package.Files)
                        {
                            var filePath = Path.Combine(ExecutorClient.ExeDir, pkgfile.FileName);

                            if (string.Equals(NormalizePath(filePath), NormalizePath(file), StringComparison.OrdinalIgnoreCase))
                            {
                                return package;
                            }
                        }
                    }
                    return null;
                }

                var exec = application ?? Assembly.GetEntryAssembly().Location;
                PackageDef pkg = findPackageWithFile(Path.GetFullPath(exec));
                if (pkg == null)
                    throw new InvalidOperationException($"{Path.GetFileName(exec)} was not installed through a package.");
                var dependencies = pkg.Dependencies.ToList();

                if(isolatedAction != null)
                {
                    // If the executing IsolatedPackageAction does not origin from OpenTAP package, we need to include it when we copy and run isolated
                    var actionAsm = isolatedAction.GetType().Assembly.Location;
                    PackageDef isolatedActionPackage = findPackageWithFile(Path.GetFullPath(actionAsm));
                    if (isolatedActionPackage == null)
                        throw new InvalidOperationException($"{Path.GetFileName(actionAsm)} was not installed through a package.");
                    if (pkg.Name != isolatedActionPackage.Name)
                        if (!dependencies.Any(p => p.Name == isolatedActionPackage.Name))
                            dependencies.Add(new PackageDependency(isolatedActionPackage.Name, new VersionSpecifier(isolatedActionPackage.Version, VersionMatchBehavior.Compatible)));
                }

                // when installing/uninstalling packages we might need to use custom package actions as well.
                var extraDependencies = PluginManager.GetPlugins<ICustomPackageAction>().Select(t => t.Assembly.Location).Distinct().ToList();
                foreach (var exDep in extraDependencies)
                {
                    var package = findPackageWithFile(exDep);
                    if (package != null && !dependencies.Any(p => p.Name == package.Name))
                    {
                        dependencies.Add(new PackageDependency(package.Name, new VersionSpecifier(package.Version, VersionMatchBehavior.Compatible)));
                    }
                }

                var deps = OpenTap.Utils.FlattenHeirarchy(dependencies, dep => (IEnumerable<PackageDependency>)packages.FirstOrDefault(x => x.Name == dep.Name)?.Dependencies ?? Array.Empty<PackageDependency>(), distinct: true);

                if (false == deps.Any(x => x.Name == pkg.Name))
                {
                    deps.Add(new PackageDependency(pkg.Name, new VersionSpecifier(pkg.Version, VersionMatchBehavior.Compatible)));
                }

                // Also copy the IDebugger implementation package over (the IDebugger assembly itself might depend on some things in that package, e.g. EnvDTE.dll)
                var debuggerAsm = Environment.GetEnvironmentVariable("OPENTAP_DEBUGGER_ASSEMBLY");
                if (!String.IsNullOrEmpty(debuggerAsm))
                {
                    var dpkg = findPackageWithFile(debuggerAsm);
                    deps.Add(new PackageDependency(dpkg.Name, new VersionSpecifier(dpkg.Version, VersionMatchBehavior.Compatible)));
                }

                bool force = isolatedAction?.Force ?? false;

                List<string> allFiles = new List<string>();
                foreach (var d in deps)
                {
                    var availPackages = packages.Where(p => p.Name == d.Name);
                    var package = availPackages.FirstOrDefault(p => d.Version.IsCompatible(p.Version));
                    if(package == null)
                    {
                        package = availPackages.FirstOrDefault();
                        if (!force)
                        {
                            if(package != null)
                                throw new Exception($"Cannot find compatible dependency '{d.Name}' {d.Version}. Version {package.Version} is installed.");
                            throw new Exception($"Cannot find needed dependency '{d.Name}'.");
                        }

                        log.Warning("Unable to find compatible package, using {0} v{1} instead.", package.Name, package.Version);
                    }
                            
                    var defPath = String.Join("/", PackageDef.PackageDefDirectory, package.Name, PackageDef.PackageDefFileName);
                    if (File.Exists(defPath))
                        allFiles.Add(defPath);

                    var fs = package.Files;
                    var brokenPackages = new HashSet<string>();
                    foreach (var file in fs)
                    {
                        // Mixing forward and backwards slashes can cause the File.Exists check to fail on Linux / MacOS
                        // Just change them to forward slashes
                        var fn = file.FileName.Replace('\\', '/');
                        string loc = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), fn);
                        if (!File.Exists(loc))
                        {
                            brokenPackages.Add(package.Name);
                            log.Debug($"Could not find file '{loc}' part of package '{package.Name}'.");
                            continue;
                        }

                        allFiles.Add(fn);
                    }

                    foreach (var name in brokenPackages)
                        log.Warning($"Package '{name}' has missing files and is broken.");
                }

                string tempFolder = Path.GetFullPath(FileSystemHelper.CreateTempDirectory());

                foreach (var _loc in allFiles.Distinct())
                {
                    string loc = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), _loc);

                    var newloc = Path.Combine(tempFolder, _loc);
                    OpenTap.FileSystemHelper.EnsureDirectoryOf(newloc);
                    if (File.Exists(newloc)) continue;
                    File.Copy(loc, newloc);
                }

                { // tell TPM Server to start new app.
                    var loc = application ?? Assembly.GetEntryAssembly().Location;
                    if (string.Equals(".dll", Path.GetExtension(loc), StringComparison.OrdinalIgnoreCase))
                    {  //.netcore wierdness.
                        loc = Path.ChangeExtension(loc, "exe");
                        if (File.Exists(loc) == false)
                            loc = loc.Substring(0, loc.Length - ".exe".Length);
                    }

                    var newname = Path.Combine(tempFolder, Path.GetFileName(loc));

                    newname = $"\"{newname}\"";  // there could be whitespace in the name.

                    // now that we start from a different dir, we need to supply a --target argument 
                    if (target != null)
                        newname = $"{newname} --target \"{target}\"";

                    tpmClient.MessageServer("run " + newname);
                }
            }
        }
    }

}
