using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OpenTap.Authentication;

namespace OpenTap.Package
{
    class PackageDependencyQuery
    {
        private const string graphQLQuery = @"query Query { packages(version: ""any"", os:""linux"", architecture:""x64"") {
        name
            version
        dependencies
        { name
            version
        }
    }
}";
        static HttpClient GetHttpClient(string url)
        {
            var httpClient = AuthenticationSettings.Current.GetClient(null, true);
            httpClient.DefaultRequestHeaders.Add("OpenTAP",
                PluginManager.GetOpenTapAssembly().SemanticVersion.ToString());
            httpClient.DefaultRequestHeaders.Add(HttpRequestHeader.Accept.ToString(), "application/xml");
            
            return httpClient;
        }
        public static async Task<PackageDependencyGraph> QueryGraph(string repoUrl)
        {
            JsonDocument json;
            using (var client = GetHttpClient(repoUrl))
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, repoUrl + "/3.1/Query");
                request.Content = new StringContent(graphQLQuery, Encoding.UTF8);
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("Accept-encoding", "gzip");
                
                var response = await client.SendAsync(request); 
                var stream = await response.Content.ReadAsStreamAsync();
                if (response.Content.Headers.ContentEncoding.Contains("gzip"))
                    stream = new GZipStream(stream, CompressionMode.Decompress);
                
                json = await JsonDocument.ParseAsync(stream);
            }

            var graph = new PackageDependencyGraph();
            graph.LoadFromJson(json);
            return graph;
        }

        public static void Query()
        {
            var managers  = PackageManagerSettings.Current.Repositories.Where(p => p.IsEnabled).Select(s => s.Manager);

            foreach (var manager in managers)
            {
                if (manager is FilePackageRepository frep)
                {
                    
                }
                else if (manager is HttpPackageRepository hrep)
                {
                    var pkgGraph = hrep.QueryGraphQL(graphQLQuery);
                }
                
            }
            
            foreach (var pkgManager in PackageManagerSettings.Current.Repositories)
            {
                

            }
            
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