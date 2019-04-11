//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CSharp;
using NUnit.Framework;
using OpenTap.Package;

namespace OpenTap.Engine.UnitTests
{
    [TestFixture]
    public class SemanticVersionTests
    {
        [Test]
        public void ParseTest()
        {
            //Parse("11", 11, 0, 0, null, null);
            Parse("1.2", 1, 2, 0, null, null);
            Parse("1.2.3", 1, 2, 3, null, null);
            Parse("8.0.912+aef38ec0", 8, 0, 912, null, "aef38ec0");

            Parse("8.0.0+237493as", 8, 0, 0, null, "237493as");
            Parse("8.0.0-beta", 8, 0, 0, "beta", null);
            Parse("8.0.0-beta+237493as", 8, 0, 0, "beta", "237493as");

            Parse("8.0.0-beta.testing-test.abc+237493as", 8, 0, 0, "beta.testing-test.abc", "237493as");
            Parse("8.0.0-beta.testing-test.abc-abc+237493as.test-1234", 8, 0, 0, "beta.testing-test.abc-abc", "237493as.test-1234");
            Parse("8.0.0+237493as.test-1234", 8, 0, 0, null, "237493as.test-1234");

            Assert.IsFalse(SemanticVersion.TryParse("", out var dummy));
            Assert.IsFalse(SemanticVersion.TryParse("__ Invalid __", out var d2));
            Assert.IsFalse(SemanticVersion.TryParse((string)null, out var d3));
            Assert.IsFalse(SemanticVersion.TryParse("1.2 Development", out var d4));
            Assert.IsFalse(SemanticVersion.TryParse("1.2.3.4", out var d5));
            Assert.IsFalse(SemanticVersion.TryParse("1.2.3.89ABCDEF", out var d6));
        }

        private void Parse(string verString, int major, int minor, int patch, string preRelease, string metadata)
        {
            var t = SemanticVersion.Parse(verString);
            Assert.AreEqual(major, t.Major);
            Assert.AreEqual(minor, t.Minor);
            Assert.AreEqual(patch, t.Patch);
            Assert.AreEqual(metadata, t.BuildMetadata);
            Assert.AreEqual(preRelease, t.PreRelease);

            var t2 = SemanticVersion.Parse(t.ToString());
            Assert.AreEqual(0, t2.CompareTo(t));
            Assert.AreEqual(0, t.CompareTo(t2));
            Assert.AreEqual(true, t.Equals(t2));
            Assert.AreEqual(true, t2.Equals(t));
        }

        [Test]
        public void VersionSortTest()
        {
            SemanticVersion[] versions = new SemanticVersion[] {
                new SemanticVersion(1,0,0,null,null),
                new SemanticVersion(0,0,0,null,null),
                new SemanticVersion(3,0,0,null,null)
            };

            Assert.AreEqual(0, versions.OrderBy(x => x).FirstOrDefault().Major);
            Assert.AreEqual(3, versions.OrderByDescending(x => x).FirstOrDefault().Major);
        }

        [Test]
        public void VersionSortTest2()
        {
            SemanticVersion[] versions = new SemanticVersion[] {
                SemanticVersion.Parse("1.2.3-rc"),
                SemanticVersion.Parse("1.2.2"),
                SemanticVersion.Parse("1.2.3-beta.10.2"),
                SemanticVersion.Parse("1.2.3-beta"),
                SemanticVersion.Parse("1.2.3-beta-beta"),
                SemanticVersion.Parse("1.2.3-beta.2"),
                SemanticVersion.Parse("1.2.3-beta.12+1.2.3.4"),
                SemanticVersion.Parse("1.2.3-beta.10+1.2.3.4"),
                SemanticVersion.Parse("1.2.3+testing"),
            };

            var order = versions.OrderByDescending(x => x).ToList();

            Assert.AreEqual("1.2.3+testing",          order[0].ToString());
            Assert.AreEqual("1.2.3-rc",               order[1].ToString());
            Assert.AreEqual("1.2.3-beta-beta",        order[2].ToString());
            Assert.AreEqual("1.2.3-beta.12+1.2.3.4",  order[3].ToString());
            Assert.AreEqual("1.2.3-beta.10.2",        order[4].ToString());
            Assert.AreEqual("1.2.3-beta.10+1.2.3.4",  order[5].ToString());
            Assert.AreEqual("1.2.3-beta.2",           order[6].ToString());
            Assert.AreEqual("1.2.3-beta",             order[7].ToString());
            Assert.AreEqual("1.2.2",                  order[8].ToString());
        }

        [Test]
        public void EqualOperatorTests()
        {
            Assert.IsTrue(SemanticVersion.Parse("1.2.3-rc") == SemanticVersion.Parse("1.2.3-rc"));
            var same = SemanticVersion.Parse("1.2.3-rc");
            var same2 = same;
            Assert.IsTrue(same == same2);
            SemanticVersion nullVer = null;
            Assert.IsTrue(nullVer == null);
            Assert.IsFalse(same == nullVer);
            Assert.IsFalse(SemanticVersion.Parse("2.2.2-rc") == SemanticVersion.Parse("1.2.3-rc"));
        }

        /// <summary>
        /// This tests the <see cref="SemanticVersion.CompareTo"/> method against paragraph 11 in the semver spec (https://semver.org/)
        /// The TestCases includes all the examples given in that paragraph
        /// </summary>
        [TestCase("1.0.0", "2.0.0")]
        [TestCase("1.0.0", "20.0.0")]
        [TestCase("20.0.0", "20.200.0")]
        [TestCase("2.0.0", "2.1.0")]
        [TestCase("2.1.0", "2.1.1")]
        [TestCase("1.0.0-alpha", "1.0.0")]
        [TestCase("1.0.0-alpha", "1.0.0-alpha.1")]
        [TestCase("1.0.0-alpha.1", "1.0.0-alpha.beta")]
        [TestCase("1.0.0-alpha.beta", "1.0.0-beta")]
        [TestCase("1.0.0-beta", "1.0.0-beta.2")]
        [TestCase("1.0.0-beta.2", "1.0.0-beta.11")]
        [TestCase("1.0.0-beta.11", "1.0.0-rc.1")]
        [TestCase("1.0.0-rc.1", "1.1.0-beta.11")]
        [TestCase("1.0.0-rc.1", "1.0.0")]
        [TestCase("1.0.0-rc.1", "1.1.0-beta.11+123")]
        [TestCase("1.0.0-rc.1", "1.0.0+csad")]
        public void VersionPrecedenceTest(string lower, string higher)
        {
            var lowerVersion = SemanticVersion.Parse(lower);
            var higherVersion = SemanticVersion.Parse(higher);
            
            Assert.AreEqual(2.CompareTo(1), higherVersion.CompareTo(lowerVersion));
            Assert.AreEqual(1.CompareTo(2), lowerVersion.CompareTo(higherVersion));
        }

        [TestCase("^9.0.0-alpha.1", "9.0.0-alpha.1", true)]
        [TestCase("^9.0.0-alpha", "9.0.0-alpha.1", true)]
        [TestCase("^9.0.0-alpha.2", "9.0.0-alpha.7", true)]
        [TestCase("^9.0.1200-alpha.1.2", "9.0.1200-alpha.1.7", true)]
        [TestCase("^9.0.0-alpha.1", "9.1.0-alpha.1", true)]
        [TestCase("^9.0.0-alpha+test", "9.0.0-alpha+test", true)]
        [TestCase("^9.0.0-alpha+test.5", "9.0.0-alpha+test.2", true)] // ignoring buildMetadata
        [TestCase("^9.0.0-beta", "9.0.0-alpha", false)]
        [TestCase("^9.0.0-alpha.2", "9.0.0-alpha.1", false)]
        [TestCase("^9.0.0-alpha.1", "9.0.0-alpha", false)]
        [TestCase("^9.0.1200-alpha.1.7", "9.0.1200-alpha.1.2", false)]
        [TestCase("^9.1.0-alpha.1", "9.0.0-alpha.1", false)]
        public void SpecifierCompatibilityTest(string specifier, string version, bool expected)
        {
            Assert.AreEqual(expected, VersionSpecifier.Parse(specifier).IsCompatible(SemanticVersion.Parse(version)));
        }

        [TestCase("9.0.0-alpha", "9.0.0-alpha", true)]
        [TestCase("9.0.0-alpha.1", "9.0.0-alpha.1", true)]
        [TestCase("1.2.3+Build-something", "1.2.3+Build-something", true)]
        [TestCase("9.0.0-beta", "9.1.0-beta", false)] // minor
        [TestCase("9.0.0-beta", "9.0.0-alpha", false)] // prerelease
        [TestCase("1.2.3+Build-something", "1.2.3+Build-something-else", false)] // buildMetadata
        public void SpecifierExactTest(string specifier, string version, bool expected)
        {
            Assert.AreEqual(expected, VersionSpecifier.Parse(specifier).IsCompatible(SemanticVersion.Parse(version)));
        }


        static Assembly generateAssemblyInMemWithoutVersion()
        {
            string cs = "public class ObjectTest { public void Run(){} }";

            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters();
            parameters.ReferencedAssemblies.Add("System.dll");

            parameters.GenerateInMemory = true;
            parameters.GenerateExecutable = false;
            CompilerResults results = provider.CompileAssemblyFromSource(parameters, cs);
            if (results.Errors.HasErrors)
            {
                var errors = results.Errors.Cast<CompilerError>().Select(err => err.ToString());
                Assert.Inconclusive(String.Join("\r\n", errors));
            }
            return results.CompiledAssembly;
        }

        [Test]
        public void TestSemVerDynamicallyLoadedAssembly()
        {
            var dynasm = generateAssemblyInMemWithoutVersion();
            var semver = dynasm.GetSemanticVersion();
            Assert.IsTrue(semver == new SemanticVersion(0, 0, 0, "", ""));
        }
    }
}
