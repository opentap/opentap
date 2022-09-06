using System;
using NUnit.Framework;
using OpenTap.Package;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OpenTap.Image.Tests
{
    public class DependencyGraphTest
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
        }
    }

    public class DependencyResolverGraphTest
    {

        [Test]
        public void TestMergeGraphs()
        {
            var graph1 = PackageDependencyQuery.LoadGraph(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(packagesResourceName), true);
            var graph2 = PackageDependencyQuery.LoadGraph(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(packagesResourceName), true);
            var graph3 = new PackageDependencyGraph();
            
            var g1 = graph1.PackageSpecifiers().ToArray();
            graph3.Absorb(graph1);
            
            var g3 = graph3.PackageSpecifiers().ToArray();
            
            graph3.Absorb(graph2);
            var g4 = graph3.PackageSpecifiers().ToArray();
            Assert.AreEqual(g1.Length, g3.Length);
            Assert.AreEqual(g1.Length, g4.Length);
        }
        
        [Test]
        public void TestQueryGraph()
        {
            var graph = PackageDependencyQuery.QueryGraph("https://packages.opentap.io", "Windows", CpuArchitecture.x64, "").Result;
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
                    var graph = PackageDependencyQuery.QueryGraph(http.Url, "Windows", CpuArchitecture.x64,"").Result;
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

        [Test]
        public void TestQueries()
        {
            var graph1 = PackageDependencyQuery.QueryGraph("https://packages.opentap.keysight.com", "Windows", CpuArchitecture.x64, "").Result;
            var graph2 = PackageDependencyQuery.QueryGraph("https://packages.opentap.keysight.com", "Windows", CpuArchitecture.x64, "beta").Result;
            var graph3 = PackageDependencyQuery.QueryGraph("https://packages.opentap.keysight.com", "Windows", CpuArchitecture.x64, "alpha").Result;
        }
        
        
        public List<string> Repositories => new List<string> { PackageCacheHelper.PackageCacheDirectory, "https://packages.opentap.io" };

        static PackageSpecifier[] str2packages(string csv)
        {
           return csv.Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(','))
                .Select(x => new PackageSpecifier(x[0].Trim(), x.Length == 1 ? VersionSpecifier.AnyRelease : VersionSpecifier.Parse(x[1].Trim())))
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
            yield return ("OpenTAP, 9.17.4; REST-API, any", "Keysight Licensing, 1.1.0+fc48665d;" +
                                                            "OpenTAP, 9.17.4;REST-API, 2.8.0-beta.19+b95ad922");
            yield return ("OpenTAP, 9.17; OSIntegration, ^1", "OpenTAP, 9.17.4;OSIntegration, 1.4.2+15f32a31");
            yield return ("OpenTAP, 9.18.4; TUI, beta", "OpenTAP, 9.18.4;TUI, 0.1.0-beta.145+6c192a43");
            yield return ("OpenTAP, 9.18; TUI, beta", "OpenTAP, 9.18.4;TUI, 0.1.0-beta.145+6c192a43");
            yield return ("OpenTAP; TUI, beta", "OpenTAP, 9.18.4;TUI, 0.1.0-beta.145+6c192a43");
            yield return ("OpenTAP; REST-API; CSV", "CSV, 9.11.0+98498e58;Keysight Licensing, 1.1.1+7a2a1fe3;OpenTAP, 9.18.4+7dec4717;REST-API, 2.9.1+e5319b91");
            yield return ("TEST-A", "TEST-A, 1.0.0; TEST-B,1.0.0;TEST-C,1.0.0");

        }}

        static IEnumerable<string[]> Specifiers2
        {
            get
            {
                foreach (var elem in Specifiers)
                {
                    yield return new [] { elem.spec, elem.result };
                }
            }
        }

        const string packagesResourceName = "OpenTap.Package.UnitTests.Image.opentap-packages.json.gz";
        readonly PackageDependencyGraph graph;
        public DependencyResolverGraphTest()
        {
            
            graph = PackageDependencyQuery.LoadGraph(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(packagesResourceName), true);
            // now add a circular reference so we can test it.
            var pa = new PackageDef() { Name = "TEST-A", Version = SemanticVersion.Parse("1.0")};
            var pb = new PackageDef() { Name = "TEST-B", Version = SemanticVersion.Parse("1.0") };
            var pc = new PackageDef() { Name = "TEST-C", Version = SemanticVersion.Parse("1.0") };
            pa.Dependencies.Add(new PackageDependency("TEST-C", VersionSpecifier.Parse("1.0.0")));
            pb.Dependencies.Add(new PackageDependency("TEST-A", VersionSpecifier.Parse("1.0.0")));
            pc.Dependencies.Add(new PackageDependency("TEST-B", VersionSpecifier.Parse("1.0.0")));
            var graph2 = new PackageDependencyGraph();
            graph2.LoadFromPackageDefs(new []{pa, pb ,pc});
            graph.Absorb(graph2);


        }
        
        [TestCaseSource(nameof(Specifiers2))]
        public void TestResolvePackages(string spec, string result)
        {
            var resolver2 = new ImageResolver(TapThread.Current.AbortToken);
            var img = image(spec);
            var r = resolver2.ResolveImage(img, graph);

            if (result == null)
                Assert.IsFalse(r.Success);
            else
            {
                Assert.IsTrue(r.Success);
                foreach (var pkg in str2packages(result))
                {
                    Assert.IsTrue(r.Packages.Any(x => x.Name == pkg.Name && x.Version.Equals(pkg.Version)));
                }
            }
        }

        [Test]
        public void VersionSorting()
        {
            string sortKey = "beta";
            string sortValues = "1.0.0, 1.1.0,1.1.1, 1.1.0-beta.1, 1.1.0-beta.2, 1.1.0-beta.3, 2.0.0, 2.0.0-beta.4";
            var lst = sortValues.Split(',').Select(x => SemanticVersion.Parse(x.Trim())).ToList();
            var key = VersionSpecifier.Parse(sortKey);
            lst.Sort(key.SortOrder);
        }
    }
}
