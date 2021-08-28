using NUnit.Framework;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpenTap.Image.Tests
{
    public class Cache
    {
        [Test]
        public void DownloadToCache()
        {
            ImageSpecifier specifier = new ImageSpecifier()
            {
                Packages = new List<PackageSpecifier>()
                {
                    new PackageSpecifier("Demonstration", VersionSpecifier.Parse("9.0.5+3cab80c8"))
                },
                Repositories = new List<string> { "packages.opentap.io" }
            };

            try
            {
                Directory.Delete(PackageCacheHelper.PackageCacheDirectory, true);

                var image = specifier.Resolve(CancellationToken.None);

                image.Cache();
                Assert.IsTrue(Directory.EnumerateFiles(PackageCacheHelper.PackageCacheDirectory).Any(s => s.Contains("Demonstration")));
                Assert.IsTrue(Directory.EnumerateFiles(PackageCacheHelper.PackageCacheDirectory).Any(s => s.Contains("OpenTAP")));
            }
            finally
            {
                if (Directory.Exists(PackageCacheHelper.PackageCacheDirectory))
                    Directory.Delete(PackageCacheHelper.PackageCacheDirectory, true);
            }
        }
    }
}
