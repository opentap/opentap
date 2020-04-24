using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpenTap.Package
{

    [Display("test", Group: "package", Description: "Runs tests on one or more packages.")]
    public class PackageTestAction : LockingPackageAction
    {
        [UnnamedCommandLineArgument("Package names", Required = true)]
        public string[] Packages { get; set; }

        [CommandLineArgument("ignore-missing", Description = "Ignore names of packages that could not be found.", ShortName = "i")]
        public bool IgnoreMissing { get; set; }

        protected override int LockedExecute(CancellationToken cancellationToken)
        {
            if (Packages == null)
                throw new Exception("No packages specified.");

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

            return installer.RunCommand("test", false) ? 0 : -1;
        }
    }
}
