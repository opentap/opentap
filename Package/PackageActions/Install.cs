//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OpenTap.Cli;

#pragma warning disable 1591 // TODO: Add XML Comments in this file, then remove this
namespace OpenTap.Package
{
    [Display("install", Group: "package", Description: "Install one or more packages.")]
    public class PackageInstallAction : IsolatedPackageAction
    {
        [Obsolete("Use Force instead.")]
        public bool ForceInstall { get => Force; set => Force = value; }

        [CommandLineArgument("dependencies", Description = "Install dependencies without asking. This is always enabled when installing bundle packages.", ShortName = "y")]
        public bool InstallDependencies { get; set; }

        [CommandLineArgument("repository", Description = CommandLineArgumentRepositoryDescription, ShortName = "r")]
        public string[] Repository { get; set; }

        [CommandLineArgument("version", Description = CommandLineArgumentVersionDescription)]
        public string Version { get; set; }

        [CommandLineArgument("os", Description = CommandLineArgumentOsDescription)]
        public string OS { get; set; }

        [CommandLineArgument("architecture", Description = CommandLineArgumentArchitectureDescription)]
        public CpuArchitecture Architecture { get; set; }

        /// <summary>
        /// This is used when specifying the install action through the CLI. If you need to specify multiple packages with different version numbers, use <see cref="PackageReferences"/>
        /// </summary>
        [UnnamedCommandLineArgument("Packages", Required = true)]
        public string[] Packages { get; set; }
        
        [CommandLineArgument("check-only", Description = "Checks if the selected package(s) can be installed, but does not install or download them.")]
        public bool CheckOnly { get; set; }
        
        [CommandLineArgument("interactive", Description = "More user responsive.")]
        public bool Interactive { get; set; }
        
        /// <summary>
        /// This is used when specifying multiple packages with different version numbers. In that case <see cref="Packages"/> can be left null.
        /// </summary>
        public PackageSpecifier[] PackageReferences { get; set; }

        public PackageInstallAction()
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
            if (Target == null)
                Target = FileSystemHelper.GetCurrentInstallationDirectory();
            var targetInstallation = new Installation(Target);

            List<IPackageRepository> repositories = new List<IPackageRepository>();
            if (Repository == null)
                repositories.AddRange(PackageManagerSettings.Current.Repositories.Where(p => p.IsEnabled).Select(s => s.Manager).ToList());
            else
                repositories.AddRange(Repository.Select(s => PackageRepositoryHelpers.DetermineRepositoryType(s)));
            
            bool installError = false;
            var installer = new Installer(Target, cancellationToken) { DoSleep = false, ForceInstall = Force };
            installer.ProgressUpdate += RaiseProgressUpdate;
            installer.Error += RaiseError;
            installer.Error += ex => installError = true;

            try
            {
                log.Debug("Fetching package information...");
                // Get package information
                List<PackageDef> packagesToInstall = PackageActionHelpers.GatherPackagesAndDependencyDefs(targetInstallation, PackageReferences, Packages, Version, Architecture, OS, repositories, Force, InstallDependencies, !Force);
                if (packagesToInstall?.Any() != true)
                {
                    log.Info("Could not find one or more packages.");
                    return 2;
                }

                var installationPackages = targetInstallation.GetPackages();

                var overWriteCheckExitCode = CheckForOverwrittenPackages(installationPackages, packagesToInstall, Force, Interactive);
                if (overWriteCheckExitCode == InstallationQuestion.Cancel)
                    return 2;

                // Check dependencies
                var issue = DependencyChecker.CheckDependencies(installationPackages, packagesToInstall, Force ? LogEventType.Warning : LogEventType.Error);
                if (issue == DependencyChecker.Issue.BrokenPackages)
                {
                    if (!Force)
                    {
                        log.Info("To fix the package conflict uninstall or update the conflicted packages.");
                        log.Info("To install packages despite the conflicts, use the --force option.");
                        return 4;
                    }
                    if(!CheckOnly)
                        log.Warning("Continuing despite breaking installed packages (--force)...");
                }

                if (CheckOnly)
                {
                    log.Info("Check completed with no problems detected.");
                    return 0;
                }

                // Download the packages
                var downloadedPackageFiles = PackageActionHelpers.DownloadPackages(PackageCacheHelper.PackageCacheDirectory, packagesToInstall);
                installer.PackagePaths.AddRange(downloadedPackageFiles);
            }
            catch (Exception e)
            {
                log.Info("Could not download one or more packages.");
                log.Info(e.Message);
                log.Debug(e);
                RaiseError(e);
                return 6;
            }
            
            log.Info("Installing to {0}", Path.GetFullPath(Target));
            
            // Uninstall old packages before
            UninstallExisting(targetInstallation, installer.PackagePaths, cancellationToken);

            var toInstall = ReorderPackages(installer.PackagePaths);
            installer.PackagePaths.Clear();
            installer.PackagePaths.AddRange(toInstall);

            // Install the package
            installer.InstallThread();

            return installError ? 5 : 0;
        }

        private void UninstallExisting(Installation installation, List<string> packagePaths, CancellationToken cancellationToken)
        {
            var installed = installation.GetPackages();

            var packages = packagePaths.Select(PackageDef.FromPackage).Select(x => x.Name).ToHashSet();
            var existingPackages = installed.Where(kvp => packages.Contains(kvp.Name)).Select(x => (x.PackageSource as InstalledPackageDefSource)?.PackageDefFilePath).ToList();

            if (existingPackages.Count == 0) return;

            var newInstaller = new Installer(Target, cancellationToken);

            //newInstaller.ProgressUpdate += RaiseProgressUpdate;
            newInstaller.Error += RaiseError;
            newInstaller.DoSleep = false;

            newInstaller.PackagePaths.AddRange(existingPackages);
            newInstaller.UninstallThread();
        }

        /// <summary>
        /// Reorder packages to ensure that dependencies are installed before a package needing it.
        /// </summary>
        /// <param name="packagePaths"></param>
        /// <returns></returns>
        private List<string> ReorderPackages(List<string> packagePaths)
        {
            var toInstall = new List<string>();

            var packages = packagePaths.ToDictionary(k => k, k => PackageDef.FromPackage(k));

            while (packages.Count > 0)
            {
                var next = packages.FirstOrDefault(pkg => pkg.Value.Dependencies.All(dep => !packages.Values.Any(p => p.Name == dep.Name)));

                if (next.Value == null) next = packages.First(); // This doesn't matter at this point

                toInstall.Add(next.Key);
                packages.Remove(next.Key);
            }

            return toInstall;
        }


        public enum InstallationQuestion
        {
            //KeepExitingFiles = 3,
            [Display("Cancel", Order: 2)]
            Cancel = 2,
            [Display("Overwrite Files", Order: 1)]
            OverwriteFile = 1,
            
            [Browsable(false)]
            Success = 0,
        }
        class AskAboutInstallingAnyway
        {
            public string Name { get; } = "Overwrite Files?";
            
            [Browsable(true)]
            public string Message { get; private set; }

            public AskAboutInstallingAnyway(string message) => Message = message;

            [Layout(LayoutMode.FloatBottom)]
            [Submit] public InstallationQuestion Response { get; set; } = InstallationQuestion.Cancel;
        }
        
        internal static InstallationQuestion CheckForOverwrittenPackages(IEnumerable<PackageDef> installedPackages, IEnumerable<PackageDef> packagesToInstall, bool force, bool interactive = false)
        {
            var conflicts = VerifyPackageHashes.CalculatePackageInstallConflicts(installedPackages, packagesToInstall);

            void buildMessage(Action<string> addLine)
            {
                foreach (var conflictGroup in conflicts.ToLookup(x => x.OffendingPackage))
                {
                    addLine($"  These files will be overwritten if package '{conflictGroup.Key.Name}' is installed:");
                    foreach (var conflict in conflictGroup)
                    {
                        addLine($"    {conflict.File.FileName} (from package '{conflict.Package.Name}').");
                    }
                }
            }
            
            if (conflicts.Any())
            {
                buildMessage(line => log.Info(line));
                if(force)
                    log.Warning("--force specified. Overwriting files.");
                else
                {
                    if (interactive)
                    {
                        StringBuilder message = new StringBuilder();
                        buildMessage(line => message.AppendLine(line));
                        var question = new AskAboutInstallingAnyway(message.ToString());
                        UserInput.Request(question);
                        return question.Response;
                    }   
                    log.Error(
                        "Installing these packages will overwrite existing files. Use --force to overwrite existing files, possible breaking installed packages.");
                }
                
                if (!force)
                    return InstallationQuestion.Cancel;
                return InstallationQuestion.OverwriteFile;
            }

            return InstallationQuestion.Success;
        }
    }
}
