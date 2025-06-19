﻿using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class PackageXmlPreprocessorTests
    {
        [Test]
        public void TestPreprocessXml([Values(true, false)] bool sign)
        {
            const string platform = "Windows";
            const string arch = "TestArch";
            const string owner = "TestOwner";
            const string packageName = "TestPackage";
            const string sourceUrl = "Some Source Url";

            var xmlString = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Package Name=""$(PackageName)"" Version=""$(GitVersion)"" OS=""$(PlatformVar)"" Architecture=""$(ArchitectureVar)"">
  <Variables>
    <PlatformVar>{platform}</PlatformVar>
    <ArchitectureVar>{arch}</ArchitectureVar>
    <Owner>{owner}</Owner>
    <PackageName>{packageName}</PackageName>
    <SourceUrl>{sourceUrl}</SourceUrl>
    <SignVar Condition=""{sign} == True"">true</SignVar>
  </Variables>
  <SourceUrl Condition=""$(PlatformVar) == Windows"">$!(SourceUrl)</SourceUrl>
  <Owner Condition=""$!(PlatformVar) == Linux"">$(Owner)</Owner>
  <Files Condition=""a == b"">
    <File Path=""WrongFile""/>
  </Files>
  <Files Condition=""1"">
    <File Path=""CorrectFile""/>
  </Files>
  <Files Condition=""b == b"">
    <File Condition="""" Path=""AlsoWrongFile""/>
  </Files>
  <Files Condition=""b == b"">
    <File Condition=""1"" Path=""AlsoCorrectFile"">
      <Sign Certificate=""Some Cert"" Condition=""'$(SignVar)' == true""/>
    </File>    
  </Files>


  <PackageActionExtensions Condition=""'$(PlatformVar)'  !=   Windows"">
    <ActionStep ExeFile=""tap.exe"" ActionName=""install""/>
  </PackageActionExtensions>
</Package>
";
            var fn = Path.GetTempFileName();
            File.WriteAllText(fn, xmlString);

            var original = XElement.Parse(xmlString);
            var evaluator = new PackageXmlPreprocessor(original);
            var expanded = evaluator.Evaluate();

            var ns = original.GetDefaultNamespace();

            void assertRemoved(string elemName)
            {
                var e1 = original.Element(ns.GetName(elemName));
                var e2 = expanded.Element(ns.GetName(elemName));

                Assert.NotNull(e1);
                Assert.IsNull(e2);
            }

            // This element should have been removed
            assertRemoved("Variables");
            assertRemoved("PackageActionExtensions");
            assertRemoved("Owner");

            Assert.AreEqual(4, original.Elements().Count(e => e.Name.LocalName == "Files"));
            Assert.AreEqual(1, expanded.Elements().Count(e => e.Name.LocalName == "Files"));

            var files = expanded.Elements().Where(e => e.Name.LocalName == "Files").ToArray();
            Assert.AreEqual(2, files[0].Nodes().Count());
            Assert.AreEqual("CorrectFile", files[0].Elements().First().Attribute("Path").Value);
            var file2 = files[0].Elements().ToArray()[1];
            Assert.AreEqual("AlsoCorrectFile", file2.Attribute("Path").Value);
            var hasSign = file2.Element(ns.GetName("Sign")) != null;
            Assert.AreEqual(sign, hasSign);

            Assert.AreEqual("$(PackageName)",original.Attribute("Name").Value);
            Assert.AreEqual(packageName,expanded.Attribute("Name").Value);
            Assert.AreEqual("$(ArchitectureVar)",original.Attribute("Architecture").Value);
            Assert.AreEqual(arch,expanded.Attribute("Architecture").Value);
        }

        [TestCase("a", "a", true)]
        [TestCase("a", "'a'", true)]
        [TestCase("'a", "'a", true)]
        [TestCase("a'", "a'", true)]
        [TestCase("'a'", "a", true)]
        [TestCase("   'a'   ", "  a  ", true)]
        [TestCase("a", "   a   ", true)]
        [TestCase("   a  ", " a  ", true)]

        [TestCase("a", "a'", false)]
        [TestCase("a", "'a", false)]
        [TestCase("'a", "a", false)]
        [TestCase("b", "a", false)]
        [TestCase("a", "b", false)]
        [TestCase("'  a   '", "a", false)]
        public void ConditionExpanderTest(string lhs, string rhs, bool success)
        {
            var expander = new ConditionExpander();
            var equality = $"{lhs}=={rhs}";
            var inEquality = $"{lhs}!={rhs}";
            Assert.AreEqual(expander.GetExpansion(equality).Any(), success, $"Expected [{equality}] to evaluate to [{success}].");
            Assert.AreEqual(expander.GetExpansion(inEquality).Any(), !success, $"Expected [{inEquality}] to evaluate to [{!success}].");
        }

        [TestCase("", false)]
        [TestCase(" ", false)]
        [TestCase("' '", false)]
        [TestCase("'  '", false)]

        [TestCase("0", true)]
        [TestCase("false", true)]
        [TestCase("123", true)]
        public void ConditionExpanderTest2(string str, bool success)
        {
            var expander = new ConditionExpander();
            Assert.AreEqual(expander.GetExpansion(str).Any(), success, $"Expected [{str}] == [{success}].");
        }


        [Test]
        public void DefaultExpanderThrowsIfPedanticTest([Values(true, false)] bool require)
        {
            string prefix = require ? "$!" : "$";
            var elem = XElement.Parse(
                $"""
                 <Package>
                 <Elem Condition="{prefix}(MyVar)" />
                 <Elem2>{prefix}(MyOtherVar)</Elem2>
                 </Package>
                 """);
            var proc = new PackageXmlPreprocessor(elem);

            try
            {
                proc.Evaluate();
                if (require)
                    Assert.Fail($"Asserted variables should throw if not set.");
            }
            catch (Exception e)
            {
                if (!require)
                    Assert.Fail($"Non-asserted variables should not throw if not set."); 
                StringAssert.Contains($"MyVar", e.Message);
                StringAssert.Contains($"MyOtherVar", e.Message);
            }
        }
    }
}