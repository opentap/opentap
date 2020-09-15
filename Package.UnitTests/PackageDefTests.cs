//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using OpenTap.Plugins.BasicSteps;
using System.Reflection;
using System;
using static OpenTap.Package.PackageDefExt;

namespace OpenTap.Package.UnitTests
{
    internal static class ReflectionHelper
    {
        static Dictionary<MemberInfo, DisplayAttribute> displayLookup = new Dictionary<MemberInfo, DisplayAttribute>();

        public static DisplayAttribute GetDisplayAttribute(this MemberInfo type)
        {
            if (!displayLookup.ContainsKey(type))
            {
                DisplayAttribute attr;
                try
                {
                    attr = type.GetCustomAttributes<DisplayAttribute>().FirstOrDefault();
                }
                catch
                {   // This might happen for outdated plugins where an Attribute type ceased to exist.
                    attr = null;
                }

                if (attr == null)
                {
                    attr = new DisplayAttribute(type.Name, null, Order: -10000, Collapsed: false);
                }

                displayLookup[type] = attr;
            }

            return displayLookup[type];
        }
    }

    [TestFixture]
    public class PackageDefTests
    {
        [TestCase("GlobTest/**/*.txt", 4)]
        [TestCase("GlobTest/*/*.txt", 3)]
        [TestCase("GlobTest/dir*/*.txt", 3)]
        [TestCase("GlobTest/dir?/*.txt", 3)]
        [TestCase("GlobTest/**/dir3/*.txt",1)]
        [TestCase("GlobTest/*/dir3/*.txt", 1)]
        [TestCase("GlobTes?/dir1/*.txt", 1)]
        [TestCase("GlobTest/empty/*.txt", 0)]
        [TestCase("GlobTest/nonexistent/*.txt", 0)]
        [TestCase("GlobTest/empty/*.txt,GlobTest/dir2/*.txt", 2)]
        public void GlobTest(string globPattern, int matchCount)
        {
            Directory.CreateDirectory("GlobTest");
            Directory.CreateDirectory("GlobTest/dir1");
            Directory.CreateDirectory("GlobTest/dir2");
            Directory.CreateDirectory("GlobTest/dir2/dir3");
            Directory.CreateDirectory("GlobTest/empty");
            File.WriteAllText("GlobTest/dir1/inDir1.txt","test");
            File.WriteAllText("GlobTest/dir2/inDir2.txt","test");
            File.WriteAllText("GlobTest/dir2/inDir2Too.txt", "test");
            File.WriteAllText("GlobTest/dir2/dir3/inDir3.txt", "test");

            try
            {
                List<PackageFile> files = new List<PackageFile>
                {
                    new PackageFile{ RelativeDestinationPath = "FirstEntry.txt" }
                };
                foreach(var x in globPattern.Split(','))
                {
                    files.Add(new PackageFile { RelativeDestinationPath = x });
                }

                files.Add(new PackageFile { RelativeDestinationPath = "LastEntry.txt" });

                files = PackageDefExt.expandGlobEntries(files);
                Assert.AreEqual("FirstEntry.txt", files.First().RelativeDestinationPath);
                Assert.AreEqual("LastEntry.txt", files.Last().RelativeDestinationPath);
                Assert.AreEqual(matchCount, files.Count-2);
            }
            finally
            {
                FileSystemHelper.DeleteDirectory("GlobTest");
            }
        }

        [Test]
        public void GetPluginName_Test()
        {
            string inputFilename = "Packages/test2/package.xml";
            PackageDef pkg = PackageDefExt.FromInputXml(inputFilename,Directory.GetCurrentDirectory());
            
            Assert.AreEqual("Test Step",pkg.Files?.FirstOrDefault()?.Plugins?.FirstOrDefault(p => p.Type == typeof(IfStep).FullName)?.BaseType);
            Assert.AreEqual(pkg.Files?.FirstOrDefault()?.Plugins.FirstOrDefault(p => p.Type == typeof(GenericScpiInstrument).FullName)?.BaseType, "Instrument");
        }

        [Test]
        public void FromXmlFile_Test()
        {
            string inputFilename = "Packages/Package/package.xml";

            CliTests.CreateOpenTAPPackage();
            PackageDef pkg = PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());
            //Assert.AreEqual(inputFilename, pkg.FileName);

            CollectionAssert.IsNotEmpty(pkg.Files);
            Assert.AreEqual("OpenTap.dll", pkg.Files[0].FileName);
            Assert.AreEqual("Version", pkg.Files[0].GetCustomData<SetAssemblyInfoData>().FirstOrDefault().Attributes);

            CollectionAssert.IsNotEmpty(pkg.PackageActionExtensions);
            Assert.AreEqual("chmod", pkg.PackageActionExtensions[0].ExeFile);
            Assert.AreEqual("install", pkg.PackageActionExtensions[0].ActionName);
            Assert.AreEqual("+x tap", pkg.PackageActionExtensions[0].Arguments);

            CollectionAssert.IsNotEmpty(pkg.Dependencies);

        }

        [Test]
        public void GitVersionDependency()
        {
            string inputFilename = "Packages/GitversionDependency/package.xml";
            string outputFilename = "GitversionDependency.TapPlugin";
            try
            {
                DummyPackageGenerator.InstallDummyPackage("DepName", new GitVersionCalulator(Directory.GetCurrentDirectory()).GetVersion().ToString() );
                PackageDef pkg = PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());
                pkg.CreatePackage(outputFilename);
                Assert.AreNotSame("$(GitVersion)", pkg.Dependencies.First().Version.ToString());
                VersionSpecifier versionSpecifier = new VersionSpecifier(pkg.Version, VersionMatchBehavior.Exact);

                Assert.AreEqual(pkg.Dependencies.FirstOrDefault(p => p.Name == "DepName").Version.ToString(), versionSpecifier.ToString());
            }
            finally
            {
                DummyPackageGenerator.UninstallDummyPackage("DepName");
                if (File.Exists(outputFilename))
                    File.Delete(outputFilename);
            }
        }

        [Test]
        public void CreatePackage_NoObfuscation()
        {
            string inputFilename = "Packages/package/package.xml";
            string outputFilename = "Test.TapPlugin";

            CliTests.CreateOpenTAPPackage();
            PackageDef pkg = PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());
            try
            {
                pkg.CreatePackage(outputFilename);
                Assert.IsTrue(File.Exists(outputFilename));
            }
            finally
            {
                if (File.Exists(outputFilename))
                    File.Delete(outputFilename);
            }
        }

        [Test]
        public void CreatePackage_NoBinFiles()
        {
            File.Copy("Packages/package_NoBinFiles/package.xml", "package_NoBinFiles.xml", true);
            string inputFilename = "package_NoBinFiles.xml";
            string outputFilename = "Test.TapPackage";

            PackageDef pkg = PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());
            try
            {
                pkg.CreatePackage(outputFilename);
                Assert.IsTrue(File.Exists(outputFilename));
            }
            finally
            {
                if (File.Exists(outputFilename))
                    File.Delete(outputFilename);
            }
        }

        [Test]
        public void CreatePackageVersioningMono()
        {
            var tmpFile = Path.GetTempFileName();
            File.Copy("OpenTap.dll", tmpFile, true);

            OpenTap.Package.SetAsmInfo.SetAsmInfo.SetInfo(tmpFile, new Version("1.2.3"), new Version("4.5.6"), SemanticVersion.Parse("0.1.2"), SetAsmInfo.UpdateMethod.Mono);

            var asm = Assembly.LoadFrom(tmpFile);
            Assert.AreEqual(1, asm.GetName().Version.Major, "Wrong major");
            Assert.AreEqual(2, asm.GetName().Version.Minor, "Wrong minor");
            Assert.AreEqual(3, asm.GetName().Version.Build, "Wrong build");
            
            Assert.AreEqual("4.5.6", asm.GetCustomAttribute<AssemblyFileVersionAttribute>().Version, "File version");
            Assert.AreEqual("0.1.2", asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion, "Informational version");

            Assert.AreEqual("4.5.6", FileVersionInfo.GetVersionInfo(tmpFile).FileVersion, "GetVersionInfo().FileVersion");
            Assert.AreEqual("0.1.2", FileVersionInfo.GetVersionInfo(tmpFile).ProductVersion, "GetVersionInfo().ProductVersion");
            Assert.AreEqual("0.1.2", FileSystemHelper.GetAssemblyVersion(tmpFile), "FileSystemHelper.GetAssemblyVersion");
        }

        [Test]
        public void CreatePackageVersioningIlAsm()
        {
            var tmpFile = Path.GetTempFileName();

            File.Delete(tmpFile);
            tmpFile += ".dll";

            File.Copy("OpenTap.dll", tmpFile, true);

            OpenTap.Package.SetAsmInfo.SetAsmInfo.SetInfo(tmpFile, new Version("1.2.3"), new Version("4.5.6"), SemanticVersion.Parse("0.1.2"), SetAsmInfo.UpdateMethod.ILDasm);

            var asm = Assembly.LoadFrom(tmpFile);
            Assert.AreEqual(1, asm.GetName().Version.Major, "Wrong major");
            Assert.AreEqual(2, asm.GetName().Version.Minor, "Wrong minor");
            Assert.AreEqual(3, asm.GetName().Version.Build, "Wrong build");

            Assert.AreEqual("4.5.6", asm.GetCustomAttribute<AssemblyFileVersionAttribute>().Version, "File version");
            Assert.AreEqual("0.1.2", asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion, "Informational version");

            Assert.AreEqual("4.5.6", FileVersionInfo.GetVersionInfo(tmpFile).FileVersion, "GetVersionInfo().FileVersion");
            Assert.AreEqual("0.1.2", FileVersionInfo.GetVersionInfo(tmpFile).ProductVersion, "GetVersionInfo().ProductVersion");
            Assert.AreEqual("0.1.2", FileSystemHelper.GetAssemblyVersion(tmpFile), "FileSystemHelper.GetAssemblyVersion");
        }

        [Test]
        public void CreatePackageDepVersions()
        {
            var tmp = Path.GetTempFileName();
            var tmp2 = Path.GetTempFileName();

            Directory.Move("Packages", "Packages2");
            try
            {
                {
                    PackageDef def = new PackageDef();
                    def.Name = "test";
                    def.InfoLink = "a";
                    def.Date = DateTime.Today;

                    def.AddFile(tmp);

                    var bs = PluginManager.GetSearcher().Assemblies.First(asm => asm.Name == "OpenTap.Plugins.BasicSteps");
                    def.Files[0].DependentAssemblies.Add(bs);

                    var searcher = new PluginSearcher();
                    searcher.Search(Directory.GetCurrentDirectory());
                    List<AssemblyData> assemblies = searcher.Assemblies.ToList();
                    def.findDependencies(new List<string>(), assemblies);

                    Assert.AreEqual(0, def.Dependencies.Count);
                    Assert.AreNotEqual(0, def.Files.Count);
                }

                {
                    PackageDef def = new PackageDef();

                    def.AddFile(tmp);

                    var bs = PluginManager.GetSearcher().Assemblies.First(asm => asm.Name == "OpenTap.Plugins.BasicSteps");
                    def.Files[0].DependentAssemblies.Add(bs);

                    var searcher = new PluginSearcher();
                    searcher.Search(Directory.GetCurrentDirectory());
                    List<AssemblyData> assemblies = searcher.Assemblies.ToList();
                    def.findDependencies(new List<string> { "OpenTap" }, assemblies);

                    Assert.AreEqual(0, def.Dependencies.Count);
                    Assert.AreNotEqual(1, def.Files.Count);
                }
            }
            finally
            {
                if (Directory.Exists("Packages"))
                    Directory.Delete("Packages", true);
                Directory.Move("Packages2", "Packages");
            }
        }

        [Test]
        public void CreatePackageDepReuse()
        {
            if (Directory.Exists("Packages2"))
                Directory.Delete("Packages2", true);

            Directory.Move("Packages", "Packages2");
            File.Copy("Packages2/DependencyTest.dll", "DependencyTest.dll", true);

            try
            {
                Directory.CreateDirectory("Packages");

                {
                    PackageDef def = new PackageDef() { Name = "pkg1", Version = SemanticVersion.Parse("1.2") };
                    def.AddFile("OpenTap.dll");
                    Directory.CreateDirectory("Packages/pkg1");
                    using (var f = File.OpenWrite("Packages/pkg1/package.xml")) def.SaveTo(f);
                }

                {
                    PackageDef def = new PackageDef() { Name = "gui", Version = SemanticVersion.Parse("1.2") };
                    def.AddFile("Keysight.OpenTap.Wpf.dll");
                    Directory.CreateDirectory("Packages/gui");
                    using (var f = File.OpenWrite("Packages/gui/package.xml")) def.SaveTo(f);
                }

                {
                    PackageDef def = new PackageDef() { Name = "rv", Version = SemanticVersion.Parse("1.2") };
                    def.AddFile("Keysight.OpenTap.Wpf.dll");
                    Directory.CreateDirectory("Packages/rv");
                    using (var f = File.OpenWrite("Packages/rv/package.xml")) def.SaveTo(f);
                }

                {
                    PackageDef def = new PackageDef();
                    def.Name = "test";
                    def.InfoLink = "a";
                    def.Date = DateTime.Today;

                    def.Dependencies.Add(new PackageDependency( "rv", VersionSpecifier.Parse("1.2")));

                    def.AddFile("DependencyTest.dll");
                    def.Files[0].DependentAssemblies.AddRange(PluginManager.GetSearcher().Assemblies.First(f => f.Name == "DependencyTest").References);

                    var searcher = new PluginSearcher();
                    searcher.Search(Directory.GetCurrentDirectory());
                    List<AssemblyData> assemblies = searcher.Assemblies.ToList();
                    def.findDependencies(new List<string>(), assemblies);

                    //Assert.AreEqual(3, def.Dependencies.Count);
                    Assert.IsTrue(def.Dependencies.Any(d => d.Name == "rv"));
                    Assert.IsTrue(def.Dependencies.Any(d => d.Name == "pkg1"));
                }

                {
                    PackageDef def = new PackageDef();
                    def.Name = "test";
                    def.InfoLink = "a";
                    def.Date = DateTime.Today;

                    def.Dependencies.Add(new PackageDependency("gui", VersionSpecifier.Parse("1.2") ));

                    def.AddFile("DependencyTest.dll");
                    def.Files[0].DependentAssemblies.AddRange(PluginManager.GetSearcher().Assemblies.First(f => f.Name == "DependencyTest").References);

                    var searcher = new PluginSearcher();
                    searcher.Search(Directory.GetCurrentDirectory());
                    List<AssemblyData> assemblies = searcher.Assemblies.ToList();
                    def.findDependencies(new List<string>(), assemblies);

                    //Assert.AreEqual(2, def.Dependencies.Count);
                    Assert.IsTrue(def.Dependencies.Any(d => d.Name == "gui"));
                    Assert.IsTrue(def.Dependencies.Any(d => d.Name == "pkg1"));
                }
            }
            finally
            {
                if (Directory.Exists("Packages"))
                    Directory.Delete("Packages", true);
                Directory.Move("Packages2", "Packages");
            }
        }

        [Test]
        public void SaveTo_Simple()
        {
            string inputFilename = "Packages/Package/package.xml";  // this package contains the old XML Schema URL
            string outputFileContent = "";

            PackageDef pkg = PackageDef.FromXml(inputFilename);

            using (Stream str = new MemoryStream())
            {
                pkg.SaveTo(str);
                using (StreamReader reader = new StreamReader(str))
                {
                    reader.BaseStream.Seek(0, 0);
                    outputFileContent = reader.ReadToEnd();
                }
            }
            string inputContent = File.ReadAllText(inputFilename);
            // check that the package now contains the new XML Schema URL
            StringAssert.Contains("xmlns=\"http://opentap.io/schemas/package\"", outputFileContent); 
        }

        [Test]
        public void SaveManyTo_Simple()
        {
            CliTests.CreateOpenTAPPackage();
            string outputFileContent = "";

            PackageDef pkg = PackageDefExt.FromInputXml("Packages/Package/package.xml", Directory.GetCurrentDirectory());
            PackageDef pkg1 = PackageDefExt.FromInputXml("Packages/test2/package.xml", Directory.GetCurrentDirectory());

            using (Stream str = new MemoryStream())
            {
                PackageDef.SaveManyTo(str, new List<PackageDef> { pkg, pkg1 });
                using (StreamReader reader = new StreamReader(str))
                {
                    reader.BaseStream.Seek(0, 0);
                    outputFileContent = reader.ReadToEnd();
                }
            }
            // check that the package now contains the new XML Schema URL
            StringAssert.Contains("xmlns=\"http://opentap.io/schemas/package\"", outputFileContent);
            Assert.AreEqual(2, Regex.Matches(outputFileContent, "xmlns").Count);
        }

        [Test]
        public void SaveTo_FromXmlFile_Dependency()
        {
            string inputXml = @"<?xml version='1.0' encoding='utf-8' ?>
<Package Name='Test3' xmlns ='http://opentap.io/schemas/package'>
  <Files>
    <File Path='OpenTap.Package.UnitTests.dll'>
        <UseVersion/>
    </File>
  </Files>
</Package>
";
            string inputFilename = "test3.package.xml";
            File.WriteAllText(inputFilename,inputXml);
            string outputFileContent = "";

            PackageDef pkg = PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());
            pkg.Files.First().IgnoredDependencies.AddRange(new[] { "abc", "test" });

            using (Stream str = new MemoryStream())
            {
                pkg.SaveTo(str);
                using (StreamReader reader = new StreamReader(str))
                {
                    reader.BaseStream.Seek(0, 0);
                    outputFileContent = reader.ReadToEnd();
                }
            }
            
            StringAssert.Contains("<PackageDependency Package=\"Test1\" Version=\"^1.2", outputFileContent);   // the Test2 package is a release version, so it should have 2 version numbers specified
            //StringAssert.Contains("<PackageDependency Package=\"Test2\" Version=\"1.2.3", outputFileContent); // the Test2 package is from an integration branch, so it should have 3 version numbers specified
            StringAssert.Contains("<PackageDependency Package=\"OpenTAP\"", outputFileContent);
            StringAssert.Contains("<IgnoreDependency>test</IgnoreDependency>", outputFileContent);

            // check that the dependency to XSeries is not there twice:
            Assert.IsFalse(Regex.IsMatch(outputFileContent, "(Test1).+\n.+(Test1)"));
        }

        [Test]
        public void findDependencies_SharedAssemblyReference()
        {
            CliTests.CreateOpenTAPPackage();
            var inst = new Installation(Directory.GetCurrentDirectory());
            var pkgs = inst.GetPackages();
            string inputXml = @"<?xml version='1.0' encoding='utf-8' ?>
<Package Name='Test3' xmlns ='http://opentap.io/schemas/package'>
  <Files>
    <File Path='OpenTap.Package.UnitTests.dll'/>
  </Files>
</Package>
";
            string inputFilename = "test3.package.xml";
            File.WriteAllText(inputFilename, inputXml);

            PackageDef pkg = PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());

            // This package should depend on the OpenTAP package, since it contains OpenTAP.dll that OpenTap.Package.UnitTests.dll from this package needs
            CollectionAssert.Contains(pkg.Dependencies.Select(d => d.Name),"OpenTAP");
        }

        [Test]
        public void findDependencies_HardcodedDependency()
        {
            CliTests.CreateOpenTAPPackage();
            var inst = new Installation(Directory.GetCurrentDirectory());
            var pkgs = inst.GetPackages();
            string inputXml = @"<?xml version='1.0' encoding='utf-8' ?>
<Package Name='Test3' xmlns ='http://opentap.io/schemas/package'>
  <Dependencies>
    <PackageDependency Package='OpenTAP' Version='Any'/>
  </Dependencies>
  <Files>
    <File Path='OpenTap.Package.UnitTests.dll'/>
  </Files>
</Package>
";
            string inputFilename = "test3.package.xml";
            File.WriteAllText(inputFilename, inputXml);

            PackageDef pkg = PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());

            // This package should depend on the OpenTAP package only once.
            Assert.AreEqual(1, pkg.Dependencies.Count(d => d.Name == "OpenTAP"));
        }

        [Test]
        public void findDependencies_HardcodedDependencyNotInstalled()
        {
            CliTests.CreateOpenTAPPackage();
            var inst = new Installation(Directory.GetCurrentDirectory());
            var pkgs = inst.GetPackages();
            string inputXml = @"<?xml version='1.0' encoding='utf-8' ?>
<Package Name='Test3' xmlns ='http://opentap.io/schemas/package'>
  <Dependencies>
    <PackageDependency Package='NotInstalled' Version='Any'/>
  </Dependencies>
  <Files>
    <File Path='OpenTap.Package.UnitTests.dll'/>
  </Files>
</Package>
";
            string inputFilename = "test3.package.xml";
            File.WriteAllText(inputFilename, inputXml);

            try
            {
                PackageDef pkg = PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());
                Assert.Fail("Missing dependency should have thrown an exception");
            }
            catch(Cli.ExitCodeException ex)
            {
                Assert.AreEqual((int)PackageCreateAction.ExitCodes.PackageDependencyError, ex.ExitCode);
            }
        }

        [Test]
        public void findDependencies_SharedAssemblyReferenceInDependencies()
        {
            CliTests.CreateOpenTAPPackage();
            var inst = new Installation(Directory.GetCurrentDirectory());
            var pkgs = inst.GetPackages();
            string inputXml = @"<?xml version='1.0' encoding='utf-8' ?>
<Package Name='Test3' xmlns ='http://opentap.io/schemas/package'>
  <Files>
    <File Path='System.Reflection.Metadata.dll'/>
  </Files>
</Package>
";
            string inputFilename = "test3.package.xml";
            File.WriteAllText(inputFilename, inputXml);

            PackageDef pkg = PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());

            // This package should not depend on the OpenTAP package, event though it contains System.Collections.Immutable.dll that System.Reflection.Metadata.dll from this package needs
            CollectionAssert.DoesNotContain(pkg.Dependencies.Select(d => d.Name), "OpenTAP");
        }

        /// <summary>
        /// This test requires that OpenTAP is installed along with the XSeries plugin
        /// </summary>
        [Test]
        public void CheckDependencies_MissingDep()
        {
            string inputFilename = "Packages/CheckDependencies_MissingDep/package.xml";

            //PackageDependencyExt.CheckDependencies(inputFilename);
            var xseries = PackageDef.FromXml(PackageDef.GetDefaultPackageMetadataPath("XSeries"));
            PackageDef.ValidateXml(inputFilename);
            var missing = PackageDef.FromXml(inputFilename);
            var tree = DependencyAnalyzer.BuildAnalyzerContext(new List<PackageDef> { xseries, missing });
            Assert.IsTrue(tree.GetIssues(missing).Any(issue => issue.IssueType == DependencyIssueType.Missing));
            //Assert.Fail("CheckDependencies should have thrown an exception");
        }

        /// <summary>
        /// This test requires that TAP is installed along with the XSeries and plugin
        /// </summary>
        [Test]
        [Ignore("??")]
        public void CheckDependencies_AllDepsInstalled()
        {

            var xseries = PackageDef.FromXml(PackageDef.GetDefaultPackageMetadataPath("XSeries"));
            //var test2 = PackageDef.FromXmlFile(PackageDef.GetDefaultPackageMetadataPath("Test2"));
            File.Copy(PackageDef.GetDefaultPackageMetadataPath("CheckDependencies_AllDepsInstalled"), "CheckDependencies_AllDepsInstalled.xml", true);
            PackageDef.ValidateXml("CheckDependencies_AllDepsInstalled.xml");
            var alldeps = PackageDef.FromXml("CheckDependencies_AllDepsInstalled.xml");
            //var tree = DependencyAnalyzer.BuildAnalyzerContext(new List<PackageDef> { xseries, test2, alldeps });
            //Assert.AreEqual(tree.BrokenPackages.Count, 1);
            //PackageDependencyExt.CheckDependencies(inputFilename);
        }

        /// <summary>
        /// This test requires that TAP is installed along with the XSeries and Test plugin
        /// </summary>
        [Test, Ignore("?")]
        public void CheckDependencies_NonDllFile()
        {
            string inputFilename = "FromXmlFile_NonDllFile.xml";

            PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());
            //PackageDependency.CheckDependencies(inputFilename);
        }

        [Test]
        [Ignore("Temporarily Disabled")]
        public void InstalledPackages_TwoPackages()
        {
            Assert.IsTrue(File.Exists(PackageDef.GetDefaultPackageMetadataPath("XSeries")), "Necessary package file missing.");

            System.Collections.Generic.List<PackageDef> target = new Installation(Directory.GetCurrentDirectory()).GetPackages();
            CollectionAssert.AllItemsAreInstancesOfType(target, typeof(PackageDef));
            CollectionAssert.AllItemsAreNotNull(target);
            CollectionAssert.AllItemsAreUnique(target);
            Assert.IsTrue(target.Any(pkg => pkg.Name == "XSeries"));
            Assert.IsTrue(target.Any(pkg => pkg.Name == "Test"));
        }

        [Test]
        [Ignore("Temporarily Disabled")]
        public void InstalledPackages_InvalidXmlIgnored()
        {
            Assert.IsTrue(File.Exists(PackageDef.GetDefaultPackageMetadataPath("XSeries")), "Necessary package file missing.");


            File.WriteAllText(Path.Combine(PackageDef.PackageDefDirectory, "Invalid.package.xml"),
@"<?xml version='1.0' encoding='utf-8' ?>
<Package Name='Invalid' xmlns='http://keysight.com/schemas/TAP/Package'>
  <Files>
    <File Path='Tap.Engine.dll' Obfuscate='false'><UseVersion/></File>
  </Files>
  <FileName>This should not be there<FileName/>
</Package>");

            System.Collections.Generic.List<PackageDef> target = new Installation(Directory.GetCurrentDirectory()).GetPackages();;


            CollectionAssert.AllItemsAreInstancesOfType(target, typeof(PackageDef));
            CollectionAssert.AllItemsAreNotNull(target);
            CollectionAssert.AllItemsAreUnique(target);
            Assert.IsTrue(target.Any(pkg => pkg.Name == "XSeries"));
            Assert.IsTrue(target.Any(pkg => pkg.Name == "Test"));
            Assert.IsFalse(target.Any(pkg => pkg.Name == "Invalid"));

            File.Delete(Path.Combine(PackageDef.PackageDefDirectory, "Invalid.package.xml"));
        }
        public static void EnsureDirectory(string filePath)
        {
            var dirname = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(dirname))
                return;
            if (!Directory.Exists(dirname))
                Directory.CreateDirectory(dirname);
        }

        [Test]
        public void FileRepositoryManagerTest()
        {
            string pkgContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package Name=""BasicSteps"" xmlns=""http://keysight.com/schemas/TAP/Package"" OS=""Linux,Windows"">
 <Files>
  <File Path=""__BASICSTEPS_DLL__"">
    <UseVersion/>
  </File>
 </Files>
</Package>
".Replace("__BASICSTEPS_DLL__", Path.GetFileName(typeof(DelayStep).Assembly.Location));
            string pkgName = "BasicSteps.xml";
            
            try
            {
                if (File.Exists(pkgName))
                    File.Delete(pkgName);
                File.WriteAllText(pkgName, pkgContent);
                CliTests.CreateOpenTAPPackage(); // to get a dependency, OpenTAP first needs to be installed
                string installDir = Path.GetDirectoryName(typeof(Package.PackageDef).Assembly.Location);
                var pkg = PackageDefExt.FromInputXml(pkgName, installDir);
                CollectionAssert.IsNotEmpty(pkg.Dependencies,"Package has no dependencies.");
                //Assert.AreEqual("OpenTAP", pkg.Dependencies.First().Name);
                pkg.CreatePackage("BasicSteps.TapPackage");

                List<IPackageRepository> repositories = new List<IPackageRepository>() { new FilePackageRepository(installDir) };

                var packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, new PackageSpecifier());
                CollectionAssert.IsNotEmpty(packages, "Repository does not list any packages.");
                Assert.IsTrue(packages.Any(p => p.Name == "BasicSteps"));


                var depVersion = pkg.Dependencies.First().Version;
                var version = new SemanticVersion(depVersion.Major ?? 0, depVersion.Minor ?? 0, depVersion.Patch ?? 0, depVersion.PreRelease, "");
                var depName = pkg.Dependencies.First().Name;
                packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(repositories, new PackageSpecifier("BasicSteps"), new PackageIdentifier(depName, version, CpuArchitecture.Unspecified, null));
                CollectionAssert.IsNotEmpty(packages, "Repository does not list any compatible \"BasicSteps\" package.");
                Assert.IsTrue(packages.First().Name == "BasicSteps");
            }
            finally
            {
                if (File.Exists("BasicSteps.TapPackage"))
                    File.Delete("BasicSteps.TapPackage");
                if (File.Exists(pkgName))
                    File.Delete(pkgName);
            }
        }

        [Test]
        public void TestCliPackaging()
        {
            var p = Process.Start("tap.exe", "package create \"Packages/Package/package.xml\" -v");
            p.WaitForExit();
            Assert.AreEqual(0, p.ExitCode);
            var plugins = Directory.EnumerateFiles(".");

            Assert.IsTrue(plugins.Any(package =>
                Path.GetFileName(package).StartsWith("Test", System.StringComparison.InvariantCultureIgnoreCase) &&
                Path.GetExtension(package).EndsWith("TapPackage", System.StringComparison.InvariantCultureIgnoreCase)
            ), "Generated OpenTAP package file not found");
        }

        /* XSeries not compiled for TAP 5.0.
        [Test]
        [DeploymentItem("package3.xml")]
        [DeploymentItem("Keysight.TapPlugin.ResultListener.SqlDatabase.dll")]
        [DeploymentItem("XSeries.2.3.0.9b084a8.TapPlugin")]
        public void DublicateAssemblies()
        {
            // This test creates assemblies to test some complicated behaviour related to 
            // creating plugin packages.

            var pkgpath = "XSeries.2.3.0.9b084a8.TapPlugin";
            var files = PluginInstaller.FilesInPackage(pkgpath);
            using (ZipPackage zip =(ZipPackage)ZipPackage.Open(pkgpath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                foreach (var part in zip.GetParts())
                {
                    try
                    {
                        string filePath = part.Uri.OriginalString.TrimStart('/');
                        EnsureDirectory(filePath);
                        using (var str = File.Create(filePath))
                        {
                            part.GetStream().CopyTo(str);
                        }
                    }
                    catch
                    {

                    }
                }
            }
            CSharpCodeProvider provider = new CSharpCodeProvider();
            CompilerParameters parameters = new CompilerParameters();
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("TapPlugin.XSignalAnalyzer.dll");
            
            parameters.ReferencedAssemblies.Add("Keysight.Tap.Engine.dll");
            parameters.GenerateInMemory = false;
            parameters.GenerateExecutable = false;
            parameters.OutputAssembly = "test1.dll";
            var asmv = "[assembly: System.Reflection.AssemblyInformationalVersionAttribute(\"CAL10604_DVT10603.2.f2d1ee5\")]\n";
            CompilerResults results = provider.CompileAssemblyFromSource(parameters, asmv + "public class Instr1:TapPlugin.XSignalAnalyzer.XsaCore {}");
            parameters.OutputAssembly = "test2.dll";
            parameters.ReferencedAssemblies.Add("TapPlugin.XSignalSource.dll");
            parameters.ReferencedAssemblies.Remove("TapPlugin.XSignalAnalyzer.dll");
            CompilerResults results2 = provider.CompileAssemblyFromSource(parameters, asmv + "public class Instr2:TapPlugin.XSignalSource.XsgCore {}");
            var pkg = PackageDefExt.FromXmlFile("package3.xml", verbose: true);
            Assert.AreEqual(1, pkg.Dependencies.Count);
            Assert.AreEqual("XSeries", pkg.Dependencies.First().PackageName);
        }*/
    }
}
