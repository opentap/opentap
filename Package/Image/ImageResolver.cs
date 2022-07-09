using System.Collections.Generic;
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
    
    internal class ImageResolution
    {
        public ImageResolution(PackageSpecifier[] pkgs, long iterations)
        {
            Packages = pkgs.OrderBy(x => x.Name).ToArray();
            this.Iterations = iterations;
        }
        public readonly PackageSpecifier[] Packages;
        public readonly long Iterations;
    }
    
    internal class ImageResolver
    {

        public long Iterations = 0; 

        
        public ImageResolution ResolveImage(ImageSpecifier image, PackageDependencyGraph graph)
        {
            Iterations++;
            List<PackageSpecifier> packages = image.Packages.ToList();
            startOver:
            // make sure that specifications are consistent.
            // 1. remove redundant package specifiers
            for (int i = 0; i < packages.Count; i++)
            {
                var pkg1 = packages[i];
                retry:
                for (int j = i + 1; j < packages.Count; j++)
                {
                    var pkg2 = packages[j];
                    if (pkg2.Name == pkg1.Name)
                    {
                        if (!pkg2.Version.IsSatisfiedBy(pkg1.Version) && !pkg1.Version.IsSatisfiedBy(pkg2.Version))
                            return null;

                        if (pkg2.Version.IsSatisfiedBy(pkg1.Version))
                        {
                            packages[i] = pkg2;
                            packages.RemoveAt(j);
                            goto retry;
                        }
                    }
                }
            }

            //2. expand dependecies for exact package specifiers
            bool modified = true;
            while (modified)
            {
                modified = false;
                for (int i = 0; i < packages.Count; i++)
                {
                    var pkg1 = packages[i];
                    if (pkg1.Version.TryToSemanticVersion(out var v) == false)
                        continue;

                    var deps = graph.GetDependencies(pkg1.Name, v);
                    foreach (var dep in deps)
                    {
                        for (int j = 0; j < packages.Count; j++)
                        {
                            var pkg2 = packages[j];
                            if (pkg2.Name == dep.Name)
                            {
                                // this dependency can be satisfied?
                                if (!pkg2.Version.IsSatisfiedBy(dep.Version) &&
                                    !dep.Version.IsSatisfiedBy(pkg2.Version))
                                {
                                    // this dependency is unresolvable
                                    return null;
                                }

                                // this dependency is more specific than the existing.
                                if (pkg2.Version != dep.Version && pkg2.Version.IsSuperSetOf(dep.Version) && pkg2.Version.MatchBehavior != VersionMatchBehavior.Exact)
                                {
                                    packages[j] = dep;
                                    modified = true;
                                    break;
                                }
                            }
                        }

                        if (!packages.Any(x => x.Name == dep.Name))
                        {
                            packages.Add(dep);
                            modified = true;
                        }
                    }
                }
            }

            List<SemanticVersion[]> allVersions = new List<SemanticVersion[]>();

            // 3. foreach package specifier get all the available versions
            for (int i = 0; i < packages.Count; i++)
            {
                var pkg = packages[i];
                var pkgs = graph.PackagesSatisfying(pkg).ToArray();
               allVersions.Add(pkgs);
            }

            // 4. prune away the versions which dependencies conflict with the required packages.
            // ok, now we know the results is some pair-wise combination of allVersions.
            // now lets try pruning them a bit
            bool retry = false;
            for (int i = 0; i < packages.Count; i++)
            {
                var pkg = packages[i];
                var versions = allVersions[i];
                var others = packages.Except(x => x == pkg).ToArray();
                var newVersions = versions.Where(x => graph.CouldSatisfy(pkg.Name, new VersionSpecifier(x, VersionMatchBehavior.Exact), others)).ToArray();
                //if (newVersions.Length != versions.Length)
                {
                    allVersions[i] = newVersions;
                    if (newVersions.Length == 1 && pkg.Version.MatchBehavior != VersionMatchBehavior.Exact)
                    {
                        packages[i] = new PackageSpecifier(pkg.Name, new VersionSpecifier(newVersions[0], VersionMatchBehavior.Exact));
                        retry = true;
                    }
                }
            }

            if (retry)
            {
                goto startOver;
            }
            
            // ok now we have X * Y * Z * ... = K possible solutions all satisfying the constraints.
            // Lets sort all the versions based on version specifiers, then fix  the version and try each combination (brute force)
            long k = allVersions.FirstOrDefault()?.LongLength ?? 0;
            for (int i = 1; i < allVersions.Count; i++)
            {
                k *= allVersions[i].LongLength;
            }

            if (k == 0) return null; // no possible solutions
            if (k == 1)
            {
                bool allExact = allVersions.All(x => x.Length  == 1);
                if (allExact)
                {
                    // this is the final case.
                    return new ImageResolution(packages.ToArray(), Iterations);
                }
            }
            
            // sort the versions based on priorities. ^ -> sort ascending, Exact, but undermined e.g  (9.17.*), sort descending.
            
            for (int i = 0; i < allVersions.Count; i++)
            {
                var pkg = packages[i];
                var versions = allVersions[i].ToList();
                
                if (pkg.Version == VersionSpecifier.Any)
                {
                    versions.Sort();
                    // any version -> take the newest first.
                    versions.Reverse();
                }else if(pkg.Version.MatchBehavior == VersionMatchBehavior.Exact)
                {
                    versions.Sort(pkg.Version.SortOrder);
                }else if (pkg.Version.MatchBehavior == VersionMatchBehavior.Compatible)
                {
                    versions = versions.OrderBy(x => x, pkg.Version.SortPartial).ToList();
                }

                allVersions[i] = versions.ToArray();
            }

            // iterate fixing variables
            for (int i = 0; i < allVersions.Count; i++)
            {
                var set = allVersions.Select(x => x[0]).ToArray();
                var pkgVersions = allVersions[i];
                if (pkgVersions.Length == 1) continue; // skip all exact versions
                for (int j = 0; j < pkgVersions.Length; j++)
                {
                    set[i] = pkgVersions[j];

                    var newSpecifier = new ImageSpecifier();
                    for (int k3 = 0; k3 < allVersions.Count; k3++)
                    {
                        newSpecifier.Packages.Add(packages[k3]);
                    }
                    for (int k2 = 0; k2 < pkgVersions.Length; k2++)
                    {
                        
                        newSpecifier.Packages[i] = new PackageSpecifier(packages[i].Name, new VersionSpecifier(allVersions[i][k2], VersionMatchBehavior.Exact));

                        // recursive to see if this specifier has a result.
                        var result = this.ResolveImage(newSpecifier, graph);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
            }

            return null;
        }
    }


    public class PackageDependencyGraph
    {
        List<string> names = new List<string>();
        List<SemanticVersion> versionLookup = new List<SemanticVersion>();
        List<VersionSpecifier> versionLookup2 = new List<VersionSpecifier>();
        Dictionary<SemanticVersion, int> version2Id = new Dictionary<SemanticVersion, int>();
        Dictionary<VersionSpecifier, int> version3Id = new Dictionary<VersionSpecifier, int>();

        Dictionary<string, SemanticVersion> stringToVersion = new Dictionary<string, SemanticVersion>();
        Dictionary<string, VersionSpecifier> stringToVersion2 = new Dictionary<string, VersionSpecifier>();

        Dictionary<string, int> name2Id = new Dictionary<string, int>(); 
        Dictionary<int, List<int>> versions = new Dictionary<int, List<int>>();
        Dictionary<(int, int), (int, int)[]> dependencies = new Dictionary<(int, int), (int, int)[]>();

        int getId(ref string name)
        {
            if (name2Id.TryGetValue(name, out var id))
            {
                name = names[id];
            }
            else
            {
                name2Id[name] = names.Count;
                id = names.Count;
                versions[id] = new List<int>();
                names.Add(name);
            }

            return id;
        }
        
        int getVersion(ref SemanticVersion v)
        {
            if (version2Id.TryGetValue(v, out var id))
                return id;
            version2Id[v] = id = versionLookup.Count;
            versionLookup.Add(v);
            return id;
        }
        
        int getVersion(string v2)
        {
            var v = stringToVersion.GetOrCreateValue(v2, SemanticVersion.Parse);
            return getVersion(ref v);
        }

        int getVersionSpecifier(string v2)
        {
            var v = stringToVersion2.GetOrCreateValue(v2, VersionSpecifier.Parse);
            return getVersionSpecifier(ref v);
        }
        
        int getVersionSpecifier(ref VersionSpecifier v)
        {
            if (version3Id.TryGetValue(v, out var id))
                return id;
            version3Id[v] = id = versionLookup2.Count;
            versionLookup2.Add(v);
            return id;
        }

        
        public void LoadFromJson(JsonDocument json)
        {
            var packages = json.RootElement.GetProperty("packages");

            foreach (var elem in packages.EnumerateArray())
            {
                var name = elem.GetProperty("name").GetString();
                var version = elem.GetProperty("version").GetString();
                if (!SemanticVersion.TryParse(version, out var v))
                    continue;

                var id = getId(ref name);
                var thisVersions = versions[id];
                thisVersions.Add(getVersion(version));
                if(elem.TryGetProperty("dependencies", out var obj))
                {
                    var l = obj.GetArrayLength();
                    if (l != 0)
                    {
                        var deps = new (int id, int y)[l];
                        int i = 0; 
                        foreach (var dep in obj.EnumerateArray())
                        {
                            var depname = dep.GetProperty("name").GetString();
                            var depid = getId(ref depname);
                            var depver = dep.GetProperty("version").GetString();
                            deps[i] = (depid, getVersionSpecifier(depver));
                            i++;
                        }

                        this.dependencies[(id, getVersion(version))] = deps;
                    }
                    
                }
            }
        }

        public IEnumerable<SemanticVersion> PackagesSatisfying(PackageSpecifier packageSpecifier)
        {
            if (name2Id.TryGetValue(packageSpecifier.Name, out var Id))
            {
                var thisVersions = versions[Id];
                foreach (var v in thisVersions)
                {
                    var semv = versionLookup[v];
                    {
                        if (packageSpecifier.Version == VersionSpecifier.Any)
                        {
                            if (semv.PreRelease == null)
                                yield return semv;
                        }else if (packageSpecifier.Version.IsCompatible(semv))
                            yield return semv;
                        
                    }
                }
            }
        }

        public bool CouldSatisfy(string pkgName, VersionSpecifier version, PackageSpecifier[] others)
        {
            if (name2Id.TryGetValue(pkgName, out var Id) && version.TryToSemanticVersion(out var v))
            {
                if (!dependencies.TryGetValue((Id, getVersion(ref v)), out var deps))
                    return true; // this package has no dependencies, so yes.
                foreach (var dep in deps)
                {
                    var depVersion = versionLookup2[dep.Item2];
                    
                    var ps = new PackageSpecifier(names[dep.Item1], depVersion);
                    var o = others.FirstOrDefault(x => x.Name == ps.Name);
                    if (o == null)
                        continue;
                    if (ps.Version.IsSatisfiedBy(o.Version) == false && o.Version.IsSatisfiedBy(ps.Version) == false)
                        return false;
                    
                    // todo protect from circular dependencies here.
                    if (!CouldSatisfy(ps.Name, depVersion, others))
                        return false;
                }

                return true;
            }

            return true;
        }

        public IEnumerable<PackageSpecifier> GetDependencies(string pkgName, SemanticVersion version)
        {
            if (name2Id.TryGetValue(pkgName, out var Id))
            {
                if (dependencies.TryGetValue((Id, getVersion(ref version)), out var deps))
                {
                    foreach (var dep in deps)
                    {
                        var depVersion = versionLookup2[dep.Item2];

                        yield return new PackageSpecifier(names[dep.Item1], depVersion);
                    }
                }
            }
        }
    }

    public class PackageDependencyQuery
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
        private static HttpClient GetHttpClient(string url)
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
                
                var response = await client.SendAsync(request); 
                var stream = await response.Content.ReadAsStreamAsync();
                json = await JsonDocument.ParseAsync(stream);
            }

            var graph = new PackageDependencyGraph();
            graph.LoadFromJson(json);
            using (var f = File.OpenWrite("opentap-packages.json"))
            {
                using (var writer = new Utf8JsonWriter(f))
                {
                    json.WriteTo(writer);
                }
                     
                
            }
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

    class HttpStringContent : HttpContent
    {
        readonly byte[] content;
        public HttpStringContent(string content)
        {
            this.content = Encoding.UTF8.GetBytes(content);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return stream.WriteAsync(content, 0, content.Length);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = content.LongLength;
            return true;
        }
    }

    public class PackageDependencyCache
    {
        
    }
}