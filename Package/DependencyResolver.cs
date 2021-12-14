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
        /// List of the dependencies that are currently not installed
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
            InstalledPackages = new Dictionary<string, PackageDef>();

            foreach (var specifier in packageSpecifiers)
                graph.AddEdge(DependencyGraph.Root, DependencyGraph.Unresolved, specifier);

            resolveGraph(graph, repositories, cancellationToken);

            CategorizeResolvedPackages();
        }

        private void resolveGraph(DependencyGraph graph, List<IPackageRepository> repositories, CancellationToken cancellationToken)
        {
            AlignArchAndOS(graph);

            bool unresolvedExists = true;
            while (unresolvedExists)
            {
                var edges = graph.TraverseEdges()
                    .Where(s => s.To == DependencyGraph.Unresolved && s.PackageSpecifier.Name != "OpenTAP")
                    .GroupBy(s => s.PackageSpecifier.Name);

                if (!edges.Any())
                {
                    unresolvedExists = false;
                    break;
                }

                var chosenEdges = edges.First().ToList();
                resolveEdge(chosenEdges, repositories, cancellationToken);
            }

            var openTapEdges = graph.TraverseEdges().Where(s => s.PackageSpecifier.Name == "OpenTAP").ToList();
            resolveEdge(openTapEdges, repositories, cancellationToken);
        }

        private void AlignArchAndOS(DependencyGraph graph)
        {
            var edges = graph.TraverseEdges();
            var packages = edges.Select(s => s.PackageSpecifier);
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
            foreach (var edge in edges)
            {
                if (edge.PackageSpecifier.Architecture != arch || edge.PackageSpecifier.OS != os)
                    edge.PackageSpecifier = new PackageSpecifier(edge.PackageSpecifier.Name, edge.PackageSpecifier.Version, arch, os);
            }
        }

        private void resolveEdge(List<DependencyEdge> edges, List<IPackageRepository> repositories, CancellationToken cancellationToken)
        {
            var versions = AlignVersions(edges.Select(s => s.PackageSpecifier).ToList());

            foreach (var specifier in versions)
            {
                ResolveDependency(specifier, edges.Where(s => s.PackageSpecifier.Version.IsSatisfiedBy(specifier.Version)).ToList(), repositories, cancellationToken);
            }
        }

        private List<PackageSpecifier> AlignVersions(List<PackageSpecifier> packages)
        {
            if (packages.Count == 1)
                return packages;

            List<PackageSpecifier> specifiers = new List<PackageSpecifier>();
            foreach (var p in packages)
            {
                if (!specifiers.Any())
                {
                    specifiers.Add(p);
                    continue;
                }
                for (int i = 0; i < specifiers.Count; i++)
                {
                    if (p.Version.IsSatisfiedBy(specifiers[i].Version))
                    {
                        // the already selected package can be used in place of this
                        continue;
                    }
                    else if (specifiers[i].Version.IsSatisfiedBy(p.Version))
                    {
                        // this package can satisfy the already selected specification, update to use this instead.
                        specifiers[i] = p;
                    }
                    else
                        specifiers.Add(p);

                }
            }
            return specifiers;
        }

        private void ResolveDependency(PackageSpecifier packageSpecifier, List<DependencyEdge> current, List<IPackageRepository> repositories, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Operation cancelled by user.");

            PackageDef alreadyExisting = graph.GetPackages().FirstOrDefault(n => packageSpecifier.IsCompatible(n));
            if (alreadyExisting != null)
            {
                foreach (var edge in current)
                    edge.To = alreadyExisting;
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

            if (resolvedDependency is null)
            {
                foreach (var edge in current)
                    edge.To = DependencyGraph.Unknown;
                return;
            }

            // We did not find any compatible packages already in the graph, but if the latest resolved satisfies any already in the graph, let's switch them out.
            IEnumerable<DependencyEdge> alreadyPresentEdges = graph.TraverseEdges().Where(s => s.To.Name == packageSpecifier.Name);
            if (alreadyPresentEdges.Any())
            {
                foreach (var presentEdge in alreadyPresentEdges)
                {
                    if (presentEdge.PackageSpecifier.Version.IsSatisfiedBy(packageSpecifier.Version))
                        presentEdge.To = resolvedDependency;
                }
            }
            graph.AddVertex(resolvedDependency);

            foreach (var edge in current)
                edge.To = resolvedDependency;

            foreach (var nextLevelDep in resolvedDependency.Dependencies)
            {
                var spec = new PackageSpecifier(nextLevelDep.Name, nextLevelDep.Version, packageSpecifier.Architecture, packageSpecifier.OS);
                graph.AddEdge(resolvedDependency, DependencyGraph.Unresolved, spec);
            }
        }

        /// <summary>
        /// Returns the resolved dependency tree
        /// </summary>
        /// <returns>Multi line dependency tree string</returns>
        internal string GetPrintableDependencyTree()
        {
            return graph.Traverse();
        }

        /// <summary>
        /// Populates Dependencies, UnknownDependencies and DependencyIssues based on resolved dependency tree
        /// </summary>
        private void CategorizeResolvedPackages()
        {
            var traversedEdges = graph.TraverseEdges();
            Dependencies = traversedEdges.Where(p => !(p.To is UnknownVertex) && !InstalledPackages.Any(s => s.Value == p.To)).Select(s => s.To).Distinct().ToList();
            UnknownDependencies = traversedEdges.Where(s => s.To is UnknownVertex).Select(p => new PackageDependency(p.PackageSpecifier.Name, p.PackageSpecifier.Version)).Distinct().ToList();

            // A dependency is only "missing" if it is not installed and has to be downloaded from a repository
            MissingDependencies = Dependencies.Where(s => !InstalledPackages.Any(p => p.Key == s.Name && s.Version == p.Value.Version)).ToList();

            var multipleVersions = traversedEdges.Where(p => !(p.To is UnknownVertex)).Select(s => s.To).Distinct().GroupBy(x => x.Name);
            foreach (var grp in multipleVersions)
            {
                if (grp.Count() > 1)
                    DependencyIssues.Add(new InvalidOperationException($"Resolved versions of package '{grp.Key}' are not compatible: {string.Join(", ", grp.SelectValues(s => s.Version))}"));
            }
        }

        private void resolve(List<IPackageRepository> repositories, IEnumerable<PackageDef> packages)
        {
            foreach (var pkg in packages)
            {
                graph.AddEdge(DependencyGraph.Root, pkg, new PackageSpecifier(pkg, VersionMatchBehavior.Exact));
                foreach (var dep in pkg.Dependencies)
                {
                    var spec = new PackageSpecifier(dep.Name, dep.Version, pkg.Architecture, pkg.OS);
                    graph.AddEdge(pkg, DependencyGraph.Unresolved, spec);
                }
            }
            resolveGraph(graph, repositories, CancellationToken.None);
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


    /// <summary>
    /// Dependency Graph structure. PackageDefs are vertices and edges between vertices are defined as PackageDef references to both vertices along with a PackageSpecifer
    /// defining the requirement of the 'From' vertex.
    /// </summary>
    internal class DependencyGraph
    {
        internal static RootVertex Root = new RootVertex();
        internal static UnresolvedVertex Unresolved = new UnresolvedVertex();
        internal static UnknownVertex Unknown = new UnknownVertex();
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

        /// <summary>
        /// Add an edge between vertices. If the vertices used in the edge is not null, they will be added as vertices.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="packageSpecifier"></param>
        public void AddEdge(PackageDef from, PackageDef to, PackageSpecifier packageSpecifier)
        {
            if (from != null && !vertices.Contains(from))
                AddVertex(from);
            if (to != null && !vertices.Contains(to))
                AddVertex(to);
            edges.Add(new DependencyEdge(from, to, packageSpecifier));
        }

        internal string Traverse()
        {
            List<string> dependencyTree = new List<string>();

            var rootEdges = edges.Where(s => s.From == Root);

            foreach (var edge in rootEdges)
            {
                traverse(edge, dependencyTree, 0, new List<PackageDef>());
            }

            return string.Join(Environment.NewLine, dependencyTree.ToArray());
        }

        private void traverse(DependencyEdge edge, List<string> dependencyTree, int v, List<PackageDef> packageDefs)
        {
            if (edge.To is UnknownVertex)
            {
                dependencyTree.Add($"{new string(' ', v * 2)}{edge.PackageSpecifier.Name} version {edge.PackageSpecifier.Version} was unable to be resolved");
                return;
            }

            dependencyTree.Add($"{new string(' ', v * 2)}{edge.PackageSpecifier.Name} version {edge.PackageSpecifier.Version} resolved to {edge.To.Version}");
            v = v + 1;
            packageDefs.Add(edge.To);
            foreach (var dependency in TraverseEdges().Where(s => s.From == edge.To))
            {
                if (!packageDefs.Contains(dependency.To))
                    traverse(dependency, dependencyTree, v, packageDefs);
            }
        }

        internal IEnumerable<DependencyEdge> TraverseEdges()
        {
            var visited = new HashSet<DependencyEdge>();
            var queue = new Queue<DependencyEdge>();
            foreach (var edge in edges.Where(s => s.From == Root))
                queue.Enqueue(edge);


            while (queue.Any())
            {
                var current = queue.Dequeue();
                visited.Add(current);
                yield return current;
                foreach (var edge in edges.Where(s => !visited.Contains(s) && s.From == current.To))
                {
                    queue.Enqueue(edge);
                }
            }
        }
    }
    internal class RootVertex : PackageDef
    {

    }

    internal class UnresolvedVertex : PackageDef
    {

    }

    internal class UnknownVertex : PackageDef
    {

    }

    [DebuggerDisplay("Edge: {PackageSpecifier.Name} needs {PackageSpecifier.Version.ToString()}, resolved {To.Version.ToString()}")]
    internal class DependencyEdge
    {
        private PackageDef to;

        public DependencyEdge(PackageDef from, PackageDef to, PackageSpecifier packageSpecifier)
        {
            PackageSpecifier = packageSpecifier;
            From = from;
            To = to;
        }

        public PackageSpecifier PackageSpecifier { get; set; }
        public PackageDef From { get; set; }
        public PackageDef To { get => to; set { to = value; } }
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
