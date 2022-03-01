using NUnit.Framework;
using OpenTap.EngineUnitTestUtils;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace OpenTap.Image.Tests
{
    public class Workflows
    {
        [OneTimeSetUp]
        public void SetUpMockedRepository()
        {
            MockInstallHelper.MockRepo();
        }

        [Test]
        public void DeployImage()
        {
            using var install1 = MockInstallHelper.CreateInstall();
            using var install2 = MockInstallHelper.CreateInstall();

            ImageSpecifier imageSpecifier = MockInstallHelper.CreateSpecifier();
            imageSpecifier.Packages = new List<PackageSpecifier>
            {
                new PackageSpecifier("Demonstration"),
                new PackageSpecifier("TUI")
            };

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
            image.Deploy(install1.Directory, CancellationToken.None);
            Console.WriteLine($"First deploy: {stopwatch.ElapsedMilliseconds} ms");

            Installation installation = install1.Installation;
            var packages = installation.GetPackages();
            Assert.IsTrue(packages.Any(s => s.Name == "OpenTAP"));
            Assert.IsTrue(packages.Any(s => s.Name == "Demonstration"));
            Assert.IsTrue(packages.Any(s => s.Name == "TUI"));

            stopwatch.Restart();
            image.Deploy(install2.Directory, CancellationToken.None);
            Console.WriteLine($"Second deploy: {stopwatch.ElapsedMilliseconds} ms");
        }

        [Test]
        public void DeployImageOnExistingInstallation()
        {
            using var tempInstall = MockInstallHelper.CreateInstall();

            ImageSpecifier imageSpecifier = MockInstallHelper.CreateSpecifier();
            imageSpecifier.Packages = new List<PackageSpecifier>
            {
                new PackageSpecifier("Demonstration"),
                new PackageSpecifier("TUI")
            };

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
            image.Deploy(tempInstall.Directory, CancellationToken.None);
            Console.WriteLine($"First deploy: {stopwatch.ElapsedMilliseconds} ms");

            Installation installation = tempInstall.Installation;
            var packages = installation.GetPackages();
            Assert.IsTrue(packages.Any(s => s.Name == "OpenTAP"));
            Assert.IsTrue(packages.Any(s => s.Name == "Demonstration"));
            Assert.IsTrue(packages.Any(s => s.Name == "TUI"));


            imageSpecifier.Packages.Remove(imageSpecifier.Packages.FirstOrDefault(s => s.Name == "Demonstration"));
            stopwatch.Restart();
            image = imageSpecifier.Resolve(CancellationToken.None);
            Console.WriteLine($"Second resolve: {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Restart();
            image.Deploy(tempInstall.Directory, CancellationToken.None);
            Console.WriteLine($"Second deploy: {stopwatch.ElapsedMilliseconds} ms");

            installation = tempInstall.Installation;
            packages = installation.GetPackages();
            Assert.IsTrue(packages.Any(s => s.Name == "OpenTAP"));
            Assert.IsTrue(packages.Any(s => s.Name == "TUI"));
            Assert.IsFalse(packages.Any(s => s.Name == "Demonstration"));
        }

        [Test]
        public void DoNotDownloadPackagesThatAreAlreadyInstalled()
        {
            using var tempInstall = MockInstallHelper.CreateInstall();

            ImageSpecifier imageSpecifier = MockInstallHelper.CreateSpecifier();
            imageSpecifier.Packages = new List<PackageSpecifier>
            {
                new PackageSpecifier("OpenTAP"),
                new PackageSpecifier("Demonstration"),
                new PackageSpecifier("TUI")
            };

            Stopwatch stopwatch = Stopwatch.StartNew();
            var image = imageSpecifier.Resolve(CancellationToken.None);
            image.Deploy(tempInstall.Directory, CancellationToken.None);
            PackageCacheHelper.ClearCache(); // Remove downloaded packages from cache
            Console.WriteLine($"Test installation deployed: {stopwatch.ElapsedMilliseconds} ms");


            imageSpecifier.Packages.Add(new PackageSpecifier("SDK"));
            stopwatch.Restart();
            image = imageSpecifier.Resolve(CancellationToken.None);
            image.Deploy(tempInstall.Directory, CancellationToken.None);
            Console.WriteLine($"Second deploy: {stopwatch.ElapsedMilliseconds} ms");

            // Now we should only have the SDK package in the cache.
            // As the other packages in the image were already in stalled in the correct version, they should not have been downloaded at all
            Assert.AreEqual(1, Directory.GetFiles(PackageCacheHelper.PackageCacheDirectory).Count(), "Unexpected number of files in cache.");
            Assert.IsTrue(File.Exists(PackageCacheHelper.GetCacheFilePath(image.Packages.First(p => p.Name == "SDK"))));

            var installation = tempInstall.Installation;
            var packages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
            Assert.AreEqual(4, packages.Count, "Unexpected number of packages in installation.");
            Assert.IsTrue(packages.Any(s => s.Name == "OpenTAP"));
            Assert.IsTrue(packages.Any(s => s.Name == "TUI"));
            Assert.IsTrue(packages.Any(s => s.Name == "Demonstration"));
            Assert.IsTrue(packages.Any(s => s.Name == "SDK"));
        }

        [Test]
        public void UninstallInOrder()
        {
            using var tempInstall = MockInstallHelper.CreateInstall();

            ImageSpecifier imageSpecifier = MockInstallHelper.CreateSpecifier();
            imageSpecifier.Packages = new List<PackageSpecifier>
            {
                new PackageSpecifier("OpenTAP"),
                new PackageSpecifier("OSIntegration")
            };

            Stopwatch stopwatch = Stopwatch.StartNew();
            var image = imageSpecifier.Resolve(CancellationToken.None);
            image.Deploy(tempInstall.Directory, CancellationToken.None);
            PackageCacheHelper.ClearCache(); // Remove downloaded packages from cache
            Console.WriteLine($"Test installation deployed: {stopwatch.ElapsedMilliseconds} ms");

            imageSpecifier.Packages.Clear();
            stopwatch.Restart();
            image = imageSpecifier.Resolve(CancellationToken.None);

            TestTraceListener logListener = new TestTraceListener();
            Log.AddListener(logListener);

            image.Deploy(tempInstall.Directory, CancellationToken.None);
            Console.WriteLine($"Second deploy (uninstall): {stopwatch.ElapsedMilliseconds} ms");

            var uninstallLog = logListener.allLog.ToString();
            StringAssert.DoesNotContain("Error", uninstallLog, $"Errors in uninstall log:\n {uninstallLog}");
            StringAssert.Contains("Starting uninstall step 'tap package list -i'", uninstallLog, $"Errors in uninstall log:\n {uninstallLog}");

            var installation = tempInstall.Installation;
            var packages = installation.GetPackages().Where(s => s.Class != "system-wide");
            Assert.AreEqual(0, packages.Count(), "Unexpected packages left in installation after uninstall.");
        }
    }
}
