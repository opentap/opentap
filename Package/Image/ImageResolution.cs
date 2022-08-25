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

        public virtual bool Success => true;

        public override string ToString() => string.Format("[ImageResolution: {0}", string.Join(", ", Packages.Select(x => $"{x.Name} {x.Version}")));
    }
}