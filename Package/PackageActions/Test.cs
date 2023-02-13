using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

#pragma warning disable 1591 // TODO: Add XML Comments in this file, then remove this
namespace OpenTap.Package
{

    [Display("test", Group: "package", Description: "Run tests on one or more packages.")]
    public class PackageTestAction : PackageAction
    {
        [UnnamedCommandLineArgument("package(s)", Required = true)]
        public string[] Packages { get; set; }

        [CommandLineArgument("ignore-missing", Description = "Ignore packages in <package(s)> that are not currently installed.", ShortName = "i")]
        public bool IgnoreMissing { get; set; }

        public override int Execute(CancellationToken cancellationToken)
        {
            if (Packages == null)
                throw new Exception("No packages specified.");

            Packages = AutoCorrectPackageNames.Correct(Packages, Array.Empty<IPackageRepository>());

            var target = LockingPackageAction.GetLocalInstallationDir();


            Installer installer = new Installer(target, cancellationToken) {DoSleep = false};
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
                    log.Error("Package '{0}' is not installed", pack);
                    anyUnrecognizedPlugins = true;
                }
            }

            if (anyUnrecognizedPlugins)
                return (int) PackageExitCodes.InvalidPackageName;

            return installer.RunCommand("test", false, false);
        }
    }
}
