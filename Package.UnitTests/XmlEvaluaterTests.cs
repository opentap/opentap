using System.IO;
using System.Xml.Linq;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class XmlEvaluaterTests
    {
        [Test]
        public void TestEvaluateXml()
        {
            const string platform = "Windows";
            const string arch = "TestArch";
            const string owner = "TestOwner";
            const string packageName = "TestPackage";
            const string sourceUrl = "Some Source Url";

            var xmlString = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Package Name=""$(PackageName)"" Version=""$(GitVersion)"" OS=""$(Platform)"" Architecture=""$(Architecture)"">
  <Variables>
    <Platform>{platform}</Platform>
    <Architecture>{arch}</Architecture>
    <Configuration>$(Platform)-$(Architecture)</Configuration>
    <Owner>{owner}</Owner>
    <PackageName>{packageName}</PackageName>
    <SourceUrl>{sourceUrl}</SourceUrl>
  </Variables>
  <SourceUrl>$(SourceUrl)</SourceUrl>
  <Owner>$(Owner)</Owner>
  <Files Condition=""a == b"">
    <File Path=""WrongFile""/>
  </Files>
  <Files Condition=""1"">
    <File Path=""CorrectFile""/>
  </Files>

  <PackageActionExtensions Condition=""'$(Platform)'  !=   'Windows'"">
    <ActionStep ExeFile=""tap.exe"" ActionName=""install""/>
  </PackageActionExtensions>
</Package>
";

            var elem = XElement.Parse(xmlString);
            var evaluater = new XmlEvaluater(elem);
            var result = evaluater.Evaluate();
        }
    }
}