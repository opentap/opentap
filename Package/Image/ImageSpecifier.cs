using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OpenTap.Package
{
    /// <summary>
    /// An <see cref="ImageSpecifier"/> defines a desired OpenTAP installation. The specifier can be resolved to an actual <see cref="ImageIdentifier"/> capable of being deployed to an actual OpenTAP installation.
    /// </summary>
    public class ImageSpecifier
    {
        /// <summary>
        /// Desired packages in the installation
        /// </summary>
        public List<PackageSpecifier> Packages { get; set; } = new List<PackageSpecifier>();
        /// <summary>
        /// OpenTAP repositories to fetch the desired packages from
        /// </summary>
        public List<string> Repositories { get; set; } = new List<string>();

        /// <summary>
        /// Resolve the desired packages from the specified repositores. This will check if the packages are available, compatible and can successfully be deployed as an OpenTAP installation
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An <see cref="ImageIdentifier"/></returns>
        public ImageIdentifier Resolve(CancellationToken cancellationToken)
        {
            List<Exception> exceptions = new List<Exception>();
            List<IPackageRepository> repositories = Repositories.Select(s => PackageRepositoryHelpers.DetermineRepositoryType(s)).ToList();
            List<PackageDef> gatheredPackages = new List<PackageDef>();
            foreach (var packageReference in Packages)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException("Resolve operation cancelled by user");
                try
                {
                    PackageDef package = PackageActionHelpers.FindPackage(packageReference, new List<PackageDef>(), repositories);
                    gatheredPackages.Add(package);
                }
                catch (Exception)
                {
                    exceptions.Add(new InvalidOperationException($"Unable to resolve package '{packageReference.Name}'"));
                }
            }
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Resolve operation cancelled by user");

            DependencyResolver dependencyResolver = new DependencyResolver(new Dictionary<string, PackageDef>(), gatheredPackages, repositories);
            if (dependencyResolver.UnknownDependencies.Any())
            {
                foreach (var dep in dependencyResolver.UnknownDependencies)
                {
                    string message = string.Format("A package dependency named '{0}' with a version compatible with {1} could not be found in any repository.", dep.Name, dep.Version);
                    exceptions.Add(new InvalidOperationException(message));
                }
            }

            gatheredPackages = dependencyResolver.Dependencies;

            // Group packages by name in order to find conflicting versions
            var gatheredPackagesGrouped = gatheredPackages.GroupBy(s => s.Name)
                                            .Select(x => x.OrderByDescending(g => g.Version));
            foreach (var package in gatheredPackagesGrouped)
            {
                if (package.Select(x => x.Version.Major).Distinct().Count() > 1)
                    exceptions.Add(new InvalidOperationException($"{package.FirstOrDefault().Name} is resolved to multiple packages with different major versions which conflicts"));
            }


            // If there is no errors, only pick the highest versions of each resolved package
            if (!exceptions.Any())
                gatheredPackages = gatheredPackages.GroupBy(s => s.Name)
                                            .Select(x => x.OrderByDescending(g => g.Version).FirstOrDefault()).ToList();

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("Resolve operation cancelled by user");

            ImageIdentifier image = new ImageIdentifier(gatheredPackages, repositories.Select(s => s.Url));

            if (exceptions.Any())
                throw new AggregateException("Image could not be resolved", exceptions);
            return image;
        }

        /// <summary>
        /// Create an <see cref="ImageSpecifier"/> from JSON or XML value. Throws <see cref="InvalidOperationException"/> if value is not valid JSON or XML
        /// </summary>
        /// <param name="value">JSON or XML formatted <see cref="ImageSpecifier"/></param>
        /// <returns>An <see cref="ImageSpecifier"/></returns>
        public static ImageSpecifier FromString(string value)
        {
            return ImageHelper.GetImageFromString(value);
        }
    }
}