using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenTap.Authentication;
using OpenTap.Repository.Client;

namespace OpenTap.Package
{
    /// <summary>
    /// This class takes care of making the right package query queries to the repository server.
    /// </summary>
    static class PackageDependencyQuery
    {
        static readonly TraceSource log = Log.CreateSource("GraphQL");
        
        /// <summary>
        /// Queries the repository for a package dependency graph. If neither version nor name is specified, it will query
        /// for all release versions of all packages. 
        /// </summary>
        /// <param name="repoUrl"></param>
        /// <param name="os"></param>
        /// <param name="deploymentInstallationArchitecture"></param>
        /// <param name="preRelease">If specified, it expands the versions to include that preRelease. E.g alpha (everything), beta(releases, RCs and betas), rc (releases and RCs). </param>
        /// <param name="name">If specified, limits the query to only packages of that name.</param>
        /// <returns></returns>
        public static PackageDependencyGraph QueryGraph(string repoUrl, string os,
            CpuArchitecture deploymentInstallationArchitecture, string preRelease = "", string name = null)
        {
            var sw = Stopwatch.StartNew();
            var repoClient = HttpPackageRepository.GetAuthenticatedClient(new Uri(repoUrl, UriKind.Absolute));
            
            var parameters = HttpPackageRepository.GetQueryParameters(version: VersionSpecifier.TryParse(preRelease, out var spec) ? spec : VersionSpecifier.AnyRelease, os: os,
                architecture: deploymentInstallationArchitecture, name: name);
            
            var result = repoClient.Query(parameters, CancellationToken.None, "name", "version", "class",
                new QuerySelection("dependencies", new List<QuerySelection>() { "name", "version" }));
            var graph = new PackageDependencyGraph();
            graph.LoadFromDictionaries(result);
            log.Debug(sw, "{1} -> found {0} packages.", graph.Count, JsonSerializer.Serialize(parameters));
            return graph;
        }

        /// <summary>
        /// Only used by unit tests
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="compressed"></param>
        /// <returns></returns>
        public static PackageDependencyGraph LoadGraph(Stream stream, bool compressed)
        {
            if(compressed)
                stream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true); 
            
            var graph = new PackageDependencyGraph();
            var doc = JsonDocument.Parse(stream);
            graph.LoadFromJson(doc);
            if (compressed)
                stream.Dispose();
            return graph;
        }
    }
}