using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OpenTap.Authentication;

namespace OpenTap.Package
{
    /// <summary>
    /// This class takes care of making the right package query queries to the repository server.
    /// </summary>
    static class PackageDependencyQuery
    {
        static string GraphQueryPackages(string os, CpuArchitecture arch, string version, string name) =>
            @"query Query { 
                packages(version: ""__VERSION__"", type:""tappackage"", os:""__OS__"", architecture:""__ARCH__"" __NAME_QUERY__) {
                  name version dependencies { 
                   name version
        }}}".Replace("__OS__", os)
                .Replace("__ARCH__", arch.ToString())
                .Replace("__VERSION__", version)
                .Replace("__NAME_QUERY__", name == null ? "" : $", name: \"{name}\"");
        static HttpClient GetHttpClient()
        {
            var httpClient = AuthenticationSettings.Current.GetClient(null, true);
            httpClient.DefaultRequestHeaders.Add("OpenTAP",
                PluginManager.GetOpenTapAssembly().SemanticVersion.ToString());
            return httpClient;
        }

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
        public static async Task<PackageDependencyGraph> QueryGraph(string repoUrl, string os,
            CpuArchitecture deploymentInstallationArchitecture, string preRelease = "", string name = null)
        {
            var sw = Stopwatch.StartNew();
            var qs = GraphQueryPackages(os, deploymentInstallationArchitecture, preRelease, name);
            JsonDocument json;
            using (var client = GetHttpClient())
            {
                // query the package dependency graph as GZip compressed JSON code.
                
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, repoUrl + "/3.1/Query");
                request.Content = new StringContent(qs, Encoding.UTF8);
                request.Headers.Add("Accept", "application/json");
                // request a gzip compressed response - otherwise it will be several MB.
                request.Headers.Add("Accept-encoding", "gzip");
                
                var response = await client.SendAsync(request); 
                var stream = await response.Content.ReadAsStreamAsync();
                // the gzip Accept-encoding is not mandatory for the server, so we must check if the content
                // encoding contains gzip.
                if (response.Content.Headers.ContentEncoding.Contains("gzip"))
                    stream = new GZipStream(stream, CompressionMode.Decompress);
                
                json = await JsonDocument.ParseAsync(stream);
                if (response.IsSuccessStatusCode == false)
                {
                    if (json.RootElement.TryGetProperty("message", out var msg))
                        throw new Exception(msg.GetString());
                    throw new Exception(json.RootElement.ToString());
                }
            }

            var graph = new PackageDependencyGraph();
            graph.LoadFromJson(json);
            log.Debug(sw, "{1} -> found {0} packages.", graph.Count, qs);
            return graph;
        }

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