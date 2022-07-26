//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
                graph.AddEdge(new RootVertex("Root"), DependencyGraph.Unresolved, specifier);

            resolveGraph(graph, repositories, cancellationToken);

            CategorizeResolvedPackages();
        }

        internal DependencyResolver(Installation tapInstallation, List<PackageSpecifier> packageSpecifiers, List<IPackageRepository> repositories, CancellationToken cancellationToken)
        {
            InstalledPackages = new Dictionary<string, PackageDef>();
            foreach (var pkg in tapInstallation.GetPackages())
                InstalledPackages[pkg.Name] = pkg;

            foreach (var specifier in packageSpecifiers)
                graph.AddEdge(new RootVertex("Root"), DependencyGraph.Unresolved, specifier);

            resolveGraph(graph, repositories, cancellationToken);

            CategorizeResolvedPackages();
        }

        internal DependencyResolver(Dictionary<string, List<PackageSpecifier>> packages, List<IPackageRepository> repositories, CancellationToken cancellationToken)
        {
            InstalledPackages = new Dictionary<string, PackageDef>();

            foreach (var img in packages)
            {
                foreach (var pack in img.Value)
                    graph.AddEdge(new RootVertex(img.Key), DependencyGraph.Unresolved, pack);
            }

            resolveGraph(graph, repositories, cancellationToken);

            CategorizeResolvedPackages();
        }

        private void resolveGraph(DependencyGraph graph, List<IPackageRepository> repositories, CancellationToken cancellationToken)
        {
            AlignArchAndOS(graph);

            bool unresolvedExists = true;
            while (unresolvedExists)
            {
                var traversedEdges = graph.TraverseEdges();

                var edges = traversedEdges
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
            {
                if (openTapPackage != null && openTapPackage.OS != null && !openTapPackage.OS.Contains(","))
                    os = openTapPackage.OS;
                else
                    os = OperatingSystem.Current.Name; // If all packages (also OpenTAP) specify 'Windows,Linux', then we should take the informed decision to roll with current platform.
            }
            else
                os = oss[0];

            // Check if all package only specify compatible architectures
            var archs = packages.Select(p => p.Architecture).Distinct()
                                                .Where(a => a != CpuArchitecture.Unspecified && a != CpuArchitecture.AnyCPU).ToList();

            CpuArchitecture arch;
            if (archs.Count != 1)
            {
                if (openTapPackage?.Architecture != null && openTapPackage.Architecture != CpuArchitecture.Unspecified && openTapPackage.Architecture != CpuArchitecture.AnyCPU)
                    arch = openTapPackage.Architecture;
                else if (InstalledPackages.ContainsKey("OpenTAP"))
                    arch = InstalledPackages["OpenTAP"].Architecture;
                else
                    arch = ArchitectureHelper.GuessBaseArchitecture;
            }
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

            if (versions.Count > 1 && versions.Any(s => s.Version.MatchBehavior == VersionMatchBehavior.Exact))
            {
                var highest = versions.OrderByDescending(s => s.Version).FirstOrDefault();
                foreach (var exactversion in versions.Where(s => s.Version.MatchBehavior == VersionMatchBehavior.Exact))
                {
                    if (exactversion != highest && !exactversion.Version.IsSatisfiedBy(highest.Version))
                    {
                        ConflictVertex conflictVertex = new ConflictVertex() { Name = edges.FirstOrDefault().PackageSpecifier.Name };
                        foreach (var edge in edges)
                            edge.To = conflictVertex;

                        return;
                    }
                }
            }

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

                if (specifiers.FirstOrDefault(s => p.Version.IsSatisfiedBy(s.Version)) is PackageSpecifier satisfyingSpecifier)
                    continue;

                for (int i = 0; i < specifiers.Count; i++)
                {
                    if (specifiers[i].Version.IsSatisfiedBy(p.Version))
                    {
                        // this package can satisfy the already selected specification, update to use this instead.
                        specifiers[i] = p;
                    }

                }
                specifiers.Add(p);
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
        internal string GetDotNotation() => graph.CreateDotNotation();

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

            foreach (var conflict in traversedEdges.Where(s => s.To is ConflictVertex).GroupBy(x => x.PackageSpecifier.Name))
            {
                DependencyIssues.Add(new InvalidOperationException($"Requested versions of package '{conflict.Key}' are not compatible: {string.Join(", ", conflict.SelectValues(s => s.PackageSpecifier.Version))}"));
            }

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
                graph.AddEdge(new RootVertex("Root"), pkg, new PackageSpecifier(pkg, VersionMatchBehavior.Exact));
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
            if (SemanticVersion.TryParse(packageSpecifier.Version.ToString().Replace("^", ""), out SemanticVersion tryExact))
            {
                VersionSpecifier spec = new VersionSpecifier(packageSpecifier.Version.Major, packageSpecifier.Version.Minor, packageSpecifier.Version.Patch, packageSpecifier.Version.PreRelease, packageSpecifier.Version.BuildMetadata, VersionMatchBehavior.Exact);
                var exactPackageSpecifier = new PackageSpecifier(packageSpecifier.Name, spec, packageSpecifier.Architecture, packageSpecifier.OS);
                if (PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, exactPackageSpecifier)
                    .FirstOrDefault(p => p.IsPlatformCompatible(exactPackageSpecifier.Architecture, exactPackageSpecifier.OS)) is PackageDef exactPackage)
                    return exactPackage;
            }

            var allVersions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, packageSpecifier.Name)
               .Where(p => p.IsPlatformCompatible(packageSpecifier.Architecture, packageSpecifier.OS))
               .Where(p => p.Version != null) // Packages with versions not in Semantic format will be null. We can't take any valuable decision on these.
               .Select(p => p.Version).Distinct();

            SemanticVersion resolvedVersion = null;

            resolvedVersion = GetLatestCompatible(allVersions, packageSpecifier);

            if (resolvedVersion is null)
            {
                string issue = DetermineResolveIssue(repositories, packageSpecifier, installedPackages);
                throw new InvalidOperationException(issue);
            }

            var exactSpec = new PackageSpecifier(packageSpecifier.Name, new VersionSpecifier(resolvedVersion, VersionMatchBehavior.Exact), packageSpecifier.Architecture, packageSpecifier.OS);
            var packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, exactSpec);

            var selected = packages.FirstOrDefault(p => p.IsPlatformCompatible(packageSpecifier.Architecture, packageSpecifier.OS));
            if (selected is null)
                return packages.FirstOrDefault(pkg => ArchitectureHelper.PluginsCompatible(pkg.Architecture, ArchitectureHelper.GuessBaseArchitecture)); // fallback to old behavior
            else
                return selected;
        }

        private static string DetermineResolveIssue(List<IPackageRepository> repositories, PackageSpecifier packageSpecifier, List<PackageDef> installedPackages)
        {
            var compatibleVersions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, packageSpecifier.Name, installedPackages.ToArray());
            var versions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(repositories, packageSpecifier.Name);

            // Any packages compatible with opentap and platform
            var filteredVersions = compatibleVersions.Where(v => v.IsPlatformCompatible(packageSpecifier.Architecture, packageSpecifier.OS)).ToList();
            if (filteredVersions.Any())
            {
                // if the specified version exist, don't say it could not be found. 
                if (versions.Any(v => packageSpecifier.Version.IsCompatible(v.Version)))
                    return $"Package '{packageSpecifier.Name}' matching version '{packageSpecifier.Version}' is not compatible. Latest compatible version is '{filteredVersions.FirstOrDefault().Version}'.";
                else
                    return $"Package '{packageSpecifier.Name}' matching version '{packageSpecifier.Version}' could not be found. Latest compatible version is '{filteredVersions.FirstOrDefault().Version}'.";
            }

            // Any compatible with platform but not opentap
            filteredVersions = versions.Where(v => v.IsPlatformCompatible(packageSpecifier.Architecture, packageSpecifier.OS)).ToList();
            if (filteredVersions.Any() && installedPackages.Any())
            {
                var opentapPackage = installedPackages.First();
                return $"Package '{packageSpecifier.Name}' does not exist in a version compatible with '{opentapPackage.Name}' version '{opentapPackage.Version}'.";
            }

            // Any compatible with opentap but not platform
            if (compatibleVersions.Any())
            {
                if (packageSpecifier.Version != VersionSpecifier.Any || packageSpecifier.OS != null || packageSpecifier.Architecture != CpuArchitecture.Unspecified)
                    return string.Format("No '{0}' package {1} was found.", packageSpecifier.Name, string.Join(" and ",
                            new string[] {
                                    packageSpecifier.Version != VersionSpecifier.Any ? $"compatible with version '{packageSpecifier.Version}'": null,
                                    packageSpecifier.OS != null ? $"compatible with '{packageSpecifier.OS}' operating system" : null,
                                    packageSpecifier.Architecture != CpuArchitecture.Unspecified ? $"with '{packageSpecifier.Architecture}' architecture" : null
                        }.Where(x => x != null).ToArray()));
                else
                    return $"Package '{packageSpecifier.Name}' does not exist in a version compatible with this OS and architecture.";
            }

            // Any version
            if (versions.Any())
            {
                var opentapPackage = installedPackages.FirstOrDefault();
                if (opentapPackage != null)
                    return $"Package '{packageSpecifier.Name}' does not exist in a version compatible with this OS, architecture and '{opentapPackage.Name}' version '{opentapPackage.Version}'.";
                else
                    return $"Package '{packageSpecifier.Name}' does not exist in a version compatible with this OS and architecture.";
            }

            return $"Package '{packageSpecifier.Name}' could not be found in any repository.";
        }

        private static SemanticVersion GetLatestCompatible(IEnumerable<SemanticVersion> allVersions, PackageSpecifier packageSpecifier)
        {
            if (packageSpecifier.Version != VersionSpecifier.Any) // If user specified "any", do not filter anything!
            {
                allVersions = allVersions.Where(p => packageSpecifier.Version.IsCompatible(p));

                if (packageSpecifier.Version.Minor != null)
                {
                    if (packageSpecifier.Version.MatchBehavior == VersionMatchBehavior.Compatible)
                    {
                        // This is to support the special case where the spec asks for a minor version that does not exist, but a newer compatible minor exists
                        // In this case, we should pick the lowest compatible minor version
                        int lowestCompatibleMinor = allVersions.Select(v => v.Minor)
                                                               .Where(m => m >= packageSpecifier.Version.Minor)
                                                               .OrderBy(m => m).FirstOrDefault();
                        allVersions = allVersions.Where(p => p.Minor == lowestCompatibleMinor);
                    }
                    else
                        allVersions = allVersions.Where(p => p.Minor == packageSpecifier.Version.Minor);
                }

                if (packageSpecifier.Version.PreRelease != null)
                    allVersions = allVersions.Where(p => ComparePreReleaseType(p.PreRelease, packageSpecifier.Version.PreRelease) == 0);
                else
                    allVersions = allVersions.Where(s => s.PreRelease == null);
            }

            if (!allVersions.Any())
                return null;
            return allVersions.OrderByDescending(v => v).FirstOrDefault();
        }

        private static int ComparePreReleaseType(string p1, string p2)
        {
            if (p1 == p2) return 0;

            if (string.IsNullOrEmpty(p1) && string.IsNullOrEmpty(p2)) return 0;
            if (string.IsNullOrEmpty(p1)) return 1;
            if (string.IsNullOrEmpty(p2)) return -1;

            var identifiers1 = p1.Split('.');
            var identifiers2 = p2.Split('.');

            if (identifiers1[0] == identifiers2[0])
                return 0;

            return string.Compare(identifiers1[0], identifiers2[0]);
        }
    }


    /// <summary>
    /// Dependency Graph structure. PackageDefs are vertices and edges between vertices are defined as PackageDef references to both vertices along with a PackageSpecifer
    /// defining the requirement of the 'From' vertex.
    /// </summary>
    internal class DependencyGraph
    {
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

        internal string CreateDotNotation()
        {
            var rootEdges = edges.Where(s => s.From is RootVertex);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("digraph{");
            List<DependencyEdge> visited = new List<DependencyEdge>();
            List<string> verticesWritten = new List<string>();
            foreach (var edge in rootEdges)
            {
                visited.Add(edge);
                createDotNotation(edge, stringBuilder, visited, verticesWritten);
            }
            stringBuilder.Append("}");

            return string.Join(Environment.NewLine, stringBuilder.ToString());
        }

        private void createDotNotation(DependencyEdge edge, StringBuilder stringBuilder, List<DependencyEdge> visited, List<string> verticesWritten)
        {
            string from = edge.From is RootVertex Root ? Root.Name : $"{edge.From.Name} {edge.From.Version.ToString(4)}";
            string to = edge.To == Unknown || edge.To is ConflictVertex ? $"{edge.PackageSpecifier.Name}" : $"{edge.To.Name} {edge.To.Version.ToString(4)}";

            if ((edge.To != Unknown && !edge.PackageSpecifier.IsCompatible(edge.To)) || edge.To is ConflictVertex)
                stringBuilder.Append($"\"{from}\" -> \"{to}\" [label=\"{edge.PackageSpecifier.Version.ToString(4)}\",color=red];");
            else
                stringBuilder.Append($"\"{from}\" -> \"{to}\" [label=\"{edge.PackageSpecifier.Version.ToString(4)}\"];");

            if (edge.From is RootVertex)
            {
                if (!verticesWritten.Contains(edge.From.Name))
                {
                    stringBuilder.Append($"\"{edge.From.Name}\"[shape=square];");
                    verticesWritten.Add(edge.From.Name);
                }
            }

            if (edge.To is UnknownVertex)
            {
                if (!verticesWritten.Contains(edge.PackageSpecifier.Name))
                {
                    stringBuilder.Append($"\"{edge.PackageSpecifier.Name}\"[color=orange,style=dashed];");
                    verticesWritten.Add(edge.PackageSpecifier.Name);
                }
            }
            else if (edge.To is ConflictVertex)
            {
                if (!verticesWritten.Contains(edge.PackageSpecifier.Name))
                {
                    stringBuilder.Append($"\"{edge.PackageSpecifier.Name}\"[style=dashed];");
                    verticesWritten.Add(edge.PackageSpecifier.Name);
                }
            }
            if (edge.To is UnknownVertex || edge.To is ConflictVertex)
                return;

            foreach (var dependency in TraverseEdges().Where(s => s.From == edge.To).Distinct())
            {
                if (!visited.Contains(dependency))
                {
                    visited.Add(dependency);
                    createDotNotation(dependency, stringBuilder, visited, verticesWritten);
                }
            }
        }

        internal IEnumerable<DependencyEdge> TraverseEdges()
        {
            var visited = new HashSet<DependencyEdge>();
            var queue = new Queue<DependencyEdge>();
            foreach (var edge in edges.Where(s => s.From is RootVertex))
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
        public RootVertex(string name)
        {
            Name = name;
        }
    }

    internal class UnresolvedVertex : PackageDef
    {

    }

    internal class UnknownVertex : PackageDef
    {

    }

    internal class ConflictVertex : PackageDef
    {

    }

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

        public override string ToString()
        {
            return $"Edge: {PackageSpecifier.Name} needs {PackageSpecifier.Version}, resolved {To.Version}";
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
            if (spec == VersionSpecifier.Any) return true;
            if (other == VersionSpecifier.Any) return false;
            SemanticVersion semanticVersion = new SemanticVersion(other.Major ?? 0, other.Minor ?? 0, other.Patch ?? 0, other.PreRelease, other.BuildMetadata);
            if (other.Patch == null || other.Minor == null)
            {
                var spec2 = new VersionSpecifier(other.Major.HasValue ? spec.Major : 0,
                    other.Minor.HasValue ? spec.Minor : 0
                    , other.Patch.HasValue ? spec.Patch : (spec.Patch.HasValue ? (int?)0 : null), spec.PreRelease, spec.BuildMetadata, spec.MatchBehavior);
                return spec2.IsCompatible(semanticVersion);
            }
            var ok = spec.IsCompatible(semanticVersion);

            return ok;
        }

        public static bool IsSuperSetOf(this VersionSpecifier spec, VersionSpecifier other)
        {
            if (!spec.IsSatisfiedBy(other)) return false;
            if (spec == other) return true;
            if (spec == VersionSpecifier.Any) return true;
            if (other == VersionSpecifier.Any) return false;
            if (spec.Major.HasValue == false && other.Major.HasValue) return true;
            if (spec.Minor.HasValue == false && other.Minor.HasValue) return true;
            if (spec.Patch.HasValue == false && other.Patch.HasValue) return true;
            return false;
        }
    }
}
