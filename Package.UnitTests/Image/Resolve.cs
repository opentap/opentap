using NUnit.Framework;
using NUnit.Framework.Internal;
using OpenTap.Authentication;
using OpenTap.Diagnostic;
using OpenTap.Image;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.IO;
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
            var imageSpecifier = MockRepository.CreateSpecifier();

            imageSpecifier.Packages = new List<PackageSpecifier>()
            {
                new PackageSpecifier("OpenTAP", VersionSpecifier.Parse("9.15.2+39e6c2a2"), os: "Windows"),
                new PackageSpecifier("Demonstration", VersionSpecifier.Parse("9.0.5+3cab80c8"))
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
        public void SimpleResolve()
        {
            var imageSpecifier = MockRepository.CreateSpecifier();
            imageSpecifier.Packages = new List<PackageSpecifier>()
            {
                new PackageSpecifier("OpenTAP")
            };
            try
            {
                var image = imageSpecifier.Resolve(System.Threading.CancellationToken.None);
            }
            catch
            {
                Assert.Fail("This should resolve");
            }
        }

        [TestCase("A", "1", "1.1.1")]
        [TestCase("A", "1", "latest")]
        [TestCase("A", "2", "error")]
        [TestCase("A", "1.2", "error")]
        [TestCase("A", "1.0.0", "1.0.0")]
        [TestCase("A", "1.0.1", "1.0.1")]
        [TestCase("Weyoooooo", "112.1337.0", "error")]
        public void SimpleVersionResolveCases(string packageName, string version, string resultingVersion)
        {
            var imageSpecifier = MockRepository.CreateSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier(packageName, VersionSpecifier.Parse(version)));

            try
            {
                var image = imageSpecifier.Resolve(System.Threading.CancellationToken.None);

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
                var resolvedPackage = image.Packages.FirstOrDefault();
                Assert.AreEqual(packageName, resolvedPackage.Name);
            }
            catch
            {
                if (resultingVersion == "error")
                    Assert.Pass();
            }
        }

        [TestCase("B", "1", "linux", CpuArchitecture.x86, "1.1.1")]
        [TestCase("B", "1", "Linux", CpuArchitecture.x86, "1.1.1")]
        [TestCase("B", "1.0", "Linux", CpuArchitecture.x86, "1.0.0")]
        [TestCase("B", "1.0", "Linux", CpuArchitecture.x64, "error")]
        [TestCase("B", "1.0", "Windows", CpuArchitecture.x86, "error")]
        [TestCase("B", "2.0", "Windows", CpuArchitecture.x86, "error")]
        [TestCase("B", "3.0", "Windows", CpuArchitecture.x64, "3.0.2")] // 3.0 means we want latest 3.0 release
        [TestCase("B", "^3.0", "Windows", CpuArchitecture.x64, "3.0.2")] // ^3.0 means we want latest compatible 3.0 release
        [TestCase("B", "^3.0.1", "Windows", CpuArchitecture.x64, "3.0.1")] // ^3.0.1 means we want 3.0.1 or compatible
        [TestCase("B", "3", "Windows", CpuArchitecture.x64, "3.1.1")] // 3 means we want latest 3 release
        [TestCase("B", "2.1.0", "Linux", CpuArchitecture.x86, "2.1.0")]
        [TestCase("B", "", "Linux", CpuArchitecture.x86, "2.1.1")]
        [TestCase("B", "beta", "Linux", CpuArchitecture.x86, "2.1.0-beta.1")]
        [TestCase("B", "", "Windows", CpuArchitecture.x64, "3.1.1")] // empty means we want latest release
        [TestCase("B", "3.0.0", "Windows", CpuArchitecture.x64, "3.0.0")] // full version means exact
        [TestCase("B", "beta", "Windows", CpuArchitecture.x64, "3.2.1-beta.1")]
        [TestCase("B", "3.2.1-beta.1", "Windows", CpuArchitecture.x64, "3.2.1-beta.1")]
        [TestCase("B", "beta", "", CpuArchitecture.AnyCPU, "3.2.1-beta.1")] // means we want latest, but minimum beta
        [TestCase("B", "3-beta", "", CpuArchitecture.AnyCPU, "3.2.1-beta.1")] // means we want latest 3 beta
        [TestCase("B", "2-beta", "Linux", CpuArchitecture.x86, "2.1.0-beta.1")] // means we want latest 2 beta
        [TestCase("B", "^3-beta", "", CpuArchitecture.AnyCPU, "3.2.1-beta.1")] // means we want latest 3, minimum beta
        [TestCase("B", "^2-beta", "Linux", CpuArchitecture.x86, "2.1.0-beta.1")] // means we want latest 2 beta
        [TestCase("B", "any", "", CpuArchitecture.AnyCPU, "3.2.2-alpha.1")]  // any means we want latest, even if it is a prerelease
        [TestCase("C", "^1.0-beta", "Linux", CpuArchitecture.x86, "1.0.0-beta.1")]
        [TestCase("C", "1.0-beta", "Linux", CpuArchitecture.x86, "1.0.0-beta.1")]
        [TestCase("C", "^1-beta", "Linux", CpuArchitecture.x86, "1.0.0-beta.1")]
        [TestCase("C", "1-beta", "Linux", CpuArchitecture.x86, "1.0.0-beta.1")]
        [TestCase("C", "beta", "Linux", CpuArchitecture.x86, "2.0.0-beta.1")]
        [TestCase("C", "^beta", "Linux", CpuArchitecture.x86, "2.0.0")]
        [TestCase("C", "alpha", "Linux", CpuArchitecture.x86, "2.0.0-alpha.1")]
        [TestCase("C", "rc", "Linux", CpuArchitecture.x86, "2.0.0-rc.1")]
        [TestCase("C", "", "Linux", CpuArchitecture.x86, "2.0.0")]
        [TestCase("C", "^beta", "Linux", CpuArchitecture.x86, "2.0.0")]
        [TestCase("C", "^alpha", "Linux", CpuArchitecture.x86, "2.0.0")]
        [TestCase("C", "^rc", "Linux", CpuArchitecture.x86, "2.0.0")]
        [TestCase("D", "^rc", "Linux", CpuArchitecture.x86, "2.1.0-rc.1")]
        [TestCase("E", "2.1.0-beta.1", "Linux", CpuArchitecture.x86, "2.1.0-beta.1")]
        [TestCase("E", "2.1.0-beta", "Linux", CpuArchitecture.x86, "2.1.0-beta.2")]
        [TestCase("E", "2.1.0-beta.2", "Linux", CpuArchitecture.x86, "2.1.0-beta.2")]
        [TestCase("E", "2.2.0-alpha.2", "Linux", CpuArchitecture.x86, "2.2.0-alpha.2.2")]
        [TestCase("E", "2.2.0-alpha.2.1", "Linux", CpuArchitecture.x86, "2.2.0-alpha.2.1")]
        [TestCase("E", "2.2.0-alpha.2.2", "Linux", CpuArchitecture.x86, "2.2.0-alpha.2.2")]
        [TestCase("E", "2.2.0-alpha", "Linux", CpuArchitecture.x86, "2.2.0-alpha.2.2")]
        [TestCase("F", "1.0", "Linux", CpuArchitecture.x86, "error")]  // we ask for 1.0, but there is only 1.1 and above in the repo
        [TestCase("F", "^1.0", "Linux", CpuArchitecture.x86, "1.1.1")] // There is only 1.1 and above in the repo, so we should get 1.1.1 (lowest compatible version in the fields we specify)
        public void FullResolveCases(string packageName, string version, string os, CpuArchitecture cpuArchitecture, string resultingVersion)
        {
            var imageSpecifier = MockRepository.CreateSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier(packageName, VersionSpecifier.Parse(version), cpuArchitecture, os));

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
                var resolvedPackage = image.Packages.FirstOrDefault();
                Assert.AreEqual(packageName, resolvedPackage.Name);
                if (os != "")
                    Assert.AreEqual(os.ToLower(), resolvedPackage.OS.ToLower());
                if (cpuArchitecture != CpuArchitecture.AnyCPU)
                    Assert.AreEqual(cpuArchitecture, resolvedPackage.Architecture);
            }
            catch (ImageResolveException ex)
            {
                if (resultingVersion == "error")
                    Assert.Pass();
                else
                    Assert.Fail(ex.Message);
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

        [TestCase("OpenTAP", "^9.13.0", "^9.13.2-beta.1", "9.13.2-beta.1")]
        [TestCase("OpenTAP", "^9.12.0", "^9.13.2-beta.1", "9.13.2-beta.1")]
        [TestCase("OpenTAP", "^9.13.2", "^9.13.2-beta.1", "9.13.2")]
        [TestCase("G", "^2.1.0-beta.2", "2.2.0-alpha.2.1", "2.2.0-alpha.2.1")]
        [TestCase("G", "^2.0.0-rc.1", "2.2.0-alpha.2.1", "2.2.0-alpha.2.1")]
        [TestCase("G", "^2.0.0-rc.1", "2.1.0-beta.2", "2.1.0-beta.2")]
        [TestCase("G", "^2.1.0-beta.1", "2.1.0-beta.2", "2.1.0-beta.2")]
        [TestCase("G", "^2.2.0-alpha.2.2", "2.3.0-rc.1", "2.3.0-rc.1")]
        [TestCase("G", "^2.0.0-rc.1", "2.3.0-rc.1", "2.3.0-rc.1")]
        [TestCase("G", "^2.1.0-beta.2", "2.0.0-rc.1", "error")]
        [TestCase("G", "^2.1.0-beta.2", "2.1.0-beta.1", "error")]
        [TestCase("Cyclic", "1.0.0", null, "1.0.0")]
        // [TestCase("OpenTAP", "^9.13", "9.14.0", "error")] ^9.13 This does not work as intended
        public void ResolveVersionConflicts(string packageName, string firstVersion, string secondVersion, string resultingVersion)
        {
            var imageSpecifier = MockRepository.CreateSpecifier();
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
                    StringAssert.AreEqualIgnoringCase(latest.First().ToString(), image.Packages.First(p => p.Name == packageName).Version.ToString());
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
            var imageSpecifier = MockRepository.CreateSpecifier();
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
            var imageSpecifier = MockRepository.CreateSpecifier();
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
            var imageSpecifier = MockRepository.CreateSpecifier();

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

            var repositories = new List<IPackageRepository> { MockRepository.Instance };

            DependencyResolver resolver = new DependencyResolver(packages, repositories, CancellationToken.None);

            string resolved = resolver.GetDotNotation();
            var unknownLine = resolved.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(p => p.Contains("Unknown"));
            Assert.IsNotNull(unknownLine);
            TestContext.WriteLine(resolved);
        }

        [Test]
        public void TrimOperatingSystemEntries()
        {
            IPackageIdentifier packageIdentifier = new PackageIdentifier("TrimOperatingSystemPackage", new SemanticVersion(1, 1, 1, null, null), CpuArchitecture.AnyCPU, "Windows , Linux");
            Assert.IsTrue(packageIdentifier.IsPlatformCompatible(CpuArchitecture.x86, "Linux"));
            Assert.IsTrue(packageIdentifier.IsPlatformCompatible(CpuArchitecture.x64, "linux"));
            Assert.IsTrue(packageIdentifier.IsPlatformCompatible(CpuArchitecture.x86, "Windows"));
            Assert.IsTrue(packageIdentifier.IsPlatformCompatible(CpuArchitecture.x64, "windows"));
            Assert.IsTrue(packageIdentifier.IsPlatformCompatible(CpuArchitecture.x86, "Linux, windows"));
            Assert.IsTrue(packageIdentifier.IsPlatformCompatible(CpuArchitecture.x64, "linux, Windows"));
        }

        [Test]
        public void TestUnknownDependencies()
        {
            var packages = new List<PackageSpecifier>();

            packages.Add("OpenTAP", "^9.10.0");
            packages.Add("MyDemoTestPlan", "1.0.0"); // this depends on Demo ^9.0.2
            packages.Add("Unknown", "1.0.0"); // this does not exist

            var repositories = new List<IPackageRepository> { MockRepository.Instance };

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

            var repositories = new List<IPackageRepository> { MockRepository.Instance };

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

            var repositories = new List<IPackageRepository> { MockRepository.Instance };

            DependencyResolver resolver = new DependencyResolver(packages, repositories, CancellationToken.None);
            Assert.AreEqual(dependenciesCount, resolver.Dependencies.Count);
            foreach (var res in resolved)
            {
                var split = res.Split(':');
                Assert.IsTrue(resolver.Dependencies.FirstOrDefault(s => s.Name == split[0]).Version.ToString().StartsWith(split[1]));
            }
        }
    }

    public static class HelperExtensions
    {
        public static void Add(this List<PackageSpecifier> packages, string packageName, string packageVersion)
        {
            packages.Add(new PackageSpecifier(packageName, VersionSpecifier.Parse(packageVersion)));
        }

        public static PackageDef WithPackageAction(this PackageDef def, ActionStep step)
        {
            def.PackageActionExtensions.Add(step);
            return def;
        }
    }
}
