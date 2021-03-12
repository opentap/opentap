using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

#pragma warning disable 1591 // TODO: Add XML Comments in this file, then remove this
namespace OpenTap.Package
{

    [Display("uninstall", Group: "package", Description: "Uninstall one or more packages.")]
    public class PackageUninstallAction : IsolatedPackageAction
    {
        [CommandLineArgument("ignore-missing", Description = "Ignore packages in <package(s)> that are not currently installed.", ShortName = "i")]
        public bool IgnoreMissing { get; set; }


        [UnnamedCommandLineArgument("package(s)", Required = true)]
        public string[] Packages { get; set; }
        
        /// <summary>
        /// Never prompt for user input.
        /// </summary>
        [CommandLineArgument("non-interactive", Description = "Never prompt for user input.")]
        public bool NonInteractive { get; set; } = false;

        protected override int LockedExecute(CancellationToken cancellationToken)
        {
            if (Packages == null)
                throw new Exception("No packages specified.");

            if (Force == false && Packages.Any(p => p == "OpenTAP") && Target == ExecutorClient.ExeDir)
            {
                log.Error("Aborting request to uninstall the OpenTAP package that is currently executing as that would brick this installation. Use --force to uninstall anyway.");
                return -4;
            }

            Installer installer = new Installer(Target, cancellationToken) { DoSleep = false };
            installer.ProgressUpdate += RaiseProgressUpdate;
            installer.Error += RaiseError;

            var installedPackages = new Installation(Target).GetPackages();

            bool anyUnrecognizedPlugins = false;
            foreach (string pack in Packages)
            {
                PackageDef package = installedPackages.FirstOrDefault(p => p.Name == pack);

                if (package != null && package.PackageSource is InstalledPackageDefSource source)
                {
                    installer.PackagePaths.Add(source.PackageDefFilePath);
                    if (package.IsBundle())
                        installer.PackagePaths.AddRange(GetPaths(package, source, installedPackages));
                }
                else if (!IgnoreMissing)
                {
                    log.Error("Package '{0}' is not installed", pack);
                    anyUnrecognizedPlugins = true;
                }
            }

            if (anyUnrecognizedPlugins)
                return -2;

            if (!Force)
                if (!CheckPackageAndDependencies(installedPackages, installer.PackagePaths))
                    return -3;

            return installer.RunCommand("uninstall", Force, true) ? 0 : -1;
        }

        private List<string> GetPaths(PackageDef package, InstalledPackageDefSource source,
            List<PackageDef> installedPackages)
        {
            if (NonInteractive)
            {
                var bundledPackages = string.Join("\n", package.Dependencies.Select(d => d.Name));
                log.Warning(
                    $"Package '{package.Name}' is a bundle and has installed:\n{bundledPackages}\n\nThese packages must be uninstalled separately.\n");
                log.Info($"Run the uninstall without the 'non-interactive' flag to interactively decide whether to keep or remove each package.");
                return new List<string>();
            }

            var result = new List<string>(package.Dependencies.Count);

            foreach (var dependency in package.Dependencies)
            {
                var dependencyPackage = installedPackages.FirstOrDefault(p => p.Name == dependency.Name);
                
                if (dependencyPackage != null && dependencyPackage.PackageSource is InstalledPackageDefSource source2)
                {
                    var question =
                        $"Package '{dependency.Name}' is a member of the bundle '{package.Name}'.\nDo you wish to uninstall '{dependency.Name}'?";

                    var req = new UninstallRequest(question) {Response = UninstallResponse.No};
                    UserInput.Request(req, true);

                    if (req.Response == UninstallResponse.Yes)
                        result.Add(source2.PackageDefFilePath);
                }
            }

            return result;
        }

        private bool CheckPackageAndDependencies(List<PackageDef> installed, List<string> packagePaths)
        {
            var packages = packagePaths.Select(PackageDef.FromXml).ToList();
            installed.RemoveIf(i => packages.Any(u => u.Name == i.Name && u.Version == i.Version));
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
                log.Warning("Plugin Dependency Conflict.");

                var question = string.Format("One or more installed packages depend on {0}. Uninstalling might cause these packages to break:\n{1}",
                    string.Join(" and ", packages.Select(p => p.Name)),
                    string.Join("\n", packagesWithIssues.Select(p => p.Name + " " + p.Version)));

                var req = new ContinueRequest { message = question, Response = ContinueResponse.Continue };
                UserInput.Request(req, true);

                return req.Response == ContinueResponse.Continue;
            }

            return true;
        }
    }

    [Obfuscation(Exclude = true)]
    enum ContinueResponse
    {
        Continue,
        Cancel
    }

    [Obfuscation(Exclude = true)]
    class ContinueRequest
    {
        [Browsable(true)]
        public string Message => message;
        internal string message;
        public string Name { get; private set; } = "Continue?";

        [Submit]
        public ContinueResponse Response { get; set; }
    }

    [Obfuscation(Exclude = true)]
    enum UninstallResponse
    {
        Yes,
        No
    }
    
    [Obfuscation(Exclude = true)]
    [Display("Uninstall bundled package?")]
    class UninstallRequest
    {
        public UninstallRequest(string message)
        {
            Message = message;
        }
        
        [Browsable(true)]
        public string Message { get; }
        [Submit] public UninstallResponse Response { get; set; }
    }
}
