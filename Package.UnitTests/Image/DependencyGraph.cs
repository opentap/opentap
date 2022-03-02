using NUnit.Framework;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static OpenTap.Image.Tests.Resolve;

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
}
