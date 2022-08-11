using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OpenTap.Package
{
    /// <summary>
    /// An <see cref="ImageSpecifier"/> defines an OpenTAP installation. The specifier can be resolved to an
    /// <see cref="ImageIdentifier"/> which can be deployed to an actual OpenTAP installation.
    /// </summary>
    public class ImageSpecifier
    {
        /// <summary>
        /// Optional name of the ImageSpecifier. Used for debugging purposes.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Desired packages in the installation
        /// </summary>
        public List<PackageSpecifier> Packages { get; set; } = new List<PackageSpecifier>();

        /// <summary>
        /// OpenTAP repositories to fetch the desired packages from
        /// These should be well formed URIs and will be interpreted relative to the BaseAddress set in AuthenticationSettings.
        /// </summary>
        public List<string> Repositories { get; set; } =
            new List<string>() { new Uri(PackageCacheHelper.PackageCacheDirectory).AbsoluteUri };

        /// <summary>
        /// Resolve the desired packages from the specified repositories. This will check if the packages are available, compatible and can successfully be deployed as an OpenTAP installation
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <exception cref="ImageResolveException">The exception thrown if the image could not be resolved</exception>
        public ImageIdentifier Resolve(CancellationToken cancellationToken)
        {
            List<IPackageRepository> repositories = Repositories.Distinct().Select(PackageRepositoryHelpers.DetermineRepositoryType).GroupBy(p => p.Url).Select(g => g.First()).ToList();
            
            DependencyResolver resolver = new DependencyResolver(Packages, repositories, cancellationToken);


            if (resolver.DependencyIssues.Any())
                throw new ImageResolveException(resolver.GetDotNotation(), $"OpenTAP packages could not be resolved", resolver.DependencyIssues);

            ImageIdentifier image = new ImageIdentifier(resolver.Dependencies, repositories.Select(s => s.Url));

            return image;
        }

        /// <summary>
        /// Merges and resolves the packages for a number of images. May throw an exception if the packages cannot be resolved.
        /// </summary>
        /// <param name="images">The images to merge.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation before time. This will cause an OperationCancelledException to be thrown.</param>
        /// <returns></returns>
        /// <exception cref="ImageResolveException">The exception thrown if the image could not be resolved</exception>
        public static ImageIdentifier MergeAndResolve(IEnumerable<ImageSpecifier> images, CancellationToken cancellationToken)
        {
            List<IPackageRepository> repositories = images.SelectMany(s => s.Repositories).Distinct().Select(PackageRepositoryHelpers.DetermineRepositoryType).ToList();

            Dictionary<string, List<PackageSpecifier>> packages = new Dictionary<string, List<PackageSpecifier>>();
            foreach (var img in images)
            {
                if (img.Name is null)
                    img.Name = "Unnamed";
                packages[img.Name] = img.Packages;
            }
            DependencyResolver resolver = new DependencyResolver(packages, repositories, cancellationToken);

            if (resolver.DependencyIssues.Any())
                throw new ImageResolveException(resolver.GetDotNotation(), $"OpenTAP packages could not be resolved", resolver.DependencyIssues);

            ImageIdentifier image = new ImageIdentifier(resolver.Dependencies, repositories.Select(s => s.Url));

            return image;
        }

        /// <summary>
        /// Resolve specified packages in the ImageSpecifier with respect to the target installation.
        /// Specified packages will take precedence over already installed packages
        /// Already installed packages, which are not specified in the imagespecifier, will remain installed.
        /// </summary>
        /// <param name="deploymentInstallation">OpenTAP installation to merge with and deploy to.</param>
        /// <param name="cancellationToken">Standard CancellationToken</param>
        /// <returns>A new Installation</returns>
        /// <exception cref="ImageResolveException">In case of resolve errors, this method will throw ImageResolveExceptions.</exception>
        public Installation MergeAndDeploy(Installation deploymentInstallation, CancellationToken cancellationToken)
        {
            List<IPackageRepository> repositories = Repositories.Select(PackageRepositoryHelpers.DetermineRepositoryType).ToList();

            DependencyResolver resolver = new DependencyResolver(deploymentInstallation, Packages, repositories, cancellationToken);

            if (resolver.DependencyIssues.Any())
                throw new ImageResolveException(resolver.GetDotNotation(), $"OpenTAP packages could not be resolved", resolver.DependencyIssues);

            var result = resolver.Dependencies.Concat(deploymentInstallation.GetPackages().Where(s => !resolver.Dependencies.Any(p => s.Name == p.Name)));

            ImageIdentifier image = new ImageIdentifier(result, Repositories);
            image.Deploy(deploymentInstallation.Directory, cancellationToken);
            //ImageDeployer.Deploy(deploymentInstallation, result.ToList(), cancellationToken);

            return new Installation(deploymentInstallation.Directory);
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

    class ImageSpecifierResolveArgs
    {
        public ImageSpecifier ImageSpecifier { get; set; }
        public PackageSpecifier PackageSpecifier { get; set; }

        public ImageSpecifierResolveArgs(ImageSpecifier ImageSpecifier, PackageSpecifier PackageSpecifier)
        {
            this.ImageSpecifier = ImageSpecifier;
            this.PackageSpecifier = PackageSpecifier;
        }
    }

    /// <summary>
    /// Exception thrown when ImageSpecifier.Resolve fails. The exception contains a dependency graph specified Dot notation.
    /// </summary>
    public class ImageResolveException : AggregateException
    {
        internal ImageResolveException(string dotGraph, string message, List<Exception> dependencyIssues) : base(message, dependencyIssues)
        {
            DotGraph = dotGraph;
        }

        /// <summary>
        /// Dependency graph specified in Dot notation
        /// </summary>
        public string DotGraph { get; private set; }

    }
}