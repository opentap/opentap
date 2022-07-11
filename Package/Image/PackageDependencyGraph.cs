using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OpenTap.Package
{
    class PackageDependencyGraph
    {
        // all the existing names and versions
        readonly List<string> names = new List<string>();
        readonly List<SemanticVersion> versionLookup = new List<SemanticVersion>();
        readonly List<VersionSpecifier> versionLookup2 = new List<VersionSpecifier>();
     
        // lookup of X to id, ID being the index in the above tables.
        readonly Dictionary<string, int> name2Id = new Dictionary<string, int>(); 
        readonly Dictionary<SemanticVersion, int> version2Id = new Dictionary<SemanticVersion, int>();
        readonly Dictionary<VersionSpecifier, int> versionSpecifier2Id = new Dictionary<VersionSpecifier, int>();
        
        // Data structures for hosting versions.
        readonly Dictionary<string, SemanticVersion> stringToVersion = new Dictionary<string, SemanticVersion>();
        readonly Dictionary<string, VersionSpecifier> stringToVersionSpecifier = new Dictionary<string, VersionSpecifier>();
        
        // versions contains all versions of a given name. Eg all versions of OpenTAP/
        readonly Dictionary<int, List<int>> versions = new Dictionary<int, List<int>>();
        
        // Dependencies for a given version 
        readonly Dictionary<(int packageNameId, int packageVersion), (int packageNameId, int versionSpecifierId)[]> dependencies = new Dictionary<(int, int), (int, int)[]>();

        int GetId(ref string name)
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
        
        int GetVersion(ref SemanticVersion v)
        {
            if (version2Id.TryGetValue(v, out var id))
                return id;
            version2Id[v] = id = versionLookup.Count;
            versionLookup.Add(v);
            return id;
        }
        
        int GetVersion(string v2)
        {
            var v = stringToVersion.GetOrCreateValue(v2, SemanticVersion.Parse);
            return GetVersion(ref v);
        }

        int GetVersionSpecifier(string v2)
        {
            var v = stringToVersionSpecifier.GetOrCreateValue(v2, VersionSpecifier.Parse);
            return GetVersionSpecifier(ref v);
        }
        
        int GetVersionSpecifier(ref VersionSpecifier v)
        {
            if (versionSpecifier2Id.TryGetValue(v, out var id))
                return id;
            versionSpecifier2Id[v] = id = versionLookup2.Count;
            versionLookup2.Add(v);
            return id;
        }

        public void LoadFromPackageDefs(IEnumerable<PackageDef> packages)
        {
            foreach (var elem in packages)
            {
                var name = elem.Name;
                var version = elem.Version;
                
                var id = GetId(ref name);
                var thisVersions = versions[id];
                thisVersions.Add(GetVersion(ref version));
                if(elem.Dependencies.Count > 0)
                {
                    var deps = new (int id, int y)[ elem.Dependencies.Count];
                    
                    for (int i = 0; i < deps.Length; i++)
                    {
                        var dep = elem.Dependencies[i];
                        var depname = dep.Name;
                        var depid = GetId(ref depname);
                        var depver = dep.Version;
                        deps[i] = (depid, GetVersionSpecifier(ref depver));
                    }

                    dependencies[(id, GetVersion(ref version))] = deps;
                }
            }

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

                var id = GetId(ref name);
                var thisVersions = versions[id];
                thisVersions.Add(GetVersion(version));
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
                            var depid = GetId(ref depname);
                            var depver = dep.GetProperty("version").GetString();
                            deps[i] = (depid, GetVersionSpecifier(depver));
                            i++;
                        }

                        this.dependencies[(id, GetVersion(version))] = deps;
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
                if (!dependencies.TryGetValue((Id, GetVersion(ref v)), out var deps))
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
                if (dependencies.TryGetValue((Id, GetVersion(ref version)), out var deps))
                {
                    foreach (var dep in deps)
                    {
                        var depVersion = versionLookup2[dep.Item2];

                        yield return new PackageSpecifier(names[dep.Item1], depVersion);
                    }
                }
            }
        }

        public void Absorb(PackageDependencyGraph graph)
        {
            
        }
    }
}