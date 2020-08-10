using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

#pragma warning disable 1591 // TODO: Add XML Comments in this file, then remove this
namespace OpenTap.Package
{
    
    [Display("show", Group: "package", Description: "Show information about a package.")]
    public class PackageShowAction : IsolatedPackageAction
    {
        private string Description { get; set; }
        private List<Tuple<string, string>> SubTags { get; } = new List<Tuple<string, string>>();
        private void ParseDescription(string description)
        {
            var doc = new XmlDocument();
            doc.LoadXml($"<Description>{description}</Description>");

            foreach (object tag in doc["Description"])
            {
                if (tag is XmlText t)
                {
                    Description = t.InnerText.Trim();
                }
                else if (tag is XmlElement e)
                {
                    var text = e.InnerText.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                        SubTags.Add(new Tuple<string, string>(e.Name, text));
                }
            }
        }
        
        private void AddWritePair(string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                keys.Add(key);
                values.Add(value);
            }
        }

        private void WritePairs()
        {
            var justify = keys.Max(x => x.Length) + 1;
            int consoleWidth;
            try
            {
                consoleWidth = Console.WindowWidth;
            }
            catch // No handle to a console window -- default to something reasonable
            {
                consoleWidth = 120; 
            }

            if (consoleWidth < 10) // assume an error and just use 120 as default.
                consoleWidth = 120;
            var wrapLength = Math.Min(consoleWidth - justify - 3, 100);
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i].PadRight(justify);
                var value = string.Join(Environment.NewLine.PadRight(justify + 2 + Environment.NewLine.Length), WordWrap(values[i], wrapLength).Where(x => !string.IsNullOrWhiteSpace(x)));
                log.Info($"{key}: {value}");
            }
        }
        
        private List<string> values = new List<string>();
        private List<string> keys = new List<string>();
        
        [CommandLineArgument("repository", Description = CommandLineArgumentRepositoryDescription, ShortName = "r")]
        public string[] Repository { get; set; }
        
        [CommandLineArgument("offline", Description = "Don't check http repositories.", ShortName = "o")]
        public bool Offline { get; set; }
        
        [UnnamedCommandLineArgument("Name", Required = true)] 
        public string Name { get; set; }

        [CommandLineArgument("version", Description = CommandLineArgumentVersionDescription)]
        public string Version { get; set; }

        [CommandLineArgument("os", Description = CommandLineArgumentOsDescription)]
        public string OS { get; set; }

        [CommandLineArgument("architecture", Description = CommandLineArgumentArchitectureDescription)]
        public CpuArchitecture Architecture { get; set; }

        [CommandLineArgument("include-files", Description = "List all files included in the package")]
        public bool IncludeFiles { get; set; } = false;
        
        [CommandLineArgument("include-plugins", Description = "List all plugins included in the package")]
        public bool IncludePlugins { get; set; } = false;
        
        private List<IPackageRepository> repositories = new List<IPackageRepository>();
        
        private VersionSpecifier versionSpec { get; set; }
        public PackageShowAction()
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

        private void DisableHttpRepositories()
        {
            repositories = new List<IPackageRepository>(repositories.OfType<FilePackageRepository>().ToList());
        }

        private PackageDef GetPackageDef(Installation targetInstallation)
        {
            // Try a number of methods to obtain the PackageDef in order of precedence
            var packageRef = new PackageSpecifier(Name, VersionSpecifier.Parse(Version ?? ""), Architecture, OS);
            PackageDef package;
            // a relative or absolute file path
            if (File.Exists(Name))
            {
                package = PackageDef.FromPackage(Name);

                if (package != null)
                {
                    Offline = true;
                    return package;
                }
            }
            
            // a currently installed package
            package = targetInstallation.GetPackages().FirstOrDefault(p => p.Name == Name && versionSpec.IsCompatible(p.Version));
            if (package != null)
                return package;

            if (Offline == false)
            {
                try
                {
                    // a release from repositories
                    package = repositories.SelectMany(x => x.GetPackages(packageRef))
                        .FindMax(p => p.Version);
                    if (package != null)
                        return package;
                }
                catch (System.Net.WebException e)
                {
                    // not connected to the internet
                    log.Error(e.Message);
                    log.Warning("Could not connect to repository. Showing results for local install");
                    DisableHttpRepositories();

                    package = repositories.SelectMany(x => x.GetPackages(packageRef))
                        .FindMax(p => p.Version);
                    if (package != null)
                        return package;
                }
            }

            if (!string.IsNullOrWhiteSpace(Version))
            {
                log.Warning($"{Name} version {Version} not found.");
            }

            if (Offline == false && string.IsNullOrWhiteSpace(Version))
            {
                // a prerelease from repositories
                packageRef = new PackageSpecifier(Name, VersionSpecifier.Parse("any"), Architecture, OS);
                package = repositories.SelectMany(x => x.GetPackages(packageRef))
                    .FindMax(p => p.Version);
            }

            return package;
        }
        protected override int LockedExecute(CancellationToken cancellationToken)
        {            
            repositories.AddRange(Repository == null
                ? PackageManagerSettings.Current.Repositories.Where(p => p.IsEnabled).Select(s => s.Manager)
                : Repository.Select(PackageRepositoryHelpers.DetermineRepositoryType));

            if (Target == null)
                Target = FileSystemHelper.GetCurrentInstallationDirectory();

            versionSpec = VersionSpecifier.Any;
            if (!String.IsNullOrWhiteSpace(Version))
            {
                versionSpec = VersionSpecifier.Parse(Version);
            }            
            
            var targetInstallation = new Installation(Target);

            try
            {
                PackageDef package = GetPackageDef(targetInstallation);
                if (package == null)
                    throw new Exception(
                        $"Package {Name} version {Version} not found in specified repositories or local install.");
                
                var ct = new CancellationTokenSource();

                var packageVersions = Offline
                    ? new List<PackageVersion>()
                    : repositories.SelectMany(repo => repo.GetPackageVersions(package.Name, ct.Token, null))
                        .Where(p => versionSpec.IsCompatible(p.Version));
                // Remove prereleases if found package is a release
                if (string.IsNullOrWhiteSpace(package.Version.PreRelease))
                    packageVersions = packageVersions.Where(p => string.IsNullOrWhiteSpace(p.Version.PreRelease));

                GetPackageInfo(package, packageVersions.ToList(), targetInstallation);
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                return 1;
            }

            return 0;
        }

        private void GetPackageInfo(PackageDef package, List<PackageVersion> packageVersions,
            Installation installation)
        {
            var allPackages = installation.GetPackages();
            var latestVersion = packageVersions?.Max(p => p.Version); 
            var packageInstalled = allPackages.Contains(package);
            if (!packageInstalled)
                allPackages.Add(package);

            var tree = DependencyAnalyzer.BuildAnalyzerContext(allPackages);
            var issues = tree.GetIssues(package);

            ParseDescription(package.Description);
            
            // Get similar releases to get available platforms and architectures
            var similarReleases = packageVersions.Where(x =>
                x.Version.Major == package.Version.Major && x.Version.Minor == package.Version.Minor).ToList();

            AddWritePair("Package Name", package.Name);
                            
            AddWritePair("Group", package.Group);

            var installedVersion = installation.GetPackages().Where(x => x.Name == package.Name)?.FirstOrDefault()?.Version;
            var installedString = installedVersion == null ? "(not installed)" : $"({installedVersion} installed)";
            AddWritePair("Version", $"{package.Version} {installedString}");
            
            if (latestVersion != null && installedVersion != null && latestVersion != installedVersion)
                AddWritePair("Newest Version in Repository", $"{latestVersion}");
            
            AddWritePair("Compatible Architectures", string.Join(Environment.NewLine, similarReleases.Select(x => x.Architecture).Distinct()));
            AddWritePair("Compatible Platforms", string.Join(Environment.NewLine, similarReleases.Select(x => x.OS).Distinct()));

            if (packageInstalled == false)
            {
                AddWritePair("Compatibility Issues", issues.Any()
                    ? $"{(string.Join(Environment.NewLine, issues.Select(x => $"{x.PackageName} - {x.IssueType.ToString()}")))}"
                    : "none");
            }

            AddWritePair("Owner", package.Owner);
            var licenseString = (package.LicenseRequired ?? "").Replace("&", " and ").Replace("|", " or ");
            AddWritePair("License Required", licenseString);
            AddWritePair("SourceUrl", package.SourceUrl);
            if (package.PackageSource is IRepositoryPackageDefSource repoPkg)
                AddWritePair("Repository", repoPkg.RepositoryUrl);
            AddWritePair("Package Type", package.FileType);
            AddWritePair("Package Class", package.Class);

            var tags = package.Tags?.Split(new string[]{ " ", "," }, StringSplitOptions.RemoveEmptyEntries);
            if (tags?.Length > 0)
                AddWritePair("Package Tags", string.Join(" ", tags));

            if (package.Dependencies.Count > 0)
            {
                AddWritePair("Dependencies", string.Join(Environment.NewLine, package.Dependencies.Select(x => x.Name)));
            }
            
            foreach (var (key, value) in SubTags)
            {
                AddWritePair(key, value);
            }
            
            AddWritePair("Description", Description);

            if (IncludeFiles)
            {
                if (package.Files.Count > 0)
                {
                    AddWritePair("Files", string.Join(Environment.NewLine, package.Files.Select(x => x.RelativeDestinationPath.Replace("\\", "/"))));
                }                
            }

            if (IncludePlugins)
            {
                var plugins = package.Files.Select(x => x.Plugins);
                
                var sb = new StringBuilder();
                foreach (var plugin in plugins)
                {
                    foreach (var p in plugin)
                    {
                        var name = p.Name;
                        var desc = p.Description;
                        sb.Append(!string.IsNullOrWhiteSpace(desc) ? $"{name} ({desc}){Environment.NewLine}" : $"{name}{Environment.NewLine}");
                    }
                }
                AddWritePair("Plugins", sb.ToString());
            }

            WritePairs();
        }
        private IEnumerable<string> WordWrap(string input, int breakLength)
        {
            // In addition to whitespace, break on these characters
            var breakableCharacters = new List<char>() {'-', ' ', '\t', '\n'};
            
            var progress = 0;
            var currentLength = 0;
            var lastBreakableCharacter = -1;
            
            foreach (var c in input)
            {
                currentLength++;

                if (breakableCharacters.Contains(c))
                    lastBreakableCharacter = currentLength;
                
                // Break if line length exceeds break length, or if the sentence contains a literal newline
                if (currentLength > breakLength || c == '\n')
                {
                    // Keep reading until we can break
                    if (lastBreakableCharacter == -1)
                        continue;
                    
                    yield return input.Substring(progress, lastBreakableCharacter).Trim();
                    
                    progress += lastBreakableCharacter;
                    currentLength -= lastBreakableCharacter;
                    lastBreakableCharacter = -1;
                }
            }
            yield return input.Substring(progress).Trim();
        }
    }
}
