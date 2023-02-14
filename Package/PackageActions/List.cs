using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

#pragma warning disable 1591 // TODO: Add XML Comments in this file, then remove this
namespace OpenTap.Package
{

    [Display("list", Group: "package", Description: "List locally installed packages and browse the online package repository.")]
    public class PackageListAction : LockingPackageAction
    {
        [CommandLineArgument("repository", Description = CommandLineArgumentRepositoryDescription, ShortName = "r")]
        public string[] Repository { get; set; }

        [CommandLineArgument("no-cache", Description = CommandLineArgumentNoCacheDescription)]
        public bool NoCache { get; set; }

        [CommandLineArgument("all", Description = "List all versions of <package> even if the OS or the CPU architecture\nare not compatible with the current machine.", ShortName = "a")]
        public bool All { get; set; }

        [CommandLineArgument("installed", Description = "Show only installed packages.", ShortName = "i")]
        public bool Installed { get; set; }

        [UnnamedCommandLineArgument("package")]
        public string Name { get; set; }

        [CommandLineArgument("version", Description = CommandLineArgumentVersionDescription)]
        public string Version { get; set; }

        [CommandLineArgument("os", Description = CommandLineArgumentOsDescription)]
        public string OS { get; set; }

        [CommandLineArgument("architecture", Description = CommandLineArgumentArchitectureDescription)]
        public CpuArchitecture Architecture { get; set; }

        public PackageListAction()
        {
            Architecture = ArchitectureHelper.GuessBaseArchitecture;
            OS = null;
        }

        protected override int LockedExecute(CancellationToken cancellationToken)
        {
            // OS was explicitly specified. This is interpreted as: Show only packages compatible with that OS. 
            bool checkOs = OS != null;
            if (OS == null)
            {
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

            if (NoCache) PackageManagerSettings.Current.UseLocalPackageCache = false;
            List<IPackageRepository> repositories = new List<IPackageRepository>();

            if (Installed == false)
            {
                repositories = PackageManagerSettings.Current.GetEnabledRepositories(Repository);
            }

            Name = AutoCorrectPackageNames.Correct(new[] { Name }, repositories)[0];

            if (Target == null)
                Target = FileSystemHelper.GetCurrentInstallationDirectory();
            
            HashSet<PackageDef> installed = new Installation(Target).GetPackages().ToHashSet();
            if (checkOs)
                installed = installed.Where(pkg => pkg.IsOsCompatible(OS)).ToHashSet();

            VersionSpecifier versionSpec = VersionSpecifier.Parse("^");
            if (!String.IsNullOrWhiteSpace(Version))
            {
                versionSpec = VersionSpecifier.Parse(Version);
            }

            if (string.IsNullOrEmpty(Name))
            {
                var packages = installed.ToList();
                packages.AddRange(PackageRepositoryHelpers.GetPackageNameAndVersionFromAllRepos(repositories, new PackageSpecifier("", versionSpec, Architecture, OS)));

                if (Installed)
                    packages = packages.Where(p => installed.Any(i => i.Name == p.Name)).ToList();

                PrintReadable(packages, installed);
            }
            else
            {
                IPackageIdentifier package = installed.FirstOrDefault(p => p.Name == Name);

                if (Installed)
                {
                    if (package is null)
                    {
                        log.Info($"{Name} is not installed");
                        return (int)ExitCodes.ArgumentError;
                    }

                    log.Info(package.Version.ToString());
                    return (int)ExitCodes.Success;
                }


                List<PackageVersion> versions = null;

                if (All)
                {
                    versions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, Name).Distinct().ToList();
                    var versionsCount = versions.Count;
                    if (versionsCount == 0) // No versions
                    {
                        log.Info($"No versions of '{Name}'.");
                        return (int)ExitCodes.Success;
                    }

                    if (Version != null) // Version is specified by user
                        versions = versions.Where(v => versionSpec.IsCompatible(v.Version)).ToList();
                    if(checkOs)
                        versions = versions.Where(v => v.IsOsCompatible(OS)).ToList();

                    if (versions.Any() == false && versionsCount > 0)
                    {
                        log.Info($"Package '{Name}' does not exists with version '{Version}'.");
                        log.Info($"Package '{Name}' exists in {versionsCount} other versions, please specify a different version.");
                        return (int)ExitCodes.Success;
                    }
                }
                else
                {
                    var opentap = new Installation(Target).GetOpenTapPackage();
                    versions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, Name, opentap).Distinct().ToList();

                    versions = versions.Where(s => s.IsPlatformCompatible(Architecture, OS)).ToList();

                    if (versions.Any() == false) // No compatible versions
                    {
                        versions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, Name).ToList();
                        if (versions.Any())
                        {
                            log.Warning($"There are no compatible versions of '{Name}'.");
                            log.Info($"There are {versions.Count} incompatible versions available. Use '--all' to show these.");
                        }
                        else
                            log.Warning($"Package '{Name}' could not be found in any repository.");

                        return (int)ExitCodes.Success;
                    }


                    var allVersion = versions;
                    versions = versions.Where(v => versionSpec.IsCompatible(v.Version)).ToList();
                    if (versions.Any() == false) // No versions that are compatible
                    {
                        if (string.IsNullOrEmpty(Version))
                            log.Warning($"There are no released versions of '{Name}'. Showing pre-releases instead.");
                        else
                            log.Warning($"Package '{Name}' does not exists with version '{Version}'.");

                        var anyPrereleaseSpecifier = new VersionSpecifier(versionSpec.Major, versionSpec.Minor, versionSpec.Patch, versionSpec.PreRelease, versionSpec.BuildMetadata, VersionMatchBehavior.AnyPrerelease | versionSpec.MatchBehavior);
                        versions = allVersion.Where(v => anyPrereleaseSpecifier.IsCompatible(v.Version)).ToList();
                        if (versions.Any())
                            PrintVersionsReadable(package, versions);

                        return (int)ExitCodes.Success;
                    }
                }
                PrintVersionsReadable(package, versions);
            }
            return (int)ExitCodes.Success;
        }

        private void PrintVersionsReadable(IPackageIdentifier package, List<PackageVersion> versions)
        {
            var verLen = versions.Select(p => p.Version?.ToString().Length).Max() ?? 0;
            var arcLen = versions.Select(p => p?.Architecture.ToString().Length).Max() ?? 0;
            var osLen = versions.Select(p => p.OS?.Length).Max() ?? 0;
            foreach (var version in versions.OrderBy(x => x.Version))
            {
                // string interpolate + format complex to add padding.
                log.Info(string.Format($"{{0,-{verLen}}} - {{1,-{arcLen}}} - {{2,-{osLen}}} - {{3}}", version.Version, version.Architecture, version.OS ?? "Unknown", package != null && package.Equals(version) ? "installed" : ""));
            }
        }

        private void PrintReadable(List<PackageDef> packages, HashSet<PackageDef> installed)
        {
            var packageList = packages.GroupBy(p => p.Name).Select(x => x.OrderByDescending(p => p.Version).First()).OrderBy(x => x.Name).ToList();

            if (packageList.Count == 0)
            {
                log.Info("Selected directory has no packages installed.");
                return;
            }

            var nameLen = packageList.Select(p => p.Name?.Length).Max() ?? 0;
            var verLen = packageList.Select(p => p.Version?.ToString().Length).Max() ?? 0;
            verLen = Math.Max(verLen, installed.Select(p => p.Version?.ToString().Length).Max() ?? 0);
            
            foreach (var plugin in packageList)
            {
                var installedPackage = installed.FirstOrDefault(p => p.Name == plugin.Name);
                var latestPackage = packages.Where(p => p.Name == plugin.Name).OrderByDescending(p => p.Version).FirstOrDefault();

                var installedString = installedPackage == null ? "" : " - installed";
                
                if (installedPackage != null && installedPackage.IsSystemWide())
                    installedString += " system-wide";

                // string interpolate + format complex to add padding.
                string logMessage = string.Format($"{{0,-{nameLen}}} - {{1,-{verLen}}}{{2}}", plugin.Name, (installedPackage ?? plugin).Version, installedString);

                if (installedPackage != null && installedPackage?.Version?.CompareTo(latestPackage.Version) < 0)
                    logMessage += $" - update available ({latestPackage.Version})";

                // assuming that all dlls in the package requires has the same or distinct license requirements. 
                // Duplicates are made if one file requires X|Y and the other X|Z or even Y|X.
                var licensesRequiredStrings = plugin.Files.Select(p => p.LicenseRequired).Where(l => string.IsNullOrWhiteSpace(l) == false).Select(l => LicenseBase.FormatFriendly(l)).Distinct();

                var licenses = string.Join(" & ", licensesRequiredStrings);

                if (licenses != "")
                    logMessage += " - requires license " + licenses;

                log.Info(logMessage);
            }
        }

    }

}
