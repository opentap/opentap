using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
        
        public ImmutableArray<PackageDef> InstalledPackages { get; set; } = ImmutableArray<PackageDef>.Empty;

        /// <summary> Creates a new instance. </summary>
        public ImageSpecifier(List<PackageSpecifier> packages, string name = "")
        {
            Packages = packages;
            Name = name;
        }

        /// <summary> Creates a new instance. </summary>
        public ImageSpecifier()
        {
            
        }

        public static ImageSpecifier FromAddedPackages(Installation installation, IEnumerable<PackageSpecifier> newPackages)
        {
            var toInstall = new List<PackageSpecifier>();
            var installed = installation.GetPackages().ToList();
            foreach (var package in newPackages)
            {
                var ext = installed.FirstOrDefault(x => x.Name == package.Name);
                if (ext != null)
                {
                    installed.Remove(ext);
                    // warn about downgrade.
                }

                if (File.Exists(package.Name))
                {
                    var package2 = PackageDef.FromPackage(package.Name);
                    toInstall.Add(new PackageSpecifier(package2.Name, package2.Version.AsExactSpecifier()));
                    installed.Add(package2);
                    continue;
                }
                toInstall.Add(package);
            }

            foreach (var pkg in installed)
            {
                toInstall.Add(new PackageSpecifier(pkg.Name, pkg.Version.AsCompatibleSpecifier()));
            }

            return new ImageSpecifier
            {
                Packages = toInstall,
                InstalledPackages = installed.ToImmutableArray()
            };
        }

        
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

            var os = Installation.Current.OS;
            var arch = Installation.Current.Architecture;
            
            var cache = new PackageDependencyCache(os, arch);
            
            cache.LoadFromRepositories();
            cache.AddPackages(InstalledPackages);
            
            var resolver = new ImageResolver(cancellationToken);
            var image = resolver.ResolveImage(this, cache.Graph);
            if (image == null)
            {
                throw new Exception($"OpenTAP packages could not be resolved");
            }
            
            ImageIdentifier image2 = new ImageIdentifier(image.Packages.Select(x => cache.GetPackageDef(x)).ToArray(), repositories.Select(s => s.Url));

            return image2;
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
            var img = new ImageSpecifier(images.SelectMany(x =>x.Packages).Distinct().ToList());
            return img.Resolve(cancellationToken);
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
            var installedSpecifiers = deploymentInstallation.GetPackages()
                .Select(x => new PackageSpecifier(x.Name, x.Version.AsCompatibleSpecifier()));

            var imageSpecifier2 = new ImageSpecifier(Packages.Concat(installedSpecifiers).ToList(), Name)
            {
                InstalledPackages = deploymentInstallation.GetPackages().ToImmutableArray()
            };
            var image = imageSpecifier2.Resolve(cancellationToken);
            
            if (image == null)
                throw new Exception($"Packages could not be resolved");

            image.Deploy(deploymentInstallation.Directory, cancellationToken);
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