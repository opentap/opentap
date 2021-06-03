using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class PackageSlashTests
    {
        private void VerifyContent(string basePath, bool shouldExist)
        {
            var files = new string[]
            {
                Path.Combine(basePath, dir1, filename),
                Path.Combine(basePath, dir1, dir2, filename),
                Path.Combine(basePath, PackageDirRelative, filename)
            };

            foreach (var file in files)
            {
                if (shouldExist)
                    Assert.AreEqual(File.ReadAllText(file), sampleText);
                else
                    FileAssert.DoesNotExist(file);
            }
            
            if (shouldExist)
                FileAssert.Exists(Path.Combine(basePath, PackageDirRelative, "package.xml"));
            else
                FileAssert.DoesNotExist(Path.Combine(basePath, PackageDirRelative, "package.xml"));
        }
        
        private const string dir1 = "TestPackageDir";
        private string TapDir => Path.GetDirectoryName(typeof(PluginSearcher).Assembly.Location);
        private const string dir2 = "Subdir";
        private const string name = "PackageName";
        private const string os = "Windows,Linux";
        private string FullName => Path.Combine(dir1, dir2, name).Replace("\\", "/");
        private string PackageDirRelative => Path.Combine("Packages", FullName);
        private string PackageDir => Path.Combine(TapDir, PackageDirRelative);
        private string PackageCache => Path.Combine(TapDir, "PackageCache");
        private const string version = "3.2.1";
        private const string filename = "SampleFile.txt";
        private const string packageFileName = "TestPackage.xml";
        private const string sampleText = "Sample File Content";

        private string outputPackagePath =>
            PackageActionHelpers.slashRegex.Replace(Path.Combine(dir1, dir2, $"{name}.{version}.TapPackage"), ".");
        private string downloadedPackagePath =>
            PackageActionHelpers.slashRegex.Replace(Path.Combine(dir1, dir2, $"{name}.{version}.{os}.TapPackage"), ".");
        
        private string description =
            "test package for testing that forward and backwards slashes are handled correctly in package names";

        [Test]
        public void SlashTests()
        {
            if (Directory.Exists(dir1))
                Directory.Delete(dir1, true);
            
            CreateTestPackage();
            VerifyPackageContent();
            DownloadPackage();
            InstallPackage();
            UninstallPackage();
        }
        
        public void CreateTestPackage()
        {
            var packageXml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Package Name=""{FullName}"" xmlns=""http://opentap.io/schemas/package"" Version=""{version}"" OS=""{os}"" >
  <Description>
      {description}
  </Description>
  <Files>
      <File Path=""{dir1}/{filename}"" SourcePath=""{filename}""/>
      <File Path=""{dir1}\{dir2}/{filename}"" SourcePath=""{filename}""/>
      <File Path=""{PackageDirRelative}/{filename}"" SourcePath=""{filename}""/>
  </Files>
</Package>
";
            File.WriteAllText(filename, sampleText);
            File.WriteAllText(packageFileName, packageXml);

            var create = new PackageCreateAction {Install = false, PackageXmlFile = packageFileName};
            Assert.AreEqual(0, create.Execute(CancellationToken.None));
        }

        public void VerifyPackageContent()
        {
            FileAssert.Exists(outputPackagePath);
            var testDir = "__NewTestDir__";
            
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
            
            ZipFile.ExtractToDirectory(outputPackagePath, testDir);
            
            VerifyContent(testDir, true);
        }
        

        public void DownloadPackage()
        {
            var outputPath = "DownloadedPackage.TapPackage";
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            
            var download = new PackageDownloadAction()
            {
                ForceInstall = true,
                Packages = new[] {FullName},
                Repository = new[] { Directory.GetCurrentDirectory() },
                OutputPath = outputPath
            };
            Assert.AreEqual(0, download.Execute(CancellationToken.None));
            FileAssert.Exists(outputPath);
            FileAssert.Exists(Path.Combine(PackageCache, outputPath));

            download = new PackageDownloadAction()
            {
                ForceInstall = true,
                Repository = new[] { Directory.GetCurrentDirectory() },
                Packages = new[] {FullName}
            };
            
            outputPath = PackageActionHelpers.slashRegex.Replace(downloadedPackagePath, ".");
                        
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            
            Assert.AreEqual(0, download.Execute(CancellationToken.None));

            FileAssert.Exists(outputPath);
            FileAssert.Exists(Path.Combine(PackageCache, outputPath));
        }

        public void InstallPackage()
        {
            if (Directory.Exists(dir1))
                Directory.Delete(dir1, true);
            
            {  // Install test
                var packageXml = Path.Combine(PackageDir, "package.xml");

                var install = new PackageInstallAction()
                {
                    Force = true,
                    NonInteractive = true,
                    Repository = new[] { Directory.GetCurrentDirectory() },
                    Packages = new[] {FullName}
                };

                if (File.Exists(packageXml))
                    File.Delete(packageXml);

                FileAssert.DoesNotExist(packageXml);
                Assert.AreEqual(0, install.Execute(CancellationToken.None));
                FileAssert.Exists(packageXml);

                VerifyContent("", true);
            }
        }

        public void UninstallPackage()
        {
            var uninstall = new PackageUninstallAction()
            {
                Force = true,
                NonInteractive = true,
                Packages = new[] {FullName}
            };

            DirectoryAssert.Exists(dir1);
            VerifyContent("", true);

            Assert.AreEqual(0, uninstall.Execute(CancellationToken.None));

            DirectoryAssert.DoesNotExist(dir1);
            VerifyContent("", false);
        }
    }
}
