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
        static string GraphQueryPackages(string os, CpuArchitecture arch) => 
            @"query Query { 
                packages(version: ""any"", type:""tappackage"", os:""__OS__"", architecture:""__ARCH__"") {
                  name version dependencies { 
                   name version
        }}}".Replace("__OS__", os).Replace("__ARCH__", arch.ToString());
        static HttpClient GetHttpClient()
        {
            var httpClient = AuthenticationSettings.Current.GetClient(null, true);
            httpClient.DefaultRequestHeaders.Add("OpenTAP",
                PluginManager.GetOpenTapAssembly().SemanticVersion.ToString());
            return httpClient;
        }
        public static async Task<PackageDependencyGraph> QueryGraph(string repoUrl, string os,
            CpuArchitecture deploymentInstallationArchitecture)
        {
            JsonDocument json;
            using (var client = GetHttpClient())
            {
                // query the package dependency graph as GZip compressed JSON code.
                
                var qs = GraphQueryPackages(os, deploymentInstallationArchitecture);
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
            }

            var graph = new PackageDependencyGraph();
            graph.LoadFromJson(json);
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