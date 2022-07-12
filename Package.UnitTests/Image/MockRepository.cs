using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace OpenTap.Image.Tests
{
    public class MockRepository : IPackageRepository
    {
        public string Url { get; private set; }
        readonly List<PackageDef> AllPackages;
        internal int ResolveCount = 0;

        private static MockRepository instance;

        /// <summary>
        /// Gets a singleton instance of this class. 
        /// This instance is already registered as a repository in that calling 
        /// PackageRepositoryHelpers.DetermineRepositoryType("mock://localhost")
        /// will return this instance.
        /// </summary>
        public static MockRepository Instance {
            get {
                if(instance == null)
                {
                    instance = new MockRepository("mock://localhost");
                    PackageRepositoryHelpers.RegisterRepository(instance);
                }
                return instance;
            }
        }

        /// <summary>
        /// Creates an specifier that has the mock repo in its list of repositories
        /// </summary>
        public static ImageSpecifier CreateSpecifier()
        {
            return new ImageSpecifier()
            {
                Repositories = new List<string>() { Instance.Url }
            };
        }


        public MockRepository(string url)
        {
            Url = url;
            AllPackages = new List<PackageDef>
                {
                    DefinePackage("OpenTAP","8.8.0"),
                    DefinePackage("OpenTAP","9.10.0"),
                    DefinePackage("OpenTAP","9.10.1"),
                    DefinePackage("OpenTAP","9.11.0"),
                    DefinePackage("OpenTAP","9.11.1"),
                    DefinePackage("OpenTAP","9.12.0"),
                    DefinePackage("OpenTAP","9.12.1"),
                    DefinePackage("OpenTAP","9.13.0"),
                    DefinePackage("OpenTAP","9.13.1"),
                    DefinePackage("OpenTAP","9.13.2-beta.1"),
                    DefinePackage("OpenTAP","9.13.2"),
                    DefinePackage("OpenTAP","9.14.0"),
                    DefinePackage("OpenTAP","9.15.2+39e6c2a2"),
                    DefinePackage("OpenTAP","9.16.0"),
                    DefinePackage("OSIntegration","1.4.0+c70929ac", CpuArchitecture.AnyCPU, "windows", ("OpenTAP", "^9.5.0+45ab79bc"))
                        .WithPackageAction(new ActionStep { ActionName = "uninstall", ExeFile = "tap", Arguments = "package list -i" }),
                    DefinePackage("SDK","9.16.0", CpuArchitecture.AnyCPU, "windows,linux,macos", ("OpenTAP", "any")),
                    DefinePackage("OpenTAP", "9.17.0-rc.4", CpuArchitecture.x64, "windows,linux"),
                    DefinePackage("TUI", "0.1.0-beta.124+b6a04994", CpuArchitecture.AnyCPU, "windows,linux,macos", ("OpenTAP", "^9.12.0+78ddca2e")),
                    DefinePackage("Keg", "0.1.0-beta.17+cd0310b9", CpuArchitecture.AnyCPU, "windows,linux", ("OpenTAP", "^9.16.0-rc.1+2b4975a7")),
                    DefinePackage("License Injector", "9.8.0-beta.5+6fce512f", CpuArchitecture.AnyCPU, "windows,linux", ("OpenTAP", "^9.16.4+654f0b6b"), ("Keg", "^0.1.0-beta.17+cd0310b9")),
                    DefinePackage("REST-API", "2.6.3+4b18b59f", CpuArchitecture.AnyCPU, "windows,linux", ("OpenTAP", "^9.16.0+6cab4b01"), ("Keysight Floating Licensing", "^1.0")),
                    DefinePackage("REST-API", "2.5.0+9aa081fd", CpuArchitecture.AnyCPU, "windows,linux", ("OpenTAP", "^9.16.0+6cab4b01"), ("Keysight Floating Licensing", "^1.0")),
                    DefinePackage("Keysight Floating Licensing", "1.0.44+8197912e", CpuArchitecture.AnyCPU, "windows,linux", ("OpenTAP", "any")),
                    DefinePackage("Keysight Floating Licensing", "1.4.1+e5816333", CpuArchitecture.AnyCPU, "windows,linux"),
                    DefinePackage("Python", "2.2.0+31a47a25", CpuArchitecture.AnyCPU, "windows,linux", ("OpenTAP", "^9.6.0+2428dfca")),
                    DefinePackage("Python", "2.3.0+c6e8a47e", CpuArchitecture.AnyCPU, "windows,linux", ("OpenTAP", "^9.15.2+39e6c2a2")),
                    DefinePackage("Python", "2.3.1+945a9e89", CpuArchitecture.AnyCPU, "windows,linux", ("OpenTAP", "^9.15.2+39e6c2a2")),
                    DefinePackage("Demonstration",  "9.0.0", CpuArchitecture.AnyCPU, "windows", ("OpenTAP", "^9.9.0")),
                    DefinePackage("Demonstration",  "9.0.1", CpuArchitecture.AnyCPU, "windows", ("OpenTAP", "^9.10.0")),
                    DefinePackage("Demonstration",  "9.0.2", CpuArchitecture.AnyCPU, "windows", ("OpenTAP", "^9.11.0")),
                    DefinePackage("Demonstration",  "9.0.5+3cab80c8", CpuArchitecture.AnyCPU, "windows,linux", ("OpenTAP", "^9.5.0+45ab79bc")),
                    DefinePackage("Demonstration",  "9.1.0", CpuArchitecture.AnyCPU, "windows", ("OpenTAP", "^9.12.0")),
                    DefinePackage("Demonstration",  "9.2.0", CpuArchitecture.AnyCPU, "windows", ("OpenTAP", "^9.12.0")),
                    DefinePackage("MyDemoTestPlan", "1.0.0", CpuArchitecture.AnyCPU, "windows", ("OpenTAP", "^9.12.1"), ("Demonstration", "^9.0.2")),
                    DefinePackage("MyDemoTestPlan", "1.1.0", CpuArchitecture.AnyCPU, "windows", ("OpenTAP", "^9.13.1"), ("Demonstration", "^9.0.2")),
                    DefinePackage("PackageWithMissingDependency","1.0.0", CpuArchitecture.AnyCPU, "windows", ("MissingPackage", "9.13.1")),
                    DefinePackage("ExactDependency","1.0.0", CpuArchitecture.AnyCPU, "windows", ("OpenTAP", "9.13.1")),
                    DefinePackage("Cyclic",         "1.0.0", CpuArchitecture.AnyCPU, "windows", ("Cyclic2", "1.0.0")),
                    DefinePackage("Cyclic2",        "1.0.0", CpuArchitecture.AnyCPU, "windows", ("Cyclic", "1.0.0")),
                    DefinePackage("Native",         "1.0.0", CpuArchitecture.x86,    "windows"),
                    DefinePackage("Native",         "1.0.0", CpuArchitecture.x64,    "windows"),
                    DefinePackage("Native",         "1.0.0", CpuArchitecture.x86,    "linux"),
                    DefinePackage("Native",         "1.0.0", CpuArchitecture.x64,    "linux"),
                    DefinePackage("Native2",        "1.0.0", CpuArchitecture.x86,    "windows"),
                    DefinePackage("Native2",        "1.0.0", CpuArchitecture.x64,    "windows"),
                    DefinePackage("Native2",        "1.0.0", CpuArchitecture.x86,    "linux"),
                    DefinePackage("Native2",        "1.0.0", CpuArchitecture.x64,    "linux"),
                    DefinePackage("A","1.0.0"),
                    DefinePackage("A","1.0.1"),
                    DefinePackage("A","1.1.1"),
                    DefinePackage("B",        "1.0.0", CpuArchitecture.x86,    "linux"),
                    DefinePackage("B",        "1.1.0", CpuArchitecture.x86,    "linux"),
                    DefinePackage("B",        "1.1.1", CpuArchitecture.x86,    "linux"),
                    DefinePackage("B",        "2.0.0", CpuArchitecture.x86,    "linux"),
                    DefinePackage("B",        "2.1.0", CpuArchitecture.x86,    "linux"),
                    DefinePackage("B",        "2.1.0-beta.1", CpuArchitecture.x86,    "linux"),
                    DefinePackage("B",        "2.1.1", CpuArchitecture.x86,    "linux"),
                    DefinePackage("B",        "3.0.0-beta.1", CpuArchitecture.x64,    "Windows"),
                    DefinePackage("B",        "3.0.0", CpuArchitecture.x64,    "Windows"),
                    DefinePackage("B",        "3.0.1", CpuArchitecture.x64,    "Windows"),
                    DefinePackage("B",        "3.0.2", CpuArchitecture.x64,    "Windows"),
                    DefinePackage("B",        "3.1.0", CpuArchitecture.x64,    "Windows"),
                    DefinePackage("B",        "3.1.1", CpuArchitecture.x64,    "Windows"),
                    DefinePackage("B",        "3.2.1-beta.1", CpuArchitecture.x64,    "Windows"),
                    DefinePackage("B",        "3.2.2-alpha.1", CpuArchitecture.x64,    "Windows"),
                    DefinePackage("C",        "1.0.0-beta.1", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("C",        "1.0.0-rc.1", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("C",        "2.0.0-alpha.1", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("C",        "2.0.0-beta.1", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("C",        "2.0.0-rc.1", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("C",        "2.0.0", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("D",        "2.0.0", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("D",        "2.1.0-rc.1", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("E",        "2.1.0-beta.1", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("E",        "2.1.0-beta.2", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("E",        "2.2.0-alpha.2.1", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("E",        "2.2.0-alpha.2.2", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("F",        "1.1.0", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("F",        "1.1.1", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("F",        "1.2.0", CpuArchitecture.x86,    "Linux"),
                    DefinePackage("G",        "2.0.0-rc.1"),
                    DefinePackage("G",        "2.1.0-beta.1"),
                    DefinePackage("G",        "2.1.0-beta.2"),
                    DefinePackage("G",        "2.2.0-alpha.2.1"),
                    DefinePackage("G",        "2.2.0-alpha.2.2"),
                    DefinePackage("G",        "2.3.0-rc.1"),
                };
        }

        private PackageDef DefinePackage(string name, string version, CpuArchitecture arch = CpuArchitecture.AnyCPU, string os = "windows", params (string name, string version)[] dependencies)
        {
            return new PackageDef
            {
                Name = name,
                Version = SemanticVersion.Parse(version),
                Architecture = arch,
                OS = os,
                Dependencies = dependencies.Select(d => new PackageDependency(d.name, VersionSpecifier.Parse(d.version))).ToList(),
                PackageSource = new HttpRepositoryPackageDefSource()
                {
                    RepositoryUrl = this.Url
                },
            };
        }

        public PackageDef[] CheckForUpdates(IPackageIdentifier[] packages, CancellationToken cancellationToken)
        {
            return Array.Empty<PackageDef>();
        }

        public void DownloadPackage(IPackageIdentifier package, string destination, CancellationToken cancellationToken)
        {
            if (!(package is PackageDef def))
                throw new Exception($"'{package}' is not from this mock repository.");

            using (var fs = File.OpenWrite(destination))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var fileEntry = zip.CreateEntry($"Packages/{package.Name}/package.xml");
                using (var s = fileEntry.Open())
                    def.SaveTo(s);
            }
        }

        public string[] GetPackageNames(CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
            ResolveCount++;
            return AllPackages.Select(p => p.Name).Distinct().ToArray();
        }
        public PackageDef[] GetPackages(PackageSpecifier package, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
            ResolveCount++;
            var list = AllPackages.Where(p => p.Name == package.Name)
                              .GroupBy(p => p.Version)
                              .OrderByDescending(g => g.Key).ToList();
            return list.FirstOrDefault(g => package.Version.IsCompatible(g.Key))?.ToArray() ?? Array.Empty<PackageDef>();
        }

        public PackageVersion[] GetPackageVersions(string packageName, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith)
        {
            ResolveCount++;
            return AllPackages.Where(p => p.Name == packageName)
                              .Select(p => new PackageVersion(p.Name, p.Version, p.OS, p.Architecture, p.Date, new List<string>()))
                              .OrderByDescending(p => p.Version)
                              .ToArray();
        }
    }

}
