using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Package
{
    [Browsable(false)]
    [Display("install", Group: "image")]
    internal class ImageInstallAction : IsolatedPackageAction
    {
        /// <summary>
        /// Path to Image file containing XML or JSON formatted Image specification, or just the string itself.
        /// </summary>
        [UnnamedCommandLineArgument("image")]
        public string ImagePath { get; set; }

        /// <summary>
        /// Option to merge with target installation. Default is false, which means overwrite installation
        /// </summary>
        [CommandLineArgument("merge")]
        public bool Merge { get; set; }
        
        /// <summary>
        /// Never prompt for user input.
        /// </summary>
        [CommandLineArgument("non-interactive", Description = "Never prompt for user input.")]
        public bool NonInteractive { get; set; } = false;

        protected override int LockedExecute(CancellationToken cancellationToken)
        {
            if (NonInteractive)
                UserInput.SetInterface(new NonInteractiveUserInputInterface());

            if (Force)
                log.Warning($"Using --force does not force an image installation");

            ImageSpecifier imageSpecifier = new ImageSpecifier();
            if (ImagePath != null)
            {
                var imageString = ImagePath;
                if (File.Exists(imageString))
                    imageString = File.ReadAllText(imageString);
                imageSpecifier = ImageSpecifier.FromString(imageString);
            }

            try
            {
                var deploymentInstallation = new Installation(Target);
                if (Merge)
                {
                    Installation newInstallation = imageSpecifier.MergeAndDeploy(deploymentInstallation, cancellationToken);
                }
                else
                {
                    
                    var cache = new PackageDependencyCache(deploymentInstallation.OS, deploymentInstallation.Architecture);
                    cache.LoadFromRepositories();
                    var resolver = new ImageResolver(cancellationToken);
                    var image = resolver.ResolveImage(imageSpecifier, cache.Graph);
                    if (image == null)
                    {
                        log.Error("Unable to resolve image");
                        return 1;
                    }
                    var imageSpecifier2 = new ImageSpecifier(image.Packages.ToList(), imageSpecifier.Name);
                    imageSpecifier2.Repositories = cache.Repositories;
                    var imageIdentifier = imageSpecifier2.Resolve(cancellationToken);
                    imageIdentifier.Deploy(Target, cancellationToken);
                }
                return 0;
            }
            catch (AggregateException e)
            {
                foreach (var innerException in e.InnerExceptions)
                    log.Error($"- {innerException.Message}");
                throw new ExitCodeException((int)PackageExitCodes.PackageDependencyError, e.Message);
            }

        }
    }
}
