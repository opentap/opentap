using System.Linq;
using System.Threading;
using NUnit.Framework;
using OpenTap.Package;

namespace OpenTap.Image.Tests
{
    [TestFixture]
    public class IterativeResolve
    {
        [SetUp]
        public void SetUp()
        {
            MockRepository.Instance.ResolveCount = 0;
        }
        [TearDown]
        public void TearDown()
        {
            TestContext.Out.WriteLine($"ResolveCount: {MockRepository.Instance.ResolveCount}");
        }

        [TestCase("9.17.2")]
        [TestCase("9.17.3-rc.1")]
        public void VisualStudioSDKDependencyTest(string expectedVersion)
        {
            var imageSpecifier = MockRepository.CreateSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("OpenTAP", VersionSpecifier.Parse(expectedVersion)));
            imageSpecifier.Packages.Add(new PackageSpecifier("Visual Studio SDK", VersionSpecifier.Parse("any")));

            var image = imageSpecifier.Resolve(CancellationToken.None);
            Assert.AreEqual(3, image.Packages.Count);
            var packages = image.Packages.ToLookup(p => p.Name);
            StringAssert.StartsWith(expectedVersion, packages["OpenTAP"].First().Version.ToString());
            StringAssert.StartsWith(expectedVersion, packages["SDK"].First().Version.ToString());

        }

        [TestCase("9.17.1", "9.17.1")]
        [TestCase("9.17.3-rc.1", "9.16.4")]
        public void DevelopersSystemDependencyTest(string expectedOpenTAP, string expectedDevsys)
        {
            var imageSpecifier = MockRepository.CreateSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("OpenTAP", VersionSpecifier.Parse(expectedOpenTAP)));
            imageSpecifier.Packages.Add(new PackageSpecifier("Developer's System", VersionSpecifier.Parse("any")));

            var image = imageSpecifier.Resolve(CancellationToken.None);
            Assert.AreEqual(5, image.Packages.Count);
            var packages = image.Packages.ToLookup(p => p.Name);

            {
                var dv = packages["Developer's System"].First().Version.ToString();
                StringAssert.StartsWith(expectedDevsys, dv, $"Expected Developer's System in version '{expectedDevsys}' but got '{dv}'.");
            }
            {
                var ev = packages["Editor"].First().Version.ToString();
                StringAssert.StartsWith(expectedDevsys, ev, $"Expected Editor in version '{expectedDevsys}' but got '{ev}'.");
            }
            {
                var ov = packages["OpenTAP"].First().Version.ToString();
                StringAssert.StartsWith(expectedOpenTAP, ov, $"Expected OpenTAP in version '{expectedOpenTAP}' but got '{ov}'.");
            }
            {
                var sv = packages["SDK"].First().Version.ToString();
                StringAssert.StartsWith(expectedOpenTAP, sv, $"Expected SDK in version '{expectedOpenTAP}' but got '{sv}'.");
            }
        }
    }
}