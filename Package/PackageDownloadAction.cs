//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Package
{
    [Display("download", Group: "package", Description: "Downloads one or more packages.")]
    public class PackageDownloadAction : LockingPackageAction
    {
        [CommandLineArgument("force", Description = "Download packages even if it results in some being broken.", ShortName = "f")]
        public bool ForceInstall { get; set; }

        [CommandLineArgument("dependencies", Description = "Download dependencies without asking.", ShortName = "y")]
        public bool InstallDependencies { get; set; }

        [CommandLineArgument("repository", Description = CommandLineArgumentRepositoryDescription, ShortName = "r")]
        public string[] Repository { get; set; }

        [CommandLineArgument("version", Description = CommandLineArgumentVersionDescription)]
        public string Version { get; set; }

        [CommandLineArgument("os", Description = CommandLineArgumentOsDescription)]
        public string OS { get; set; }

        [CommandLineArgument("architecture", Description = CommandLineArgumentArchitectureDescription)]
        public CpuArchitecture Architecture { get; set; }

        [UnnamedCommandLineArgument("packages", Required = true)]
        public string[] Packages { get; set; }

        [CommandLineArgument("dry-run", Description = "Initiates the command and checks for errors, but does not download any packages.")]
        public bool DryRun { get; set; } = false;

        /// <summary>
        /// This is used when specifying multiple packages with different version numbers. In that case <see cref="Packages"/> can be left null.
        /// </summary>
        public PackageSpecifier[] PackageReferences { get; set; }

        /// <summary>
        /// PackageDef of downloaded packages. Value is null until packages have actually been downloaded (after Execute)
        /// </summary>
        public IEnumerable<PackageDef> DownloadedPackages { get; private set; } = null; 

        static PackageDownloadAction()
        {
            log =  OpenTap.Log.CreateSource("Download");
        }

        public PackageDownloadAction()
        {
            Architecture = ArchitectureHelper.GuessBaseArchitecture;
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

        protected override int LockedExecute(CancellationToken cancellationToken)
        {
            string destinationDir = Target ?? Directory.GetCurrentDirectory();
            Installation destinationInstallation = new Installation(destinationDir);

            List<IPackageRepository> repositories = new List<IPackageRepository>();

            if (Repository == null)
                repositories.AddRange(PackageManagerSettings.Current.Repositories.Where(p => p.IsEnabled).Select(s => s.Manager).ToList());
            else
                repositories.AddRange(Repository.Select(s => PackageRepositoryHelpers.DetermineRepositoryType(s)));
            
            List<PackageDef> PackagesToDownload = PackageActionHelpers.GatherPackagesAndDependencyDefs(destinationInstallation, PackageReferences, Packages, Version, Architecture, OS, repositories, ForceInstall, InstallDependencies, false);

            if (PackagesToDownload == null)
                return 2;

            if (!DryRun)
                PackageActionHelpers.DownloadPackages(destinationDir, PackagesToDownload);
            else
                log.Info("Dry run completed. Specified packages are available.");

            DownloadedPackages = PackagesToDownload;
            return 0;
        }
        
        private static string MakeFilename(string osList)
        {
            return FileSystemHelper.EscapeBadPathChars(osList.Replace("/", "").Replace("\\", ""));
        }
    }
}
