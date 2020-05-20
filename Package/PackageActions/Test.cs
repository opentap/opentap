using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

#pragma warning disable 1591 // TODO: Add XML Comments in this file, then remove this
namespace OpenTap.Package
{

    [Display("test", Group: "package", Description: "Runs tests on one or more packages.")]
    public class PackageTestAction : PackageAction
    {
        [UnnamedCommandLineArgument("Package names", Required = true)]
        public string[] Packages { get; set; }

        [CommandLineArgument("ignore-missing", Description = "Ignore names of packages that could not be found.", ShortName = "i")]
        public bool IgnoreMissing { get; set; }

        public override int Execute(CancellationToken cancellationToken)
        {
            if (Packages == null)
                throw new Exception("No packages specified.");
            
            var target = LockingPackageAction.GetLocalInstallationDir();
            

            Installer installer = new Installer(target, cancellationToken) { DoSleep = false };
            installer.ProgressUpdate += RaiseProgressUpdate;
            installer.Error += RaiseError;

            var installedPackages = new Installation(target).GetPackages();

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

            return installer.RunCommand("test", false, false) ? 0 : -1;
        }
    }
}
