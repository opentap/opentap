using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpenTap.Package
{

    [Display("list", Group: "package", Description: "List installed packages.")]
    public class PackageListAction : LockingPackageAction
    {
        [CommandLineArgument("repository", Description = CommandLineArgumentRepositoryDescription, ShortName = "r")]
        public string[] Repository { get; set; }

        [CommandLineArgument("all", Description = "List all versions of a package when using the <Name> argument.", ShortName = "a")]
        public bool All { get; set; }

        [CommandLineArgument("installed", Description = "Only show packages that are installed.", ShortName = "i")]
        public bool Installed { get; set; }

        [UnnamedCommandLineArgument("Name")]
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
            if (OS == null)
            {
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

            List<IPackageRepository> repositories = new List<IPackageRepository>();

            if (Installed == false)
            {
                if (Repository == null)
                    repositories.AddRange(PackageManagerSettings.Current.Repositories.Where(p => p.IsEnabled).Select(s => s.Manager));
                else 
                    repositories.AddRange(Repository.Select(s => PackageRepositoryHelpers.DetermineRepositoryType(s)));
            }

            if (Target == null)
                Target = FileSystemHelper.GetCurrentInstallationDirectory();

            HashSet<PackageDef> installed = new Installation(Target).GetPackages().ToHashSet();


            VersionSpecifier versionSpec = VersionSpecifier.Any;
            if (!String.IsNullOrWhiteSpace(Version))
            {
                versionSpec = VersionSpecifier.Parse(Version);
            }

            if (string.IsNullOrEmpty(Name))
            {
                var packages = installed.ToList();
                packages.AddRange(PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, new PackageSpecifier("", versionSpec, Architecture, OS)));

                if (Installed)
                    packages = packages.Where(p => installed.Any(i => i.Name == p.Name)).ToList();

                PrintReadable(packages, installed);
            }
            else
            {
                IPackageIdentifier package = installed.FirstOrDefault(p => p.Name == Name);
                List<PackageVersion> versions = null;

                if (All)
                {
                    log.Info($"All available versions of '{Name}':\n");
                    versions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, Name);
                    var versionsCount = versions.Count;
                    if (versionsCount == 0) // No versions
                    {
                        log.Info($"No versions of '{Name}'.");
                        return 0;
                    }

                    if (Version != null) // Version is specified by user
                        versions = versions.Where(v => versionSpec.IsCompatible(v.Version)).ToList();

                    if (versions.Any() == false && versionsCount > 0)
                    {
                        log.Info($"Package '{Name}' does not exists with version '{Version}'.");
                        log.Info($"Package '{Name}' exists in {versionsCount} other versions, please specify a different version.");
                    }
                    else
                        PrintVersionsReadable(package, versions);
                }
                else
                {
                    var opentap = new Installation(Target).GetOpenTapPackage();
                    versions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, Name, opentap);

                    if (versions.Any() == false) // No compatible versions
                    {
                        log.Warning($"There are no compatible versions of '{Name}'.");
                        versions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, Name).ToList();
                        if (versions.Any())
                            log.Info($"There are {versions.Count} incompatible versions available. Use '--all' to show these.");

                        return 0;
                    }

                    versions = versions.Where(v => versionSpec.IsCompatible(v.Version)).ToList(); // Filter compatible versions
                    if (versions.Any() == false) // No versions that are compatible
                    {
                        if (string.IsNullOrEmpty(Version))
                            log.Warning($"There are no released versions of '{Name}'.");
                        else
                            log.Warning($"Package '{Name}' does not exists with version '{Version}'.");

                        if (versions.Any())
                            log.Info($"There are {versions.Count} pre-released versions available. Use '--version <pre-release>' (e.g. '--version rc') or '--all' to show these.");

                        return 0;
                    }

                    PrintVersionsReadable(package, versions);
                }
            }

            return 0;
        }

        private void PrintVersionsReadable(IPackageIdentifier package, List<PackageVersion> versions)
        {
            var verLen = versions.Select(p => p.Version?.ToString().Length).Max();
            var arcLen = versions.Select(p => p?.Architecture.ToString().Length).Max();
            var osLen = versions.Select(p => p.OS?.Length).Max();
            foreach (var version in versions)
            {
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

            var nameLen = packageList.Select(p => p.Name?.Length).Max();
            var verLen = packageList.Select(p => p.Version?.ToString().Length).Max() ?? 0;
            verLen = Math.Max(verLen, installed.Select(p => p.Version?.ToString().Length).Max() ?? 0);
            
            foreach (var plugin in packageList)
            {
                var installedPackage = installed.FirstOrDefault(p => p.Name == plugin.Name);
                var latestPackage = packages.Where(p => p.Name == plugin.Name).OrderByDescending(p => p.Version).FirstOrDefault();

                string logMessage = string.Format(string.Format("{{0,-{0}}} - {{1,-{1}}} - {{2}}", nameLen, verLen), plugin.Name, (installedPackage ?? plugin).Version, installedPackage != null ? "installed" : "");

                if (installedPackage != null && installedPackage?.Version?.CompareTo(latestPackage.Version) < 0)
                    logMessage += " - update available";

                // assuming that all dlls in the package requires has the same or distinct license requirements. 
                // Duplicates are made if one file requires X|Y and the other X|Z or even Y|X.
                var licensesRequiredStrings = plugin.Files.Select(p => p.LicenseRequired).Where(l => string.IsNullOrWhiteSpace(l) == false).Select(LicenseBase.FormatFriendly).Distinct();

                var licenses = string.Join(" & ", licensesRequiredStrings);

                if (licenses != "")
                    logMessage += " - requires license " + licenses;

                log.Info(logMessage);
            }
        }

    }

}
