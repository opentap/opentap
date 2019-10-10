//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    public abstract class IsolatedPackageAction : LockingPackageAction
    {
        public override int Execute(CancellationToken cancellationToken)
        {
            if (String.IsNullOrEmpty(Target))
                Target = GetLocalInstallationDir();
            else
                Target = Path.GetFullPath(Target.Trim());
            if (!Directory.Exists(Target))
            {
                log.Error("Destination directory \"{0}\" does not exist.", Target);
                return -1;
            }

            if (ExecutorClient.IsExecutorMode) // do we support running isolated?
            {
                if (!ExecutorClient.IsRunningIsolated) // are we already running isolated?
                {
                    // Detected Executor, try to run running isolated...
                    if (RunIsolated(target: Target, isolatedAction: this))
                        return 0; // we succeeded in "recursively" running everything isolated from a different process, we are done now.
                    else
                    {
                        var exec = Path.GetFileName(Assembly.GetEntryAssembly().Location);
                        if (this is PackageInstallAction installAction && installAction.ForceInstall || this is PackageUninstallAction uninstallAction && uninstallAction.Force)
                            log.Warning($"Unable to run isolated because {exec} was not installed through a package.");
                        else
                            throw new InvalidOperationException($"Unable to run isolated because {exec} was not installed through a package. Use --force to try to install anyway.");
                    }
                }
            }

            return base.Execute(cancellationToken);
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(new Uri(path).LocalPath)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       .ToUpperInvariant();
        }


        private static bool RunIsolated(string application = null, string target = null, IsolatedPackageAction isolatedAction = null)
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
                            var filePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), pkgfile.FileName);

                            if (string.Equals(NormalizePath(filePath), NormalizePath(file), StringComparison.OrdinalIgnoreCase))
                            {
                                return package;
                            }
                        }
                    }
                    return null;
                }

                var exec = application ?? Assembly.GetEntryAssembly().Location;
                var pkg = findPackageWithFile(Path.GetFullPath(exec));
                if (pkg == null)
                    return false;
                var dependencies = pkg.Dependencies.ToList();

                // If the executing IsolatedPackageAction does not origin from OpenTAP package, we need to include it when we copy and run isolated
                var isolatedActionPackage = findPackageWithFile(Path.GetFullPath(isolatedAction.GetType().Assembly.Location));
                if (isolatedActionPackage is object && pkg.Name != isolatedActionPackage.Name)
                    if (!dependencies.Any(p => p.Name == isolatedActionPackage.Name))
                        dependencies.Add(new PackageDependency(isolatedActionPackage.Name, new VersionSpecifier(isolatedActionPackage.Version, VersionMatchBehavior.Compatible)));

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

                var deps = OpenTap.Utils.FlattenHeirarchy(dependencies, dep => (IEnumerable<PackageDependency>)packages.FirstOrDefault(x => x.Name == dep.Name && !dependencies.Any(s => s.Name == dep.Name))?.Dependencies ?? Array.Empty<PackageDependency>(), distinct: true);

                if (false == deps.Any(x => x.Name == pkg.Name))
                {
                    deps.Add(new PackageDependency(pkg.Name, new VersionSpecifier(pkg.Version, VersionMatchBehavior.Compatible)));
                }

                List<string> allFiles = new List<string>();
                foreach (var d in deps)
                {
                    if (!packages.Any(p => p.Name == d.Name && d.Version.IsCompatible(p.Version)))
                        throw new Exception($"Unable to run isolated. Cannot find needed dependency '{d.Name}'.");
                    var package = packages.First(p => p.Name == d.Name && d.Version.IsCompatible(p.Version));
                    var defPath = String.Join("/", PackageDef.PackageDefDirectory, package.Name, PackageDef.PackageDefFileName);
                    if (File.Exists(defPath))
                        allFiles.Add(defPath);

                    var fs = package.Files;
                    var brokenPackages = new HashSet<string>();
                    foreach (var file in fs)
                    {
                        string loc = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), file.FileName);
                        if (!File.Exists(loc))
                        {
                            brokenPackages.Add(package.Name);
                            log.Debug($"Could not find file '{loc}' part of package '{package.Name}'.");
                            continue;
                        }

                        allFiles.Add(file.FileName);
                    }

                    foreach (var name in brokenPackages)
                        log.Warning($"Package '{name}' has missing files and is broken.");
                }
#if DEBUG && !NETCOREAPP
                // in debug builds tap.exe tries to attach a debugger using EnvDTE.dll, that dll needs to be copied as well.
                if (File.Exists("EnvDTE.dll"))
                    allFiles.Add("EnvDTE.dll");
#endif

                string tempFolder = Path.GetFullPath(FileSystemHelper.CreateTempDirectory());

                foreach (var _loc in allFiles.Distinct())
                {
                    string loc = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), _loc);

                    var newloc = Path.Combine(tempFolder, _loc);
                    OpenTap.FileSystemHelper.EnsureDirectory(newloc);
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
                    tpmClient.Dispose();
                    return true;
                }
            }
        }
    }

}
