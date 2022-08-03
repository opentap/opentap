using NUnit.Framework;
using OpenTap.Image;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.Image.Tests
{
    [TestFixture]
    public class Deserialize
    {
        [Test]
        public void TestJsonImage()
        {

            string imageJson = @"
            {
                ""Packages"": [
                    {
                        ""Name"": ""Demonstration""
                    },
                    {
                        ""Name"": ""Yardstick"",
                        ""Version"": ""beta"",
                        ""OS"": ""Windows"",
                        ""Architecture"": ""AnyCPU""
                    },
                    {
                        ""Name"": ""OpenTAP"",
                        ""Version"": ""9.15.2""
                    },
                    {
                        ""Name"": ""REST-API"",
                        ""Version"": ""beta""
                    }
                ],
                ""Repositories"": [
                    ""https://packages.opentap.io"",
                    ""https://packages.opentap.keysight.com""
                ]
            }";

            var specifier = ImageSpecifier.FromString(imageJson);
            Assert.True(specifier.Packages.Count == 4);
            Assert.True(specifier.Packages[0].Name == "Demonstration");
            Assert.True(specifier.Packages[1].Name == "Yardstick");
            Assert.True(specifier.Packages[2].Name == "OpenTAP");
            Assert.True(specifier.Packages[3].Name == "REST-API");

            Assert.True(specifier.Packages[1].Version == VersionSpecifier.Parse("beta"));
            Assert.True(specifier.Packages[1].Architecture == CpuArchitecture.AnyCPU);
            Assert.True(specifier.Packages[1].OS == "Windows");

            Assert.True(specifier.Repositories.Count == 3);
            Assert.True(specifier.Repositories[0] == new Uri(PackageCacheHelper.PackageCacheDirectory).AbsoluteUri);
            Assert.True(specifier.Repositories[1] == "https://packages.opentap.io");
            Assert.True(specifier.Repositories[2] == "https://packages.opentap.keysight.com");

        }

        [Test]
        public void TestXmlImage()
        {

            string imageXml = @"<?xml version=""1.0""?>
<Image>
  <Packages>
    <PackageSpecifier Name=""Editor"" Version=""9.9.1+ca3d0108"" OS=""Windows"" Architecture=""x64"" />
    <PackageSpecifier Name=""TUI"" Version=""any"" />
    <PackageSpecifier Name=""Demonstration"" Version=""9.0.3+cb113229"" OS=""Windows,Linux"" Architecture=""AnyCPU"" />
  </Packages>
  <Repositories>
    <string>https://packages.opentap.io</string>
    <string>C:\git\installercreate\bin\Debug</string>
  </Repositories>
</Image>
";

            var specifier = ImageSpecifier.FromString(imageXml);

            //TapSerializer tapSerializer = new TapSerializer();
            //tapSerializer.AddSerializers(new List<ITapSerializerPlugin>() { new PackageSpecifierSerializerPlugin() });
            //string valueReSerialized = tapSerializer.SerializeToString(specifier);

            Assert.True(specifier.Packages.Count == 3);
            Assert.True(specifier.Packages[0].Name == "Editor");
            Assert.True(specifier.Packages[1].Name == "TUI");
            Assert.True(specifier.Packages[2].Name == "Demonstration");

            Assert.True(specifier.Packages[0].Version == VersionSpecifier.Parse("9.9.1+ca3d0108"));
            Assert.True(specifier.Packages[0].Architecture == CpuArchitecture.x64);
            Assert.True(specifier.Packages[0].OS == "Windows");

            Assert.True(specifier.Packages[1].Version == VersionSpecifier.Parse("any"));

            Assert.True(specifier.Packages[2].Version == VersionSpecifier.Parse("9.0.3+cb113229"));
            Assert.True(specifier.Packages[2].Architecture == CpuArchitecture.AnyCPU);
            Assert.True(specifier.Packages[2].OS == "Windows,Linux");

            Assert.True(specifier.Repositories.Count == 2);
            Assert.True(specifier.Repositories[0] == "https://packages.opentap.io");
            Assert.True(specifier.Repositories[1] == @"C:\git\installercreate\bin\Debug");
        }
    }
}
