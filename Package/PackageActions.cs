//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenTap.Cli;

namespace OpenTap.Package
{
    public abstract class LockingPackageAction : PackageAction
    {
        /// <summary>
        /// Unlockes the package action to allow multiple running at the same time.
        /// </summary>
        public bool Unlocked { get; set; }
        
        /// <summary>
        /// The location to apply the command to. The default is the location of OpenTap.PackageManager.exe
        /// </summary>
        [CommandLineArgument("target", Description = "The location where the command is applied. The default is the directory of the application itself.", ShortName = "t")]
        public string Target { get; set; }


        private static string GetLocalInstallationDir()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        /// <summary>
        /// Get the named mutex used to lock the specified TAP installation directory while it is being changed.
        /// </summary>
        /// <param name="target">The TAP installation directory</param>
        /// <returns></returns>
        public static Mutex GetMutex(string target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var fullDir = Path.GetFullPath(target).Replace('\\', '/');
            var hasher = SHA256.Create();
            var hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(fullDir));

            return new Mutex(false, "Keysight.Tap.Package InstallLock " + BitConverter.ToString(hash).Replace("-", ""));
        }

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
                    bool runIsolated = false;
                    bool force = true;
                    if (this is PackageInstallAction install)
                    {
                        runIsolated = true;
                        force = install.ForceInstall;
                    }
                    if (this is PackageUninstallAction uninstall)
                    {
                        runIsolated = true;
                        force = uninstall.Force;
                    }
                    if (runIsolated)
                    {
                        if (RunIsolated(target : Target))
                            return 0; // we succeeded in "recursively" running everything isolated from a different process, we are done now.
                        else
                        {
                            var exec = Path.GetFileName(Assembly.GetEntryAssembly().Location);
                            if (force)
                                log.Warning($"Unable to run isolated because {exec} was not installed through a package.");
                            else
                                throw new InvalidOperationException($"Unable to run isolated because {exec} was not installed through a package. Use --force to try to install anyway.");
                        }
                    }
                }
            }
            
            using (Mutex state = GetMutex(Target))
            {
                if (Unlocked == false && !state.WaitOne(0))
                {
                    throw new ExitCodeException(5, "Cannot perform operation while another TAP Package Manager is running.");
                }

                return LockedExecute(cancellationToken);
            }
        }

        protected abstract int LockedExecute(CancellationToken cancellationToken);

        public static bool RunIsolated(string application = null, string target = null)
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
                            if (string.Compare(Path.GetFullPath(pkgfile.FileName), file, true) == 0)
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
                var deps = OpenTap.Utils.FlattenHeirarchy(pkg.Dependencies, dep => (IEnumerable<PackageDependency>)packages.FirstOrDefault(x => x.Name == dep.Name)?.Dependencies ?? Array.Empty<PackageDependency>(), distinct: true);
                if (false == deps.Any(x => x.Name == pkg.Name))
                {
                    deps.Add(new PackageDependency(pkg.Name, new VersionSpecifier(pkg.Version, VersionMatchBehavior.Compatible)));
                }

                List<string> allFiles = new List<string>();
                foreach (var d in deps)
                {
                    var fs = packages.First(p => p.Name == d.Name && d.Version.IsCompatible(p.Version)).Files;
                    foreach (var file in fs)
                    {
                        allFiles.Add(file.FileName);
                    }
                }
#if DEBUG && !NETCOREAPP
                // in debug builds tap.exe tries to attach a debugger using EnvDTE.dll, that dll needs to be copied as well.
                if (File.Exists("EnvDTE.dll"))
                    allFiles.Add("EnvDTE.dll");
#endif

                string tempFolder = Path.GetFullPath(FileSystemHelper.CreateTempDirectory());

                Dictionary<string, string> fileloc = new Dictionary<string, string>();

                foreach (var file in Directory.EnumerateFiles(".", "*", SearchOption.AllDirectories))
                {
                    fileloc[Path.GetFileName(file)] = file;
                }

                foreach (var _loc in allFiles.Distinct())
                {
                    string loc = _loc;
                    if (!File.Exists(loc))
                    {
                        var name = Path.GetFileName(loc);
                        if (fileloc.ContainsKey(name))
                            loc = fileloc[Path.GetFileName(loc)];
                        else
                            loc = null;
                    }

                    if (loc == null)
                    {
                        // warning
                        continue;
                    }

                    var newloc = Path.Combine(tempFolder, loc);
                    OpenTap.FileSystemHelper.EnsureDirectory(newloc);
                    if (File.Exists(newloc)) continue;
                    File.Copy(loc, newloc);
                }

                { // tell TPM Server to start new app.
                    var loc = application ?? Assembly.GetEntryAssembly().Location;
                    if (string.Compare(".dll", Path.GetExtension(loc), true) == 0)
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
    
    [Display("list", Group: "package", Description: "List installed packages.")]
    public class PackageListAction : LockingPackageAction
    {
        [CommandLineArgument("repository", Description = "Search this repository for packages instead of using\nsettings from 'Package Manager.xml'.", ShortName = "r")]
        public string[] Repository { get; set; }

        [CommandLineArgument("all", Description = "List all versions of a package when using the <Name> argument.", ShortName = "a")]
        public bool All { get; set; }

        [CommandLineArgument("installed", Description = "Only show packages that are installed.", ShortName = "i")]
        public bool Installed { get; set; }

        [UnnamedCommandLineArgument("Name")]
        public string Name { get; set; }

        [CommandLineArgument("version", Description = "Specify a version string that the package must be compatible with.\nTo specify a version that has to match exactly start the number with '!'. E.g. \"!8.1.319-beta\".")]
        public string Version { get; set; }

        [CommandLineArgument("os", Description = "Override which OS to target.")]
        public string OS { get; set; }

        [CommandLineArgument("architecture", Description = "Override which CPU to target.")]
        public CpuArchitecture Architecture { get; set; }

        public PackageListAction()
        {
            Architecture = ArchitectureHelper.GuessBaseArchitecture;
            OS = null;
        }

        protected override int LockedExecute(CancellationToken cancellationToken)
        {
            if (OS == null)
            {
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.MacOSX:
                        OS = "OSX";
                        break;
                    case PlatformID.Unix:
                        OS = "Linux";
                        break;
                    default:
                        OS = "Windows";
                        break;
                }
            }

            if (Repository != null)
            {
                PackageManagerSettings.Current.Repositories.Clear();
                PackageManagerSettings.Current.Repositories.AddRange(Repository.Select(rep => new PackageManagerSettings.RepositorySettingEntry { IsEnabled = true, Url = rep }));
            }

            if (Target != null)
                Directory.SetCurrentDirectory(Target);

            HashSet<PackageDef> installed = new Installation(Directory.GetCurrentDirectory()).GetPackages().ToHashSet();

            
            VersionSpecifier versionSpec = VersionSpecifier.Any;
            if (!String.IsNullOrWhiteSpace(Version))
            {
                versionSpec = VersionSpecifier.Parse(Version);
            }

            if (string.IsNullOrEmpty(Name))
            {
                var packages = installed.ToList();
                packages.AddRange(PackageRepositoryHelpers.GetPackagesFromAllRepos(new PackageSpecifier("", versionSpec, Architecture, OS)));
                
                if (Installed)
                    packages = packages.Where(p => installed.Any(i => i.Name == p.Name)).ToList();
                
                PrintReadable(packages, installed);
            }
            else
            {
                IPackageIdentifier package = installed.FirstOrDefault(p => p.Name == Name);
                List<PackageVersion> versions = null;

                if (All)
                {
                    log.Info($"All available versions of '{Name}':\n");
                    versions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(Name);
                    var versionsCount = versions.Count;
                    if (versionsCount == 0) // No versions
                    {
                        log.Info($"No versions of '{Name}'.");
                        return 0;
                    }
                    
                    if (Version != null) // Version is specified by user
                        versions = versions.Where(v => versionSpec.IsCompatible(v.Version)).ToList();

                    if (versions.Any() == false && versionsCount > 0)
                    {
                        log.Info($"Package '{Name}' does not exists with version '{Version}'.");
                        log.Info($"Package '{Name}' exists in {versionsCount} other versions, please specify a different version.");
                    }
                    else
                        PrintVersionsReadable(package, versions);
                }
                else
                {
                    var opentap = new Installation(Target).GetOpenTapPackage();
                    versions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(Name, opentap);
    
                    if (versions.Any() == false) // No compatible versions
                    {
                        log.Warning($"There are no compatible versions of '{Name}'.");
                        versions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(Name).ToList();
                        if (versions.Any())
                            log.Info($"There are {versions.Count} incompatible versions available. Use '--all' to show these.");

                        return 0;
                    }

                    versions = versions.Where(v => versionSpec.IsCompatible(v.Version)).ToList(); // Filter compatible versions
                    if (versions.Any() == false) // No versions that are compatible
                    {
                        if (string.IsNullOrEmpty(Version))
                            log.Warning($"There are no released versions of '{Name}'.");
                        else
                            log.Warning($"Package '{Name}' does not exists with version '{Version}'.");

                        if (versions.Any())
                            log.Info($"There are {versions.Count} pre-released versions available. Use '--version <pre-release>' (e.g. '--version rc') or '--all' to show these.");

                        return 0;
                    }
                    
                    PrintVersionsReadable(package, versions);
                }
            }
            
            return 0;
        }

        private void PrintVersionsReadable(IPackageIdentifier package, List<PackageVersion> versions)
        {
            var verLen = versions.Select(p => p.Version?.ToString().Length).Max();
            var arcLen = versions.Select(p => p?.Architecture.ToString().Length).Max();
            var osLen = versions.Select(p => p.OS?.Length).Max();
            foreach (var version in versions)
                log.Info(string.Format($"{{0,-{verLen}}} - {{1,-{arcLen}}} - {{2,-{osLen}}} - {{3}}", version.Version, version.Architecture, version.OS ?? "Unknown", package != null && package.Equals(version) ? "installed" : "available"));
        }

        private void PrintReadable(List<PackageDef> packages, HashSet<PackageDef> installed)
        {
            var plist = packages.GroupBy(p => p.Name).Select(x => x.OrderByDescending(p => p.Version).First()).OrderBy(x => x.Name).ToList();

            if (plist.Count == 0)
            {
                log.Info("Selected directory has no packages installed or available.");
                return;
            }

            var nameLen = plist.Select(p => p.Name?.Length).Max();
            var verLen = plist.Select(p => p.Version?.ToString().Length).Max() ?? 0;
            verLen = Math.Max(verLen, installed.Select(p => p.Version?.ToString().Length).Max() ?? 0);


            foreach (var plugin in plist)
            {
                var installedPackage = installed.FirstOrDefault(p => p.Name == plugin.Name);
                var latestPackage = packages.Where(p => p.Name == plugin.Name).OrderByDescending(p => p.Version).FirstOrDefault();

                string logMessage = string.Format(string.Format("{{0,-{0}}} - {{1,-{1}}} - {{2}}", nameLen, verLen), plugin.Name, (installedPackage ?? plugin).Version, installedPackage != null ? "installed" : "available");
                if (installedPackage != null && installedPackage?.Version?.CompareTo(latestPackage.Version) < 0)
                    logMessage += " - update available";

                var licenses = string.Join(" & ", plugin.Files.Select(p => p.LicenseRequired).Where(l => string.IsNullOrWhiteSpace(l) == false).Select(LicenseBase.FormatFriendly));

                if (licenses != "")
                    logMessage += " - requires license " + licenses;

                log.Info(logMessage);
            }
        }
    }

    public abstract class PackageRunCommandAction : LockingPackageAction
    {
        private string Command { get { return this.GetType().GetAttribute<DisplayAttribute>().Name; } }

        protected virtual bool DoForce()
        {
            return false;
        }

        protected virtual bool ShouldIgnorePackage(string packageName)
        {
            return false;
        }

        [UnnamedCommandLineArgument("Package names", Required = true)]
        public string[] Packages { get; set; }

        protected override int LockedExecute(CancellationToken cancellationToken)
        {
            if (Command == null)
                throw new Exception("Invalid command.");

            if (Packages == null)
                throw new Exception("No packages specified.");

            Installer installer;
            installer = new Installer(Target, cancellationToken) { DoSleep = false};
            installer.ProgressUpdate += RaiseProgressUpdate;
            installer.Error += RaiseError;

            var installation = new Installation(Target);
            var plugins = installation.GetPackages();

            bool anyUnrecognizedPlugins = false;
            foreach (string arg in Packages)
            {
                PackageDef package = plugins.Where(p => p.Name == arg).FirstOrDefault();

                if (package != null)
                    installer.PackagePaths.Add(package.Location); // TODO: Fix this with #2951
                else if (!ShouldIgnorePackage(arg))
                {
                    log.Error("Could not find installed plugin named '{0}'", arg);
                    anyUnrecognizedPlugins = true;
                }
            }

            if (anyUnrecognizedPlugins)
                return -2;

            if (!PreCommand(installer.PackagePaths))
                return -3;

            return installer.RunCommand(Command, DoForce()) ? 0 : -1;
        }

        protected virtual bool PreCommand(List<string> packagePaths)
        {
            return true;
        }
    }

    [Display("uninstall", Group: "package", Description: "Uninstall one or more packages.")]
    public class PackageUninstallAction : PackageRunCommandAction
    {
        [CommandLineArgument("force", Description = "Try to uninstall packages completely even if some steps fail.", ShortName = "f")]
        public bool Force { get; set; }

        [CommandLineArgument("ignore-missing", Description = "Ignore names of packages that could not be found.", ShortName = "i")]
        public bool IgnoreMissing { get; set; }

        protected override bool DoForce()
        {
            return Force;
        }

        protected override bool ShouldIgnorePackage(string packageName)
        {
            return IgnoreMissing;
        }

        protected override bool PreCommand(List<string> packagePaths)
        {
            if (Force) return true;

            var packages = packagePaths.Select(PackageDef.FromXmlFile).ToList();
            var installed = new Installation(Target).GetPackages();
            installed = installed.RemoveIf(i => !packages.Any(u => u.Name == i.Name && u.Version == i.Version));
            var analyzer = DependencyAnalyzer.BuildAnalyzerContext(installed);
            var packagesWithIssues = new List<PackageDef>();

            foreach (var inst in installed)
            {
                if (analyzer.GetIssues(inst).Any(i => packages.Any(p => p.Name == i.PackageName)))
                    packagesWithIssues.Add(inst);
            }

            if (packages.Any(p => p.Files.Any(f => f.FileName.ToLower().EndsWith("OpenTap.dll"))))
            {
                log.Error("OpenTAP cannot be uninstalled.");
                return false;
            }

            if (packagesWithIssues.Any())
            {
                log.Warning("Plugin Dependecy Conflict.");

                var question = string.Format("One or more installed packages depend on {0}. Uninstalling might cause these packages to break:\n{1}",
                    string.Join(" and ", packages.Select(p => p.Name)),
                    string.Join("\n", packagesWithIssues.Select(p => p.Name + " " + p.Version)));

                var req = new ContinueRequest { message = question + "\nContinue?", Response = true };
                UserInput.Request(req, TimeSpan.MaxValue, true);

                return req.Response;
            }
            else
            {
                foreach (var bundle in packages.Where(p => p.IsBundle()).ToList())
                {
                    log.Warning("Package '{0}' is a bundle and has installed:\n{1}\n\nThese packages must be uninstalled separately.\n",
                        bundle.Name,
                        string.Join("\n", bundle.Dependencies.Select(d => d.Name)));
                }

                return true;
            }
        }
    }

    class ContinueRequest
    {
        [Browsable(true)]
        public string Message => message;
        internal string message;
        public string Name { get; private set; } = "Continue?";
        public bool Response { get; set; }
    }

    [Display("test", Group: "package", Description: "Runs tests on one or more packages.")]
    public class PackageTestAction : PackageRunCommandAction
    {
    }
}
