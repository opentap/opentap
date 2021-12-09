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
        /// List of the dependencies to the specified packages that are currently not installed and has to be downloaded from a repository
        /// </summary>
        public List<PackageDef> MissingDependencies = new List<PackageDef>();

        /// <summary>
        /// List of the dependencies to the specified packages that could not be found in the package repositories
        /// </summary>
        public List<PackageDependency> UnknownDependencies = new List<PackageDependency>();

        /// <summary>
        /// List of dependency issues as exceptions. This can for example be version mismatches.
        /// </summary>
        public List<Exception> DependencyIssues = new List<Exception>();
        internal List<string> resolutionTree = new List<string>();

        internal DependencyTreeNode TreeRoot;
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
            CategorizeResolvedPackages();
        }

        internal DependencyResolver(Dictionary<string, PackageDef> installedPackages, IEnumerable<PackageDef> packages, List<IPackageRepository> repositories)
        {
            InstalledPackages = installedPackages;
            resolve(repositories, packages);
            CategorizeResolvedPackages();
        }

        internal DependencyResolver(List<PackageSpecifier> packageSpecifiers, List<IPackageRepository> repositories, CancellationToken cancellationToken)
        {
            var alignedSpecifiers = CheckCompatibility(packageSpecifiers);
            if (DependencyIssues.Any())
                return;
            InstalledPackages = new Dictionary<string, PackageDef>();
            TreeRoot = DependencyTreeNode.CreateRoot();
            foreach (var specifier in alignedSpecifiers)
                ResolveDependenciesRecursive(repositories, specifier, TreeRoot, cancellationToken);

            CategorizeResolvedPackages();
        }

        /// <summary>
        /// Returns the resolved dependency tree
        /// </summary>
        /// <returns>Multi line dependency tree string</returns>
        public string GetPrintableDependencyTree()
        {
            return string.Join(Environment.NewLine, resolutionTree);
        }

        /// <summary>
        /// Populates Dependencies, UnknownDependencies and DependencyIssues based on resolved dependency tree
        /// </summary>
        private void CategorizeResolvedPackages()
        {
            Dictionary<string, DependencyTreeNode> selectedNodes = new Dictionary<string, DependencyTreeNode>();
            foreach (var p in TreeRoot.WalkChildren())
            {
                if (selectedNodes.TryGetValue(p.PackageSpecifier.Name, out var s))
                {
                    if (p.PackageSpecifier.Version.IsSatisfiedBy(s.PackageSpecifier.Version))
                    {
                        // the already selected package can be used in place of this
                        continue;
                    }
                    else if (s.PackageSpecifier.Version.IsSatisfiedBy(p.PackageSpecifier.Version))
                    {
                        // this package can satisfy the already selected specification, update to use this instead.
                        selectedNodes[p.PackageSpecifier.Name] = p;
                    }
                    else
                        DependencyIssues.Add(new InvalidOperationException($"Specified versions of package '{p.PackageSpecifier.Name}' are not compatible: {p.PackageSpecifier.Version} - {s.PackageSpecifier.Version}"));
                }
                else
                {
                    selectedNodes.Add(p.PackageSpecifier.Name, p);
                }
            }

            Dependencies = selectedNodes.Values.Where(p => p.Package != null && !InstalledPackages.Any(s => s.Value == p.Package)).Select(s => s.Package).ToList();
            UnknownDependencies = selectedNodes.Values.Where(s => s.Package == null).Select(p => new PackageDependency(p.PackageSpecifier.Name, p.PackageSpecifier.Version)).ToList();

            // A dependency is only "missing" if it is not installed and has to be downloaded from a repository
            MissingDependencies = Dependencies.Where(s => !InstalledPackages.Any(p => p.Key == s.Name && s.Version == p.Value.Version) && !(s.PackageSource is FilePackageDefSource)).ToList();

            if (UnknownDependencies.Any())
            {
                foreach (var dep in UnknownDependencies)
                {
                    string err = $"Unable to find package '{dep.Name}'";
                    if (!string.IsNullOrEmpty(dep.Version.ToString()))
                        err += $" version {dep.Version}";
                    DependencyIssues.Add(new InvalidOperationException(err));
                }
            }
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
            TreeRoot = DependencyTreeNode.CreateRoot();
            foreach (var pkg in packages)
            {
                var node = TreeRoot.AddChild(pkg, new PackageSpecifier(pkg, VersionMatchBehavior.Compatible));
                foreach (var dep in pkg.Dependencies)
                {
                    var spec = new PackageSpecifier(dep.Name, dep.Version, pkg.Architecture, pkg.OS);
                    ResolveDependenciesRecursive(repositories, spec, node, CancellationToken.None);
                }
            }
        }

        private void ResolveDependenciesRecursive(List<IPackageRepository> repositories, PackageSpecifier dependency, DependencyTreeNode parent, CancellationToken cancellationToken)
        {
            if(cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Operation cancelled by user.");

            string indent = new string(' ', parent.WalkParents().Count() * 2);
            // Can this dependency be satisfied by something that is already in the tree?
            DependencyTreeNode node = TreeRoot.WalkChildren().FirstOrDefault(n => dependency.IsCompatible(n.Package));
            if (node != null)
            {
                // check that that node does not already have the parent as one of its children (cyclic dependency)
                if (node.WalkChildren().Any(n => n == parent))
                    return;// throw new InvalidOperationException("Cyclic dependencies not supported.");

                node.AddParent(parent);
                resolutionTree.Add($"{indent}{dependency.Name} version {dependency.Version} resolved to {node.Package.Version}");
                return;
            }

            PackageDef depPkg = GetPackageDefFromInstallation(dependency.Name, dependency.Version);
            if (depPkg == null)
                depPkg = GetPackageDefFromRepo(repositories, dependency);

            var newNode = parent.AddChild(depPkg, dependency);

            if (depPkg != null)
                resolutionTree.Add($"{indent}{dependency.Name} version {dependency.Version} resolved to {depPkg.Version}");
            else
                resolutionTree.Add($"{indent}{dependency.Name} version {dependency.Version} was unable to be resolved");

            if (depPkg == null)
                return;

            foreach (var nextLevelDep in depPkg.Dependencies)
            {
                var spec = new PackageSpecifier(nextLevelDep.Name, nextLevelDep.Version, dependency.Architecture, dependency.OS);
                ResolveDependenciesRecursive(repositories, spec, newNode, cancellationToken);
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
                var allVersions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, spec.Name, InstalledPackages.Values.ToArray())
                                                         .Select(p => p.Version).Distinct();
                if (spec.Version.Minor != null)
                    allVersions = allVersions.Where(v => v.Minor == spec.Version.Minor);
                if (spec.Version.Patch != null)
                    allVersions = allVersions.Where(v => v.Patch == spec.Version.Patch);
                SemanticVersion ver = allVersions.OrderByDescending(v => v).FirstOrDefault();
                var exactSpec = new PackageSpecifier(name, new VersionSpecifier(ver, VersionMatchBehavior.Exact), spec.Architecture, spec.OS);
                packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, exactSpec);
            }

            var selected = packages.FirstOrDefault(p => p.IsPlatformCompatible(spec.Architecture, spec.OS));
            if (selected is null)
                return packages.FirstOrDefault(pkg => ArchitectureHelper.PluginsCompatible(pkg.Architecture, ArchitectureHelper.GuessBaseArchitecture)); // fallback to old behavior
            else
                return selected;
        }
    }

    [DebuggerDisplay("Node: {ToString()}")]
    internal class DependencyTreeNode
    {
        private DependencyTreeNode(PackageDef packageDef, PackageSpecifier packageSpecifier)
        {
            PackageSpecifier = packageSpecifier ?? throw new ArgumentNullException(nameof(packageSpecifier));
            Package = packageDef;
        }
        /// <summary>
        /// The requirement of the version of this package. This is the highest requirement of the requirements of all the parent nodes
        /// </summary>
        public PackageSpecifier PackageSpecifier { get; }

        /// <summary>
        /// The package that this node represents (the dependency was resolved to this package)
        /// </summary>
        public PackageDef Package { get; }

        public static DependencyTreeNode CreateRoot()
        {
            return new DependencyTreeNode();
        }

        private DependencyTreeNode()
        {

        }

        /// <summary>
        /// The parent nodes. These represent the packages that depend on this package.
        /// </summary>
        internal IEnumerable<DependencyTreeNode> ParentNodes => parentNodes;
        internal List<DependencyTreeNode> parentNodes = new List<DependencyTreeNode>();

        internal void AddParent(DependencyTreeNode parent)
        {
            this.parentNodes.Add(parent);
            parent.childNodes.Add(this);
        }

        /// <summary>
        /// Child nodes of this node. These represents the dependencies of the package.
        /// </summary>
        internal IEnumerable<DependencyTreeNode> ChildNodes => childNodes;
        internal List<DependencyTreeNode> childNodes = new List<DependencyTreeNode>();
        internal DependencyTreeNode AddChild(PackageDef packageDef, PackageSpecifier packageSpecifier)
        {
            var childNode = new DependencyTreeNode(packageDef, packageSpecifier);
            childNode.parentNodes.Add(this);
            childNodes.Add(childNode);
            return childNode;
        }



        public IEnumerable<DependencyTreeNode> WalkChildren() => WalkTree(ChildNodes, n => n.ChildNodes);

        public IEnumerable<DependencyTreeNode> WalkParents() => WalkTree(ParentNodes, n => n.ParentNodes);

        private static IEnumerable<T> WalkTree<T>(IEnumerable<T> rootNodes, Func<T, IEnumerable<T>> getChildren)
        {
            foreach (T node in rootNodes)
            {
                foreach (T child in WalkTree(getChildren(node), getChildren))
                    yield return child;
                yield return node;
            }
        }

        public override string ToString()
        {
            return Package?.Name ?? "root";
        }
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
