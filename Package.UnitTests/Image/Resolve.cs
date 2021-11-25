using NUnit.Framework;
using OpenTap.Image;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap.Image.Tests
{
    [TestFixture]
    public class Resolve
    {
        [Test]
        public void ResolveRestApiAndDependencies()
        {
            ImageSpecifier imageSpecifier = new ImageSpecifier()
            {
                Packages = new List<PackageSpecifier>() { new PackageSpecifier("REST-API", VersionSpecifier.Parse("2.4.0")) },
                Repositories = new List<string>() { "packages.opentap.io" }
            };
            var image = imageSpecifier.Resolve(System.Threading.CancellationToken.None);
            Assert.IsNotNull(image);

            Assert.AreEqual(4, image.Packages.Count());
            List<string> packagesNamesExpected = new List<string>() { "OpenTAP", "REST-API", "RPC Base", "Keysight Floating Licensing" };
            foreach (var packageName in packagesNamesExpected)
                Assert.IsTrue(image.Packages.Any(s => s.Name == packageName));
        }


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
        [TestCase("REST-API", "2.4.0", "1.10.5", "error")]

        // two different minor versions should reslove to a version semantically compatible with both.
        // We should pick the lowest compatible minor version, to keep the resolution result as stable as possible.  
        [TestCase("OpenTAP","9.10","9.11","9.11")]

        // In the context of image resolution, ^ means "latest compatible version" in the same way it does for `tap packages install`
        [TestCase("OpenTAP", "^9.10", null, "latest")]

        // When one of two conflicting minor versions specifies ^, but the other specifies a later minor, take the later minor,
        // but do not move all the way to the latest version to keep the resolution result as stable as possible.
        [TestCase("OpenTAP", "^9.10", "9.11", "9.11")]
        [TestCase("OpenTAP", "9.10", "^9.11", "9.11")]
        [TestCase("OpenTAP", "9.10", "^9.10", "9.10")]
        
        // Same version specified twice
        [TestCase("OpenTAP", "9.10", "9.10", "9.10")]
        public void ResolveVersionConflicts(string packageName, string firstVersion, string secondVersion, string resultingVersion)
        {
            ImageSpecifier imageSpecifier = new ImageSpecifier();
            imageSpecifier.Repositories = new List<string>() { "packages.opentap.io" };
            imageSpecifier.Packages.Add(new PackageSpecifier(packageName, VersionSpecifier.Parse(firstVersion)));
            if(secondVersion != null)
                imageSpecifier.Packages.Add(new PackageSpecifier(packageName, VersionSpecifier.Parse(secondVersion)));
            try
            {
                var image = imageSpecifier.Resolve(System.Threading.CancellationToken.None);
                if (resultingVersion is null || resultingVersion == "error")
                    Assert.Fail("This should fail to resolve");
                if (resultingVersion == "latest")
                {
                    var latest = new HttpPackageRepository("packages.opentap.io").GetPackageVersions(packageName).Select(v => v.Version).Where(v => v.PreRelease == null).Distinct().ToList();
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
    }
}
