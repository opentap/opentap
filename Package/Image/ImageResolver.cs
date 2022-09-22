using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OpenTap.Package
{ 
    /// <summary>
    /// Resolves packages dependencies for an image.This should be able to resolve any set of package dependencies, but
    /// it may take a long time in some edge cases. In most cases however it seems to settle quite quickly.
    /// </summary>
    class ImageResolver
    {
        CancellationToken cancelToken;
        public ImageResolver(CancellationToken cancelToken)
        {
            this.cancelToken = cancelToken;
        }
           

        long Iterations;

        public ImageResolution ResolveImage(ImageSpecifier image, PackageDependencyGraph graph)
        {
            cancelToken.ThrowIfCancellationRequested();
            Iterations++;
            List<PackageSpecifier> packages = image.Packages.ToList();
            
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
                            return new FailedImageResolution(new []{pkg1, pkg2}, Iterations);

                        if (!pkg1.Version.IsSatisfiedBy(pkg2.Version))
                        {
                            // select pkg1 instead of pkg2
                            packages.RemoveAt(j);
                            goto retry;
                        }
                        if(!pkg2.Version.IsSatisfiedBy(pkg1.Version)) // pkg1 is satisfied by pkg2
                        {
                            // select pkg2 instead of pkg1
                            packages[i] = pkg2;
                            packages.RemoveAt(j);
                            goto retry;
                        }
                        // pkg1 might still be different from pkg2 if one is exact and another one isn't.
                    }
                }
            }

            //2. expand dependencies for exact package specifiers
            bool modified = true;
            while (modified)
            {
                modified = false;
                for (int i = 0; i < packages.Count; i++)
                {
                    var pkg1 = packages[i];
                    if (pkg1.Version.TryAsExactSemanticVersion(out var v) == false)
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
                                    return new FailedImageResolution(new[]{pkg2, dep}, Iterations);
                                }

                                // this dependency is more specific than the existing.
                                if (pkg2.Version != dep.Version && (pkg2.Version.IsSuperSetOf(dep.Version) || !dep.Version.IsSatisfiedBy(pkg2.Version)) && pkg2.Version.MatchBehavior != VersionMatchBehavior.Exact)
                                {
                                    packages[j] = dep;
                                    modified = true;
                                    break;
                                }
                            }
                        }

                        // add the dependency
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
            // now let's try pruning them a bit
            bool retry = false;
            for (int i = 0; i < packages.Count; i++)
            {
                var pkg = packages[i];
                var versions = allVersions[i];
                var others = packages.Except(x => x == pkg).ToArray();
                var newVersions = versions.Where(x =>
                        graph.CouldSatisfy(pkg.Name, new VersionSpecifier(x, VersionMatchBehavior.Exact), others, image.FixedPackages))
                    .ToArray();
                allVersions[i] = newVersions;
                if (newVersions.Length == 1 && !pkg.Version.IsExact)
                {
                    packages[i] = new PackageSpecifier(pkg.Name,
                        // newVersions[0] will always be exact.
                        new VersionSpecifier(newVersions[0], VersionMatchBehavior.Exact));
                    retry = true;
                }else if (newVersions.Length == 1)
                {
                    if (pkg.Version.PreRelease != newVersions[0].PreRelease ||
                        pkg.Version.BuildMetadata != newVersions[0].BuildMetadata)
                    { 
                        // in this case update the version for the package specifier
                        // the exact dependencies has changed, so we need to do another round of resolution.
                        packages[i] = new PackageSpecifier(pkg.Name,
                            new VersionSpecifier(newVersions[0], VersionMatchBehavior.Exact));
                        retry = true;
                    }
                }
            }

            if (retry)
                return ResolveImage(new ImageSpecifier(packages.ToList()){FixedPackages = image.FixedPackages}, graph);
            
            // 5. ok now we have X * Y * Z * ... = K possible solutions all satisfying the constraints.
            // Lets sort all the versions based on version specifiers, then fix  the version and try each combination (brute force)
            long k = allVersions.FirstOrDefault()?.LongLength ?? 0;
            for (int i = 1; i < allVersions.Count; i++)
            {
                k *= allVersions[i].LongLength;
            }

            if (k == 0)
            {
                List<PackageSpecifier> ResolveProblems = new List<PackageSpecifier>();
                for (int i = 0; i < allVersions.Count; i++)
                {
                    if (allVersions[i].Length == 0)
                    {
                        ResolveProblems.Add(packages[i]);
                    }
                }

                if (packages.Count == 0) 
                    // special case when no packages are specified.
                    // this is an uncommon trivial corner case.
                    return new ImageResolution(Array.Empty<PackageSpecifier>(), Iterations);
                
                return new FailedImageResolution(ResolveProblems, Iterations); // no possible solutions,
            }

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
                // skip the following if there is less than two versions.
                if (allVersions[i].Length <= 1) continue;
                var versions = allVersions[i].ToList();
                
                if (pkg.Version == VersionSpecifier.Any)
                {
                    versions.Sort();
                    // any version -> take the newest first.
                    versions.Reverse();
                }else if(pkg.Version.MatchBehavior.HasFlag(VersionMatchBehavior.Exact))
                {
                    //exact may be more than one version, even though the match behavior is 'exact'.
                    // for example OpenTAP '9.17' is exact, but many versions matches that.
                    // We are interested in the newest in this case, so order newest -> oldest within the span.
                    versions.Sort();
                    versions.Reverse();
                }
                else if (pkg.Version.MatchBehavior.HasFlag(VersionMatchBehavior.Compatible))
                {
                    // In this case we want the closest possible version. So we use an ascending sorting.
                    // 'SortPartial' is used for sorting within an incomplete version e.g '^9.17'
                    // ^9.17 -> We want the newest 9.17 version, but if we cannot get a 9.17.* a  newer will suffice. 
                    versions = versions.OrderBy(x => x, pkg.Version.SortPartial).ToList();
                }

                allVersions[i] = versions.ToArray();
            }

            // Iterate each package, fixing one variable and iterating the whole available span.
            // note that this is a recursive call so even though we only fix _one_ variable here
            // the rest of the variables will be fixed in the recursions.
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
                        if (result.Success)
                            return result;
                    }
                }
            }
            
            // this probably never happens as we already returned null, when K was 0.
            return new FailedImageResolution(Array.Empty<PackageSpecifier>(), Iterations);
        }
    }

    class FailedImageResolution : ImageResolution
    {
        private IReadOnlyList<PackageSpecifier> resolveProblems;
        public FailedImageResolution(IReadOnlyList<PackageSpecifier> resolveProblems, long iterations) : base(Array.Empty<PackageSpecifier>(), iterations)
        {
            this.resolveProblems = resolveProblems;
        }

        public override bool Success => false;

        public override string ToString()
        {
            return "Unable to resolve packages: " + string.Join(", ", resolveProblems.Select(x => $"{x.Name}: {x.Version}"));
        }
    }
}