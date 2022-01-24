using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    /// <summary>
    /// An <see cref="ImageSpecifier"/> defines an OpenTAP installation. The specifier can be resolved to an
    /// <see cref="ImageIdentifier"/> which can be deployed to an actual OpenTAP installation.
    /// </summary>
    public class ImageSpecifier
    {
        static TraceSource log = Log.CreateSource("ImageResolution");
        /// <summary>
        /// Desired packages in the installation
        /// </summary>
        public List<PackageSpecifier> Packages { get; set; } = new List<PackageSpecifier>();
        /// <summary>
        /// OpenTAP repositories to fetch the desired packages from
        /// </summary>
        public List<string> Repositories { get; set; } = new List<string>();

        /// <summary>
        /// Resolve the desired packages from the specified repositories. This will check if the packages are available, compatible and can successfully be deployed as an OpenTAP installation
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An <see cref="ImageIdentifier"/></returns>
        public ImageIdentifier Resolve(CancellationToken cancellationToken)
        {
            List<IPackageRepository> repositories = Repositories.Select(PackageRepositoryHelpers.DetermineRepositoryType).ToList();

            DependencyResolver resolver = new DependencyResolver(Packages, repositories, cancellationToken);

            string dotGraph = resolver.GetDotNotation("Image");
            log.Debug($"https://quickchart.io/graphviz?graph={WebUtility.UrlEncode(dotGraph)}");
            log.Flush();

            if (resolver.DependencyIssues.Any())
                throw new ImageResolveException(dotGraph, $"OpenTAP packages could not be resolved", resolver.DependencyIssues);

            ImageIdentifier image = new ImageIdentifier(resolver.Dependencies, repositories.Select(s => s.Url));

            return image;
        }

        internal Installation MergeAndDeploy(Installation deploymentInstallation, CancellationToken cancellationToken)
        {
            List<IPackageRepository> repositories = Repositories.Select(PackageRepositoryHelpers.DetermineRepositoryType).ToList();

            DependencyResolver resolver = new DependencyResolver(deploymentInstallation, Packages, repositories, cancellationToken);
            string dotGraph = resolver.GetDotNotation("Image");
            log.Debug($"https://quickchart.io/graphviz?graph={WebUtility.UrlEncode(dotGraph)}");
            log.Flush();

            if (resolver.DependencyIssues.Any())
                throw new ImageResolveException(dotGraph, $"OpenTAP packages could not be resolved", resolver.DependencyIssues);

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