using NUnit.Framework;
using OpenTap.Image;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            string precalculatedHash = "805C8B060075B7F0A76174BA90C32AEA515F6509";
            Assert.AreEqual(precalculatedHash, image.Id);

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
    }
}
