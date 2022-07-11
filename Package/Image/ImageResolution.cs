using System.Linq;

namespace OpenTap.Package
{
    class ImageResolution
    {
        public ImageResolution(PackageSpecifier[] pkgs, long iterations)
        {
            Packages = pkgs.OrderBy(x => x.Name).ToArray();
            this.Iterations = iterations;
        }
        public readonly PackageSpecifier[] Packages;
        public readonly long Iterations;
    }
}