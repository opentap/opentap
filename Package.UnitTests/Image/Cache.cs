using NUnit.Framework;
using OpenTap.Package;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace OpenTap.Image.Tests
{
    public class Cache
    {
        [Test]
        public void DownloadToCache()
        {
            ImageSpecifier specifier = MockRepository.CreateSpecifier();
            specifier.Packages = new List<PackageSpecifier>()
            {
                new PackageSpecifier("Demonstration", VersionSpecifier.Parse("9.0.5+3cab80c8"))
            };

            try
            {
                PackageCacheHelper.ClearCache();

                var image = specifier.Resolve(CancellationToken.None);

                image.Cache();
                Assert.IsTrue(Directory.EnumerateFiles(PackageCacheHelper.PackageCacheDirectory).Any(s => s.Contains("Demonstration")));
                Assert.IsTrue(Directory.EnumerateFiles(PackageCacheHelper.PackageCacheDirectory).Any(s => s.Contains("OpenTAP")));
            }
            finally
            {
                PackageCacheHelper.ClearCache();
            }
        }
    }
}
