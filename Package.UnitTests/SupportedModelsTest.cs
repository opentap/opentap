using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests;

[TestFixture]
public class SupportedModelsTest
{
    [SupportedModels("OpenTAP", "N9002A")]
    public class SupportedModelsInstrument : Instrument
    {
    }

    [SupportedModels("OpenTAP 2", "N9002B", "N9002C")]
    [SupportedModels("Not OpenTAP", "N9002D")]
    [SupportedModels("Not OpenTAP", "N9002E")]
    public class SupportedModelsInstrument2 : Instrument
    {
    }

    [Test]
    public void TestSupportedModelsAddedToPackageXml()
    {
        var path = typeof(SupportedModelsTest).Assembly.Location;
        var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Package Name=""Supported Models Package""   Version=""1.0.0"" OS=""{OperatingSystem.Current}"" Architecture=""AnyCPU"">
    <Files>
        <File Path=""{Path.GetFileName(path)}""/>
    </Files>
</Package>
";
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, xml);

            {
                // Test creating package from raw xml
                var inputXmlPackage = PackageDefExt.FromInputXml(tmp, Path.GetDirectoryName(path));
                testContent(inputXmlPackage);
            }
            {
                // Test real package
                var outFile = Path.GetTempFileName();
                File.Delete(outFile);
                try
                {
                    var act = new PackageCreateAction { PackageXmlFile = tmp, OutputPaths = [outFile] };
                    act.Execute(CancellationToken.None);
                    var realPackage = PackageDef.FromPackage(outFile);
                    testContent(realPackage);
                }
                finally
                {
                    File.Delete(outFile);
                }
            }

            void testContent(PackageDef pkg)
            {
                {
                    // Test that the first instrument supports 1 model from 1 manufacturer
                    var manufacturerOne = pkg.Files.SelectMany(x => x.Plugins)
                        .First(plugin => plugin.Name == nameof(SupportedModelsInstrument));
                    Assert.That(manufacturerOne.SupportedModels.Length, Is.EqualTo(1));
                    Assert.That(manufacturerOne.SupportedModels[0].Models.Length, Is.EqualTo(1));
                    Assert.That(manufacturerOne.SupportedModels[0].Models[0], Is.EqualTo("N9002A"));
                }
                {
                    // Test that the second instrument supports 2 manufacturers, and furthermore that
                    // the models from the 2nd manufacturer were correctly merged despite being declared in two different attributes
                    var manufacturerTwo = pkg.Files.SelectMany(x => x.Plugins)
                        .First(plugin => plugin.Name == nameof(SupportedModelsInstrument2));
                    var attrs = manufacturerTwo.SupportedModels;
                    Assert.That(attrs.Length, Is.EqualTo(2));
                    var o1 = attrs.FirstOrDefault(x => x.Manufacturer == "OpenTAP 2");
                    Assert.NotNull(o1);
                    var o2 = attrs.FirstOrDefault(x => x.Manufacturer == "Not OpenTAP");
                    Assert.NotNull(o2);
                    Assert.That(o1.Models, Is.EqualTo(new string[] { "N9002B", "N9002C" }));
                    Assert.That(o2.Models, Is.EqualTo(new string[] { "N9002D", "N9002E" }));
                }
            }
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}