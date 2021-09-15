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
        /// Path to Image file containing XML or JSON formatted Image specification
        /// </summary>
        [UnnamedCommandLineArgument("image")]
        public string ImagePath { get; set; }

        /// <summary>
        /// Option to merge with target installation. Default is false, which means overwrite installation
        /// </summary>
        [CommandLineArgument("merge")]
        public bool Merge { get; set; }

        protected override int LockedExecute(CancellationToken cancellationToken)
        {
            log.Info($"Modifying installation: {Target}");
            try
            {
                var imageString = File.ReadAllText(ImagePath);
                var image = ImageSpecifier.FromString(imageString);
                if (Merge)
                {
                    var installation = new Installation(Target);
                    image.Packages.AddRange(installation.GetPackages().Select(p => new PackageSpecifier(p)));
                }
                var imageIdentifier = image.Resolve(cancellationToken);
                imageIdentifier.Deploy(Target, cancellationToken);
                return 0;
            }
            catch (AggregateException e)
            {
                foreach (var innerException in e.InnerExceptions)
                    log.Error(innerException.Message);
                throw;
            }
        }
    }
}
