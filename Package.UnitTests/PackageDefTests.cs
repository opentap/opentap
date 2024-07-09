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
using System.Runtime.Loader;
using System.Xml;
using OpenTap.Cli;
using OpenTap.Engine.UnitTests.TestTestSteps;
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
        [Test]
        public void MetaDataTest()
        {
            var package = new PackageDef();
            package.Name = "test";
            package.Version = SemanticVersion.Parse("1.0.0");
            package.MetaData.Add("Kind", "UnitTest");
            package.MetaData.Add("Test", "Value");

            using (var stream = new MemoryStream())
            {
                package.SaveTo(stream);
                package = null;

                // Save the package to xml
                stream.Seek(0, 0);
                stream.Position = 0;
                string xml = new StreamReader(stream).ReadToEnd();
                Assert.IsNotNull(xml);
                Assert.IsNotEmpty(xml);
                
                // Check xml contains the right elements
                var doc = new XmlDocument();
                doc.LoadXml(xml);
                var packageNode = doc.DocumentElement;
                Assert.NotNull(packageNode);
                Assert.IsTrue(packageNode.HasChildNodes);
                Assert.IsTrue(packageNode.ChildNodes.Count == 2);
                Assert.IsTrue(packageNode.FirstChild.Name == "Kind");
                Assert.IsTrue(packageNode.LastChild.Name == "Test");
                
                // Load the package from xml
                stream.Seek(0, 0);
                stream.Position = 0;
                package = PackageDef.FromXml(stream);
                
                Assert.NotNull(package);
                Assert.IsTrue(package.MetaData.ContainsKey("Kind") && package.MetaData["Kind"] == "UnitTest");
                Assert.IsTrue(package.MetaData.ContainsKey("Test") && package.MetaData["Test"] == "Value");
            }
        }

        [Test]
        public void MetaDataOverriding()
        {
            var package = new PackageDef();
            package.Name = "test";
            package.Version = SemanticVersion.Parse("1.0.0");
            package.MetaData.Add("Description", "Something that should not be added.");

            using (var stream = new MemoryStream())
            {
                Assert.Catch<ExitCodeException>(() => package.SaveTo(stream));
            }
        }
        
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
            Assert.AreEqual(pkg.Files?.FirstOrDefault()?.Plugins.FirstOrDefault(p => p.Type == "OpenTap.Plugins.BasicSteps.GenericScpiInstrument")?.BaseType, "Instrument");
        }

        [Test]
        public void FromXmlFile_Test()
        {
            string inputFilename = "Packages/Package/package.xml";

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
            string inputFilename = "GitVersionDependency-package.xml";
            string outputFilename = "GitversionDependency.TapPlugin";
            try
            {
                DummyPackageGenerator.InstallDummyPackage("DepName", new GitVersionCalulator(Directory.GetCurrentDirectory()).GetVersion().ToString() );
                PackageDef pkg = PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());
                using (var file = CreateStream(outputFilename))
                    pkg.CreatePackage(file);
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
            string inputFilename = "Packages/Package/package.xml";
            string outputFilename = "Test.TapPlugin";

            PackageDef pkg = PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());
            try
            {
                using(var file = CreateStream(outputFilename))
                    pkg.CreatePackage(file);
                Assert.IsTrue(File.Exists(outputFilename));
            }
            finally
            {
                if (File.Exists(outputFilename))
                    File.Delete(outputFilename);
            }
        }

        private static FileStream CreateStream(string outputFilename) => new FileStream(outputFilename, FileMode.Create,
            FileAccess.ReadWrite, FileShare.ReadWrite, 4096);

        [Test]
        public void CreatePackage_NoBinFiles()
        {
            File.Copy("Packages/Package_NoBinFiles/package.xml", "package_NoBinFiles.xml", true);
            string inputFilename = "package_NoBinFiles.xml";
            string outputFilename = "Test.TapPackage";

            PackageDef pkg = PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());
            try
            {
                using(var file = CreateStream(outputFilename))
                    pkg.CreatePackage(file);
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

            SetAsmInfo.SetAsmInfo.SetInfo(tmpFile, new Version("1.2.3"), new Version("4.5.6"), SemanticVersion.Parse("0.1.2"));

            // Side loading assemblies with different versions is not supported in netcore
            // Load the dll in a new context instead. If we load it in the current context,
            // the currently loaded OpenTAP.dll will be returned instead
            var ctx = new AssemblyLoadContext("tmp");
            var asm = ctx.LoadFromAssemblyPath(tmpFile);
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
        [Platform(Exclude="Unix,Linux,MacOsX")]
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
            // tap package create X --package-dir C:\MyPackages
            
            string PackageInstallDir = "C:\\MyPackages\\";
            var inst = new Installation(Directory.GetCurrentDirectory());

            if (PackageInstallDir != null)
                inst = new Installation(PackageInstallDir);
            if (inst.GetPackages().Count() == 0)
                inst = new Installation(System.IO.Path.GetDirectoryName(typeof(TestPlan).Assembly.Location));    
            
            
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
                Assert.AreEqual((int)PackageExitCodes.PackageDependencyError, ex.ExitCode);
            }
        }

        [Test]
        public void findDependencies_SharedAssemblyReferenceInDependencies()
        {
            var inst = new Installation(Directory.GetCurrentDirectory());
            var pkgs = inst.GetPackages();
            string inputXml = @"<?xml version='1.0' encoding='utf-8' ?>
<Package Name='Test3' xmlns ='http://opentap.io/schemas/package'>
  <Files>
    <File Path='System.Reflection.MetadataLoadContext.dll'/>
  </Files>
</Package>
";
            string inputFilename = "test3.package.xml";
            File.WriteAllText(inputFilename, inputXml);

            PackageDef pkg = PackageDefExt.FromInputXml(inputFilename, Directory.GetCurrentDirectory());

            // This package should not depend on the OpenTAP package, event though it contains System.Collections.Immutable.dll that System.Reflection.Metadata.dll from this package needs
            CollectionAssert.DoesNotContain(pkg.Dependencies.Select(d => d.Name), "OpenTAP");
        }

        static string GetEmbeddedFile(string file)
        {
            var resourceName = Assembly.GetCallingAssembly().GetName().Name + "." + file.Replace("/", ".");
            var stream = Assembly.GetCallingAssembly()
                .GetManifestResourceStream(resourceName);
            return new StreamReader(stream).ReadToEnd();
        }
        
        /// <summary>
        /// This test requires that OpenTAP is installed along with the XSeries plugin
        /// </summary>
        [Test]
        public void CheckDependencies_MissingDep()
        {
            
            string xSeriesPath = "Packages/XSeries/package.xml";
            string inputFilename = "Packages/CheckDependencies_MissingDep/package.xml";
            var xml= GetEmbeddedFile(inputFilename);
            var xSeriesXml = GetEmbeddedFile(xSeriesPath);
            FileSystemHelper.EnsureDirectoryOf(xSeriesPath);
            FileSystemHelper.EnsureDirectoryOf(inputFilename);
            File.WriteAllText(xSeriesPath, xSeriesXml);
            File.WriteAllText(inputFilename, xml);
            try
            {

                //PackageDependencyExt.CheckDependencies(inputFilename);
                var xseries = PackageDef.FromXml(xSeriesPath);
                PackageDef.ValidateXml(inputFilename);
                var missing = PackageDef.FromXml(inputFilename);
                var tree = DependencyAnalyzer.BuildAnalyzerContext(new List<PackageDef> {xseries, missing});
                Assert.IsTrue(tree.GetIssues(missing).Any(issue => issue.IssueType == DependencyIssueType.Missing));
            }
            finally
            {
                FileSystemHelper.DeleteDirectory(Path.GetDirectoryName(xSeriesPath));
                FileSystemHelper.DeleteDirectory(Path.GetDirectoryName(inputFilename));
            }
            //Assert.Fail("CheckDependencies should have thrown an exception");
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
<Package Name=""BasicSteps"" xmlns=""http://keysight.com/schemas/TAP/Package"" OS=""Linux,Windows,MacOS"">
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
                string installDir = Path.GetDirectoryName(typeof(Package.PackageDef).Assembly.Location);
                var pkg = PackageDefExt.FromInputXml(pkgName, installDir);
                CollectionAssert.IsNotEmpty(pkg.Dependencies,"Package has no dependencies.");
                //Assert.AreEqual("OpenTAP", pkg.Dependencies.First().Name);
                using(var file = CreateStream("BasicSteps.TapPackage"))
                    pkg.CreatePackage(file);

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
            var p = Process.Start("tap", "package create \"Packages/Package/package.xml\" -v");
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

        [Test]
        [Platform(Exclude="Unix,Linux,MacOsX")]
        public void TestSetAssemblyInfo()
        {
            File.Copy("Packages/SetAsmInfoTest.dll", "SetAsmInfoTest.dll", true);
            var fileName = $"SetAsmInfoTest.dll";

            // Check if version is null
            Assert.IsNull(ReadAssemblyVersionStep.GetVersion(fileName), "Assembly version is not null as expected.");

            // Check if version has been new version inserted
            SetAsmInfo.SetAsmInfo.SetInfo(fileName, Version.Parse("1.2.3.4"), Version.Parse("2.3.4.5"), SemanticVersion.Parse("2.3.4-test"));
            Assert.IsTrue(ReadAssemblyVersionStep.GetVersion(fileName)?.Equals(SemanticVersion.Parse("2.3.4-test")), "Assembly version was not inserted correctly.");

            // Check if version has been updated.
            SetAsmInfo.SetAsmInfo.SetInfo(fileName, Version.Parse("1.2.3.4"), Version.Parse("2.3.4.5"), SemanticVersion.Parse("3.4.5-test"));
            Assert.IsTrue(ReadAssemblyVersionStep.GetVersion(fileName)?.Equals(SemanticVersion.Parse("3.4.5-test")), "Assembly version was not updated correctly.");
        }

        [Test]
        public void PackageValidationTest()
        {
            var testDir = "metadatatest";
            
            void ValidateXml(string pkg)
            {
                var xmlPath = Path.Combine(testDir, "package.xml");
                if (File.Exists(xmlPath))
                    File.Delete(xmlPath);
                
                File.WriteAllText(xmlPath, pkg);

                PackageDef.ValidateXml(xmlPath);
            }
            
            try
            {
                Directory.CreateDirectory(testDir);
                
                // Package with metadata
                var package = @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Package Name=""testing"" Version=""1.0.0"" Architecture=""AnyCPU"" OS=""Windows"" xmlns=""http://opentap.io/schemas/package"">
                    <Something>testing</Something>
                    <Owner>testing</Owner>
                    </Package>";
                Assert.DoesNotThrow(() => ValidateXml(package), "Package with metadata");
                
                
                // Package with no name
                package = @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Package Version=""1.0.0"" Architecture=""AnyCPU"" OS=""Windows"" xmlns=""http://opentap.io/schemas/package"" />";
                Assert.Throws<InvalidDataException>(() => ValidateXml(package), "Package with no Name");
                
                // Package with empty name
                package = @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Package Name="""" Version=""1.0.0"" Architecture=""AnyCPU"" OS=""Windows"" xmlns=""http://opentap.io/schemas/package"" />";
                Assert.Throws<InvalidDataException>(() => ValidateXml(package), "Package with no Name");
                
                
                // Package with no version. Should not fail, we automatically set it in the serializer <see cref="PackageDefinitionSerializerPlugin"/>
                package = @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Package Name=""testing"" Architecture=""AnyCPU"" OS=""Windows"" xmlns=""http://opentap.io/schemas/package"" />";
                Assert.DoesNotThrow(() => ValidateXml(package), "Package with no version");
                
                // Package with empty version. Should not fail, we automatically set it in the serializer <see cref="PackageDefinitionSerializerPlugin"/>
                package = @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Package Name=""testing"" Version="""" Architecture=""AnyCPU"" OS=""Windows"" xmlns=""http://opentap.io/schemas/package"" />";
                Assert.DoesNotThrow(() => ValidateXml(package), "Package with no version");
                
                package = @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Package Name=""testing"" Version=""$(GitVersion)"" Architecture=""AnyCPU"" OS=""Windows"" xmlns=""http://opentap.io/schemas/package"" />";
                Assert.DoesNotThrow(() => ValidateXml(package), "Package with macro version");

                
                // Package with no OS. Should not fail, we set the Windows as default in the constructor.
                package = @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Package Name=""testing"" Version=""1.0.0"" Architecture=""AnyCPU"" xmlns=""http://opentap.io/schemas/package"" />";
                Assert.DoesNotThrow(() => ValidateXml(package), "Package with no OS");
                
                // Package with empty OS
                package = @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Package Name=""testing"" Version=""1.0.0"" Architecture=""AnyCPU"" OS="""" xmlns=""http://opentap.io/schemas/package"" />";
                Assert.Throws<InvalidDataException>(() => ValidateXml(package), "Package with empty OS");
                
                
                // Package with no Architecture. Should not fail, we set the AnyCPU as default in the constructor.
                package = @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Package Name=""testing"" Version=""1.0.0"" OS=""Windows"" xmlns=""http://opentap.io/schemas/package"" />";
                Assert.DoesNotThrow(() => ValidateXml(package), "Package with no Architecture");
                
                // Package with empty Architecture
                package = @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <Package Name=""testing"" Version=""1.0.0"" OS=""Windows"" Architecture="""" xmlns=""http://opentap.io/schemas/package"" />";
                Assert.Throws<ArgumentException>(() => ValidateXml(package), "Package with empty Architecture"); // Throws an ArgumentException from the serializer because enum must have a value
            }
            finally
            {
                try
                {
                    Directory.Delete(testDir, true);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
