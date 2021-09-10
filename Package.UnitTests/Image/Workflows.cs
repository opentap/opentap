using NUnit.Framework;
using OpenTap.Diagnostic;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpenTap.Image.Tests
{
    public class Workflows
    {
        [Test]
        public void DeployImage()
        {
            string temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string temp2 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            ImageSpecifier imageSpecifier = new ImageSpecifier();
            imageSpecifier.Repositories = new List<string>() { "packages.opentap.io" };
            imageSpecifier.Packages = new List<PackageSpecifier>
            {
                new PackageSpecifier("Demonstration"),
                new PackageSpecifier("TUI")
            };
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                var image = imageSpecifier.Resolve(CancellationToken.None);
                Console.WriteLine($"Resolve: {stopwatch.ElapsedMilliseconds} ms");
                Assert.IsNotNull(image);
                Assert.IsTrue(image.Packages.Any(s => s.Name == "OpenTAP"));
                Assert.IsTrue(image.Packages.Any(s => s.Name == "Demonstration"));
                Assert.IsTrue(image.Packages.Any(s => s.Name == "TUI"));

                stopwatch.Restart();
                image.Cache();
                Console.WriteLine($"Cache: {stopwatch.ElapsedMilliseconds} ms");


                stopwatch.Restart();
                image.Deploy(temp, CancellationToken.None);
                Console.WriteLine($"First deploy: {stopwatch.ElapsedMilliseconds} ms");

                Installation installation = new Installation(temp);
                var packages = installation.GetPackages();
                Assert.IsTrue(packages.Any(s => s.Name == "OpenTAP"));
                Assert.IsTrue(packages.Any(s => s.Name == "Demonstration"));
                Assert.IsTrue(packages.Any(s => s.Name == "TUI"));

                stopwatch.Restart();
                image.Deploy(temp2, CancellationToken.None);
                Console.WriteLine($"Second deploy: {stopwatch.ElapsedMilliseconds} ms");
            }
            finally
            {
                if (Directory.Exists(temp))
                    Directory.Delete(temp, true);

                if (Directory.Exists(temp2))
                    Directory.Delete(temp2, true);
            }
        }

        [Test]
        public void DeployImageOnExistingInstallation()
        {
            string temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            ImageSpecifier imageSpecifier = new ImageSpecifier();
            imageSpecifier.Repositories = new List<string>() { "packages.opentap.io" };
            imageSpecifier.Packages = new List<PackageSpecifier>
            {
                new PackageSpecifier("Demonstration"),
                new PackageSpecifier("TUI")
            };
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                var image = imageSpecifier.Resolve(CancellationToken.None);
                Console.WriteLine($"Resolve: {stopwatch.ElapsedMilliseconds} ms");
                Assert.IsNotNull(image);
                Assert.IsTrue(image.Packages.Any(s => s.Name == "OpenTAP"));
                Assert.IsTrue(image.Packages.Any(s => s.Name == "Demonstration"));
                Assert.IsTrue(image.Packages.Any(s => s.Name == "TUI"));

                stopwatch.Restart();
                image.Cache();
                Console.WriteLine($"Cache: {stopwatch.ElapsedMilliseconds} ms");
                foreach (var pkg in image.Packages)
                    Assert.IsTrue(File.Exists(PackageCacheHelper.GetCacheFilePath(pkg)));

                stopwatch.Restart();
                image.Deploy(temp, CancellationToken.None);
                Console.WriteLine($"First deploy: {stopwatch.ElapsedMilliseconds} ms");

                Installation installation = new Installation(temp);
                var packages = installation.GetPackages();
                Assert.IsTrue(packages.Any(s => s.Name == "OpenTAP"));
                Assert.IsTrue(packages.Any(s => s.Name == "Demonstration"));
                Assert.IsTrue(packages.Any(s => s.Name == "TUI"));


                imageSpecifier.Packages.Remove(imageSpecifier.Packages.FirstOrDefault(s => s.Name == "Demonstration"));
                stopwatch.Restart();
                image = imageSpecifier.Resolve(CancellationToken.None);
                Console.WriteLine($"Second resolve: {stopwatch.ElapsedMilliseconds} ms");
                stopwatch.Restart();
                image.Deploy(temp, CancellationToken.None);
                Console.WriteLine($"Second deploy: {stopwatch.ElapsedMilliseconds} ms");
                
                installation = new Installation(temp);
                packages = installation.GetPackages();
                Assert.IsTrue(packages.Any(s => s.Name == "OpenTAP"));
                Assert.IsTrue(packages.Any(s => s.Name == "TUI"));
                Assert.IsFalse(packages.Any(s => s.Name == "Demonstration"));
            }
            finally
            {
                if (Directory.Exists(temp))
                    Directory.Delete(temp, true);
            }
        }

        [Test]
        public void DoNotDownloadPackagesThatAreAlreadyInstalled()
        {
            string temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            ImageSpecifier imageSpecifier = new ImageSpecifier();
            imageSpecifier.Repositories = new List<string>() { "packages.opentap.io" };
            imageSpecifier.Packages = new List<PackageSpecifier>
            {
                new PackageSpecifier("OpenTAP"),
                new PackageSpecifier("Demonstration"),
                new PackageSpecifier("TUI")
            };
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                var image = imageSpecifier.Resolve(CancellationToken.None);
                image.Deploy(temp, CancellationToken.None);
                PackageCacheHelper.ClearCache(); // Remove downloaded packages from cache
                Console.WriteLine($"Test installation deployed: {stopwatch.ElapsedMilliseconds} ms");


                imageSpecifier.Packages.Add(new PackageSpecifier("SDK"));
                stopwatch.Restart();
                image = imageSpecifier.Resolve(CancellationToken.None);
                image.Deploy(temp, CancellationToken.None);
                Console.WriteLine($"Second deploy: {stopwatch.ElapsedMilliseconds} ms");

                // Now we should only have the SDK package in the chache.
                // As the other packages in the image were already in stalled in the correct version, they should not have been downloaded at all
                Assert.AreEqual(1,Directory.GetFiles(PackageCacheHelper.PackageCacheDirectory).Count(),"Unexpected number of files in cache.");
                Assert.IsTrue(File.Exists(PackageCacheHelper.GetCacheFilePath(image.Packages.First(p => p.Name == "SDK"))));

                var installation = new Installation(temp);
                var packages = installation.GetPackages();
                Assert.AreEqual(4, packages.Count, "Unexpected number of packages in installation.");
                Assert.IsTrue(packages.Any(s => s.Name == "OpenTAP"));
                Assert.IsTrue(packages.Any(s => s.Name == "TUI"));
                Assert.IsTrue(packages.Any(s => s.Name == "Demonstration"));
                Assert.IsTrue(packages.Any(s => s.Name == "SDK"));
            }
            finally
            {
                if (Directory.Exists(temp))
                    Directory.Delete(temp, true);
            }
        }
    }
}
