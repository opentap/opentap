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
        
        /// <summary>
        /// Never prompt for user input.
        /// </summary>
        [CommandLineArgument("non-interactive", Description = "Never prompt for user input.")]
        public bool NonInteractive { get; set; } = false;
        
        [CommandLineArgument("OS", Description = "Specify which operative system to resolve packages for.")]
        public string Os { get; set; }

        [CommandLineArgument("Architecture", Description = "Specify which architecture to resolve packages for.")]
        public CpuArchitecture Architecture { get; set; } = CpuArchitecture.Unspecified;
        
        [CommandLineArgument("dry-run", Description = "Only print the result, don't install the packages.")]
        public bool DryRun { get; set; }

        [CommandLineArgument("repository", ShortName = "r", Description = "Repositories to use for resolving the image.")]
        public string[] Repositories { get; set; } = null;

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
                if (Merge)
                {
                    var deploymentInstallation = new Installation(Target);
                    Installation newInstallation = imageSpecifier.MergeAndDeploy(deploymentInstallation, cancellationToken);
                }
                else
                {       
                    var sw = Stopwatch.StartNew();
                    var r= imageSpecifier.Resolve(TapThread.Current.AbortToken);

                    if (r == null)
                    {
                        log.Error(sw, "Unable to resolve image");
                        return 1;
                    }
                    log.Debug(sw, "Resolution done");
                    if (DryRun)
                    {
                        log.Info("Resolved packages:");
                        foreach (var pkg in r.Packages)
                        {
                            log.Info("   {0}:    {1}", pkg.Name, pkg.Version);
                        }
                        return 0;
                    }
                    r.Deploy(Target, cancellationToken);
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
