//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpenTap.Cli;

#pragma warning disable 1591 // TODO: Add XML Comments in this file, then remove this
namespace OpenTap.Package
{
    [Display("download", Group: "package", Description: "Download one or more packages.")]
    public class PackageDownloadAction : LockingPackageAction
    {
        [CommandLineArgument("force", Description = "Download packages even if it results in some being broken.", ShortName = "f")]
        public bool ForceInstall { get; set; }

        [CommandLineArgument("dependencies", Description = "Download dependencies without asking.", ShortName = "y")]
        public bool InstallDependencies { get; set; }

        [CommandLineArgument("repository", Description = CommandLineArgumentRepositoryDescription, ShortName = "r")]
        public string[] Repository { get; set; }

        [CommandLineArgument("no-cache", Description = CommandLineArgumentNoCacheDescription)]
        public bool NoCache { get; set; }

        [CommandLineArgument("version", Description = CommandLineArgumentVersionDescription)]
        public string Version { get; set; }

        [CommandLineArgument("os", Description = CommandLineArgumentOsDescription)]
        public string OS { get; set; }

        [CommandLineArgument("architecture", Description = CommandLineArgumentArchitectureDescription)]
        public CpuArchitecture Architecture { get; set; }

        [UnnamedCommandLineArgument("package(s)", Required = true)]
        public string[] Packages { get; set; }

        /// <summary>
        /// Represents the --out command line argument which specifies the path to the output file.
        /// </summary>
        [CommandLineArgument("out", Description = "Path to the output files. Can a file or a folder.", ShortName = "o")]
        public string OutputPath { get; set; }

        [CommandLineArgument("dry-run", Description = "Initiate the command and check for errors, but don't download any packages.")]
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
            log = OpenTap.Log.CreateSource("Download");
        }

        public PackageDownloadAction()
        {
            Architecture = ArchitectureHelper.GuessBaseArchitecture;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                    OS = "MacOS";
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

            if (NoCache) PackageManagerSettings.Current.UseLocalPackageCache = false;
            List<IPackageRepository> repositories = PackageManagerSettings.Current.GetEnabledRepositories(Repository);
            Packages = AutoCorrectPackageNames.Correct(Packages, repositories);

            List<PackageDef> PackagesToDownload = PackageActionHelpers.GatherPackagesAndDependencyDefs(
                destinationInstallation, PackageReferences, Packages, Version, Architecture, OS, repositories,
                ForceInstall, InstallDependencies, false, false, false);
            
            if (PackagesToDownload?.Any() != true)
                return (int)ExitCodes.ArgumentError;
            
            var progressPercentage = 0.0f;

            if (!DryRun)
            {
                if (OutputPath != null)
                {
                    if (OutputPath.EndsWith("/") || OutputPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                        Directory.CreateDirectory(OutputPath);

                    if (Directory.Exists(OutputPath))
                    {
                        destinationDir = OutputPath;
                    }
                    else
                    {
                        // If a filename is specified, name the first Package argument 
                        destinationDir = new FileInfo(OutputPath).DirectoryName;
                        Directory.CreateDirectory(destinationDir);

                        // In this case, 'OutputPath' is a specific filename. If we are downloading multiple packages,
                        // this should be the filename of the first package specified. After this file is downloaded,
                        // the rest of the packages to be downloaded, if any, will be processed normally.
                        var firstPackage = Packages?.FirstOrDefault() ?? PackageReferences?.FirstOrDefault()?.Name;
                        if (firstPackage != null)
                        {
                            var package = PackagesToDownload.First(p =>
                                p.Name == firstPackage || p.PackageSource is FilePackageDefSource s &&
                                s.PackageFilePath == Path.GetFullPath(firstPackage));

                            // The total progress of downloading 1 package
                            var packageProgressAmount = 1.0f / PackagesToDownload.Count;

                            PackageActionHelpers.DownloadPackages(destinationDir, new List<PackageDef>() {package},
                                new List<string>() {OutputPath},
                                (percent, msg) => RaiseProgressUpdate((int) (packageProgressAmount * percent), msg));

                            progressPercentage = packageProgressAmount * 100;
                            RaiseProgressUpdate((int) progressPercentage, $"Downloaded {package.Name}");

                            PackagesToDownload.Remove(package);
                        }
                    }
                }

                // The total remaining progress - 100.0 if not using the --out parameter - ((nPackages - 1) / nPackages) otherwise
                var remainingPercentage = 100.0f - progressPercentage;

                try
                {
                    // Download the remaining packages
                    PackageActionHelpers.DownloadPackages(destinationDir, PackagesToDownload,
                        ignoreCache: NoCache,
                        progressUpdate: (partialPercent, message) =>
                        {
                            var partialProgressPercentage = partialPercent * (remainingPercentage / 100);
                            RaiseProgressUpdate((int)(progressPercentage + partialProgressPercentage), message);
                        });
                }
                catch(OperationCanceledException)
                {
                    log.Debug("Download canceled.");
                    return (int)ExitCodes.UserCancelled;
                }
            }
            else
                log.Info("Dry run completed. Specified packages are available.");

            DownloadedPackages = PackagesToDownload;

            return (int)ExitCodes.Success;
        }
    }
}
