using NUnit.Framework;
using OpenTap.Package;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OpenTap.Image.Tests
{
    public class DependencyGraph
    {
        [TestCase("OpenTAP", "9.10.0", "Demonstration", "9.2.0", "error", "error")]
        [TestCase("OpenTAP", "9.10.0", "ExactDependency", "1.0.0", "error", "error")]
        [TestCase("OpenTAP", "9.13.1", "ExactDependency", "1.0.0", "9.13.1", "1.0.0")]
        [TestCase("OpenTAP", "9.13", "ExactDependency", "1.0.0", "9.13.1", "1.0.0")]
        [TestCase("Cyclic", "1.0.0", "Cyclic2", "1.0.0", "1.0.0", "1.0.0")]
        [TestCase("OpenTAP", "9.13", "ExactDependency", "^1.0.0", "9.13.1", "1.0.0")]
        [TestCase("OpenTAP", "^9.10.0", "Demonstration", "^9.0.6", "9.12.0", "9.1.0")]
        public void ResolveDependencies(string package1, string package1version, string package2, string package2version, string resultingVersion1, string resultingVersion2)
        {
            List<IPackageRepository> repositories = new List<IPackageRepository>() { MockRepository.Instance };

            List<PackageSpecifier> specifiers = new List<PackageSpecifier>();
            specifiers.Add(new PackageSpecifier(package1, VersionSpecifier.Parse(package1version)));
            specifiers.Add(new PackageSpecifier(package2, VersionSpecifier.Parse(package2version)));
            DependencyResolver resolver = new DependencyResolver(specifiers, repositories, CancellationToken.None);


            string resolved = resolver.GetDotNotation();
            TestContext.WriteLine(resolved);
            TestContext.WriteLine($"Resolve count: {MockRepository.Instance.ResolveCount}");

            if (resultingVersion1 is null || resultingVersion1 == "error")
                Assert.IsTrue(resolver.DependencyIssues.Any());
            else
            {
                StringAssert.StartsWith(resultingVersion1, resolver.Dependencies.First(p => p.Name == package1).Version.ToString());
                StringAssert.StartsWith(resultingVersion2, resolver.Dependencies.First(p => p.Name == package2).Version.ToString());
            }
        }
    }

    public class DependencyResolverGraphTest
    {
        [Test]
        public void Test1()
        {
            var graph = PackageDependencyQuery.QueryGraph("https://packages.opentap.io").Result;
        }

        [Test]
        public void ParsePackages()
        {
            var packagesResourceName = "OpenTap.Package.UnitTests.Image.opentap-packages.json.gz";
            var graph = PackageDependencyQuery.LoadGraph(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(packagesResourceName), true);
            var img = new ImageSpecifier();
            //img.Packages.Add("OpenTAP", "9.17.4");
            //img.Packages.Add("SDK", "any");
            //img.Packages.Add("Developer's System", "9.17.2");
            //img.Packages.Add("REST-API", "^2.6.0");
            //img.Packages.Add("Keysight Floating Licensing", "^1.4");
            
            img.Packages.Add("OpenTAP", "9.17");
            img.Packages.Add("Developer's System", "Any");
            img.Packages.Add("REST-API", "any");
            img.Packages.Add("TUI", "beta");
            img.Packages.Add("SDK", "9.17.2");

            
            var resolver = new ImageResolver();
            var sw = Stopwatch.StartNew();
            
            var result = resolver.ResolveImage(img, graph);
            

            var elapsed = sw.Elapsed;
        }
    }
}
