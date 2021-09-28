using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using NUnit.Framework;
using OpenTap.Diagnostic;

namespace OpenTap.Package.UnitTests
{
    public class PackageCreateIncludeDependenciesTest
    {
        [SetUp]
        public void SetUp()
        {
            var s = Session.Create();
            sessionListener.Value = new TestLogListener();
            Log.AddListener(sessionListener.Value);
            sessionVariable.Value = s;
        }

        [TearDown]
        public void TearDown()
        {
            sessionVariable.Value.Dispose();
        }

        private readonly SessionLocal<Session> sessionVariable = new SessionLocal<Session>();
        private readonly SessionLocal<TestLogListener> sessionListener = new SessionLocal<TestLogListener>();
        private TestLogListener Listener => sessionListener.Value;
        
        private class TestLogListener : ILogListener
        {
            public List<Event> Events = new List<Event>();

            public void EventsLogged(IEnumerable<Event> events)
            {
                Events.AddRange(events);
            }

            public void Flush()
            {
            }

            public override string ToString()
            {
                return string.Join("\n", Events.Select(e => e.Message));
            }

            public bool AnyContains(string msg)
            {
                return Events.Any(e => e.Message.Contains(msg));
            }

            public void AssertContains(string msg)
            {
                Assert.IsTrue(AnyContains(msg), ToString());
            }

            public void AssertDoesNotContain(string msg)
            {
                Assert.IsFalse(AnyContains(msg), ToString());
            }

            public int Count(string msg)
            {
                return Events.Count(e => e.Message.Contains(msg));
            }
        }
        
        private class PackageDep
        {
            public string Name { get; }
            public string Version { get; }

            public PackageDep(string name, string version)
            {
                Name = name;
                Version = version;
            }
        }
        private class FileDep
        {
            public FileDep(string content, bool includeDeps)
            {
                Content = content;
                IncludeDeps = includeDeps;
            }
            
            public string Content { get; set; }
            public bool IncludeDeps { get; set; }
        }

        private string BuildTestPlan(params PackageDep[] dependencies)
        {
            var xmlBase = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TestPlan type=""OpenTap.TestPlan"" Locked=""false"">
  <Package.Dependencies>
  </Package.Dependencies>
</TestPlan>";
            var node = new XmlDocument();
            node.LoadXml(xmlBase);
            var depTag = node.GetElementsByTagName("Package.Dependencies")[0];

            foreach (var dependency in dependencies)
            {
                var pkg = node.CreateElement("Package");
                pkg.SetAttribute("Name", dependency.Name);
                if (dependency.Version != null)
                    pkg.SetAttribute("Version", dependency.Version);

                depTag.AppendChild(pkg);
            }

            return node.OuterXml;
        }
        private string CreatePackageXml(params FileDep[] files)
        {
            var packageDefInput = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Package Name=""TestPackage""
    Version=""1.0.0""    
    OS=""Windows,Linux"">    
  <Files>
  </Files>
</Package>
";
            var node = new XmlDocument();
            node.LoadXml(packageDefInput);
            var fileTag = node.GetElementsByTagName("Files")[0];

            foreach (var file in files)
            {
                var tmp = Path.GetTempFileName();
                File.WriteAllText(tmp, file.Content);
                
                var ele = node.CreateElement("File");
                ele.SetAttribute("Path",  tmp);

                if (file.IncludeDeps)
                {
                    var includeDeps = node.CreateElement("IncludePackageDependencies");
                    ele.AppendChild(includeDeps);
                }
                fileTag.AppendChild(ele);
            }

            var packageXml = Path.GetTempFileName();
            node.Save(packageXml);

            return packageXml;
        }
        
        
        private PackageDef BuildPackage(string packageXml)
        {
            var outFile = Path.GetTempFileName();
            File.Delete(outFile);
            var act = new PackageCreateAction {PackageXmlFile = packageXml, OutputPaths = new[] {outFile}};
            act.Execute(CancellationToken.None);
            return PackageDef.FromPackage(outFile);
        }
        
        [Test]
        public void DependenciesAddedTest([Values(true, false)] bool AddDependency, [Values("any", null)] string Version)
        {
            var plan1 = BuildTestPlan(new PackageDep("P1", Version));
            var packageXml = CreatePackageXml(new FileDep(plan1, AddDependency));

            try
            {
                var pkg = BuildPackage(packageXml);

                if (AddDependency)
                {
                    Assert.AreEqual(1, pkg.Dependencies.Count);
                    StringAssert.AreEqualIgnoringCase(pkg.Dependencies.First().RawVersion, "any");
                    StringAssert.AreEqualIgnoringCase(pkg.Dependencies.First().Name, "P1");
                }
                else
                {
                    Assert.AreEqual(0, pkg.Dependencies.Count);
                }
            }
            catch (Exception ex)
            {
                Assert.Fail($"Unexpected exception: {ex.Message}");
            }
        }

        [Test]
        public void ConflictingDependenciesTest()
        {
            var version1 = VersionSpecifier.Parse("1.0.0");
            var version2 = VersionSpecifier.Parse("2.0.0");
            
            var plan1 = BuildTestPlan(
                new PackageDep("P1", version1.ToString()),
                new PackageDep("P1", version2.ToString()));
            var packageXml = CreatePackageXml(new FileDep(plan1, true));

            var pkg = BuildPackage(packageXml);
            Listener.AssertContains("mutually exclusive");
            Listener.AssertContains("Dependency conflict");
        }

        [Test]
        public void BumpedDependenciesTest()
        {
            var version1 = VersionSpecifier.Parse("1.1.0");
            var version2 = VersionSpecifier.Parse("1.2.0");
            var version3 = VersionSpecifier.Any;
            
            var plan1 = BuildTestPlan(
                new PackageDep("P1", version1.ToString()),
                new PackageDep("P2", version2.ToString()));
            
            var plan2 = BuildTestPlan(
                new PackageDep("P2", version1.ToString()),
                new PackageDep("P1", version2.ToString()));

            var plan3 = BuildTestPlan(new PackageDep("P2", version3.ToString()),
                new PackageDep("P1", version3.ToString()));
            
            var packageXml = CreatePackageXml(new FileDep(plan3, true), new FileDep(plan2, true), new FileDep(plan1, true));
            var pkg = BuildPackage(packageXml);
            
            Assert.IsTrue(pkg.Dependencies.Any(d => d.Name == "P1" && d.RawVersion == "^1.2.0" && d.Version.ToString() == "^1.2.0"));
            Assert.IsTrue(pkg.Dependencies.Any(d => d.Name == "P2" && d.RawVersion == "^1.2.0" && d.Version.ToString() == "^1.2.0"));
        }

        [Test]
        public void EmptyNameGeneratesException()
        {
            var version = VersionSpecifier.Any;
            var plan = BuildTestPlan(new PackageDep("", version.ToString()));

            var packageXml = CreatePackageXml(new FileDep(plan, true));

                var pkg = BuildPackage(packageXml);
                
                Listener.AssertContains("Attribute 'Name' is not set");            
        }

        [Test]
        public void InvalidVersionGeneratesException()
        {
            var plan = BuildTestPlan(new PackageDep("P1", "abc123"));
            var packageXml = CreatePackageXml(new FileDep(plan, true));

            var pkg = BuildPackage(packageXml);
            Listener.AssertContains("Attribute 'Version' is not a valid version specifier");
        }

        [Test]
        public void TestInvalidXml()
        {
            var plan = "Some string which is definitely not a valid XML document";
            var packageXml = CreatePackageXml(new FileDep(plan, true));

            var pkg = BuildPackage(packageXml);
            
            Listener.AssertContains("is not a valid XML document.");
        }
    }
}
