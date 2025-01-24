using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace OpenTap.Package
{
    /// <summary>
    /// Exception thrown when ImageSpecifier.Resolve fails.
    /// </summary>
    public class ImageResolveException : AggregateException
    {
        internal ImageSpecifier Image { get; }
        internal ImmutableArray<PackageDef> InstalledPackages { get; }
        internal ImageResolution Result { get; }

        internal ImageResolveException(ImageResolution result, ImageSpecifier image,
            ImmutableArray<PackageDef> installedPackages)
        {
            Result = result;
            Image = image;
            InstalledPackages = installedPackages;
        }

        /// <summary>
        /// The description of the resolution error.
        /// </summary>
        public override string Message => ToString();

        /// <summary>
        /// Convert the resolution error to a string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var unsatisfiedDependencies = new List<PackageDef>();
            if (Result is FailedImageResolution fir && fir.resolveProblems is not GenericResolutionProblem)
            {
                return fir.ToString();
            }
            
            foreach (var pkg in InstalledPackages)
            {
                if (!pkg.Dependencies.All(dep => isSatisfied(dep, InstalledPackages)))
                    unsatisfiedDependencies.Add(pkg);
            }

            if (unsatisfiedDependencies.Any())
            {
                var sb = new StringBuilder();
                var namePadding = unsatisfiedDependencies.Max(n => n.Name.Length);
                foreach (var sat in unsatisfiedDependencies)
                {
                    var missingDeps = sat.Dependencies.Where(dep => !isSatisfied(dep, InstalledPackages)).ToArray();
                    missingDeps = missingDeps.Where(m =>
                        this.Image.Packages.Where(p => p.Name == m.Name)
                            .All(requested => !m.Version.IsSatisfiedBy(requested.Version))).ToArray();
                    if (missingDeps.Any())
                    {
                        string missingMsg = string.Join(" and ",
                            missingDeps.Select(pkg => $"{pkg.Name}:{pkg.Version}"));
                        sb.AppendLine($"{sat.Name.PadRight(namePadding)} missing {missingMsg}");
                    }
                }

                return $"{sb} ({Image} from {string.Join(", ", Image.Repositories)})";
            }

            return Result.ToString();
        }

        static bool isSatisfied(PackageDependency dep, IEnumerable<PackageDef> InstalledPackages)
        {
            foreach (var i in InstalledPackages)
            {
                if (i.Name == dep.Name)
                {
                    if (dep.Version.IsSatisfiedBy(i.Version.AsExactSpecifier()))
                        return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Dependency graph specified in Dot notation
        /// </summary>
        [Obsolete("This will always be null.")]
        public string DotGraph { get; private set; }
    }
}
