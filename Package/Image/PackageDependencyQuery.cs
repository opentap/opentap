using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenTap.Package
{
    internal class PackageQueryException : Exception
    {
        public PackageQueryException(string message) : base(message)
        {

        }
    }

    /// <summary>
    /// This class takes care of making the right package query queries to the repository server.
    /// </summary>
    static class PackageDependencyQuery
    {
        static readonly TraceSource log = Log.CreateSource("GraphQL");
        
        // This dictionary is used to keep trach of a cache key per repo.
        // the cache key is selected to be the available package names.
        static readonly Dictionary<string, string> repoToRepoKey = new();
        
        static string GetRepoKey(string repo)
        {
            if (!repoToRepoKey.TryGetValue(repo, out var names))
            {
                names =  StringHash(string.Join(",", GetAllNames(repo).OrderBy(x => x)));
                repoToRepoKey[repo] = names;
            }
            return names;
        }
        
        // this calculates a hash and also makes sure it can be a filename.
        // some filesystems are not case sensitive, but I'll assume no collisions
        // even if lower and upper case letters are treated the same.
        static string StringHash(string str)
        {
            using var hash = SHA1.Create();
            var r = Convert.ToBase64String(hash.ComputeHash(new MemoryStream(Encoding.UTF8.GetBytes(str))));
            return r.Replace("/", "__");
        }
        
        // gets the cached query from a file if it exists. 
        static bool GetCachedQuery(string repo, string key, out JsonElement json)
        {
            json = default;
            var fileKey = StringHash(repo + "|" + key);
            if (File.Exists(".package-query-cache/" + fileKey))
            {
               
                var sw = Stopwatch.StartNew();
                try
                {
                    json = JsonDocument.Parse(File.ReadAllText(".package-query-cache/" + fileKey)).RootElement;
                    log.Debug(sw, "Loaded query from cache");
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            
            return false;
        }
        

        static bool dirInitialized;
        
        // Caches a the result of a query. 
        // The repoKey is saved so that we can check it when the cache is read later.
        static void CacheQuery(string repo, string key, string repoKey, PackageDependencyGraph json, DateTime date)
        {
            if (!dirInitialized)
            {
                Directory.CreateDirectory(".package-query-cache");
                dirInitialized = true;
            }
            
            var fileKey = StringHash(repo + "|" + key);
            var file = ".package-query-cache/" + fileKey;
            File.Delete(file);
            using var stream = File.OpenWrite(file);
            using var jsonWriter = new Utf8JsonWriter(stream);
            jsonWriter.WriteStartObject();
            jsonWriter.WriteStartObject("data");
            
            // write date and key to the cache.
            jsonWriter.WriteString("date", date);
            jsonWriter.WriteString("repoKey", repoKey);
            
            json.WriteToJson(jsonWriter, "objects");
            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndObject();

        }
        
        // Get the names of all the available packages in the repo.
        // If this ever changes, we have to invalidate the caches for that repo.
        // this may occur if a user is granted access to a package they did not previously see.
        public static IEnumerable<string> GetAllNames(string repoUrl)
        {
            var httpClient = HttpPackageRepository.GetAuthenticatedClient(new Uri(repoUrl, UriKind.Absolute)).HttpClient;
            httpClient.DefaultRequestHeaders.Add("WWW-Authenticate", "token");
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var graphqlQuery = $@"query Query {{ 
    objects(directory:""/Packages/"", distinctName: true) {{
        name
    }}
}}";
            var postTo = repoUrl.TrimEnd('/') + "/4.0/query";
            var req = new HttpRequestMessage(HttpMethod.Post, postTo);
            req.Content = new StringContent(graphqlQuery, Encoding.UTF8, "application/json");
            req.Headers.Add("Accept", "application/json");
            var res = httpClient.SendAsync(req).Result;
            var names = res.Content.ReadAsStringAsync().Result;
            var json = JsonDocument.Parse(names);
            var data = json.RootElement.GetProperty("data");
            var objects = data.GetProperty("objects");
            var allNames = objects.EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToArray();
            return allNames;
        }
        
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
            var repoKey = GetRepoKey(repoUrl);
            var parameterString = string.Join(", ", parameters.Select(kvp => $"{kvp.Key}: {maybeQuote(kvp.Value)}"));

            var graph = new PackageDependencyGraph();
            var searchKey = "";
            if (GetCachedQuery(repoUrl, parameterString, out var json2))
            {
                try
                {
                    
                    var date = json2.GetProperty("data").GetProperty("date").GetDateTime();
                    var repoKey2 = json2.GetProperty("data").GetProperty("repoKey").GetString();
                    if (repoKey2 == repoKey)
                    {
                        // the cache is valid. Now read the json into the graph
                        // and set the search key so that it will only show pacakges newer than the cache.
                        graph.LoadFromJson(json2.GetProperty("data").GetProperty("objects"));
                        searchKey = $@", search: ""UploadedDate > {date.Year}-{date.Month}-{date.Day}""";
                    }
                }
                catch
                {
                    // read cache failed for some reason.
                }
            }
            
            
            var graphqlQuery = $@"query Query {{ 
    objects({parameterString} {searchKey} ) {{
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
            
            int cachedPackages = graph.Count;
            if (res.IsSuccessStatusCode)
            {
            
                var packages = res.Content.ReadAsStringAsync().Result;
                if(packages.Any()){
                    
                    graph.LoadFromJson(JsonDocument.Parse(packages).RootElement.GetProperty("data").GetProperty("objects"));
                    
                    // offset the date so that if it takes some time to upload the package, we dont miss it
                    // even though the upload time might be recorded before the package is actually available.
                    // this may happen across midnight, so just to be on the safe side, we subtract two days.
                    CacheQuery(repoUrl, parameterString, repoKey, graph, DateTime.Now.Subtract(TimeSpan.FromDays(2)));
                }
                log.Debug(sw, "{1} -> found {0} packages ({2} cached).", graph.Count, JsonSerializer.Serialize(parameters), cachedPackages);
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
                            if (errstring != null) throw new PackageQueryException($"Unauthorized: {errstring}");
                        }
                    }

                    // This should not happen, but to be safe
                    var fallback = string.Join("\n", errors);
                    throw new PackageQueryException($"Unauthorized: {fallback}");
                }
            } 
            throw new PackageQueryException($"{(int)res.StatusCode} {res.StatusCode}.");
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
