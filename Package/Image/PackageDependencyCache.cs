using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenTap.Package
{
    class PackageDependencyCache
    {
        readonly string os;
        readonly CpuArchitecture deploymentInstallationArchitecture;
        readonly PackageDependencyGraph graph = new PackageDependencyGraph();
        public PackageDependencyGraph Graph => graph;
        public List<string> Repositories { get; private set; } = new List<string>();
        static readonly  TraceSource log = Log.CreateSource("Package Query");

        public PackageDependencyCache(string os, CpuArchitecture deploymentInstallationArchitecture)
        {
            this.os = os;
            this.deploymentInstallationArchitecture = deploymentInstallationArchitecture;
        }
        
        public void LoadFromRepositories()
        {
            var urls0 = new List<string> {  PackageCacheHelper.PackageCacheDirectory};
            var urls = PackageManagerSettings.Current.Repositories.Where(x => x.IsEnabled).Select(x => x.Url).ToList();
            
            urls0.AddRange(urls);
            Repositories = urls0;
            
            var repositories = urls0.Select(PackageRepositoryHelpers.DetermineRepositoryType).ToArray();
            
            foreach (var graph in repositories.AsParallel().Select(GetGraph))
            {
                this.graph.Absorb(graph);   
            }
        }
        
        PackageDependencyGraph GetGraph(IPackageRepository repo)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (repo is HttpPackageRepository http)
                {
                    return PackageDependencyQuery.QueryGraph(http.Url, os, deploymentInstallationArchitecture).Result;
                }

                if (repo is FilePackageRepository fpkg)
                {
                    var graph = new PackageDependencyGraph();
                    var packages = fpkg.GetAllPackages(TapThread.Current.AbortToken);
                    graph.LoadFromPackageDefs(packages);
                    return graph;
                }
            }
            finally
            {
                log.Debug(sw, "Read packages from {0}", repo);
            }

            return null;
        }
    }
}