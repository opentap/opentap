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
using OpenTap.Package.PackageInstallHelpers;

#pragma warning disable 1591 // TODO: Add XML Comments in this file, then remove this
namespace OpenTap.Package
{
    [Display("install", Group: "package", Description: "Install one or more packages.")]
    public class PackageInstallAction : IsolatedPackageAction
    {
        static readonly TraceSource log = Log.CreateSource("Install");

        [Obsolete("Use Force instead.")]
        public bool ForceInstall { get => Force; set => Force = value; }

        [CommandLineArgument("dependencies", Description = "Install dependencies without asking. This is always enabled when installing bundle packages.", ShortName = "y")]
        public bool InstallDependencies { get; set; }

        [Obsolete("It is no longer supported to ignore dependencies as it causes a broken installation when used.")]
        [CommandLineArgument("no-dependencies", Description = "Don't install dependencies. This is implied when using --force.")]
        public bool IgnoreDependencies { get; set; }

        [CommandLineArgument("overwrite", Description = "Overwrite files that already exist without asking. This is implied when using --force.")]
        public bool Overwrite { get; set; }

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

        /// <summary>
        /// This is used when specifying the install action through the CLI. If you need to specify multiple packages with different version numbers, use <see cref="PackageReferences"/>
        /// </summary>
        [UnnamedCommandLineArgument("package(s)", Required = true)]
        public string[] Packages { get; set; }

        [CommandLineArgument("check-only", Description = "Checks if the selected package(s) can be installed, but does not install or download them.")]
        public bool CheckOnly { get; set; }

        [Obsolete("Interactive is the default. Use --non-interactive to disable.")]
        [CommandLineArgument("interactive", Description = "More user responsive.")]
        [Browsable(false)]
        public bool Interactive { get; set; }

        /// <summary>
        /// Never prompt for user input.
        /// </summary>
        [CommandLineArgument("non-interactive", Description = "Never prompt for user input.")]
        public bool NonInteractive { get; set; } = false;

        [CommandLineArgument("no-downgrade", Description = "Don't install if the same or a newer version is already installed.")]
        public bool NoDowngrade { get; set; }

        [CommandLineArgument("unpack-only", Description = "Only unpack the package payload into the installation directory and\n" +
                                                          "skip any additional install actions the package might have defined.\n" +
                                                          "This can leave the installed package unusable.")]
        public bool UnpackOnly { get; set; }

        // This is set when system wide packages are being installed.
        public bool SystemWideOnly { get; set; }

        /// <summary>
        /// This is used when specifying multiple packages with different version numbers. In that case <see cref="Packages"/> can be left null.
        /// </summary>
        public PackageSpecifier[] PackageReferences { get; set; }


        private string DefaultOs;

        public PackageInstallAction()
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

            DefaultOs = OS;
        }

        private int DoExecute(CancellationToken cancellationToken)
        {
            if (Target == null)
                Target = FileSystemHelper.GetCurrentInstallationDirectory();
            var targetInstallation = new Installation(Target);

            if (NoCache) PackageManagerSettings.Current.UseLocalPackageCache = false;
            List<IPackageRepository> repositories = PackageManagerSettings.Current.GetEnabledRepositories(Repository);
            Packages = AutoCorrectPackageNames.Correct(Packages, repositories);

            bool installError = false;
            var installer = new Installer(Target, cancellationToken)
            { DoSleep = false, ForceInstall = Force, UnpackOnly = UnpackOnly };
            installer.ProgressUpdate += RaiseProgressUpdate;
            installer.Error += RaiseError;
            installer.Error += ex => installError = true;

            try
            {
                log.Debug("Fetching package information...");

                RaiseProgressUpdate(5, "Gathering packages.");

                // If exact version is specified, check if it's already installed
                if (Version != null && SemanticVersion.TryParse(Version, out var vs) && Force == false)
                {
                    foreach (var pkg in Packages)
                    {
                        // We should always install when it's a file, this could be part of development
                        if (File.Exists(pkg))
                            break;

                        PackageIdentifier pid = new PackageIdentifier(pkg, vs, Architecture, OS);
                        var installedPackage = targetInstallation.GetPackages().FirstOrDefault(p => p.Name == pid.Name);
                        if (installedPackage != null && pid.Version.Equals(installedPackage.Version))
                        {
                            log.Info($"Package '{pid.Name}' '{installedPackage.Version}' is already installed.");
                            return (int)ExitCodes.Success;
                        }
                    }
                }

                // Get package information
                bool askToInstallDependencies = !NonInteractive;
                if (Force)
                    askToInstallDependencies = false;

                List<PackageDef> packagesToInstall;
                try
                {
                    packagesToInstall = PackageActionHelpers.GatherPackagesAndDependencyDefs(
                        targetInstallation, PackageReferences, Packages, Version, Architecture, OS, repositories, Force,
                        InstallDependencies, Force, askToInstallDependencies, NoDowngrade);
                }
                catch (ImageResolveException ex)
                {
                    // If the problem is exactly that we failed to resolve a compatible release version of a package,
                    // ask the user to retry the resolution with a pre-release instead.
                    // We could also ask for an RC / Any, but if there are no releases, then it is likely also the case that there are no RCs.
                    // 'any' would be the most likely specifier to succeed, but it is probably a bit too extreme to suggest something so bleeding edge.
                    // A beta version strikes a good middle ground. It is pretty likely to resolve, and probably won't be too unstable.
                    if (NonInteractiveUserInputInterface.IsSet()) throw;
                    if (Packages.Length != 1) throw;
                    if (false == (ex.Result is FailedImageResolution fir)) throw;
                    if (fir.resolveProblems.Count != 1) throw;

                    var problem = fir.resolveProblems[0];
                    if (problem.Version != VersionSpecifier.AnyRelease) throw;
                    // Only show the beta option if the resolution problem was with the package we are trying to install
                    if (problem.Name != Packages[0]) throw;

                    var req = new AskAboutPrerelease($"Package '{problem.Name}' has no compatible release version. Try a beta version instead?");
                    UserInput.Request(req);

                    if (req.Response == UseBetaQuestion.No) throw;

                    Version = "beta";
                    packagesToInstall = PackageActionHelpers.GatherPackagesAndDependencyDefs(
                        targetInstallation, PackageReferences, Packages, Version, Architecture, OS, repositories, Force,
                        InstallDependencies, Force, askToInstallDependencies, NoDowngrade);
                }

                if (SystemWideOnly)
                {
                    // the current process is for installing system wide packages only.
                    packagesToInstall = packagesToInstall.Where(x => x.IsSystemWide()).ToList();
                }

                if (packagesToInstall?.Any() != true)
                {
                    if (NoDowngrade)
                    {
                        log.Info("No package(s) were upgraded.");
                        return (int)ExitCodes.Success;
                    }

                    log.Info("Could not find one or more packages.");
                    return (int)PackageExitCodes.PackageDependencyError;
                }

                foreach (var pkg in packagesToInstall)
                {
                    // print a warning if the selected package is incompatible with the host platform.
                    // or return an error if the package does not match.
                    var platformCompatible =
                        pkg.IsPlatformCompatible(targetInstallation.Architecture, targetInstallation.OS);

                    if (!Force)
                    {
                        // print a warning if necessary.
                        // --os and --architecture are not really supported when --force is not enabled.
                        // we only allow resolving to installable packages.
                        var differentArch = ArchitectureHelper.GuessBaseArchitecture != Architecture;

                        bool printedWarning = false;
                        void maybePrintWarning()
                        {
                            if (printedWarning) return;
                            printedWarning = true;
                            log.Warning("OS or Architecture were specified without --force. Only compatible packages will be selected.");

                        }
                        foreach (var package in packagesToInstall)
                        {
                            if (differentArch)
                            {
                                if (package.Architecture != CpuArchitecture.AnyCPU && Architecture != package.Architecture)
                                {
                                    maybePrintWarning();
                                    log.Warning("Selected package {2} architecture {0} instead of {1}", package.Architecture, Architecture, package.Name);
                                }
                            }

                            if (OS != DefaultOs)
                            {
                                if (!package.IsOsCompatible(OS))
                                {
                                    maybePrintWarning();
                                    log.Warning("Selected package {2} for {0} instead of {1}", package.Name, package.OS, OS);
                                }
                            }
                        }

                    }

                    if (!platformCompatible)
                    {
                        var selectedPlatformCompatible = pkg.IsPlatformCompatible(Architecture, OS);
                        var message =
                            $"Selected package {pkg.Name} for {pkg.OS}, {pkg.Architecture} is incompatible with the host platform {targetInstallation.OS}, {targetInstallation.Architecture}.";
                        if (selectedPlatformCompatible || Force)
                            // --OS [arg] was used or --force is specified. Try to install it anyway. 
                            log.Warning(message);
                        else
                        {
                            log.Error(message);
                            return (int)ExitCodes.ArgumentError;
                        }
                    }
                }

                var installationPackages = targetInstallation.GetPackages();

                var overWriteCheckExitCode = CheckForOverwrittenPackages(installationPackages, packagesToInstall,
                    Force || Overwrite, !(NonInteractive || Overwrite));
                if (overWriteCheckExitCode == InstallationQuestion.Cancel)
                {
                    log.Info("Install cancelled by user.");
                    return (int)ExitCodes.UserCancelled;
                }

                if (overWriteCheckExitCode == InstallationQuestion.OverwriteFile)
                    log.Warning("Overwriting files. (--{0} option specified).", Overwrite ? "overwrite" : "force");

                RaiseProgressUpdate(10, "Gathering dependencies.");

                if (!SystemWideOnly)
                {
                    // Sometimes system wide packages depends on non-system wide packages.
                    // when installing system wide packages in a sub-process we dont need to check the dependencies
                    // because that has already been done.

                    bool checkDependencies = !Force || CheckOnly;
                    var installedToCheck =
                        installationPackages.Where(p =>
                            p.IsSystemWide() ==
                            false); // don't use system-wide to check, as that would prevent installing older stuff, if a system-wide package depends on newer versions.
                    var issue = DependencyChecker.CheckDependencies(installedToCheck, packagesToInstall,
                        Force ? LogEventType.Information :
                        checkDependencies ? LogEventType.Error : LogEventType.Warning);

                    if (checkDependencies)
                    {
                        if (issue == DependencyChecker.Issue.BrokenPackages)
                        {
                            log.Info("To fix the package conflict uninstall or update the conflicted packages.");
                            log.Info(
                                "To install packages despite the conflicts, use the --force option. Note that this can break the installation.");
                            return (int)PackageExitCodes.PackageDependencyError;
                        }

                        if (CheckOnly)
                        {
                            log.Info("Check completed with no problems detected.");
                            return (int)ExitCodes.Success;
                        }
                    }
                }

                // System wide packages require elevated privileges. Install them in a separate elevated process.
                var systemWide = packagesToInstall.Where(p => p.IsSystemWide()).ToArray();

                // If we are already running as administrator, skip this and install normally
                if (systemWide.Any() && SubProcessHost.IsAdmin() == false)
                {
                    RaiseProgressUpdate(20, "Installing system-wide packages.");
                    var installStep = new PackageInstallStep()
                    {
                        Packages = systemWide,
                        Repositories = repositories.Select(r => r.Url).ToArray(),
                        Target = PackageDef.SystemWideInstallationDirectory,
                        Force = Force,
                        SystemWideOnly = true
                    };

                    var processRunner = new SubProcessHost
                    {
                        ForwardLogs = true,
                        MutedSources =
                        {
                            "CLI", "Session", "Resolver", "AssemblyFinder", "PluginManager", "TestPlan", "UpdateCheck",
                            "Installation"
                        }
                    };

                    var result = processRunner.Run(installStep, true, cancellationToken);
                    if (result != Verdict.Pass)
                    {
                        var ex = new Exception(
                            $"Failed installing system-wide packages. Try running the command as administrator.");
                        RaiseError(ex);
                    }

                    var pct = ((double)systemWide.Length / systemWide.Length + packagesToInstall.Count) * 100;
                    RaiseProgressUpdate((int)pct, "Installed system-wide packages.");
                    // And remove the system wide packages from the list
                    packagesToInstall = packagesToInstall.Except(p => p.IsSystemWide()).ToList();
                }

                // Download the packages
                // We divide the progress by 2 in the progress update because we assume downloading the packages
                // accounts for half the installation progress. So when all the packages have finished downloading,
                // we have finished 10 + (100/2)% of the installation process.

                var downloadedPackageFiles = PackageActionHelpers.DownloadPackages(
                    PackageCacheHelper.PackageCacheDirectory, packagesToInstall,
                    progressUpdate: (progress, msg) => RaiseProgressUpdate(10 + progress / 2, msg),
                    ignoreCache: NoCache);

                installer.PackagePaths.AddRange(downloadedPackageFiles);
            }
            catch (OperationCanceledException e)
            {
                log.Info(e.Message);
                return (int)ExitCodes.UserCancelled;
            }
            catch (ImageResolveException ex)
            {
                if (Packages != null && Packages.Length > 0)
                {
                    log.Error("Could not resolve {0}{1}", 
                        string.Join(", ", Packages.Select(x => $"{x}")),
                        string.IsNullOrWhiteSpace(Version) ? "" : $" v{Version}");    
                }
                else
                {
                    log.Error("Could not resolve one or more packages.");
                }

                if (ex.Image != null)
                {
                    log.Info("Image configuration:");
                    foreach (var req in ex.Image.Packages)
                    {
                        log.Info($"  {req.Name}: {req.Version}");
                    }
                }

                var unsatisfiedDependencies = ex.InstalledPackages.Where(x => false == x.Dependencies.All(dep =>
                    ex.InstalledPackages.Any(x2 =>
                        x2.Name == dep.Name && dep.Version.IsSatisfiedBy(x2.Version.AsExactSpecifier())))).ToArray();
                if (unsatisfiedDependencies.Any())
                {
                    log.Warning("This might be because of the following broken packages:");

                    var unsatisfiedStrings = unsatisfiedDependencies.Select(x =>
                    {
                        var missingDeps = x.Dependencies.Where(dep => !ex.InstalledPackages.Any(x2 =>
                            x2.Name == dep.Name && dep.Version.IsSatisfiedBy(x2.Version.AsExactSpecifier())));
                        string missingMsg = string.Join(" and ", missingDeps.Select(x2 => $"{x2.Name}:{x2.Version}"));
                        return $"{x.Name} missing {missingMsg}";
                    });
                    foreach (var str in unsatisfiedStrings)
                    {
                        log.Info("   {0}.", str);
                    }
                }

                log.Debug("{0}", ex.Message);
                return (int)ExitCodes.PackageResolutionError;
            }
            catch (Exception e)
            {
                log.Info("Could not download one or more packages.");
                log.Info(e.Message);
                log.Debug(e);
                RaiseError(e);
                return (int)ExitCodes.NetworkError;
            }

            log.Info("Installing to {0}", Path.GetFullPath(Target));

            // Uninstall old packages before
            UninstallExisting(targetInstallation, installer.PackagePaths, cancellationToken);

            var toInstall = ReorderPackages(installer.PackagePaths);
            installer.PackagePaths.Clear();
            installer.PackagePaths.AddRange(toInstall);

            // Install the package
            installer.InstallThread();

            if (installError)
                return (int)PackageExitCodes.PackageInstallError;

            return 0;
        }

        protected override int LockedExecute(CancellationToken cancellationToken)
        {
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

        private void UninstallExisting(Installation installation, List<string> packagePaths, CancellationToken cancellationToken)
        {
            var installed = installation.GetPackages();

            var packages = packagePaths.Select(PackageDef.FromPackage).Select(x => x.Name).ToHashSet();
            var existingPackages = installed.Where(kvp => packages.Contains(kvp.Name)).Select(x => (x.PackageSource as XmlPackageDefSource)?.PackageDefFilePath).ToList();

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
            [Layout(LayoutMode.FullRow)]
            public string Message { get; private set; }

            public AskAboutInstallingAnyway(string message) => Message = message;

            [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
            [Submit] public InstallationQuestion Response { get; set; } = InstallationQuestion.Cancel;
        }

        internal enum UseBetaQuestion
        {
            [Display("Yes")]
            Beta = 0,
            [Display("No")]
            No = 1,
        }

        class AskAboutPrerelease
        {
            [Browsable(true)]
            [Layout(LayoutMode.FullRow)]
            public string Message { get; private set; }
            public AskAboutPrerelease(string message) => Message = message;
            [Layout(LayoutMode.FloatBottom | LayoutMode.FullRow)]
            [Submit] public UseBetaQuestion Response { get; set; } = UseBetaQuestion.No;
        }

        internal static InstallationQuestion CheckForOverwrittenPackages(IEnumerable<PackageDef> installedPackages,
            IEnumerable<PackageDef> packagesToInstall, bool force, bool interactive = false)
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
                if (interactive)
                {
                    StringBuilder message = new StringBuilder();
                    buildMessage(line => message.AppendLine(line));
                    var question = new AskAboutInstallingAnyway(message.ToString());
                    UserInput.Request(question);
                    return question.Response;
                }

                buildMessage(line => log.Info(line));

                if (force)
                {
                    return InstallationQuestion.OverwriteFile;
                }

                log.Error(
                    "Installing these packages will overwrite existing files. " +
                    "Use --overwrite to overwrite existing files, possibly breaking installed packages.");
                return InstallationQuestion.Cancel;
            }

            return InstallationQuestion.Success;
        }
    }
}
