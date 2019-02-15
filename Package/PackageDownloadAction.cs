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

        [CommandLineArgument("repository", Description = "Search this repository for packages instead of using\nsettings from 'Package Manager.xml'.", ShortName = "r")]
        public string[] Repository { get; set; }

        [CommandLineArgument("version", Description = "Specify a version string that the package must be compatible with.\nTo specify a version that has to match exactly start the number with '!'. E.g. \"!8.1.319-beta\".")]
        public string Version { get; set; }

        [CommandLineArgument("os", Description = "Override which OS to target.")]
        public string OS { get; set; }

        [CommandLineArgument("architecture", Description = "Override which CPU to target.")]
        public CpuArchitecture Architecture { get; set; }

        [UnnamedCommandLineArgument("packages", Required = true)]
        public string[] Packages { get; set; }

        [CommandLineArgument("dry-run", Description = "Initiates the command and checks for errors, but does not download any packages.")]
        public bool DryRun { get; set; } = false;

        /// <summary>
        /// This is used when specifying multiple packages with different version numbers. In that case <see cref="Packages"/> can be left null.
        /// </summary>
        public PackageSpecifier[] PackageReferences { get; set; }

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
            
            List<PackageDef> PackagesToDownload = PackageActionHelpers.GatherPackagesAndDependencyDefs(PackageCacheHelper.PackageCacheDirectory, destinationDir, PackageReferences, Packages, Version, Architecture, OS, Repository, ForceInstall, InstallDependencies, false);

            if (PackagesToDownload == null)
                return 2;

            if (DryRun)
            {
                log.Info("Dry run completed. Specified packages are available.");
                return 0;
            }

            PackageActionHelpers.DownloadPackages(destinationDir, PackagesToDownload);

            return 0;
        }
        
        private static string MakeFilename(string osList)
        {
            return FileSystemHelper.EscapeBadPathChars(osList.Replace("/", "").Replace("\\", ""));
        }
    }
}
