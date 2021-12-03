//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    /// <summary>
    /// Finds dependencies for specified packages in Package Repositories
    /// </summary>
    public class DependencyResolver
    {
        /// <summary>
        /// List of all the dependencies to the specified packages
        /// </summary>
        public List<PackageDef> Dependencies = new List<PackageDef>();

        /// <summary>
        /// List of the dependencies to the specified packages that are currently not installed
        /// </summary>
        public List<PackageDef> MissingDependencies = new List<PackageDef>();

        /// <summary>
        /// List of the dependencies to the specified packages that could not be found in the package repositories
        /// </summary>
        public List<PackageDependency> UnknownDependencies = new List<PackageDependency>();

        public List<Exception> DependencyIssues = new List<Exception>();

        private List<DependencyTreeNode> Tree = new List<DependencyTreeNode>();

        private TraceSource log = Log.CreateSource("DependencyResolver");
        /// <summary>
        /// Instantiates a new dependency resolver.
        /// </summary>
        /// <param name="packages">The packages to resolve dependencies for.</param>
        /// <param name="tapInstallation">The tap installation containing installed packages.</param>
        /// <param name="repositories">The repositories to use for resolving dependencies</param>
        public DependencyResolver(Installation tapInstallation, IEnumerable<PackageDef> packages, List<IPackageRepository> repositories)
        {
            InstalledPackages = new Dictionary<string, PackageDef>();
            foreach (var pkg in tapInstallation.GetPackages())
                InstalledPackages[pkg.Name] = pkg;

            resolve(repositories, packages);
        }

        internal DependencyResolver(Dictionary<string, PackageDef> installedPackages, IEnumerable<PackageDef> packages, List<IPackageRepository> repositories)
        {
            InstalledPackages = installedPackages;
            resolve(repositories, packages);
        }

        internal DependencyResolver(List<PackageSpecifier> packageSpecifiers, List<IPackageRepository> repositories, CancellationToken cancellationToken)
        {
            CheckCompatibility(packageSpecifiers);
            if (DependencyIssues.Any())
                return;
            InstalledPackages = new Dictionary<string, PackageDef>();
            foreach (var specifier in packageSpecifiers)
                GetDependenciesRecursive(repositories, new PackageDependency(specifier.Name, specifier.Version), ArchitectureHelper.GuessBaseArchitecture, OperatingSystem.Current.Name);

            if (UnknownDependencies.Any())
                DependencyIssues.AddRange(UnknownDependencies.Select(s => new InvalidOperationException($"Unable to find {s.Name} version {s.Version}")));
            Dependencies.AddRange(Tree.Select(s => s.Package));

        }

        private void CheckCompatibility(List<PackageSpecifier> packages)
        {
            var oss = packages.Select(p => p.OS).Distinct().Where(o => string.IsNullOrEmpty(o) == false && o.Contains(",") == false).ToList();
            var openTapPackage = packages.FirstOrDefault(p => p.Name == "OpenTAP");
            string os;
            if (oss.Count != 1)
                os = openTapPackage?.OS ?? OperatingSystem.Current.Name;
            else
                os = oss[0];

            // Check if all package only specify compatible architectures
            var archs = packages.Select(p => p.Architecture).Distinct()
                                                .Where(a => a != CpuArchitecture.Unspecified && a != CpuArchitecture.AnyCPU).ToList();

            CpuArchitecture arch;
            if (archs.Count != 1)
                arch = openTapPackage?.Architecture ?? ArchitectureHelper.GuessBaseArchitecture;
            else
                arch = archs[0];

            // Check if same package is specified in multiple versions:
            var versionAmbiguities = packages.GroupBy(s => s.Name).Where(g => g.Count() > 1);
            foreach (var va in versionAmbiguities)
            {
                var versions = va.SelectValues(p => p.Version).ToList();
                for (int i = 0; i < versions.Count; i++)
                {
                    for (int j = i; j < versions.Count; j++)
                    {
                        if (i == j)
                            continue; // Do not compare versions with themselves
                        if (!versions[i].IsCompatible(versions[j]))
                            DependencyIssues.Add(new InvalidOperationException($"Specified versions of package '{va.Key}' are not compatible: {versions[i]} - {versions[j]}"));
                    }
                }
                var highest = va.OrderByDescending(g => g.Version).FirstOrDefault();
                packages.RemoveAll(s => s.Name == va.Key);
                packages.Add(highest);
            }
        }

        private void resolve(List<IPackageRepository> repositories, IEnumerable<PackageDef> packages)
        {
            Dependencies.AddRange(packages);

            
            foreach(var pkg in packages)
            {
                var node = new DependencyTreeNode(pkg,  new VersionSpecifier(pkg.Version, VersionMatchBehavior.Compatible));
                Tree.Add(node);
                foreach (var dep in pkg.Dependencies)
                {
                    var spec = new PackageSpecifier(dep.Name, dep.Version, pkg.Architecture, pkg.OS);
                    ResolveDependenciesRecursive(repositories, spec, node);
                }
            }
        }

        private void ResolveDependenciesRecursive(List<IPackageRepository> repositories, PackageSpecifier dependency, DependencyTreeNode parent)
        {
            // Can this dependency be satisfied by something that is already in the tree?
            DependencyTreeNode node = Tree.FirstOrDefault(n => dependency.IsCompatible(n.Package));
            if (node != null)
            {
                node.ParentNodes.Add(parent);
                return;
            }

            PackageDef depPkg = GetPackageDefFromInstallation(dependency.Name, dependency.Version);
            if (depPkg == null)
            {
                depPkg = GetPackageDefFromRepo(repositories, dependency.Name, dependency.Version);
                MissingDependencies.Add(depPkg); // Todo: Don't add missing dependencies here, let's calculate that all in the end. The dependency here might be replaced later.
            }
            if (depPkg == null)
            {
                UnknownDependencies.Add(parent.Package.Dependencies.First(d => d.Name == dependency.Name));
                return;
            }
            // We found a new dependency, replace the old which we already determined was not compatible
            if (Tree.FirstOrDefault(p => p.Package.Name == depPkg.Name) is DependencyTreeNode previouslyResolved)
            {
                Tree.RemoveIf(p => p.Package.Name == depPkg.Name);
                if (!previouslyResolved.VersionRequirement.IsCompatible(dependency.Version))
                    DependencyIssues.Add(new InvalidOperationException($"Versions of package '{depPkg.Name}' are not compatible: {depPkg.Version} - {previouslyResolved.PackageDependency.Version}"));
            }
            var newNode = new DependencyTreeNode(depPkg, dependency.Version);
            Tree.Add(newNode);
            foreach (var nextLevelDep in depPkg.Dependencies)
            {
                var spec = new PackageSpecifier(nextLevelDep.Name, nextLevelDep.Version, dependency.Architecture, dependency.OS);
                ResolveDependenciesRecursive(repositories, spec, newNode);
            }
        }

        private Dictionary<string, PackageDef> InstalledPackages;

        PackageDef GetPackageDefFromInstallation(string name, VersionSpecifier version)
        {
            if (name.ToLower().EndsWith(".tappackage"))
                name = Path.GetFileNameWithoutExtension(name);
            if (InstalledPackages.ContainsKey(name))
            {
                PackageDef package = InstalledPackages[name];
                // Check that the installed package is compatible with the required package
                if (version.IsCompatible(package.Version))
                    return package;
            }
            return null;
        }

        private PackageDef GetPackageDefFromRepo(List<IPackageRepository> repositories, string name, VersionSpecifier version)
        {
            if (name.ToLower().EndsWith(".tappackage"))
                name = Path.GetFileNameWithoutExtension(name);

            var specifier = new PackageSpecifier(name, version, CpuArchitecture.Unspecified, OperatingSystem.Current.ToString());
            var packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, specifier, InstalledPackages.Values.ToArray());

            if (packages.Any() == false)
            {
                packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, specifier);
                if (packages.Any())
                {
                    log.Warning($"Unable to find a version of '{name}' package compatible with currently installed packages. Some installed packages may be upgraded.");
                }
            }

            if (!packages.Any())
                return null;
            if (version.Minor is null)
                return packages.OrderByDescending(pkg => pkg.Version).FirstOrDefault(pkg => ArchitectureHelper.PluginsCompatible(pkg.Architecture, ArchitectureHelper.GuessBaseArchitecture));

            if (version.Patch is null)
                return packages.OrderByDescending(pkg => pkg.Version).Where(s => s.Version.Minor == version.Minor).FirstOrDefault(pkg => ArchitectureHelper.PluginsCompatible(pkg.Architecture, ArchitectureHelper.GuessBaseArchitecture));

            return packages.OrderBy(pkg => pkg.Version).FirstOrDefault(pkg => ArchitectureHelper.PluginsCompatible(pkg.Architecture, ArchitectureHelper.GuessBaseArchitecture));
        }
    }

    internal class DependencyTreeNode
    {
        public DependencyTreeNode(PackageDef packageDef, VersionSpecifier versionReq)
        {
            VersionRequirement = versionReq ?? throw new ArgumentNullException(nameof(versionReq));
            Package = packageDef ?? throw new ArgumentNullException(nameof(packageDef));
        }

        /// <summary>
        /// The requirement of the version of this package. This is the highest requirement of the requirements of all the parent nodes
        /// </summary>
        internal VersionSpecifier VersionRequirement { get; set; }
        
        /// <summary>
        /// The package that this node represents (the dependency was resolved to this package)
        /// </summary>
        internal PackageDef Package { get; set; }

        /// <summary>
        /// The parent nodes. These represent the packages that depend on this package.
        /// </summary>
        internal List<DependencyTreeNode> ParentNodes { get; set; } = new List<DependencyTreeNode>();

        /// <summary>
        /// Child nodes of this node. These represents the dependencies of the package.
        /// </summary>
        internal List<DependencyTreeNode> ChildNodes { get; set; } = new List<DependencyTreeNode>();
    }

    static class PackageCompatibilityHelper
    {
        public static bool IsCompatible(this PackageSpecifier spec, IPackageIdentifier pkg)
        {
            return spec.Name == pkg.Name &&
                   spec.Version.IsCompatible(pkg.Version) &&
                   ArchitectureHelper.PluginsCompatible(pkg.Architecture, spec.Architecture); // TODO: Should we check OS?
        }
    }
}
