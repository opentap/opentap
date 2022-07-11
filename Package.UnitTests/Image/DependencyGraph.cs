using System;
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
        public void TestQueryGraph()
        {
            var graph = PackageDependencyQuery.QueryGraph("https://packages.opentap.io").Result;
        }

        [Test]
        public void TestCollapseQueryGraph()
        {
            List<IPackageRepository> repositories = Repositories.Select(PackageRepositoryHelpers.DetermineRepositoryType).ToList();
            var graph0 = new PackageDependencyGraph();
            foreach (var repo in repositories)
            {
                if (repo is HttpPackageRepository http)
                {
                    var graph = PackageDependencyQuery.QueryGraph(http.Url).Result;
                    graph0.Absorb(graph);
                }

                if (repo is FilePackageRepository fpkg)
                {
                    var graph = new PackageDependencyGraph();
                    var packages = fpkg.GetAllPackages(TapThread.Current.AbortToken);
                    graph.LoadFromPackageDefs(packages);
                    graph0.Absorb(graph);
                    
                }
            }
        }
        
        public List<string> Repositories => new List<string> { PackageCacheHelper.PackageCacheDirectory, "https://packages.opentap.io" };

        static PackageSpecifier[] str2packages(string csv)
        {
           return csv.Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(','))
                .Select(x => new PackageSpecifier(x[0].Trim(), VersionSpecifier.Parse(x[1].Trim())))
                .ToArray();
        }
        
        static ImageSpecifier image(string csv)
        {
            var packages = str2packages(csv);
            var image = new ImageSpecifier();
            image.Packages.AddRange(packages);
            return image;
        }
        
        static string unresolvable = nameof(unresolvable);
        static IEnumerable<(string spec, string result)> Specifiers
        {
        get{
            yield return (
                "OpenTAP, 9.17.4; SDK, any; Developer's System, any; REST-API, ^2.6.0; Keysight Floating Licensing, ^1.4",
                "CSV, 9.11.0+98498e58;Developer's System, 9.17.4+5e508f08;Editor, 9.17.4+5e508f08;" +
                "Keysight Floating Licensing, 1.4.1+e5816333;Keysight Licensing, 1.1.1+7a2a1fe3;" +
                "OpenTAP, 9.17.4;OSIntegration, 1.4.2+15f32a31;REST-API, 2.6.0+895d94fa;" +
                "Results Viewer, 9.17.4+5e508f08;SDK, 9.17.4+ea67c63a;SQLite and PostgreSQL, 9.4.2+3009aace;" +
                "Timing Analyzer, 9.17.4+5e508f08;Visual Studio SDK, 1.2.6+ef0ad804;WPF Controls, 9.17.4+5e508f08");
            yield return ("OpenTAP, 9.17.4; OSIntegration, ^1", "OpenTAP, 9.17.4;OSIntegration, 1.4.2+15f32a31");
            yield return ("OpenTAP, 9.17.4; OpenTAP, 9.17.2", null);
            yield return ("OpenTAP, 9.17.4;Developer's System CE, any", "CSV, 9.11.0+98498e58;Developer's System CE, 9.17.4+5e508f08;Editor CE, 9.17.4+5e508f08;" +
                                                                        "Keysight Licensing, 1.1.1+7a2a1fe3;OpenTAP, 9.17.4;OSIntegration, 1.4.2+15f32a31;" +
                                                                        "Results Viewer CE, 9.17.4+5e508f08;SDK, 9.17.4+ea67c63a;SQLite and PostgreSQL, 9.4.2+3009aace;" +
                                                                        "Visual Studio SDK CE, 1.2.6+ef0ad804;WPF Controls, 9.17.4+5e508f08");
            yield return ("OpenTAP, 9.17.4; REST-API, any", "Keysight Floating Licensing, 1.4.1+e5816333;Keysight Licensing, 1.0.0+8a228623;" +
                                                            "OpenTAP, 9.17.4;REST-API, 2.7.0+2d67ea81");
            yield return ("OpenTAP, 9.17; OSIntegration, ^1", "OpenTAP, 9.17.4;OSIntegration, 1.4.2+15f32a31");

        }}

        static IEnumerable<string[]> Specifiers2
        {
            get
            {
                foreach (var elem in Specifiers)
                {
                    yield return new string[] { elem.spec, elem.result };
                }
            }
        }

        readonly PackageDependencyGraph graph;
        public DependencyResolverGraphTest()
        {
            var packagesResourceName = "OpenTap.Package.UnitTests.Image.opentap-packages.json.gz";
            graph = PackageDependencyQuery.LoadGraph(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(packagesResourceName), true);
        }
        
        [TestCaseSource(nameof(Specifiers2))]
        public void ParsePackages(string spec, string result)
        {
            var resolver2 = new ImageResolver();
            var sw = Stopwatch.StartNew();
            var img = image(spec);
            var r = resolver2.ResolveImage(img, graph);

            if (result == null)
                Assert.IsNull(r);
            else
            {
                Assert.IsNotNull(r);
                var imageString = string.Join(";", r.Packages.Select(x => $"{x.Name}, {x.Version}"));
                if (result == null) return;
                foreach (var pkg in str2packages(result))
                {
                    Assert.IsTrue(r.Packages.Any(x => x.Name == pkg.Name && x.Version.Equals(pkg.Version)));
                }
            }

            var elapsed = sw.Elapsed;
            
        }
    }
}
