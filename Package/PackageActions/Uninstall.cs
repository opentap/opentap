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
        
        private int DoExecute(CancellationToken cancellationToken)
        {
            if (Force == false && Packages.Any(p => p == "OpenTAP") && Target == ExecutorClient.ExeDir)
            {
                log.Error(
                    "Aborting request to uninstall the OpenTAP package that is currently executing as that would brick this installation. Use --force to uninstall anyway.");
                return (int) ExitCodes.ArgumentError;
            }

            Installer installer = new Installer(Target, cancellationToken) {DoSleep = false};
            installer.ProgressUpdate += RaiseProgressUpdate;
            installer.Error += RaiseError;

            var installation = new Installation(Target);
            var installedPackages = installation.GetPackages();

            bool anyUnrecognizedPlugins = false;
            foreach (string pack in Packages)
            {
                PackageDef package = installedPackages.FirstOrDefault(p => p.Name == pack);

                if (package != null && package.PackageSource is InstalledPackageDefSource source)
                {
                    installer.PackagePaths.Add(source.PackageDefFilePath);
                    if (package.IsBundle())
                        installer.PackagePaths.AddRange(GetPaths(package, installedPackages));
                }
                else if (!IgnoreMissing)
                {
                    log.Error("Package '{0}' is not installed", pack);
                    anyUnrecognizedPlugins = true;
                }
            }

            if (anyUnrecognizedPlugins)
                return (int) PackageExitCodes.InvalidPackageName;

            if (!Force)
                if (!CheckPackageAndDependencies(installedPackages, installer.PackagePaths, out var userCancelled))
                {
                    if (userCancelled)
                    {
                        log.Info("Uninstall cancelled by user.");
                        return (int) ExitCodes.UserCancelled;
                    }

                    return (int) PackageExitCodes.PackageDependencyError;
                }

            var status = installer.RunCommand("uninstall", Force, true);
            if (status == (int) ExitCodes.GeneralException)
                return (int) PackageExitCodes.PackageUninstallError;
            return status;
        }
        
        protected override int LockedExecute(CancellationToken cancellationToken)
        {
            if (Packages == null)
                throw new Exception("No packages specified.");
            
            var currentInterface = UserInput.GetInterface();
            if (NonInteractive)
                UserInput.SetInterface(new NonInteractiveUserInputInterface());

            try
            {
                return DoExecute(cancellationToken);
            }
            finally
            {
                IncrementChangeId(Target);
                UserInput.SetInterface(currentInterface);
            }
        }

        private List<string> GetPaths(PackageDef package, List<PackageDef> installedPackages)
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
                
                if (dependencyPackage != null && dependencyPackage.PackageSource is XmlPackageDefSource source2)
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

        private bool CheckPackageAndDependencies(List<PackageDef> installed, List<string> packagePaths, out bool userCancelled)
        {
            userCancelled = false;
            var packages = packagePaths.Select(str =>
            {
                var pkg = PackageDef.FromXml(str);
                pkg.PackageSource = new XmlPackageDefSource{PackageDefFilePath = str};
                return pkg;
            }).ToList();
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

                if (req.Response == ContinueResponse.Continue)
                    return true;
                userCancelled = true;
                return false;
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
        [Layout(LayoutMode.FullRow)]
        public string Message => message;
        internal string message;
        public string Name { get; private set; } = "Continue?";

        [Submit]
        [Layout(LayoutMode.FullRow | LayoutMode.FloatBottom)]
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
        [Layout(LayoutMode.FullRow)]
        public string Message { get; }
        [Layout(LayoutMode.FullRow | LayoutMode.FloatBottom)]
        [Submit] public UninstallResponse Response { get; set; }
    }
}
