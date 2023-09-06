using OpenTap.Cli;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace OpenTap.Package
{
    [Browsable(false)]
    [Display("install", Group: "image")]
    internal class ImageInstallAction : IsolatedPackageAction
    {
        /// <summary>
        /// Path to Image file containing XML or JSON formatted Image specification, or just the string itself, e.g "REST-API,TUI:beta".
        /// </summary>
        [UnnamedCommandLineArgument("image")]
        public string ImagePath { get; set; }

        /// <summary>
        /// Option to merge with target installation. Default is false, which means overwrite installation
        /// </summary>
        [CommandLineArgument("merge")]
        public bool Merge { get; set; }
        
        /// <summary> Never prompt for user input. </summary>
        [CommandLineArgument("non-interactive", Description = "Never prompt for user input.")]
        public bool NonInteractive { get; set; } = false;
        
        /// <summary> Which operative system to resolve packages for. </summary>
        [CommandLineArgument("os", Description = "Specify which operative system to resolve packages for.")]
        public string Os { get; set; }

        /// <summary> Which CPU architecture to resolve packages for. </summary>
        [CommandLineArgument("architecture", Description = "Specify which architecture to resolve packages for.")]
        public CpuArchitecture Architecture { get; set; } = CpuArchitecture.Unspecified;
        
        /// <summary> dry-run - resolve, but don't install the packages. </summary>
        [CommandLineArgument("dry-run", Description = "Only print the result, don't install the packages.")]
        public bool DryRun { get; set; }

        /// <summary> Which repositories to use. </summary>
        [CommandLineArgument("repository", ShortName = "r", Description = "Repositories to use for resolving the image.")]
        public string[] Repositories { get; set; } = null;

        protected override int LockedExecute(CancellationToken cancellationToken)
        {
            if (NonInteractive)
                UserInput.SetInterface(new NonInteractiveUserInputInterface());

            if (Force)
                log.Warning($"Using --force does not force an image installation");

            if (string.IsNullOrEmpty(ImagePath))
                throw new ArgumentException("'image' not specified.", nameof(ImagePath));
            
            var imageString = ImagePath;
            if (File.Exists(imageString))
                imageString = File.ReadAllText(imageString);
            var imageSpecifier = ImageSpecifier.FromString(imageString);

            // image specifies any repositories?
            if(imageSpecifier.Repositories?.Any() != true)
            {
                if (Repositories?.Any() == true)
                    imageSpecifier.Repositories = Repositories.ToList();
                else
                    imageSpecifier.Repositories = PackageManagerSettings.Current.Repositories
                        .Where(x => x.IsEnabled)
                        .Select(x => x.Url)
                        .ToList();
            }
            else
            {
                if (Repositories?.Any() == true)
                    imageSpecifier.Repositories = Repositories.ToList();
            }

            if (!string.IsNullOrWhiteSpace(Os))
                imageSpecifier.OS = Os;
            if (Architecture!= CpuArchitecture.Unspecified)
                imageSpecifier.Architecture = Architecture;


            try
            {
                ImageIdentifier image;
                var sw = Stopwatch.StartNew();
                if (Merge)
                {
                    var deploymentInstallation = new Installation(Target);
                    image = imageSpecifier.MergeAndResolve(deploymentInstallation, cancellationToken);
                }
                else
                {
                    // Here we add the list of currently installed packages to 'AdditionalPackages'.
                    // These packages will be added to the imageSpecifier's internal cache of known packages.
                    // This fixes an edge case where an image fails to resolve because one of the specified
                    // packages is already installed, but is not available in any of the specified repositories
                    // (or the local cache). In that case, if the installed version is compatible with the image,
                    // the image resolution will still succeed even if the package cannot be found.
                    // This is possible e.g. with debug installs where the debug package does not exist in any repository,
                    // or if the package cache has been cleared by the user, and an image install is attempted
                    // without the original source repository of some package.
                    imageSpecifier.AdditionalPackages.AddRange(new Installation(Target).GetPackages());
                    image = imageSpecifier.Resolve(TapThread.Current.AbortToken);
                }

                if (image == null)
                {
                    log.Error(sw, "Unable to resolve image");
                    return 1;
                }

                log.Debug(sw, "Image resolution done");

                if (image.Packages.Count == 0)
                {
                    // We should prompt the user and warn them that they are about to completely wipe their installation.
                    var req = new WipeInstallationQuestion();
                    UserInput.Request(req, true);

                    if (req.WipeInstallation == WipeInstallationResponse.No)
                    {
                        throw new OperationCanceledException("Image installation was canceled.");
                    }
                }
                
                log.Debug("Image hash: {0}", image.Id);
                if (DryRun)
                {
                    log.Info("Resolved packages:");
                    foreach (var pkg in image.Packages)
                    {
                        log.Info("   {0}:    {1}", pkg.Name, pkg.Version);
                    }

                    return 0;
                }

                image.Deploy(Target, cancellationToken);
                return 0;
            }
            catch (AggregateException e)
            {
                foreach (var innerException in e.InnerExceptions)
                    log.Error($"- {innerException.Message}");
                throw new ExitCodeException((int)PackageExitCodes.PackageDependencyError, e.Message);
            }

        }
        
        enum WipeInstallationResponse
        {
            No,
            Yes,
        }

        [Display("Completely wipe installation?")]
        class WipeInstallationQuestion
        {
            [Browsable(true)]
            [Layout(LayoutMode.FullRow)]
            public string Message { get; } = "You are about to completely wipe your installation. Do you wish to Continue?";

            [Submit]
            [Layout(LayoutMode.FullRow | LayoutMode.FloatBottom)]
            public WipeInstallationResponse WipeInstallation { get; set; } = WipeInstallationResponse.Yes;
        }
    }
}
