using NUnit.Framework;
using NUnit.Framework.Internal;
using OpenTap.Diagnostic;
using OpenTap.Image;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace OpenTap.Image.Tests
{
    [TestFixture]
    public class Resolve
    {
        [Test]
        public void ResolveAndVerifyID()
        {
            ImageSpecifier imageSpecifier = new ImageSpecifier()
            {
                Packages = new List<PackageSpecifier>() {
                    new PackageSpecifier("OpenTAP", VersionSpecifier.Parse("9.15.2+39e6c2a2"), os: "Windows"),
                    new PackageSpecifier("Demonstration", VersionSpecifier.Parse("9.0.5+3cab80c8"))
                },
                Repositories = new List<string>() { "packages.opentap.io" }
            };
            var image = imageSpecifier.Resolve(System.Threading.CancellationToken.None);
            Assert.IsNotNull(image);

            Assert.AreEqual(2, image.Packages.Count());
            foreach (var specifier in imageSpecifier.Packages)
                Assert.IsTrue(image.Packages.Any(s => s.Name == specifier.Name && s.Version.ToString() == specifier.Version.ToString()));

            var image2 = imageSpecifier.Resolve(System.Threading.CancellationToken.None);
            Assert.AreEqual(image2.Id, image.Id);
        }

        [Test]
        public void ResolveMultiMajors()
        {
            ImageSpecifier imageSpecifier = new ImageSpecifier()
            {
                Packages = new List<PackageSpecifier>() {
                    new PackageSpecifier("REST-API", VersionSpecifier.Parse("2.4.0")),
                    new PackageSpecifier("REST-API", VersionSpecifier.Parse("1.10.5"))

                },
                Repositories = new List<string>() { "packages.opentap.io" }
            };
            try
            {
                var image = imageSpecifier.Resolve(System.Threading.CancellationToken.None);
                Assert.Fail("This may not be resolved");
            }
            catch (AggregateException ex)
            {
                Assert.AreEqual(1, ex.InnerExceptions.Count());
            }
        }

        [Test]
        public void ResolveMissingPackage()
        {
            ImageSpecifier imageSpecifier = new ImageSpecifier()
            {
                Packages = new List<PackageSpecifier>() {
                    new PackageSpecifier("Weyoooo", VersionSpecifier.Parse("112.1337.0"))
                },
                Repositories = new List<string>() { "packages.opentap.io" }
            };
            try
            {
                var image = imageSpecifier.Resolve(System.Threading.CancellationToken.None);
                Assert.Fail("This should fail to resolve");
            }
            catch (AggregateException ex)
            {
                Assert.AreEqual(1, ex.InnerExceptions.Count());
            }
        }

        // two conflicting major versions of the same plugin should just refuse to resolve
        [TestCase("OpenTAP", "9.10", "8.8", "error")]

        // two different minor versions should reslove to a version semantically compatible with both.
        // We should pick the lowest compatible minor version, to keep the resolution result as stable as possible.  
        [TestCase("OpenTAP", "9.10", "9.11", "error")]

        // In the context of image resolution, ^ means "This or compatible version"
        [TestCase("OpenTAP", "^9.10", null, "9.10.1")] //^9.10 This does not work as intended
        [TestCase("OpenTAP", "9", null, "latest")]

        // When one of two conflicting minor versions specifies ^, but the other specifies a later minor, take the later minor,
        // but do not move all the way to the latest version to keep the resolution result as stable as possible.
        [TestCase("OpenTAP", "^9.10", "9.11", "9.11")]
        [TestCase("OpenTAP", "9.11", "^9.10", "9.11")]
        [TestCase("OpenTAP", "9.10", "^9.11", "error")]
        [TestCase("OpenTAP", "9.10", "^9.10", "9.10")]

        // Same version specified twice
        [TestCase("OpenTAP", "9.10", "9.10", "9.10.1")]
        [TestCase("OpenTAP", "9.10.0", "9.10.0", "9.10.0")]

        // don't trip if there is no exact specification
        [TestCase("OpenTAP", "^9.10", "^9.10", "9.10.1")]
        [TestCase("OpenTAP", "^9.10.0", "^9.10.0", "9.10.0")]

        // Specifying not-exact & exact versions should equal the exact version they have same preceding elements
        [TestCase("OpenTAP", "9.13", "9.13.1", "9.13.1")]
        [TestCase("OpenTAP", "9", "9.13.1", "9.13.1")]

        // Specifying not-exact & exact versions should error when they do not have same preceding elements
        [TestCase("OpenTAP", "9.14", "9.13.1", "error")]
        [TestCase("OpenTAP", "^9.13", "9.13.2", "9.13.2")]

        [TestCase("Cyclic", "1.0.0", null, "1.0.0")]

        // [TestCase("OpenTAP", "^9.13", "9.14.0", "error")] ^9.13 This does not work as intended
        public void ResolveVersionConflicts(string packageName, string firstVersion, string secondVersion, string resultingVersion)
        {
            PackageRepositoryHelpers.RegisterRepository(new MockRepository("mock://localhost"));
            ImageSpecifier imageSpecifier = new ImageSpecifier();
            imageSpecifier.Repositories = new List<string>() { "mock://localhost" };
            imageSpecifier.Packages.Add(new PackageSpecifier(packageName, VersionSpecifier.Parse(firstVersion)));
            if (secondVersion != null)
                imageSpecifier.Packages.Add(new PackageSpecifier(packageName, VersionSpecifier.Parse(secondVersion)));
            try
            {
                var image = imageSpecifier.Resolve(CancellationToken.None);
                if (resultingVersion is null || resultingVersion == "error")
                    Assert.Fail("This should fail to resolve");
                if (resultingVersion == "latest")
                {
                    var repo = PackageRepositoryHelpers.DetermineRepositoryType("mock://localhost");
                    var latest = repo.GetPackageVersions(packageName).Select(v => v.Version).Where(v => v.PreRelease == null).Distinct().ToList();
                    StringAssert.StartsWith(latest.First().ToString(), image.Packages.First(p => p.Name == packageName).Version.ToString());
                }
                else
                    StringAssert.StartsWith(resultingVersion, image.Packages.First(p => p.Name == packageName).Version.ToString());
            }
            catch (AggregateException ex)
            {
                if (resultingVersion is null || resultingVersion == "error")
                    Assert.AreEqual(1, ex.InnerExceptions.Count());
                else
                    throw;
            }
        }

        [TestCase("OpenTAP", "Native", CpuArchitecture.AnyCPU, CpuArchitecture.x86, CpuArchitecture.x86)]
        //[TestCase("Native", "Native2", CpuArchitecture.x64, CpuArchitecture.x86, null)] // We are more tolorant, and resolve this to ArchitectureHelper.GuessBaseArchitecture
        [TestCase("Native", "Native2", CpuArchitecture.x64, CpuArchitecture.Unspecified, CpuArchitecture.x64)]
        [TestCase("Native", "Native2", CpuArchitecture.x86, CpuArchitecture.Unspecified, CpuArchitecture.x86)]
        [TestCase("Native", "Native2", CpuArchitecture.Unspecified, CpuArchitecture.x86, CpuArchitecture.x86)]
        [TestCase("Native", "Native2", CpuArchitecture.Unspecified, CpuArchitecture.x64, CpuArchitecture.x64)]
        public void ResolveArchConflicts(string packageName1, string packageName2, CpuArchitecture firstArch, CpuArchitecture secondArch, CpuArchitecture? resultingArch)
        {
            PackageRepositoryHelpers.RegisterRepository(new MockRepository("mock://localhost"));
            ImageSpecifier imageSpecifier = new ImageSpecifier();
            imageSpecifier.Repositories = new List<string>() { "mock://localhost" };
            imageSpecifier.Packages.Add(new PackageSpecifier(packageName1, VersionSpecifier.Any, firstArch));
            imageSpecifier.Packages.Add(new PackageSpecifier(packageName2, VersionSpecifier.Any, secondArch));
            try
            {
                var image = imageSpecifier.Resolve(CancellationToken.None);
                if (!resultingArch.HasValue)
                    Assert.Fail($"This should fail to resolve, but resolved to {String.Join(",", image.Packages.Select(p => p.Architecture))}");
                Assert.IsTrue(image.Packages.All(p => p.Architecture == CpuArchitecture.AnyCPU || p.Architecture == resultingArch.Value), $"Package archs was {String.Join(", ", image.Packages.Select(p => p.Architecture))}");
            }
            catch (AggregateException ex)
            {
                if (!resultingArch.HasValue)
                    Assert.AreEqual(1, ex.InnerExceptions.Count());
                else
                    throw;
            }
        }

        [TestCase("OpenTAP", "9.10.0", "Demonstration", "9.2.0", "error", "error")]
        [TestCase("OpenTAP", "9.10.0", "ExactDependency", "1.0.0", "error", "error")]
        [TestCase("OpenTAP", "9.13.1", "ExactDependency", "1.0.0", "9.13.1", "1.0.0")]
        [TestCase("OpenTAP", "9.13", "ExactDependency", "1.0.0", "9.13.1", "1.0.0")]
        [TestCase("Cyclic", "1.0.0", "Cyclic2", "1.0.0", "1.0.0", "1.0.0")]

        public void ResolvePackages(string package1, string package1version, string package2, string package2version, string resultingVersion1, string resultingVersion2)
        {
            PackageRepositoryHelpers.RegisterRepository(new MockRepository("mock://localhost"));
            ImageSpecifier imageSpecifier = new ImageSpecifier();
            imageSpecifier.Repositories = new List<string>() { "mock://localhost" };
            imageSpecifier.Packages.Add(new PackageSpecifier(package1, VersionSpecifier.Parse(package1version)));
            imageSpecifier.Packages.Add(new PackageSpecifier(package2, VersionSpecifier.Parse(package2version)));
            try
            {
                var image = imageSpecifier.Resolve(CancellationToken.None);
                if (resultingVersion1 is null || resultingVersion1 == "error")
                    Assert.Fail("This should fail to resolve");

                StringAssert.StartsWith(resultingVersion1, image.Packages.First(p => p.Name == package1).Version.ToString());
                StringAssert.StartsWith(resultingVersion2, image.Packages.First(p => p.Name == package2).Version.ToString());
            }
            catch (AggregateException ex)
            {
                if (resultingVersion1 is null || resultingVersion1 == "error")
                    Assert.AreEqual(1, ex.InnerExceptions.Count());
                else
                    throw;
            }
        }


        [Test]
        public void ResolveDependencyVersionConflicts()
        {
            PackageRepositoryHelpers.RegisterRepository(new MockRepository("mock://localhost"));
            ImageSpecifier imageSpecifier = new ImageSpecifier();
            imageSpecifier.Repositories.Add("mock://localhost");

            // these might come from a KS8500 Station Image definition
            imageSpecifier.Packages.Add("OpenTAP", "9");
            imageSpecifier.Packages.Add("Demonstration", "9.1");

            imageSpecifier.Packages.Add("MyDemoTestPlan", "1.0.0"); // this depends on Demo ^9.0.2

            var image = imageSpecifier.Resolve(System.Threading.CancellationToken.None);

            // Make sure we got version 9.1, even when the repo contained 9.2
            StringAssert.StartsWith("9.1", image.Packages.First(p => p.Name == "Demonstration").Version.ToString());
        }


        [Test]
        public void TreeWithMissing()
        {
            var packages = new List<PackageSpecifier>();

            packages.Add("OpenTAP", "^9.10.0"); // this depends on Demo ^9.0.2
            packages.Add("MyDemoTestPlan", "1.0.0"); // this depends on Demo ^9.0.2
            packages.Add("Unknown", "1.0.0"); // this does not exist

            MockRepository repository = new MockRepository("mock://localhost");
            var repositories = new List<IPackageRepository> { repository };

            DependencyResolver resolver = new DependencyResolver(packages, repositories, CancellationToken.None);

            string resolved = resolver.GetDotNotation("Image");
            var unknownLine = resolved.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(p => p.Contains("Unknown"));
            Assert.IsNotNull(unknownLine);
            TestContext.WriteLine(resolved);
        }

        [Test]
        public void TestUnknownDependencies()
        {
            var packages = new List<PackageSpecifier>();

            packages.Add("OpenTAP", "^9.10.0");
            packages.Add("MyDemoTestPlan", "1.0.0"); // this depends on Demo ^9.0.2
            packages.Add("Unknown", "1.0.0"); // this does not exist

            var repositories = new List<IPackageRepository> { new MockRepository("mock://localhost") };

            DependencyResolver resolver = new DependencyResolver(packages, repositories, CancellationToken.None);
            Assert.AreEqual(1, resolver.UnknownDependencies.Count);
            Assert.AreEqual("Unknown", resolver.UnknownDependencies.FirstOrDefault().Name);
        }

        [Test]
        public void TestMissingDependencies()
        {
            var packages = new List<PackageSpecifier>();

            packages.Add("OpenTAP", "^9.10.0");
            packages.Add("MyDemoTestPlan", "1.0.0"); // this depends on Demo ^9.0.2
            packages.Add("Unknown", "1.0.0"); // this does not exist

            var repositories = new List<IPackageRepository> { new MockRepository("mock://localhost") };

            DependencyResolver resolver = new DependencyResolver(packages, repositories, CancellationToken.None);
            Assert.AreEqual(3, resolver.MissingDependencies.Count);
            Assert.IsTrue(resolver.MissingDependencies.Any(s => s.Name == "OpenTAP"));
            Assert.IsTrue(resolver.MissingDependencies.Any(s => s.Name == "MyDemoTestPlan"));
            Assert.IsTrue(resolver.MissingDependencies.Any(s => s.Name == "Demonstration"));
        }

        [TestCase("OpenTAP", "^9.10.0", "MyDemoTestPlan", "1.0.0", 3, "OpenTAP:9.12.1", "MyDemoTestPlan:1.0.0", "Demonstration:9.0.2")]
        public void TestDependencies(string packageName, string firstVersion, string secondPackageName, string secondVersion, int dependenciesCount, params string[] resolved)
        {
            var packages = new List<PackageSpecifier>();

            packages.Add(packageName, firstVersion); 
            packages.Add(secondPackageName, secondVersion); // this depends on Demo ^9.0.2

            var repositories = new List<IPackageRepository> { new MockRepository("mock://localhost") };

            DependencyResolver resolver = new DependencyResolver(packages, repositories, CancellationToken.None);
            Assert.AreEqual(dependenciesCount, resolver.Dependencies.Count);
            foreach (var res in resolved) {
                var split = res.Split(':');
                Assert.IsTrue(resolver.Dependencies.FirstOrDefault(s => s.Name == split[0]).Version.ToString().StartsWith(split[1]));
            }
        }


        static List<IPackageRepository> repos = new List<IPackageRepository>
        {
            new MockRepository("hej"),
            new HttpPackageRepository("packages.opentap.io")
        };
        /// <summary>
        /// Test that the mock repo behaves the same way as the http one
        /// </summary>
        [TestCaseSource(nameof(repos))]
        public void MockRepositoryTester(IPackageRepository repo)
        {
            var versions = repo.GetPackageVersions("OpenTAP");
            CollectionAssert.IsOrdered(versions.Select(p => p.Version).Reverse());
            var latestReleaseVersion = versions.Select(p => p.Version).First(v => v.PreRelease == null);
            var latestVersion = versions.Select(p => p.Version).First();

            var spec = new PackageSpecifier("OpenTAP", VersionSpecifier.Parse("^9.10"));
            var pkgs = repo.GetPackages(spec);
            Assert.AreEqual(1, pkgs.Select(p => p.Version).Distinct().Count());
            Assert.AreEqual(latestReleaseVersion, pkgs.First().Version);

            var spec2 = new PackageSpecifier("OpenTAP", VersionSpecifier.Parse("^9.10-beta"));
            var pkgs2 = repo.GetPackages(spec2);
            Assert.AreEqual(1, pkgs2.Select(p => p.Version).Distinct().Count());
            Assert.AreEqual(latestVersion, pkgs2.First().Version);

            var spec3 = new PackageSpecifier("OpenTAP", VersionSpecifier.Parse("beta"));
            var pkgs3 = repo.GetPackages(spec3);
            Assert.AreEqual(1, pkgs3.Select(p => p.Version).Distinct().Count());
            Assert.AreEqual(latestVersion, pkgs3.First().Version);
        }

   }

    public static class HelperExtensions
    {
        public static void Add(this List<PackageSpecifier> packages, string packageName, string packageVersion)
        {
            packages.Add(new PackageSpecifier(packageName, VersionSpecifier.Parse(packageVersion)));
        }
    }
}
