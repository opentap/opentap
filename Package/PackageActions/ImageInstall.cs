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

namespace OpenTap.Package.PackageActions
{
    [Browsable(false)]
    [Display("install", Group: "image")]
    public class ImageInstallAction : LockingPackageAction
    {
        [UnnamedCommandLineArgument("image")]
        public string ImagePath { get; set; }

        [CommandLineArgument("merge")]
        public bool Merge { get; set; }

        protected override int LockedExecute(CancellationToken cancellationToken)
        {
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
