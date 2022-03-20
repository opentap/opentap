using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap.Package
{
    internal class ResolverHeuristics
    {
        /// <summary>
        /// This is the expected utility of resolving a package specifier.
        /// The more specific the version is, the higher the expected utility.
        /// The reasoning is that all children of this node will only consider versions
        /// that are compatible with all of their parents, so if this node produces
        /// fewer PackageIdentifierTreeNode children, then all children will need to
        /// consider fewer candidates. This effectively means that all 'any' dependencies
        /// will be considered last (except for potential unresolved non-any children)
        ///
        /// In the event that no candidate can resolve an 'any' dependency, we will be
        /// forced to back up the tree to an earlier node.
        /// </summary>
        /// <returns></returns>
        public static int Value(PackageSpecifier specifier)
        {
            var v = specifier.Version;
            return Value(v);
        }

        public static int Value(VersionSpecifier v)
        {
            int any = v == VersionSpecifier.Any ? 1 : 2;
            int exact = v.MatchBehavior == VersionMatchBehavior.Exact ? 1 : 0;
            int major = v.Major == null ? 0 : 1;
            int minor = v.Minor == null ? 0 : 1;
            int patch = v.Patch == null ? 0 : 1;

            return any + exact + major + minor + patch;
        }

        /// <summary>
        /// Score the fitness of a package definition based on how well it matches the specifier it is supposed to satisfy
        /// </summary>
        /// <param name="def"></param>
        /// <param name="specifier"></param>
        /// <returns></returns>
        public static int Value(PackageDef def, PackageSpecifier specifier)
        {
            return Value(def.Version, specifier);
        }

        public static int Value(SemanticVersion version, PackageSpecifier specifier)
        {
            var target = specifier.Version;
            // If this satisfies an 'any' dependency, return 1.
            if (target == VersionSpecifier.Any) return 1;
            var score = 1;
            if (version.Minor == target.Minor)
                score += 1;
            if (version.Patch == target.Patch)
                score += 1;
            return score;
        }
    }

    class PackageResolver
    {
        public List<IPackageRepository> Repositories { get; }
        public PackageResolver(List<IPackageRepository> repositories)
        {
            Repositories = repositories;
        }

        private ConcurrentDictionary<PackageSpecifier, List<PackageDef>> lookup = new ConcurrentDictionary<PackageSpecifier, List<PackageDef>>();

        public List<PackageDef> GetPackages(PackageSpecifier specifier)
        {
            var packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(Repositories, specifier).ToList();
            return packages.Where(p => p.IsPlatformCompatible(specifier.Architecture, specifier.OS))
                           .Where(p => p.Version != null)
                           .Distinct()
                           .ToList();
            // return lookup.GetOrAdd(specifier, _ => PackageRepositoryHelpers.GetPackagesFromAllRepos(Repositories, specifier).ToList())
            //              .Where(p => p.IsPlatformCompatible(specifier.Architecture, specifier.OS))
            //              .Where(p => p.Version != null)
            //              .Distinct()
            //              .ToList();

        }

        public List<SemanticVersion> GetAllVersionsFromAllRepos(PackageSpecifier specifier)
        {
            var allVersions = PackageRepositoryHelpers.GetAllVersionsFromAllRepos(Repositories, specifier.Name)
                                                      .Where(p => p.IsPlatformCompatible(specifier.Architecture, specifier.OS))
                                                      .Where(p => p.Version !=
                                                                  null) // Packages with versions not in Semantic format will be null. We can't take any valuable decision on these.
                                                      .Select(p => p.Version).Distinct()
                                                      .ToList();
            return allVersions;
        }

    }

    class ResolverTreeNode
    {
        public ResolverTreeNode(PackageResolver resolver, string name)
        {
            PackageResolver = resolver;
            Name = name;
        }

        public ResolverTreeNode(PackageResolver resolver, ResolverTreeNode parent, string name)
        {
            PackageResolver = resolver;
            Parent = parent;
            Name = name;
        }

        public ResolverTreeNode Parent { get; } = null;
        public List<ResolverTreeNode> Children { get; } = new List<ResolverTreeNode>();

        public string Name { get; }
        public PackageResolver PackageResolver { get; set; }

        private static TraceSource log = Log.CreateSource("Tree Dependency Solver");

        private bool MergeSpecs(List<PackageSpecifier> packages, out List<PackageSpecifier> merged)
        {
            merged = new List<PackageSpecifier>();
            // In case of duplicate package specifiers, merge them all into a single specifier
            foreach (var group in packages.GroupBy(p => p.Name))
            {
                VersionSpecifier version = VersionSpecifier.Any;
                foreach (var s in group)
                {
                    if (version.IsSatisfiedBy(s.Version))
                    {
                        // The specifier with the higher value is more specific
                        if (ResolverHeuristics.Value(s.Version) > ResolverHeuristics.Value(version))
                            version = s.Version;
                    }
                    else
                    {
                        if (!s.Version.IsSatisfiedBy(version))
                        {
                            log.Debug($"Incompatible versions package '{group.Key}': '{version}' and '{s.Version}' are mutually exclusive.");
                            return false;
                        }
                    }
                }
                merged.Add(new PackageSpecifier(group.Key, version));
            }

            return true;
        }

        public List<PackageDef> GetResolved()
        {
            var resolved = new List<PackageDef>();
            if (Package != null) resolved.Add(Package);
            var p = Parent;
            while (p != null)
            {
                if (p.Package != null) resolved.Add(p.Package);
                p = p.Parent;
            }

            return resolved;
        }

        public ResolverTreeNode Solve(List<PackageSpecifier> packages)
        {
            if (!(MergeSpecs(packages, out var merged)))
                return null;
            var resolved = GetResolved();
            var resolvedNames = resolved.Select(r => r.Name).ToHashSet();

            var toResolve = merged.Except(m => resolvedNames.Contains(m.Name)).ToList();
            if (toResolve.Count == 0) return this;

            var nextPackage = toResolve.FindMax(ResolverHeuristics.Value);
            merged.Remove(nextPackage);

            var next = new ResolverTreeNode(PackageResolver, this, $"{nextPackage.Name} - {nextPackage.Version}");
            Children.Add(next);
            return next.Solve(nextPackage, merged);
        }

        public PackageDef Package { get; private set; }

        public ResolverTreeNode Solve(PackageSpecifier next, List<PackageSpecifier> rest)
        {
            var allVersions = PackageResolver.GetAllVersionsFromAllRepos(next);

            foreach (var version in allVersions.OrderByDescending(p => ResolverHeuristics.Value(p, next))
                                               .ThenByDescending(p => p))
            {
                var exactSpec = new PackageSpecifier(next.Name, new VersionSpecifier(version, VersionMatchBehavior.Exact), next.Architecture, next.OS);
                if (next.Version.IsSatisfiedBy(exactSpec.Version) == false) continue;
                var def = PackageResolver.GetPackages(exactSpec).FirstOrDefault();
                Package = def;
                if (def == null) continue;
                var newDeps = rest.ToList();
                newDeps.Add(exactSpec);
                newDeps.AddRange(def.Dependencies.Select(d => new PackageSpecifier(d.Name, d.Version)));
                var result = Solve(newDeps);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }

}