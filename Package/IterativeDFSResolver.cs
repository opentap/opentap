using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OpenTap.Package
{
    /// <summary>
    /// Finds dependencies for specified packages in Package Repositories.
    /// If a given node turns out to be unresolvable, this resolver will back up the tree
    /// to an earlier stage and retry the dependency resolution from this point.
    /// </summary>
    internal class IterativeDFSResolver : IDependencyResolver
    {
        private Dictionary<string, PackageDef> InstalledPackages { get; } = new Dictionary<string, PackageDef>();
        DependencyGraph graph = new DependencyGraph();

        public IterativeDFSResolver(List<PackageSpecifier> packages, List<IPackageRepository> repositories, CancellationToken cancellationToken)
        {
            Packages = packages;
            Repositories = repositories;
            CancellationToken = cancellationToken;
        }

        public List<PackageDef> Dependencies { get; } = new List<PackageDef>();
        public List<PackageDef> MissingDependencies { get; } = new List<PackageDef>();
        public List<PackageDependency> UnknownDependencies { get; } = new List<PackageDependency>();
        public List<Exception> DependencyIssues { get; } = new List<Exception>();
        public List<PackageSpecifier> Packages { get; set; }
        public List<IPackageRepository> Repositories { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public string GetDotNotation() => graph.CreateDotNotation();
        public void Resolve()
        {
            foreach (var specifier in Packages)
                graph.AddEdge(new RootVertex("Root"), DependencyGraph.Unresolved, specifier);
            resolveGraph();
            CategorizeResolvedPackages();
        }

        private void resolveGraph()
        {
            bool MegaResolver(PackageSpecifier packageSpecifier)
            {
                return false;
            }

            while (!CancellationToken.IsCancellationRequested)
            {
                var unresolved = graph.TraverseEdges()
                    .Where(s => s.To is UnresolvedVertex)
                    .GroupBy(s => s.PackageSpecifier.Name)
                    .ToArray();

                if (!unresolved.Any())
                    break;

                foreach (var edge in graph.TraverseEdges())
                {

                }
            }
        }

        /// <summary>
        /// Populates Dependencies, UnknownDependencies and DependencyIssues based on resolved dependency tree
        /// </summary>
        private void CategorizeResolvedPackages()
        {
            var traversedEdges = graph.TraverseEdges().ToArray();
            Dependencies.AddRange(traversedEdges.Where(p => !(p.To is UnknownVertex) && !InstalledPackages.Any(s => s.Value == p.To)).Select(s => s.To).Distinct());
            UnknownDependencies.AddRange(traversedEdges.Where(s => s.To is UnknownVertex).Select(p => new PackageDependency(p.PackageSpecifier.Name, p.PackageSpecifier.Version)).Distinct());

            // A dependency is only "missing" if it is not installed and has to be downloaded from a repository
            MissingDependencies.AddRange(Dependencies.Where(s => !InstalledPackages.Any(p => p.Key == s.Name && s.Version == p.Value.Version)));

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
    }
}