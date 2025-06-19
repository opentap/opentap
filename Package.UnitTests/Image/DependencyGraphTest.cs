using System;
using NUnit.Framework;
using OpenTap.Package;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework.Constraints;

namespace OpenTap.Image.Tests
{
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
            IQueryPrereleases repo = new HttpPackageRepository("https://packages.opentap.io");
            var graph = repo.QueryPrereleases("Windows", CpuArchitecture.x64, "", null);
        }

        [Test]
        public void TestCollapseQueryGraph()
        {
            List<IPackageRepository> repositories = Repositories.Select(PackageRepositoryHelpers.DetermineRepositoryType).ToList();
            var graph0 = new PackageDependencyGraph();
            foreach (var repo in repositories)
            {
                if (repo is IQueryPrereleases http)
                {
                    var graph = http.QueryPrereleases("Windows", CpuArchitecture.x64,"", null);
                    graph0.Absorb(graph);
                }
                else if (repo is FilePackageRepository fpkg)
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
            // requesting a beta, but the newest is actually a release. The release versions should then be resolved.
            yield return ("Developer's System, beta", "CSV, 9.11.0+98498e58;Developer's System, 9.17.2-beta.12+a3f06537;" +
                                                      "Editor, 9.17.2-beta.12+a3f06537;Keysight Licensing, 1.1.0-rc.3+fc48665d;" +
                                                      "OpenTAP, 9.17.4-rc.1+3ffb292e;OSIntegration, 1.4.2+15f32a31;Results Viewer, 9.17.2-beta.12+a3f06537;" +
                                                      "SDK, 9.17.4-rc.1+3ffb292e;SQLite and PostgreSQL, 9.4.2+3009aace;" +
                                                      "Timing Analyzer, 9.17.2-beta.12+a3f06537;Visual Studio SDK, 1.2.5+08b71a6e;WPF Controls, 9.17.2-beta.12+a3f06537");
            yield return ("OpenTAP, 9.17.4;OpenTAP, ^9.17.4;OpenTAP, 9.17.4", "OpenTAP, 9.17.4");
            yield return ("OpenTAP, 9.17.4;OpenTAP, ^9.17.4+ea67c63a;OpenTAP, 9.17.4", "OpenTAP, 9.17.4");

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
            {
                Assert.IsFalse(r.Success);
            }
            else
            {
                Assert.IsTrue(r.Success);
                foreach (var pkg in str2packages(result))
                {
                    Assert.IsTrue(r.Packages.Any(x => x.Name == pkg.Name && x.Version.Equals(pkg.Version)));
                }
            }
            
            // Ensure that each package occurs only once in the image resolution
            var members = r.Packages.ToLookup(pkg => pkg.Name);
            foreach (var m in members)
            {
                Assert.IsTrue(m.Count() == 1);
            }
        }

        [TestCase("I,0.2.0-beta.5","H,0.2.0-beta.1,I,0.2.0-beta.5")]
        [TestCase("H,^0.1.0;I,0.2.0-beta.5","H,0.2.0-beta.1,I,0.2.0-beta.5")]
        public void TestResolvePackagesDependencyExpansion(string spec, string result)
        { 
            var resolver2 = new ImageResolver(TapThread.Current.AbortToken);
            var img = image(spec);
            var dep = new PackageDependencyCache("Windows", CpuArchitecture.AnyCPU, [MockRepository.Instance.Url]);
            dep.LoadFromRepositories();

            var r = resolver2.ResolveImage(img, dep.Graph);

            Assert.IsTrue(r.Success);
            foreach (var pkg in str2packages(result))
            {
                Assert.IsTrue(r.Packages.Any(x => x.Name == pkg.Name && x.Version.Equals(pkg.Version)));
            }
        }

        [Test]
        public void TestPartialVersionSort2()
        {
            var wrt = new PackageSpecifier("Basic Mixins", VersionSpecifier.Parse("^0"));
            var unordered = new[]
                {
                    "0.4.0-alpha.1",
                    "0.1.0",
                    "0.3.0-beta.1",
                    "0.2.0-rc.1",
                }.Select(SemanticVersion.Parse);
            var expected = new[]
                {
                    "0.1.0",
                    "0.2.0-rc.1",
                    "0.3.0-beta.1",
                    "0.4.0-alpha.1",
                }.Select(SemanticVersion.Parse).ToArray();
            
            var sorted = unordered.OrderBy(x => x, wrt.Version.SortPartial).ToList();
            CollectionAssert.AreEqual(expected, sorted);
        }

        [Test]
        public void TestPartialVersionSort()
        {
            var wrt = new PackageSpecifier("Basic Mixins", VersionSpecifier.Parse("^0"));
            var versions = new[]
            {
                "0.2.0-alpha.1.1",
                "0.2.0-beta.1",
                "0.0.1-alpha.11.1",
                "0.0.1-alpha.19.2",
                "0.0.1-beta.9",
                "0.2.1-alpha.1.1",
                "0.0.1-beta.13",
                "0.0.1-beta.20",
                "0.0.1-beta.21",
                "0.1.0",
            }.Select(SemanticVersion.Parse);
            var expected = new[]
            {
                "0.1.0",
                "0.2.0-beta.1",
                "0.0.1-beta.21",
                "0.0.1-beta.20",
                "0.0.1-beta.13",
                "0.0.1-beta.9",
                "0.2.1-alpha.1.1",
                "0.2.0-alpha.1.1",
                "0.0.1-alpha.19.2",
                "0.0.1-alpha.11.1",
            }.Select(SemanticVersion.Parse);
            var sorted = versions.OrderBy(x => x, wrt.Version.SortPartial).ToList();
            CollectionAssert.AreEqual(expected, sorted);
        }

        [TestCase("J,^0-beta","J,0.2.0-beta.1", false)]
        [TestCase("J,^0.1-beta","J,0.1.2-rc.2", false)]
        [TestCase("J,^0.1.0-beta","J,0.1.2-rc.2", false)]
        [TestCase("J,^0.2-beta","J,0.2.0-beta.1", false)]
        [TestCase("J,^0.2.0-beta","J,0.2.0-beta.1", false)]
        [TestCase("J,^0","J,0.1.2-rc.2", true)]
        [TestCase("J,^0.0","J,0.1.2-rc.2", true)]
        [TestCase("J,^0.1","J,0.2.0-beta.1", true)]
        [TestCase("J,^0.2","J,0.3.1-alpha.1.2", true)]
        [TestCase("J,^0.1.0","J,0.1.1-alpha.1.2", true)]
        [TestCase("J,^0.1.1","J,0.1.2-rc.2", true)]
        public void TestResolvePackagesDependencyExpansion2(string spec, string result, bool fetchAlpha)
        { 
            var resolver2 = new ImageResolver(TapThread.Current.AbortToken);
            var img = image(spec);
            var dep = new PackageDependencyCache("Windows", CpuArchitecture.AnyCPU, [MockRepository.Instance.Url]);
            dep.LoadFromRepositories();

            var r = resolver2.ResolveImage(img, dep.Graph);

            var fetchedPackages = dep.Graph.PackageSpecifiers().ToLookup(pkg => pkg.Name);
            var js = fetchedPackages["J"].ToArray();

            if (fetchAlpha)
            {
                Assert.That(js.Any(j => j.Version.PreRelease?.StartsWith("alpha") == true), Is.True);
            } 
            else
            {
                Assert.That(js.Any(j => j.Version.PreRelease?.StartsWith("alpha") == true), Is.False);
            }

            Assert.That(js.Any(j => j.Version.PreRelease?.StartsWith("beta") == true), Is.True);
            Assert.That(js.Any(j => j.Version.PreRelease?.StartsWith("rc") == true), Is.True);


            if (result == null)
            {
                Assert.IsFalse(r.Success);
            }
            {
                Assert.IsTrue(r.Success);
                foreach (var pkg in str2packages(result))
                {
                    Assert.IsTrue(r.Packages.Any(x => x.Name == pkg.Name && x.Version.Equals(pkg.Version)));
                }
            }
        }

        [Test]
        public void TestResolvePackagesFromXml()
        {
            PackageDef defFromXml(byte[] xml)
            { 
                using var ms = new MemoryStream();
                ms.Write(xml.ToArray(), 0, xml.Length);
                ms.Seek(0, SeekOrigin.Begin);
                return PackageDef.FromXml(ms);
            }
            
            var pkg1Xml = """ 
                          <Package Name="Pkg1" Version="1.1.1" >
                              <Dependencies>
                                  <PackageDependency Package="Pkg2" Version="^1.0.1-beta.2" />
                              </Dependencies>
                          </Package> 
                          """u8;
            var pkg2Xml = """<Package Name="Pkg2" Version="1.0.1-beta.1" />"""u8;

            var pkg1 = defFromXml(pkg1Xml.ToArray());
            var pkg2 = defFromXml(pkg2Xml.ToArray());
            
            var resolver2 = new ImageResolver(TapThread.Current.AbortToken);
            // This image should fail to resolve because:
            // Pkg1 depends on Pkg2 version ^1.0.1-beta.2
            // The latest version of Pkg2 is 1.0.1-beta.1
            var img = image("Pkg1,1.1.1");
            var g = new PackageDependencyGraph();
            g.LoadFromPackageDefs([pkg1, pkg2]);
            var r = resolver2.ResolveImage(img, g); 
            
            Assert.IsFalse(r.Success);
        }
    }
}
