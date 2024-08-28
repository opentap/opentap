using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    class PackageDependencyCache
    {
        readonly string os;
        readonly CpuArchitecture deploymentInstallationArchitecture;
        readonly PackageDependencyGraph graph = new PackageDependencyGraph();
        public PackageDependencyGraph Graph => graph;
        public List<string> Repositories { get; }
        static readonly TraceSource log = Log.CreateSource("Package Query");

        public PackageDependencyCache(string os, CpuArchitecture deploymentInstallationArchitecture, IEnumerable<string> repositories = null)
        {
            graph.UpdatePrerelease = UpdatePrerelease;
            this.os = os;
            this.deploymentInstallationArchitecture = deploymentInstallationArchitecture;
            var urls = PackageManagerSettings.Current.GetEnabledRepositories(repositories).Select(x => x.Url).ToList();
            Repositories = urls;
        }

        private void UpdatePrerelease(string name, string version)
        {
            var graphs2 = graphs
                .Where(g => repos[g] is HttpPackageRepository)
                .AsParallel()
                .Select(graph =>
                {
                    var graph2 = PackageDependencyQuery.QueryGraph(repos[graph].Url, os, deploymentInstallationArchitecture,
                        version, name);
                    graph.Absorb(graph2);
                    return graph2;
                });
            foreach (var g in graphs2)
            {
                Graph.Absorb(g);
            }
        }

        readonly List<PackageDependencyGraph> graphs = new List<PackageDependencyGraph>();
        readonly Dictionary<PackageDependencyGraph, IPackageRepository> repos =
            new Dictionary<PackageDependencyGraph, IPackageRepository>();
        public void LoadFromRepositories()
        {
            Exception GetInnermostException(Exception ex)
            {
                return ex switch
                {
                    AggregateException aex when aex.InnerExceptions.Count == 1 => GetInnermostException(
                        aex.InnerException),
                    TargetInvocationException tex => GetInnermostException(tex.InnerException),
                    _ => ex
                };
            }

            var repositories = Repositories.Select(PackageRepositoryHelpers.DetermineRepositoryType).ToArray();
            graphs.Clear();
            var graphList = repositories.AsParallel().TrySelect(repo => (graph: GetGraph(repo), repo: repo),
                (ex, repo) =>
                {
                    var innermost = GetInnermostException(ex);
                    if (innermost is TaskCanceledException or OperationCanceledException)
                    {
                        log.Warning($"Timeout while querying '{repo.Url}': {innermost.Message}", innermost);
                    } 
                    else if (innermost is PackageQueryException)
                    {
                        log.Warning($"Error querying '{repo.Url}': {innermost.Message}", innermost);
                    }
                    else
                    {
                        log.Warning($"Repository is unreachable: {innermost.Message}", innermost);
                    }
                });
            foreach (var r in graphList)
            {
                if (r.graph == null)
                    continue; // error while querying repo.
                graphs.Add(r.graph);
                repos[r.graph] = r.repo;
                graph.Absorb(r.graph);   
            }
        }

        List<PackageDef> addedPackages = new List<PackageDef>();
        public void AddPackages(IEnumerable<PackageDef> packages)
        {
            var graph = new PackageDependencyGraph();
            graph.LoadFromPackageDefs(packages);
            this.graph.Absorb(graph);
            addedPackages.AddRange(packages);
        }
        
        PackageDependencyGraph GetGraph(IPackageRepository repo)
        {
            
                if (repo is HttpPackageRepository http)
                {
                    return PackageDependencyQuery.QueryGraph(http.Url, os, deploymentInstallationArchitecture, "");
                } 
                
                if (repo is FilePackageRepository fpkg)
                {
                    var sw = Stopwatch.StartNew();
                    var graph = new PackageDependencyGraph();
                    var packages = fpkg.GetAllPackages(TapThread.Current.AbortToken);
                    graph.LoadFromPackageDefs(packages.Where(x => x.IsPlatformCompatible(deploymentInstallationArchitecture, os)));
                    
                    log.Debug(sw, "Read {1} packages from {0}", repo, packages.Length);
                    
                    return graph;
                }
                
                {
                    // This occurs during unit testing when mock repositories are used.
                    var sw = Stopwatch.StartNew();
                    var graph = new PackageDependencyGraph();
                    var names = repo.GetPackageNames();
                    List<PackageDef> packages = new List<PackageDef>();
                    foreach (var name in names)
                    {
                        foreach (var version in repo.GetPackageVersions(name))
                        {
                            var pkgs = repo.GetPackages(new PackageSpecifier(version.Name, version.Version.AsExactSpecifier(), version.Architecture, version.OS), TapThread.Current.AbortToken);
                            packages.AddRange(pkgs);
                        }
                    }

                    var compatiblePackages = packages
                        .Where(x => x.IsPlatformCompatible(deploymentInstallationArchitecture, os)).ToArray();
                    graph.LoadFromPackageDefs(compatiblePackages);
                    
                    log.Debug(sw, "Read {1} packages from {0}", repo, packages.Count);
                    return graph;
                }
        }

        public PackageDef GetPackageDef(PackageSpecifier packageSpecifier)
        {
            
            if (packageSpecifier.Version.TryAsExactSemanticVersion(out var v) == false)
                return null;
            var ps = new PackageSpecifier(packageSpecifier.Name, packageSpecifier.Version,
                deploymentInstallationArchitecture, os);
            foreach (var graph in graphs)
            {
                if (graph.HasPackage(packageSpecifier.Name, v))
                {

                    if (repos.TryGetValue(graph, out var repo))
                    {
                        var pkgs = repo.GetPackages(ps);
                        if (pkgs.FirstOrDefault() is PackageDef r)
                            return r;
                    }
                }
            }

            return addedPackages.FirstOrDefault(x =>
                x.Name == packageSpecifier.Name && packageSpecifier.Version.IsSatisfiedBy(x.Version.AsExactSpecifier()));
        }
    }
}