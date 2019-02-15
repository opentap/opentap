//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Linq;
using OpenTap.Cli;
using System.Collections.Generic;
using OpenTap.Package;
using OpenTap.Plugins.BasicSteps;
using System.Reflection;
using System;
using System.Threading;

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
        public void GetPluginName_Test()
        {
            string inputFilename = "Packages/test2.package.xml";
            PackageDef pkg = PackageDefExt.FromXmlFile(inputFilename);
            
            Assert.AreEqual(pkg.Files?.FirstOrDefault()?.Plugins?.FirstOrDefault(p => p.Type == typeof(IfStep).FullName)?.BaseType, "Test Step");
            Assert.AreEqual(pkg.Files?.FirstOrDefault()?.Plugins?.FirstOrDefault(p => p.Type == typeof(NotifyingResultListener).FullName)?.BaseType, "Result Listener");
            Assert.AreEqual(pkg.Files?.FirstOrDefault()?.Plugins.FirstOrDefault(p => p.Type == typeof(RawSCPIInstrument).FullName)?.BaseType, "Instrument");
        }

        [Test]
        public void FromXmlFile_Test()
        {
            string inputFilename = "Packages/package.xml";

            PackageDef pkg = PackageDefExt.FromXmlFile(inputFilename);
            //Assert.AreEqual(inputFilename, pkg.FileName);

            CollectionAssert.IsNotEmpty(pkg.Files);
            Assert.AreEqual("OpenTap.dll", pkg.Files[0].FileName);
            Assert.AreEqual(false, pkg.Files[0].DoObfuscate);
            Assert.AreEqual("Version", pkg.Files[0].SetAssemblyInfo);

            CollectionAssert.IsNotEmpty(pkg.PackageActionExtensions);
            Assert.AreEqual("chmod", pkg.PackageActionExtensions[0].ExeFile);
            Assert.AreEqual("install", pkg.PackageActionExtensions[0].ActionName);
            Assert.AreEqual("+x tap", pkg.PackageActionExtensions[0].Arguments);

            CollectionAssert.IsNotEmpty(pkg.Dependencies);

        }

        [Test]
        public void FromXmlCheckSign_Test()
        {
            string inputFilename = "Packages/package_sign.xml";

            PackageDef pkg = PackageDefExt.FromXmlFile(inputFilename);
            //Assert.AreEqual(inputFilename, pkg.FileName);

            Assert.AreEqual(false, pkg.Files[0].DoObfuscate);
            Assert.AreEqual(true, pkg.Files[0].UseVersion);
            Assert.AreEqual("Test 123,./", pkg.Files[0].Sign);
        }

        [Test]
        public void CreatePackage_NoObfuscation()
        {
            string inputFilename = "Packages/package.xml";
            string outputFilename = "Test.TapPlugin";

            PackageDef pkg = PackageDefExt.FromXmlFile(inputFilename);
            try
            {
                pkg.CreatePackage(outputFilename, Directory.GetCurrentDirectory());
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
            File.Copy("Packages/package_NoBinFiles.xml", "package_NoBinFiles.xml", true);
            string inputFilename = "package_NoBinFiles.xml";
            string outputFilename = "Test.TapPackage";

            PackageDef pkg = PackageDefExt.FromXmlFile(inputFilename);
            try
            {
                pkg.CreatePackage(outputFilename, Directory.GetCurrentDirectory());
                Assert.IsTrue(File.Exists(outputFilename));
            }
            finally
            {
                if (File.Exists(outputFilename))
                    File.Delete(outputFilename);
            }
}

        [Test]
        public void CreatePackage_Obfuscation()
        {
            string inputFilename = "Packages/package.xml";
            string outputFilename = "Test.TapPackage";

            if (File.Exists(outputFilename)) File.Delete(outputFilename);

            PackageDef pkg = PackageDefExt.FromXmlFile(inputFilename);

            pkg.Files[0].DoObfuscate = true;

            try
            {
                pkg.CreatePackage(outputFilename, Directory.GetCurrentDirectory(), obfuscator: Obfuscator.Obfuscar);
                Assert.IsTrue(File.Exists(outputFilename));
            }
            finally
            {
                if (File.Exists(outputFilename))
                    File.Delete(outputFilename);
            }
        }

        [Test]
        public void CreatePackage_ObfuscationSubDir()
        {
            string inputFilename = "Packages/package.xml";
            string outputFilename = "Test.TapPlugin";

            if (File.Exists(outputFilename)) File.Delete(outputFilename);

            PackageDef pkg = PackageDefExt.FromXmlFile(inputFilename);

            var tmpDir1 = Path.Combine(Path.GetTempPath(), "tmp" + new System.Random().Next().ToString());
            var tmpPath2 = Path.Combine(Path.GetTempPath(), "tmpa" + new System.Random().Next().ToString(), "Keysight.Ccl.Licensing.Api.dll");

            Directory.CreateDirectory(tmpDir1);
            Directory.CreateDirectory(Path.GetDirectoryName(tmpPath2));

            pkg.Files[0].DoObfuscate = true;

            foreach (PackageFile item in pkg.Files)
            {
                var newFile = Path.Combine(tmpDir1, Path.GetFileName(item.FileName));

                File.Copy(item.FileName, newFile, true);
                item.FileName = newFile;
            }

            File.Copy("Keysight.Ccl.Licensing.Api.dll", tmpPath2, true);
            pkg.Files.Add(new PackageFile { FileName = tmpPath2, DoObfuscate = false, UseVersion = false, RelativeDestinationPath = tmpPath2 });

            try
            {
                pkg.CreatePackage(outputFilename, Directory.GetCurrentDirectory(), obfuscator: Obfuscator.Obfuscar);
                Assert.IsTrue(File.Exists(outputFilename));
            }
            finally
            {
                if (File.Exists(outputFilename))
                    File.Delete(outputFilename);
            }
        }
        
        public class DotfuscatorTests
        {
            [Test]
            public void CreatePackage_Dotfuscation()
            {
                string inputFilename = "Packages/package.xml";
                string outputFilename = "Test2.TapPackage";

                if (File.Exists(outputFilename)) File.Delete(outputFilename);

                PackageDef pkg = PackageDefExt.FromXmlFile(inputFilename);

                pkg.Files[0].DoObfuscate = true;

                try
                {
                    pkg.CreatePackage(outputFilename, Directory.GetCurrentDirectory(), obfuscator: Obfuscator.Dotfuscator);
                    Assert.IsTrue(File.Exists(outputFilename));
                }
                catch(Exception ex)
                {
                    if (ex.Data.Contains("StdErr") && ex.Data.Contains("StdOut"))
                        throw new Exception(ex.Message + Environment.NewLine +
                            "Errors: " + (ex.Data["StdErr"] ?? "") + Environment.NewLine +
                            "Output: " + (ex.Data["StdOut"] ?? "") + Environment.NewLine);
                    else
                        throw ex;
                }
                finally
                {
                    if (File.Exists(outputFilename))
                        File.Delete(outputFilename);
                }
            }

            [Test]
            public void CreatePackage_DotfuscationSubDir()
            {
                string inputFilename = "Packages/package.xml";
                string outputFilename = "Test3.TapPlugin";

                if (File.Exists(outputFilename)) File.Delete(outputFilename);

                PackageDef pkg = PackageDefExt.FromXmlFile(inputFilename);

                var tmpDir1 = Path.Combine(Path.GetTempPath(), "tmp" + new System.Random().Next().ToString());
                var tmpPath2 = Path.Combine(Path.GetTempPath(), "tmpa" + new System.Random().Next().ToString(), "Keysight.Ccl.Licensing.Api.dll");

                Directory.CreateDirectory(tmpDir1);
                Directory.CreateDirectory(Path.GetDirectoryName(tmpPath2));

                pkg.Files[0].DoObfuscate = true;

                foreach (PackageFile item in pkg.Files)
                {
                    var newFile = Path.Combine(tmpDir1, Path.GetFileName(item.FileName));

                    File.Copy(item.FileName, newFile, true);
                    item.FileName = newFile;
                }

                File.Copy("Keysight.Ccl.Licensing.Api.dll", tmpPath2, true);
                pkg.Files.Add(new PackageFile { FileName = tmpPath2, DoObfuscate = false, UseVersion = false, RelativeDestinationPath = tmpPath2 });

                try
                {
                    pkg.CreatePackage(outputFilename, Directory.GetCurrentDirectory(), obfuscator: Obfuscator.Dotfuscator);
                    Assert.IsTrue(File.Exists(outputFilename));
                }
                    catch(Exception ex)
                    {
                        if (ex.Data.Contains("StdErr") && ex.Data.Contains("StdOut"))
                            throw new Exception(ex.Message + Environment.NewLine +
                                "Errors: " + (ex.Data["StdErr"] ?? "") + Environment.NewLine +
                                "Output: " + (ex.Data["StdOut"] ?? "") + Environment.NewLine);
                        else
                            throw ex;
                }
                finally
                {
                    if (File.Exists(outputFilename))
                        File.Delete(outputFilename);
                }
            }
        }

        [Test]
        public void CreatePackage_Signing()
        {
            string inputFilename = "Packages/package.xml";
            string outputFilename = "Test.TapPackage";

            if (File.Exists(outputFilename)) File.Delete(outputFilename);

            PackageDef pkg = PackageDefExt.FromXmlFile(inputFilename);

            foreach (PackageFile item in pkg.Files)
            {
                item.Sign = "Keysight Technologies, Inc.";
            }


            try
            {
                pkg.CreatePackage(outputFilename, Directory.GetCurrentDirectory());
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
                    def.Date = "123";

                    def.AddFile(tmp);

                    var bs = PluginManager.GetSearchedAssemblies().First(asm => asm.Name == "OpenTap.Plugins.BasicSteps");
                    def.Files[0].DependentAssemblyNames.Add(bs);

                    def.findDependencies(new List<string>());

                    Assert.AreEqual(0, def.Dependencies.Count);
                    Assert.AreNotEqual(0, def.Files.Count);
                }

                {
                    PackageDef def = new PackageDef();

                    def.AddFile(tmp);

                    var bs = PluginManager.GetSearchedAssemblies().First(asm => asm.Name == "OpenTap.Plugins.BasicSteps");
                    def.Files[0].DependentAssemblyNames.Add(bs);

                    def.findDependencies(new List<string> { "OpenTap" });

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
            try
            {
                Directory.CreateDirectory("Packages");

                {
                    PackageDef def = new PackageDef() { Name = "pkg1", Version = SemanticVersion.Parse("1.2") };
                    def.AddFile("OpenTap.dll");

                    using (var f = File.OpenWrite("Packages/pkg1.package.xml")) def.SaveTo(f);
                }

                {
                    PackageDef def = new PackageDef() { Name = "gui", Version = SemanticVersion.Parse("1.2") };
                    def.AddFile("Keysight.OpenTap.Gui.Controls.dll");

                    using (var f = File.OpenWrite("Packages/gui.package.xml")) def.SaveTo(f);
                }

                {
                    PackageDef def = new PackageDef() { Name = "rv", Version = SemanticVersion.Parse("1.2") };
                    def.AddFile("Keysight.OpenTap.Gui.Controls.dll");

                    using (var f = File.OpenWrite("Packages/rv.package.xml")) def.SaveTo(f);
                }

                {
                    PackageDef def = new PackageDef();
                    def.Name = "test";
                    def.InfoLink = "a";
                    def.Date = "123";

                    def.Dependencies.Add(new PackageDependency( "rv", VersionSpecifier.Parse("1.2")));

                    def.AddFile("Keysight.OpenTap.ResultsViewer.exe");
                    def.Files[0].DependentAssemblyNames.AddRange(PluginManager.GetSearchedAssemblies().First(f => f.Name == "Keysight.OpenTap.ResultsViewer").References);

                    def.findDependencies(new List<string>());
                    
                    //Assert.AreEqual(3, def.Dependencies.Count);
                    Assert.IsTrue(def.Dependencies.Any(d => d.Name == "rv"));
                    Assert.IsTrue(def.Dependencies.Any(d => d.Name == "pkg1"));
                }

                {
                    PackageDef def = new PackageDef();
                    def.Name = "test";
                    def.InfoLink = "a";
                    def.Date = "123";

                    def.Dependencies.Add(new PackageDependency("gui", VersionSpecifier.Parse("1.2") ));

                    def.AddFile("Keysight.OpenTap.ResultsViewer.exe");
                    def.Files[0].DependentAssemblyNames.AddRange(PluginManager.GetSearchedAssemblies().First(f => f.Name == "Keysight.OpenTap.ResultsViewer").References);

                    def.findDependencies(new List<string>());

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
            string inputFilename = "Packages/package.xml";  // this package contains the old XML Schema URL
            string outputFileContent = "";

            PackageDef pkg = PackageDef.FromXmlFile(inputFilename);

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

            PackageDef pkg = PackageDefExt.FromXmlFile("Packages/package.xml");
            PackageDef pkg1 = PackageDefExt.FromXmlFile("Packages/test2.package.xml");

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
            string inputFilename = "Packages/test3.package.xml";
            string outputFileContent = "";

            PackageDef pkg = PackageDefExt.FromXmlFile(inputFilename);
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

        /// <summary>
        /// This test requires that TAP is installed along with the XSeries plugin
        /// </summary>
        [Test]
        public void CheckDependencies_MissingDep()
        {
            string inputFilename = "Packages/CheckDependencies_MissingDep.xml";

            try
            {
                //PackageDependencyExt.CheckDependencies(inputFilename);
                var xseries = PackageDef.FromXmlFile(Path.Combine(PackageDef.PackageDefDirectory, "XSeries.package.xml"));
                PackageDef.ValidateXml(inputFilename);
                var missing = PackageDef.FromXmlFile(inputFilename);
                var tree = DependencyAnalyzer.BuildAnalyzerContext(new List<PackageDef> { xseries, missing });
                Assert.IsTrue(tree.GetIssues(missing).Any(issue => issue.IssueType == DependencyIssueType.Missing));
            }
            catch (DependencyNotInstalledException ex)
            {
                StringAssert.Contains("Dependency2", ex.MissingDependency.Name);
                return;
            }
            //Assert.Fail("CheckDependencies should have thrown an exception");
        }

        /// <summary>
        /// This test requires that TAP is installed along with the XSeries and plugin
        /// </summary>
        [Test]
        [Ignore("??")]
        public void CheckDependencies_AllDepsInstalled()
        {

            var xseries = PackageDef.FromXmlFile(Path.Combine(PackageDef.PackageDefDirectory, "XSeries.package.xml"));
            var qcfemto = PackageDef.FromXmlFile(Path.Combine(PackageDef.PackageDefDirectory, "Test2.package.xml"));
            File.Copy(Path.Combine(PackageDef.PackageDefDirectory, "CheckDependencies_AllDepsInstalled.xml"), "CheckDependencies_AllDepsInstalled.xml", true);
            PackageDef.ValidateXml("CheckDependencies_AllDepsInstalled.xml");
            var alldeps = PackageDef.FromXmlFile("CheckDependencies_AllDepsInstalled.xml");
            var tree = DependencyAnalyzer.BuildAnalyzerContext(new List<PackageDef> { xseries, qcfemto, alldeps });
            Assert.AreEqual(tree.BrokenPackages.Count, 1);
            //PackageDependencyExt.CheckDependencies(inputFilename);
        }

        /// <summary>
        /// This test requires that TAP is installed along with the XSeries and Test plugin
        /// </summary>
        [Test, Ignore("?")]
        public void CheckDependencies_NonDllFile()
        {
            string inputFilename = "FromXmlFile_NonDllFile.xml";

            PackageDefExt.FromXmlFile(inputFilename);
            //PackageDependency.CheckDependencies(inputFilename);
        }

        [Test]
        [Ignore("Temporarily Disabled")]
        public void InstalledPackages_TwoPackages()
        {
            Assert.IsTrue(File.Exists(Path.Combine(PackageDef.PackageDefDirectory, "XSeries.package.xml")), "Necessary package file missing.");
            Assert.IsTrue(File.Exists(Path.Combine(PackageDef.PackageDefDirectory, "Test.package.xml")), "Necessary package file missing.");
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
            Assert.IsTrue(File.Exists(Path.Combine(PackageDef.PackageDefDirectory, "XSeries.package.xml")), "Necessary package file missing.");
            Assert.IsTrue(File.Exists(Path.Combine(PackageDef.PackageDefDirectory, "Test.package.xml")), "Necessary package file missing.");

            File.WriteAllText(Path.Combine(PackageDef.PackageDefDirectory, "Invalid.package.xml"),
@"<?xml version='1.0' encoding='utf-8' ?>
<Package Name='Invalid' xmlns='http://keysight.com/schemas/TAP/Package'>
  <Files>
    <File Path='Tap.Engine.dll' Obfuscate='false' UseVersion='true'></File>
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
<Package Name=""BasicSteps"" xmlns=""http://keysight.com/schemas/TAP/Package"">
 <Files>
  <File Path=""__BASICSTEPS_DLL__"" Obfuscate=""false"" UseVersion=""true""/>
 </Files>
</Package>
".Replace("__BASICSTEPS_DLL__", Path.GetFileName(typeof(DelayStep).Assembly.Location));
            string pkgName = "BasicSteps.xml";
            
            try
            {
                if (File.Exists(pkgName))
                    File.Delete(pkgName);
                File.WriteAllText(pkgName, pkgContent);
                var pkg = PackageDefExt.FromXmlFile(pkgName);
                CollectionAssert.IsNotEmpty(pkg.Dependencies,"Package has not dependencies.");
                Assert.AreEqual("OpenTAP",pkg.Dependencies.First().Name);
                pkg.CreatePackage("BasicSteps.TapPackage", Directory.GetCurrentDirectory(), skipObfuscation: true);

                PackageManagerSettings.Current.Repositories.Clear();
                var entry = new PackageManagerSettings.RepositorySettingEntry();
                entry.IsEnabled = true;
                entry.Url = Directory.GetCurrentDirectory();
                PackageManagerSettings.Current.Repositories.Add(entry);

                var packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(new PackageSpecifier());
                CollectionAssert.IsNotEmpty(packages, "Repository does not list any packages.");
                Assert.IsTrue(packages.Any(p => p.Name == "BasicSteps"));


                var depVersion = pkg.Dependencies.First().Version;
                var version = new SemanticVersion(depVersion.Major ?? 0, depVersion.Minor ?? 0, depVersion.Patch ?? 0, depVersion.PreRelease, "");
                packages = PackageRepositoryHelpers.GetPackagesFromAllRepos(new PackageSpecifier("BasicSteps"), new PackageIdentifier("OpenTAP", version, CpuArchitecture.Unknown, null));
                CollectionAssert.IsNotEmpty(packages, "Repository does not list any compatible \"BasicSteps\" package.");
                Assert.IsTrue(packages.First().Name == "BasicSteps");
            }
            finally
            {
                if (File.Exists("BasicSteps.TapPackage"))
                    File.Delete("BasicSteps.TapPackage");
                if (File.Exists(pkgName))
                    File.Delete(pkgName);
                PackageManagerSettings.Current.Invalidate();
            }
        }

        [Test]
        public void TestCliPackaging()
        {
            var p = Process.Start("tap.exe", "package create \"Packages/package.xml\" --obfuscator none -v");
            p.WaitForExit();
            Assert.AreEqual(0, p.ExitCode);
            var plugins = Directory.EnumerateFiles(".");

            Assert.IsTrue(plugins.Any(package =>
                Path.GetFileName(package).StartsWith("Test", System.StringComparison.InvariantCultureIgnoreCase) &&
                Path.GetExtension(package).EndsWith("TapPackage", System.StringComparison.InvariantCultureIgnoreCase)
            ), "Generated TAP package file not found");
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
