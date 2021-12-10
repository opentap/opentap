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
        /// List of all the dependencies including the specified packages
        /// </summary>
        public List<PackageDef> Dependencies = new List<PackageDef>();

        /// <summary>
        /// List of the dependencies that are currently not installed and has to be downloaded from a repository
        /// </summary>
        public List<PackageDef> MissingDependencies = new List<PackageDef>();

        /// <summary>
        /// List of the dependencies that could not be found in the package repositories
        /// </summary>
        public List<PackageDependency> UnknownDependencies = new List<PackageDependency>();

        /// <summary>
        /// List of dependency issues as exceptions. This can for example be version mismatches.
        /// </summary>
        public List<Exception> DependencyIssues = new List<Exception>();

        private TraceSource log = Log.CreateSource("DependencyResolver");

        DependencyGraph graph = new DependencyGraph();

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

            foreach (var specifier in alignedSpecifiers)
                ResolveDependenciesRecursive(repositories, specifier, null, cancellationToken);

            CategorizeResolvedPackages();
        }

        /// <summary>
        /// Returns the resolved dependency tree
        /// </summary>
        /// <returns>Multi line dependency tree string</returns>
        public string GetPrintableDependencyTree()
        {
            return graph.Traverse();
        }

        /// <summary>
        /// Populates Dependencies, UnknownDependencies and DependencyIssues based on resolved dependency tree
        /// </summary>
        private void CategorizeResolvedPackages()
        {
            Dictionary<string, DependencyEdge> selectedNodes = new Dictionary<string, DependencyEdge>();

            foreach (var edge in graph.GetEdges())
            {
                if (selectedNodes.TryGetValue(edge.PackageSpecifier.Name, out var s))
                {
                    if (edge.PackageSpecifier.Version.IsSatisfiedBy(s.PackageSpecifier.Version))
                    {
                        // the already selected package can be used in place of this
                        continue;
                    }
                    else if (s.PackageSpecifier.Version.IsSatisfiedBy(edge.PackageSpecifier.Version))
                    {
                        // this package can satisfy the already selected specification, update to use this instead.
                        selectedNodes[edge.PackageSpecifier.Name] = edge;
                    }
                    else
                        DependencyIssues.Add(new InvalidOperationException($"Specified versions of package '{edge.PackageSpecifier.Name}' are not compatible: {edge.PackageSpecifier.Version} - {s.PackageSpecifier.Version}"));
                }
                else
                {
                    selectedNodes.Add(edge.PackageSpecifier.Name, edge);
                }
            }


            Dependencies = selectedNodes.Values.Where(p => p.To != null && !InstalledPackages.Any(s => s.Value == p.To)).Select(s => s.To).ToList();
            UnknownDependencies = selectedNodes.Values.Where(s => s.To == null).Select(p => new PackageDependency(p.PackageSpecifier.Name, p.PackageSpecifier.Version)).ToList();

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
            foreach (var pkg in packages)
            {
                graph.AddVertex(pkg); //TreeRoot.AddChild(pkg, new PackageSpecifier(pkg, VersionMatchBehavior.Compatible));
                graph.AddDependency(null, pkg, new PackageSpecifier(pkg, VersionMatchBehavior.Exact));
                foreach (var dep in pkg.Dependencies)
                {
                    var spec = new PackageSpecifier(dep.Name, dep.Version, pkg.Architecture, pkg.OS);
                    ResolveDependenciesRecursive(repositories, spec, pkg, CancellationToken.None);
                }
            }
        }

        private void ResolveDependenciesRecursive(List<IPackageRepository> repositories, PackageSpecifier packageSpecifier, PackageDef parent, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Operation cancelled by user.");

            PackageDef alreadyExisting = graph.GetPackages().FirstOrDefault(n => packageSpecifier.IsCompatible(n));
            if (alreadyExisting != null)
            {
                graph.AddDependency(parent, alreadyExisting, packageSpecifier);
                return;
            }

            PackageDef resolvedDependency = GetPackageDefFromInstallation(packageSpecifier);
            if (resolvedDependency == null)
            {
                try
                {
                    resolvedDependency = GetPackageDefFromRepo(repositories, packageSpecifier, InstalledPackages.Values.ToList());
                }
                catch (Exception ex)
                {
                    DependencyIssues.Add(ex);
                }
            }

            if (resolvedDependency != null)
                graph.AddVertex(resolvedDependency);
            graph.AddDependency(parent, resolvedDependency, packageSpecifier);

            if (resolvedDependency == null)
                return;

            foreach (var nextLevelDep in resolvedDependency.Dependencies)
            {
                var spec = new PackageSpecifier(nextLevelDep.Name, nextLevelDep.Version, packageSpecifier.Architecture, packageSpecifier.OS);
                ResolveDependenciesRecursive(repositories, spec, resolvedDependency, cancellationToken);
            }
        }

        private Dictionary<string, PackageDef> InstalledPackages;

        PackageDef GetPackageDefFromInstallation(PackageSpecifier specifier)
        {
            return InstalledPackages.Values.FirstOrDefault(s => specifier.IsCompatible(s));
        }

        internal static PackageDef GetPackageDefFromRepo(List<IPackageRepository> repositories, PackageSpecifier packageSpecifier, List<PackageDef> installedPackages)
        {
            IEnumerable<PackageDef> packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, packageSpecifier, installedPackages.ToArray());

            if (packages.Any() == false)
                packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, packageSpecifier);

            if (!packages.Any())
            {
                var compatibleVersions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, packageSpecifier.Name, installedPackages.ToArray());
                var versions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, packageSpecifier.Name);

                // Any packages compatible with opentap and platform
                var filteredVersions = compatibleVersions.Where(v => v.IsPlatformCompatible(packageSpecifier.Architecture, packageSpecifier.OS)).ToList();
                if (filteredVersions.Any())
                {
                    // if the specified version exist, don't say it could not be found. 
                    if (versions.Any(v => packageSpecifier.Version.IsCompatible(v.Version)))
                        throw new InvalidOperationException($"Package '{packageSpecifier.Name}' matching version '{packageSpecifier.Version}' is not compatible. Latest compatible version is '{filteredVersions.FirstOrDefault().Version}'.");
                    else
                        throw new InvalidOperationException($"Package '{packageSpecifier.Name}' matching version '{packageSpecifier.Version}' could not be found. Latest compatible version is '{filteredVersions.FirstOrDefault().Version}'.");
                }

                // Any compatible with platform but not opentap
                filteredVersions = versions.Where(v => v.IsPlatformCompatible(packageSpecifier.Architecture, packageSpecifier.OS)).ToList();
                if (filteredVersions.Any() && installedPackages.Any())
                {
                    var opentapPackage = installedPackages.First();
                    throw new InvalidOperationException($"Package '{packageSpecifier.Name}' does not exist in a version compatible with '{opentapPackage.Name}' version '{opentapPackage.Version}'.");
                }

                // Any compatible with opentap but not platform
                if (compatibleVersions.Any())
                {
                    if (packageSpecifier.Version != VersionSpecifier.Any || packageSpecifier.OS != null || packageSpecifier.Architecture != CpuArchitecture.Unspecified)
                        throw new InvalidOperationException(string.Format("No '{0}' package {1} was found.", packageSpecifier.Name, string.Join(" and ",
                                new string[] {
                                    packageSpecifier.Version != VersionSpecifier.Any ? $"compatible with version '{packageSpecifier.Version}'": null,
                                    packageSpecifier.OS != null ? $"compatible with '{packageSpecifier.OS}' operating system" : null,
                                    packageSpecifier.Architecture != CpuArchitecture.Unspecified ? $"with '{packageSpecifier.Architecture}' architecture" : null
                            }.Where(x => x != null).ToArray())));
                    else
                        throw new InvalidOperationException($"Package '{packageSpecifier.Name}' does not exist in a version compatible with this OS and architecture.");
                }

                // Any version
                if (versions.Any())
                {
                    var opentapPackage = installedPackages.FirstOrDefault();
                    if (opentapPackage != null)
                        throw new InvalidOperationException($"Package '{packageSpecifier.Name}' does not exist in a version compatible with this OS, architecture and '{opentapPackage.Name}' version '{opentapPackage.Version}'.");
                    else
                        throw new InvalidOperationException($"Package '{packageSpecifier.Name}' does not exist in a version compatible with this OS and architecture.");
                }

                throw new InvalidOperationException($"Package '{packageSpecifier.Name}' could not be found in any repository.");
            }

            // if the version is not fully specified, or it is a compatible version, don't rely on
            // the repo to pick the correct version, instead ask for all versions and pick one here
            if (packageSpecifier.Version.MatchBehavior.HasFlag(VersionMatchBehavior.Compatible) || packageSpecifier.Version.Minor is null || packageSpecifier.Version.Patch is null)
            {
                var allVersions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, packageSpecifier.Name, installedPackages.ToArray())
                                                         .Select(p => p.Version).Distinct();
                if (packageSpecifier.Version.Minor != null)
                    allVersions = allVersions.Where(v => v.Minor == packageSpecifier.Version.Minor);
                if (packageSpecifier.Version.Patch != null)
                    allVersions = allVersions.Where(v => v.Patch == packageSpecifier.Version.Patch);
                SemanticVersion ver = allVersions.OrderByDescending(v => v).FirstOrDefault();
                var exactSpec = new PackageSpecifier(packageSpecifier.Name, new VersionSpecifier(ver, VersionMatchBehavior.Exact), packageSpecifier.Architecture, packageSpecifier.OS);
                packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, exactSpec);
            }

            var selected = packages.FirstOrDefault(p => p.IsPlatformCompatible(packageSpecifier.Architecture, packageSpecifier.OS));
            if (selected is null)
                return packages.FirstOrDefault(pkg => ArchitectureHelper.PluginsCompatible(pkg.Architecture, ArchitectureHelper.GuessBaseArchitecture)); // fallback to old behavior
            else
                return selected;
        }
    }

    internal class DependencyGraph
    {
        private readonly List<PackageDef> vertices = new List<PackageDef>();
        private readonly List<DependencyEdge> edges = new List<DependencyEdge>();
        public void AddVertex(PackageDef vertex)
        {
            vertices.Add(vertex);
        }

        internal IEnumerable<PackageDef> GetPackages()
        {
            return vertices;
        }
        internal List<DependencyEdge> GetEdges()
        {
            return edges;
        }

        public void AddDependency(PackageDef from, PackageDef to, PackageSpecifier packageSpecifier)
        {
            if (from != null && !vertices.Contains(from))
                AddVertex(from);
            if (to != null && !vertices.Contains(to))
                AddVertex(to);
            edges.Add(new DependencyEdge(from, to, packageSpecifier));
        }

        internal IEnumerable<DependencyEdge> GetDependencyEdges(PackageDef package)
        {
            return edges.Where(s => s.From == package);
        }

        internal string Traverse()
        {
            List<string> dependencyTree = new List<string>();

            var root = edges.Where(s => s.From == null);

            foreach (var package in root)
            {
                traverse(package, dependencyTree, 0);
            }

            return string.Join(Environment.NewLine, dependencyTree.ToArray());
        }

        private void traverse(DependencyEdge edge, List<string> dependencyTree, int v)
        {
            if (edge.To is null)
            {
                dependencyTree.Add($"{new string(' ', v * 2)}{edge.PackageSpecifier.Name} version {edge.PackageSpecifier.Version} was unable to be resolved");
                return;
            }

            dependencyTree.Add($"{new string(' ', v * 2)}{edge.PackageSpecifier.Name} version {edge.PackageSpecifier.Version} resolved to {edge.To.Version}");
            foreach (var dependency in GetDependencyEdges(edge.To))//  graph[edge.Dependency])
            {
                traverse(dependency, dependencyTree, v++);
            }
        }
    }

    [DebuggerDisplay("Edge: {from.Name} needs {packageSpecifier.Version.ToString()}, resolved {To.Version.ToString()}")]
    internal class DependencyEdge
    {
        public DependencyEdge(PackageDef from, PackageDef to, PackageSpecifier packageSpecifier)
        {
            PackageSpecifier = packageSpecifier;
            From = from;
            To = to;
        }

        public PackageSpecifier PackageSpecifier { get; set; }
        public PackageDef From { get; set; }
        public PackageDef To { get; set; }
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
