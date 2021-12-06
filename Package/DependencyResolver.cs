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

        //private List<DependencyTreeNode> Tree = new List<DependencyTreeNode>();
        private DependencyTree Tree = new DependencyTree();
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
            Finish();
        }

        internal DependencyResolver(Dictionary<string, PackageDef> installedPackages, IEnumerable<PackageDef> packages, List<IPackageRepository> repositories)
        {
            InstalledPackages = installedPackages;
            resolve(repositories, packages);
            Finish();
        }

        internal DependencyResolver(List<PackageSpecifier> packageSpecifiers, List<IPackageRepository> repositories, CancellationToken cancellationToken)
        {
            var alignedSpecifiers = CheckCompatibility(packageSpecifiers);
            if (DependencyIssues.Any())
                return;
            InstalledPackages = new Dictionary<string, PackageDef>();
            foreach (var specifier in alignedSpecifiers)
                ResolveDependenciesRecursive(repositories, specifier, Tree);

            Finish();
        }


        private void Finish()
        {
            // Populate UnknownDependencies, Dependencies & DependencyIssues
            var versionAmbiguities = Tree.AllNodes.GroupBy(s => s.Package.Name).Where(g => g.Count() > 1);
            foreach (var va in versionAmbiguities)
            {
                var versions = va.SelectValues(p => p.VersionRequirement).ToList();
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
                var highest = va.OrderByDescending(g => g.VersionRequirement).FirstOrDefault();
                Dependencies.Add(highest.Package);
            }
            var rest = Tree.AllNodes.Where(s => !Dependencies.Any(p => p.Name == s.Package.Name)).Select(s => s.Package);
            if (rest.Any())
                Dependencies.AddRange(rest);

            if (UnknownDependencies.Any())
                DependencyIssues.AddRange(UnknownDependencies.Select(s => new InvalidOperationException($"Unable to find {s.Name} version {s.Version}")));

            MissingDependencies = Dependencies.Where(s => !InstalledPackages.Any(p => p.Key == s.Name)).ToList();
        }

        /// <summary>
        /// Checks package version, architecture and OS specifications for compatibility, and returns a new distilled set of compatible specifications if possible.
        /// </summary>
        /// <param name="packages"></param>
        private IEnumerable<PackageSpecifier> CheckCompatibility(IEnumerable<PackageSpecifier> packages)
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

            Dictionary<string, PackageSpecifier> selectedSpecifiers = new Dictionary<string, PackageSpecifier>();
            foreach (var p in packages)
            {
                if (selectedSpecifiers.TryGetValue(p.Name, out var s))
                {
                    if (p.Version.IsSatisfiedBy(s.Version))
                    {
                        // the already selected package can be used in place of this
                        continue;
                    }
                    else if (s.Version.IsSatisfiedBy(p.Version))
                    {
                        // this package can satisfy the already selected specification, update to use this instead.
                        selectedSpecifiers[p.Name] = new PackageSpecifier(p.Name, p.Version, arch, os);
                    }
                    else
                        DependencyIssues.Add(new InvalidOperationException($"Specified versions of package '{p.Name}' are not compatible: {p.Version} - {s.Version}"));
                }
                else
                {
                    selectedSpecifiers.Add(p.Name, new PackageSpecifier(p.Name, p.Version, arch, os));
                }
            }
            return selectedSpecifiers.Values;
        }

        private void resolve(List<IPackageRepository> repositories, IEnumerable<PackageDef> packages)
        {
            Dependencies.AddRange(packages);


            foreach (var pkg in packages)
            {
                var node = new DependencyTreeNode(pkg, new VersionSpecifier(pkg.Version, VersionMatchBehavior.Compatible));
                Tree.ChildNodes.Add(node);
                foreach (var dep in pkg.Dependencies)
                {
                    var spec = new PackageSpecifier(dep.Name, dep.Version, pkg.Architecture, pkg.OS);
                    ResolveDependenciesRecursive(repositories, spec, node);
                }
            }
        }

        private void ResolveDependenciesRecursive(List<IPackageRepository> repositories, PackageSpecifier dependency, DependencyTree parent)
        {
            // Can this dependency be satisfied by something that is already in the tree?
            DependencyTreeNode node = Tree.AllNodes.FirstOrDefault(n => dependency.IsCompatible(n.Package));
            if (node != null)
            {
                node.ParentNodes.Add(parent);
                return;
            }

            PackageDef depPkg = GetPackageDefFromInstallation(dependency.Name, dependency.Version);
            if (depPkg == null)
            {
                depPkg = GetPackageDefFromRepo(repositories, dependency);
                //MissingDependencies.Add(depPkg); // Todo: Don't add missing dependencies here, let's calculate that all in the end. The dependency here might be replaced later.
            }
            if (depPkg == null)
            {
                UnknownDependencies.Add(new PackageDependency(dependency.Name, dependency.Version));
                return;
            }
            // We found a new dependency, replace the old which we already determined was not compatible
            //if (Tree.FirstOrDefault(p => p.Package.Name == depPkg.Name) is DependencyTreeNode previouslyResolved)
            //{
            //    Tree.RemoveIf(p => p.Package.Name == depPkg.Name);
            //    if (!previouslyResolved.VersionRequirement.IsCompatible(dependency.Version))
            //        DependencyIssues.Add(new InvalidOperationException($"Versions of package '{depPkg.Name}' are not compatible: {depPkg.Version} - {previouslyResolved.PackageDependency.Version}"));
            //}
            var newNode = new DependencyTreeNode(depPkg, dependency.Version);
            parent.ChildNodes.Add(newNode);
            Tree.AllNodes.Add(newNode);
            foreach (var nextLevelDep in depPkg.Dependencies)
            {
                var spec = new PackageSpecifier(nextLevelDep.Name, nextLevelDep.Version, dependency.Architecture, dependency.OS);
                ResolveDependenciesRecursive(repositories, spec, newNode);
            }
        }

        private List<DependencyTreeNode> FlattenHierarchy(DependencyTree tree)
        {
            List<DependencyTreeNode> nodes = new List<DependencyTreeNode>();
            AddNodes(tree.ChildNodes, nodes);
            return nodes;
        }

        private static void AddNodes(List<DependencyTreeNode> tree, List<DependencyTreeNode> nodes)
        {
            foreach (DependencyTreeNode node in tree)
            {
                if (!nodes.Contains(node))
                {
                    nodes.Add(node);
                    AddNodes(tree, node.ChildNodes);
                }
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

        private PackageDef GetPackageDefFromRepo(List<IPackageRepository> repositories, PackageSpecifier spec)
        {
            var name = spec.Name;
            if (name.ToLower().EndsWith(".tappackage"))
                name = Path.GetFileNameWithoutExtension(name);

            //var specifier = new PackageSpecifier(name, version, CpuArchitecture.Unspecified, OperatingSystem.Current.ToString());
            IEnumerable<PackageDef> packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, spec, InstalledPackages.Values.ToArray());

            if (packages.Any() == false)
            {
                packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, spec);
                if (packages.Any())
                {
                    log.Warning($"Unable to find a version of '{name}' package compatible with currently installed packages. Some installed packages may be upgraded.");
                }
            }

            if (!packages.Any())
                return null;

            // if the version is not fully specified, or it is a compatible version, don't rely on
            // the repo to pick the correct version, instead ask for all versions and pick one here
            if (spec.Version.MatchBehavior.HasFlag(VersionMatchBehavior.Compatible) || spec.Version.Minor is null || spec.Version.Patch is null)
            {
                var allVersions =PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, spec.Name, InstalledPackages.Values.ToArray())
                                                         .Select(p => p.Version).Distinct();
                if (spec.Version.Minor != null)
                    allVersions = allVersions.Where(v => v.Minor == spec.Version.Minor);
                if (spec.Version.Patch != null)
                    allVersions = allVersions.Where(v => v.Patch == spec.Version.Patch);
                SemanticVersion ver = allVersions.OrderByDescending(v => v).FirstOrDefault();
                var exactSpec = new PackageSpecifier(name, new VersionSpecifier(ver, VersionMatchBehavior.Exact), spec.Architecture, spec.OS);
                packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, exactSpec);
            }

            var selected = packages.FirstOrDefault(p => p.IsPlatformCompatible(spec.Architecture,spec.OS));
            if (selected is null)
                return packages.FirstOrDefault(pkg => ArchitectureHelper.PluginsCompatible(pkg.Architecture, ArchitectureHelper.GuessBaseArchitecture)); // fallback to old behavior
            else
                return selected;
        }
    }

    internal class DependencyTreeNode : DependencyTree
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
        internal List<DependencyTree> ParentNodes { get; set; } = new List<DependencyTree>();
    }

    internal class DependencyTree
    {
        /// <summary>
        /// Child nodes of this node. These represents the dependencies of the package.
        /// </summary>
        internal List<DependencyTreeNode> ChildNodes { get; set; } = new List<DependencyTreeNode>();
        internal List<DependencyTreeNode> AllNodes { get; set; } = new List<DependencyTreeNode>();
    }

    static class PackageCompatibilityHelper
    {
        public static bool IsCompatible(this PackageSpecifier spec, IPackageIdentifier pkg)
        {
            return spec.Name == pkg.Name &&
                   spec.Version.IsCompatible(pkg.Version) &&
                   (spec.Architecture is CpuArchitecture.Unspecified ||
                   ArchitectureHelper.PluginsCompatible(pkg.Architecture, spec.Architecture)); // TODO: Should we check OS?
        }

        /// <summary>
        /// Returns true if this specifier can be satisfied by the given version. Really the same behavior as VersionSpecifier.IsCompatible, just with a better name.
        /// </summary>
        public static bool IsSatisfiedBy(this VersionSpecifier spec, VersionSpecifier other)
        {
            return spec.IsCompatible(other);
        }
    }
}
