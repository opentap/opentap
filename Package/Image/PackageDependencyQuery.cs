using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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
            var httpClient = HttpPackageRepository.GetAuthenticatedClient(new Uri(repoUrl, UriKind.Absolute)).HttpClient;
            httpClient.DefaultRequestHeaders.Add("WWW-Authenticate", "token");
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var parameters = HttpPackageRepository.GetQueryParameters(
                version: VersionSpecifier.TryParse(preRelease, out var spec) ? spec : VersionSpecifier.AnyRelease,
                os: os,
                architecture: deploymentInstallationArchitecture, name: name);

            string maybeQuote(object o)
            {
                if (o is string s)
                    return $"\"{s}\"";
                return o.ToString();
            }

            var parameterString = string.Join(", ", parameters.Select(kvp => $"{kvp.Key}: {maybeQuote(kvp.Value)}"));

            var graphqlQuery = $@"query Query {{ 
    objects({parameterString}) {{
        name
        version
        dependencies {{
            name
            version
        }}
    }}
}}";
            var postTo = repoUrl.TrimEnd('/') + "/4.0/query";
            var req = new HttpRequestMessage(HttpMethod.Post, postTo);
            req.Content = new StringContent(graphqlQuery, Encoding.UTF8, "application/json");
            req.Headers.Add("Accept", "application/json");
            var res = httpClient.SendAsync(req).Result;
            if (res.IsSuccessStatusCode)
            {
                var graph = new PackageDependencyGraph();
                var packages = res.Content.ReadAsStringAsync().Result;
                {
                    var json = JsonDocument.Parse(packages);
                    var data = json.RootElement.GetProperty("data");
                    var objects = data.GetProperty("objects");
                    // The result will be wrapped in { "data": { "objects": ... } }
                    // We need to unwrap it a bit
                    // json = json.RootElement.GetProperty()
                    graph.LoadFromJson(objects);
                }
                log.Debug(sw, "{1} -> found {0} packages.", graph.Count, JsonSerializer.Serialize(parameters));
                return graph;
            }

            if (res.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (res.Headers.TryGetValues("WWW-Authenticate", out var authErrors))
                {
                    // This is usually a single value similar to:
                    // Token error=invalid_token, error_description=Invalid User Token.
                    var errors = authErrors.ToArray();
                    foreach (var err in errors)
                    {
                        if (err.Contains("error_description="))
                        {
                            var errstring = err.Split('=').LastOrDefault();
                            if (errstring != null) throw new Exception($"Unauthorized: {errstring}");
                        }
                    }

                    // This should not happen, but to be safe
                    var fallback = string.Join("\n", errors);
                    throw new Exception($"Unauthorized: {fallback}");
                }
            } 
            throw new Exception($"{(int)res.StatusCode} {res.StatusCode}.");
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
            var packages = doc.RootElement.GetProperty("packages");
            graph.LoadFromJson(packages);
            if (compressed)
                stream.Dispose();
            return graph;
        }
    }
}