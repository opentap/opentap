using System;
using System.Linq;
using NUnit.Framework;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class AssemblyDataTest
    {
        void assemblyDataVersionTest2(string versionstr, object semverTest, string version)
        {
            // semverTest can be a bool or a SemanticVersion.
            
            
            var testasm = new AssemblyData("./test.dll", null) { RawVersion = versionstr, Version = version == null ? null : Version.Parse(version)};
            if(semverTest is bool isSemver2 && isSemver2 == true)
                Assert.AreEqual(SemanticVersion.Parse(versionstr), testasm.SemanticVersion);
            else if (semverTest is SemanticVersion semver2)
                Assert.AreEqual(semver2, testasm.SemanticVersion);
            else 
                Assert.IsNull(testasm.SemanticVersion);
            if(version != null)
                Assert.AreEqual(Version.Parse(version), testasm.Version);
            else
                Assert.IsNull(testasm.Version);
        }
        /// <summary>
        /// Verify that the assembly data versions corresponds to expected values.
        /// </summary>
        [Test]
        public void AssemblyDataVersionTest()
        {
            var testasm = new AssemblyData("./test.dll", null) { RawVersion = "0.3.0-beta.8+7b4c999b", Version = Version.Parse("0.3.0.0")};
            Assert.AreEqual(SemanticVersion.Parse("0.3.0-beta.8+7b4c999b"), testasm.SemanticVersion);
            Assert.AreEqual(new Version(0,3,0,0), testasm.Version);
            assemblyDataVersionTest2("0.3.0-beta.8+7b4c999b", true, "0.3.0.0");
            assemblyDataVersionTest2("0.3.0", true, "0.3.0");
            assemblyDataVersionTest2("0.3.0.0", new SemanticVersion(0, 3, 0, null, null), "0.3.0.0");
            assemblyDataVersionTest2("1.0", true, "1.0");
            assemblyDataVersionTest2("asd", false, null);
        }

        [Test]
        public void ConflictingAssemblyVersionTest()
        {
            var s = PluginManager.GetSearcher();
            var globAsm = s.Assemblies.FirstOrDefault(a => a.Name.Contains("DotNet.Glob"));
            
            Assert.AreEqual(SemanticVersion.Parse("3.0.1"),  SemanticVersion.Parse(globAsm.SemanticVersion.ToString(3)));
            Assert.AreEqual(Version.Parse("3.0.1.0"), globAsm.Version);
            
            var newtonsoftAsm = s.Assemblies.FirstOrDefault(a => a.Name.Contains("Newtonsoft.Json"));
            Assert.AreEqual(SemanticVersion.Parse("12.0.3"), SemanticVersion.Parse(newtonsoftAsm.SemanticVersion.ToString(3)));
            // The newtonsoft package is actually version 12.0.0.3, but the assembly version is 12.0.0.0 for some reason.
            // This was changed in 9.18.2 due to a regression, but it has been so for a long long time.  
            Assert.AreEqual(Version.Parse("12.0.0.0"), newtonsoftAsm.Version);
        }

        [Test]
        public void OpenTapVersionTest()
        {
            PluginManager.GetOpenTapAssembly().SemanticVersion.ToString();
        }
        
    }
}