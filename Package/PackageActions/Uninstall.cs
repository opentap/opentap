using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace OpenTap.Package
{

    [Display("uninstall", Group: "package", Description: "Uninstall one or more packages.")]
    public class PackageUninstallAction : IsolatedPackageAction
    {
        [CommandLineArgument("ignore-missing", Description = "Ignore names of packages that could not be found.", ShortName = "i")]
        public bool IgnoreMissing { get; set; }


        [UnnamedCommandLineArgument("Package names", Required = true)]
        public string[] Packages { get; set; }

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
                    installer.PackagePaths.Add(source.PackageDefFilePath);
                else if (!IgnoreMissing)
                {
                    log.Error("Could not find installed plugin named '{0}'", pack);
                    anyUnrecognizedPlugins = true;
                }
            }

            if (anyUnrecognizedPlugins)
                return -2;

            if (!Force)
                if (!CheckPackageAndDependencies(installedPackages, installer.PackagePaths))
                    return -3;

            return installer.RunCommand("uninstall", Force) ? 0 : -1;
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
                log.Warning("Plugin Dependecy Conflict.");

                var question = string.Format("One or more installed packages depend on {0}. Uninstalling might cause these packages to break:\n{1}",
                    string.Join(" and ", packages.Select(p => p.Name)),
                    string.Join("\n", packagesWithIssues.Select(p => p.Name + " " + p.Version)));

                var req = new ContinueRequest { message = question, Response = ContinueResponse.Continue };
                UserInput.Request(req, true);

                return req.Response == ContinueResponse.Continue;
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
}
