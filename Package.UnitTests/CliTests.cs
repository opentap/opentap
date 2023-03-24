//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using OpenTap.Cli;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace OpenTap.Package.UnitTests
{
    static class DummyPackageGenerator
    {
        public static string GeneratePackage(PackageDef definition)
        {
            foreach (var packageFile in definition.Files)
            {
                File.CreateText(packageFile.FileName).Close();
            }
            string defFileName = "generated_package.xml";
            using (var stream = File.Create(defFileName))
                definition.SaveTo(stream);

            var proc = OpenTap.Engine.UnitTests.TapProcessContainer.StartFromArgs("package create " + defFileName, TimeSpan.FromMinutes(5)); 
            proc.WaitForEnd();
            string output = proc.ConsoleOutput;
            string outputFile = definition.Name + "." + definition.Version + ".TapPackage";
            if (File.Exists(outputFile))
                return outputFile;
            else
                throw new Exception(output);
        }

        public static void AddFile(this PackageDef def, string filename)
        {
            def.Files.Add(new PackageFile { RelativeDestinationPath = filename, Plugins = new List<PluginFile>() });
        }

        internal static void InstallDummyPackage(string packageName = "Dummy", string version = "1.0", string contentFileName = "Dummy")
        {
            string installDir = Path.GetDirectoryName(typeof(Package.PackageDef).Assembly.Location);
            string packageXmlPath = Path.Combine(installDir, "Packages", packageName, "package.xml");
            Directory.CreateDirectory(Path.Combine(installDir, "Packages", packageName));
            File.WriteAllText(packageXmlPath, $@"<?xml version='1.0' encoding='utf-8' ?>
<Package Name='{packageName}' Version='{version}' xmlns ='http://opentap.io/schemas/package'>
  <Files>
    <File Path='Packages/{packageName}/{contentFileName}.txt'/>
  </Files>
</Package>");
            string contentFilePath = Path.Combine(installDir, "Packages", packageName, contentFileName);
            if (!File.Exists(contentFilePath))
                File.WriteAllText(contentFilePath, "hello");
        }

        internal static void UninstallDummyPackage(string packageName = "Dummy")
        {
            string installDir = Path.GetDirectoryName(typeof(Package.PackageDef).Assembly.Location);
            string packageDir = Path.Combine(installDir, "Packages", packageName);
            if (Directory.Exists(packageDir))
            {
                Directory.Delete(packageDir, true);
            }
        }
    }

    [TestFixture]
    public class CliTests
    {
        [TestCase(true, true, null)]                 // tap package download --out /tmp/Nested/TargetDir/ pkg -r /tmp
        [TestCase(true, false, null)]                // tap package download --out /tmp/Nested/TargetDir/ /tmp/pkg.TapPackage
        [TestCase(true, true, "pkg.TapPackage")]     // tap package download --out /tmp/Nested/TargetDir/pkg.TapPackage pkg -r /tmp
        [TestCase(true, false, "pkg.TapPackage")]     // tap package download --out /tmp/Nested/TargetDir/pkg.TapPackage /tmp/pkg.TapPackage
        [TestCase(false, false, null)]               // tap package download /tmp/pkg.TapPackage
        [TestCase(false, true, null)]                // tap package download pkg -r /tmp
        public void DownloadTest(bool useOut, bool useRepo, string outFileName)
        {
            var depDef = new PackageDef {Name = "Pkg1", Version = SemanticVersion.Parse("1.0"), OS = "Windows,Linux,MacOS"};
            depDef.AddFile("Dependency.txt");
            string dep0File = DummyPackageGenerator.GeneratePackage(depDef);

            string tempFn = Path.Combine(Path.GetTempPath(), Path.GetFileName(dep0File));

            if (File.Exists(tempFn))
                File.Delete(tempFn);
            File.Move(dep0File, tempFn);

            string outArg = Path.Combine(Path.GetTempPath(), "Nested", "TargetDir" + Path.DirectorySeparatorChar);
            if (outFileName != null)
                outArg = Path.Combine(outArg, outFileName);

            string targetFile;
            // Expected output path differs depending on whether or not we specify --out
            if (useOut && outFileName == null)
                targetFile = Path.GetFullPath(Path.Combine(outArg, PackageActionHelpers.GetQualifiedFileName(depDef)));
            else if (useOut && outFileName != null)
                targetFile = Path.GetFullPath(outArg);
            else
                targetFile = Path.Combine(Path.GetDirectoryName(typeof(Package.PackageDef).Assembly.Location),
                    PackageActionHelpers.GetQualifiedFileName(depDef)); 
            try
            {

                var args = $"download ";
                if (useOut)
                    args += $" --out {outArg} ";
                
                if (useRepo)
                    args += $" {depDef.Name} -r {Path.GetDirectoryName(tempFn)} ";
                else
                    args += $" {Path.GetFileName(tempFn)} ";
                args += " --no-cache";

                string output = RunPackageCli(args, out var exitCode, Path.GetDirectoryName(tempFn));
                Assert.AreEqual(0, exitCode, "Unexpected exit code");
                StringAssert.Contains($@"Downloaded '{depDef.Name}' to '{targetFile}'.", output);
                Assert.IsTrue(File.Exists(targetFile));
            }
            finally
            {
                File.Delete(dep0File);
                File.Delete(tempFn);
                if (useOut && outFileName == null)
                    Directory.Delete(outArg, true);
                else
                    File.Delete(targetFile);
            }
        }
        
        [Test]
        public void InstallLocalFile()
        {
            var package = new PackageDef();
            package.Name = "Dummy Something";
            package.Version = SemanticVersion.Parse("1.0.0");
            package.Description = "Cached version";

            var file = DummyPackageGenerator.GeneratePackage(package);
            if (File.Exists(Path.Combine(PackageCacheHelper.PackageCacheDirectory, file)))
                File.Delete(Path.Combine(PackageCacheHelper.PackageCacheDirectory, file));
            File.Move(file, Path.Combine(PackageCacheHelper.PackageCacheDirectory, file));

            package.Description = "Right version";
            var file2 = DummyPackageGenerator.GeneratePackage(package);

            var result = RunPackageCli("install -v -f \"" + Path.GetFullPath(file2) + "\"", out int exitcode);
            Assert.IsTrue(result.ToLower().Contains("installed"));
            Assert.IsTrue(result.ToLower().Contains("downloading file without searching"));

            var installedPackage = new Installation(Directory.GetCurrentDirectory()).GetPackages().FirstOrDefault(p => p.Name == package.Name);
            Assert.IsNotNull(installedPackage, "Package was not installed");
            Assert.AreEqual(package.Description, installedPackage.Description);
        }
        
        [Test]
        public void CheckInvalidPackageName()
        {
            // Check if name contains invalid character
            try
            {
                DummyPackageGenerator.GeneratePackage(new PackageDef(){Name = "Op/en:T\\AP"});
                Assert.Fail("Path contains invalid character");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Assert.True(e.Message.Contains("invalid file path characters"));
            }
        }

        [TestCase(".test", 1, typeof(XmlException))]
        [TestCase("t-est", 2, typeof(Exception))]
        [TestCase("te st", 3, typeof(XmlException))]
        [TestCase("tes.t", 4, typeof(Exception))]
        [TestCase("1test", 1, typeof(XmlException))]
        [TestCase("test?", 5, typeof(XmlException))]
        public void CheckInvalidMetadata(string invalidMetadataKey, int index, Type exceptionType)
        {
            // Check if metadata contains invalid characters
            var package = new PackageDef()
            {
                Name = "test",
                MetaData = { {invalidMetadataKey,""} },
                Version = SemanticVersion.Parse("1.0.0")
            };

            try
            {
                DummyPackageGenerator.GeneratePackage(package);
            }
            catch (Exception e)
            {
                Assert.IsAssignableFrom(exceptionType, e);
                if (e is XmlException)
                    return;

                Assert.True(e.Message.Contains($"Found invalid character"), "Package metadata keys contains invalid");
                Assert.True(e.Message.Contains($" in package metadata key '{invalidMetadataKey}' at position {index}."), "Package metadata keys contains invalid");
            }
        }

        [Test]
        public void ListTest()
        {
            int exitCode;
            string output = RunPackageCli("list", out exitCode);
            Assert.AreEqual(0, exitCode, $"Unexpected exit code.{Environment.NewLine}{output}");
            StringAssert.Contains("OpenTAP ", output);
            Debug.Write(output);
        }

        [Test]
        public void ShowTest()
        {
            int exitCode;
            string output = RunPackageCli("show OpenTAP", out exitCode);
            Assert.AreEqual(0, exitCode, $"Unexpected exit code.{Environment.NewLine}{output}");
            StringAssert.Contains("OpenTAP", output);
            Debug.Write(output);
        }

        [Ignore("This does not work on build runners")]
        [Test]
        public void TAPUninstallSelfTest()
        {
            int exitCode;
            string testDir = "../UninstallOpenTAP";
            try
            {
                Directory.CreateDirectory(testDir);
                string output = RunPackageCli($"install Packages/OpenTAP.TapPackage --target {testDir} -f", out exitCode);
                Debug.Write(output);
                Assert.AreEqual(0, exitCode, "Unexpected exit code.\r\n" + output);
                Debug.Write("--------------------------------------------");
                string workingDir = Path.Combine(Directory.GetCurrentDirectory(), testDir);
                output = RunPackageCliWrapped($"uninstall OpenTAP -v", out exitCode, workingDir, Path.Combine(workingDir, "tap.exe"));
                Debug.Write(output);
                if (File.Exists(Path.Combine(testDir, "OpenTap.dll")) || File.Exists(Path.Combine(testDir, "OpenTap.Package.dll")) || Directory.Exists(Path.Combine(testDir, "Packages")))
                    Console.WriteLine(output);
                Assert.False(File.Exists(Path.Combine(testDir, "OpenTap.dll")), "OpenTap.dll was not deleted!");
                Assert.False(File.Exists(Path.Combine(testDir, "OpenTap.Package.dll")), "OpenTap.Package.dll was not deleted!");
                Assert.False(Directory.Exists(Path.Combine(testDir, "Packages")), "Packages directory was not deleted!");
            }
            finally
            {
                try
                {
                    Directory.Delete(testDir, true);
                }
                catch { }
            }
        }

        [Test]
        public void InstallOutsideTapDir()
        {
            var depDef = new PackageDef();
            depDef.Name = "Pkg1";
            depDef.Version = SemanticVersion.Parse("1.0");
            depDef.AddFile("Dependency.txt");
            depDef.OS = OperatingSystem.Current.Name;
            string dep0File = DummyPackageGenerator.GeneratePackage(depDef);
            
            string tempFn = Path.Combine(Path.GetTempPath(), Path.GetFileName(dep0File));

            if (File.Exists(tempFn))
                File.Delete(tempFn);
            File.Move(dep0File, tempFn);

            try
            {
                if (File.Exists("Dependency.txt"))
                    File.Delete("Dependency.txt");
                int exitCode;
                string output = RunPackageCli("install " + Path.GetFileName(tempFn), out exitCode, Path.GetDirectoryName(tempFn));
                Assert.AreEqual(0, exitCode, "Unexpected exit code");
                StringAssert.Contains("Installed Pkg1", output);
                Assert.IsTrue(File.Exists("Dependency.txt"));
                PluginInstaller.Uninstall(depDef, Directory.GetCurrentDirectory());
            }
            finally
            {
                File.Delete(dep0File);
                File.Delete(tempFn);
            }
        }

        [Test]
        public void InstallOutsideTapDirInSubDir()
        {
            var depDef = new PackageDef();
            depDef.Name = "Pkg1";
            depDef.Version = SemanticVersion.Parse("1.0");
            depDef.OS = OperatingSystem.Current.Name;
            depDef.AddFile("Dependency.txt");
            string dep0File = DummyPackageGenerator.GeneratePackage(depDef);

            string tempFn = Path.Combine(Path.GetTempPath(), Path.GetFileName(dep0File));

            if (File.Exists(tempFn))
                File.Delete(tempFn);
            File.Move(dep0File, tempFn);

            string testDir = Path.Combine(Path.GetTempPath(), "lolDir");

            try
            {
                if (File.Exists("Dependency.txt"))
                    File.Delete("Dependency.txt");
                int exitCode;
                Directory.CreateDirectory(testDir);
                string output = RunPackageCli($"install --target {testDir} {Path.GetFileName(tempFn)}", out exitCode, Path.GetDirectoryName(tempFn));
                Assert.AreEqual(0, exitCode, "Unexpected exit code");
                StringAssert.Contains("Installed Pkg1", output);
                Assert.IsTrue(File.Exists(Path.Combine(testDir, "Dependency.txt")));
            }
            finally
            {
                File.Delete(dep0File);
                File.Delete(tempFn);
                Directory.Delete(testDir, true);
            }
        }

        [Test, Retry(3)]
        public void InstallFileWithDependenciesTest()
        {
            var depDef = new PackageDef();
            depDef.Name = "Dependency";
            depDef.Version = SemanticVersion.Parse("1.0");
            depDef.AddFile("Dependency.txt");
            string dep0File = DummyPackageGenerator.GeneratePackage(depDef);

            var dummyDef = new PackageDef();
            dummyDef.Name = "Dummy";
            dummyDef.Version = SemanticVersion.Parse("1.0");
            dummyDef.AddFile("Dummy.txt");
            dummyDef.Dependencies.Add(new PackageDependency( "Dependency", VersionSpecifier.Parse("1.0")));
            DummyPackageGenerator.InstallDummyPackage("Dependency"); // We need to have "Dependency" installed before we can create a package that depends on it.
            string dummyFile = DummyPackageGenerator.GeneratePackage(dummyDef);
            DummyPackageGenerator.UninstallDummyPackage("Dependency");

            try
            {
                if (File.Exists("Dependency.txt"))
                    File.Delete("Dependency.txt");
                if (File.Exists("Dummy.txt"))
                    File.Delete("Dummy.txt");
                int exitCode;
                string output = RunPackageCli("install Dummy -y", out exitCode);
                Assert.AreEqual(0, exitCode, "Unexpected exit code");
                StringAssert.Contains("Dummy", output);
                StringAssert.Contains("Dependency", output);
                Assert.IsTrue(File.Exists("Dummy.txt"));
                Assert.IsTrue(File.Exists("Dependency.txt"));
            }
            finally
            {
                PluginInstaller.Uninstall(dummyDef, Directory.GetCurrentDirectory());
                PluginInstaller.Uninstall(depDef, Directory.GetCurrentDirectory());
                File.Delete(dep0File);
                File.Delete(dummyFile);
            }
        }

        [Test]
        public void CyclicDependenciesTest()
        {
            DummyPackageGenerator.InstallDummyPackage(); // We need to have "Dummy" installed before we can create a package that depends on it.
            var depDef = new PackageDef();
            depDef.Name = "Dependency";
            depDef.OS="Windows,Linux,MacOS";
            depDef.Version = SemanticVersion.Parse("1.0");
            depDef.AddFile("Dependency.txt");
            depDef.Dependencies.Add(new PackageDependency("Dummy", VersionSpecifier.Parse("1.0")));
            string dep0File = DummyPackageGenerator.GeneratePackage(depDef);
            DummyPackageGenerator.UninstallDummyPackage();

            DummyPackageGenerator.InstallDummyPackage("Dependency");
            var dummyDef = new PackageDef();
            dummyDef.Name = "Dummy";
            dummyDef.OS="Windows,Linux";
            dummyDef.Version = SemanticVersion.Parse("1.0");
            dummyDef.AddFile("Dummy.txt");
            dummyDef.Dependencies.Add(new PackageDependency("Dependency", VersionSpecifier.Parse("1.0")));
            string dummyFile = DummyPackageGenerator.GeneratePackage(dummyDef);
            DummyPackageGenerator.UninstallDummyPackage("Dependency");

            try
            {
                if (File.Exists("Dependency.txt"))
                    File.Delete("Dependency.txt");
                if (File.Exists("Dummy.txt"))
                    File.Delete("Dummy.txt");
                var output = RunPackageCli("install Dummy -y", out var exitCode);
                Assert.AreEqual(0, exitCode, "Unexpected exit code.\r\n" + output);
                StringAssert.Contains("Dummy", output);
                StringAssert.Contains("Dependency", output);
                Assert.IsTrue(File.Exists("Dependency.txt"));
                Assert.IsTrue(File.Exists("Dummy.txt"));
            }
            finally
            {
                PluginInstaller.Uninstall(dummyDef, Directory.GetCurrentDirectory());
                PluginInstaller.Uninstall(depDef, Directory.GetCurrentDirectory());
                File.Delete(dep0File);
                File.Delete(dummyFile);
            }
        }
        
        [Test]
        public void UninstallTest()
        {
            try
            {
                int exitCode;
                string output = RunPackageCli("install NoDepsPlugin -f -y --os Windows", out exitCode);
                Assert.AreEqual(0, exitCode, "Unexpected installation exit code");
                StringAssert.Contains("NoDepsPlugin", output);
                Assert.IsTrue(File.Exists("Packages/NoDepsPlugin/Tap.Plugins.NoDepsPlugin.dll"));

                output = RunPackageCli("uninstall NoDepsPlugin", out exitCode);
                Assert.AreEqual(0, exitCode, "Unexpected uninstallation exit code");
                StringAssert.Contains("NoDepsPlugin", output);
                Assert.IsFalse(File.Exists("Packages/NoDepsPlugin/Tap.Plugins.NoDepsPlugin.dll"));
            }
            finally
            {
                if (Directory.Exists("Packages/NoDepsPlugin/Tap.Plugins.NoDepsPlugin.dll"))
                    Directory.Delete("Packages/NoDepsPlugin/Tap.Plugins.NoDepsPlugin.dll",true);
            }
        }

        [Test]
        public void InstallNonExistentFileTest()
        {
            int exitCode;
            string output = RunPackageCli("install NonExistent.TapPackage", out exitCode);
            Assert.AreEqual((int)ExitCodes.GeneralException, exitCode, "Unexpected exit code.\n" + output);
            StringAssert.Contains("Package 'NonExistent.TapPackage' not found.", output);
        }

        [Test]
        public void UninstallFirstTest()
        {
            var dep0Def = new PackageDef();
            dep0Def.Name = "UninstallPackage";
            dep0Def.Version = SemanticVersion.Parse("0.1");
            dep0Def.AddFile("UninstallText.txt");
            dep0Def.OS = OperatingSystem.Current.Name;
            string dep0File = DummyPackageGenerator.GeneratePackage(dep0Def);

            var dep1Def = new PackageDef();
            dep1Def.Name = "UninstallPackage";
            dep1Def.Version = SemanticVersion.Parse("0.2");
            dep1Def.AddFile("SubDir/UninstallText.txt");
            dep1Def.OS = OperatingSystem.Current.Name;
            Directory.CreateDirectory("SubDir");
            string dep1File = DummyPackageGenerator.GeneratePackage(dep1Def);

            int exitCode;
            string output = RunPackageCli("install " + dep0File, out exitCode);
            Assert.AreEqual(0, exitCode, "Unexpected exit code1: " + output);

            Assert.IsTrue(File.Exists("UninstallText.txt"), "File0 should exist");

            output = RunPackageCli("install " + dep1File, out exitCode);
            Assert.AreEqual(0, exitCode, "Unexpected exit code2: " + output);

            Assert.IsTrue(File.Exists("SubDir/UninstallText.txt"), "File1 should exist");
            Assert.IsFalse(File.Exists("UninstallText.txt"), "File0 should not exist");
        }

        [Test]
        public void UpgradeDependencyTest()
        {
            var dep0Def = new PackageDef();
            dep0Def.Name = "Dependency";
            dep0Def.Version = SemanticVersion.Parse("0.1");
            dep0Def.AddFile("Dependency0.txt");
            string dep0File = DummyPackageGenerator.GeneratePackage(dep0Def);

            var dep1Def = new PackageDef();
            dep1Def.Name = "Dependency";
            dep1Def.Version = SemanticVersion.Parse("1.0");
            dep1Def.AddFile("Dependency1.txt");
            string dep1File = DummyPackageGenerator.GeneratePackage(dep1Def);

            var dummyDef = new PackageDef();
            dummyDef.Name = "Dummy";
            dummyDef.Version = SemanticVersion.Parse("1.0");
            dummyDef.AddFile("Dummy.txt");
            dummyDef.Dependencies.Add(new PackageDependency("Dependency", VersionSpecifier.Parse("1.0")));
            DummyPackageGenerator.InstallDummyPackage("Dependency");
            string dummyFile = DummyPackageGenerator.GeneratePackage(dummyDef);
            DummyPackageGenerator.UninstallDummyPackage("Dependency");


            try
            {
                int exitCode;
                string output = RunPackageCli("install " + dep0File + " --force", out exitCode);
                Assert.AreEqual(0, exitCode, "Unexpected exit code");
                
                output = RunPackageCli("install Dummy -y -f", out exitCode);
                Assert.AreEqual(0, exitCode, "Unexpected exit code");
                //StringAssert.Contains("upgrading", output);
                Assert.IsTrue(File.Exists("Dependency1.txt"));
            }
            finally
            {
                PluginInstaller.Uninstall(dummyDef, Directory.GetCurrentDirectory());
                PluginInstaller.Uninstall(dep1Def, Directory.GetCurrentDirectory());
                File.Delete(dep0File);
                File.Delete(dep1File);
                File.Delete(dummyFile);
            }
        }

        [Test]
        public void InstallFromRepoTest()
        {
            int exitCode;
            // TODO: we need the --version part below because the release version of License Injector does not yet support OpenTAP 9.x, when it does, we can remove it again.
            string output = RunPackageCli("install \"Demonstration\" -r http://packages.opentap.io", out exitCode);
            Assert.AreEqual(0, exitCode, "Unexpected exit code: " + output);
            Assert.IsTrue(output.Contains("Installed Demonstration"));
            output = RunPackageCli("uninstall \"Demonstration\" -f", out exitCode);
            Assert.AreEqual(0, exitCode, "Unexpected exit code: " + output);
        }

        [Test]
        public void InstallFileWithMissingDependencyTest()
        {
            var def = new PackageDef();
            def.Name = "Dummy2";
            def.OS = "Windows,Linux,MacOS";
            def.Version = SemanticVersion.Parse("1.0");
            def.AddFile("Dummy.txt");
            def.Dependencies.Add(new PackageDependency("Missing", VersionSpecifier.Parse("1.0")));
            DummyPackageGenerator.InstallDummyPackage("Missing");
            string pkgFile = DummyPackageGenerator.GeneratePackage(def);
            DummyPackageGenerator.UninstallDummyPackage("Missing");

            try
            {
                int exitCode;
                string output = RunPackageCli("install Dummy2", out exitCode);
                Assert.AreNotEqual(0, exitCode, "Unexpected exit code");
                StringAssert.Contains("Could not resolve Dummy2", output);
            }
            finally
            {
                File.Delete(pkgFile);
            }
        }

        [Test]
        public void InstallPackagesFileTest()
        {
            string packageName = "REST-API", prerelease = "rc";
            var installation = new Installation(Directory.GetCurrentDirectory());
            
            int exitCode;

            string output = RunPackageCli("install -v -f \"" + packageName + "\" --version \"1.1.180-" + prerelease + "\" -y", out exitCode);
            var installedAfter = installation.GetPackages();

            if (installedAfter.Any(p => p.Name == packageName) == false)
                Console.WriteLine(output);
            
            Assert.IsTrue(installedAfter.Any(p => p.Name == packageName), "Package '" + packageName + "' was not installed.");
            Assert.IsTrue(installedAfter.Any(p => p.Name == packageName && p.Version.PreRelease == prerelease), "Package '" + packageName + "' was not installed with '--version'.");
            
            output = RunPackageCli("uninstall \"" + packageName + "\"", out exitCode);
            installedAfter = installation.GetPackages();
            Assert.IsFalse(installedAfter.Any(p => p.Name == packageName), "Package '" + packageName + "' was not uninstalled.");
        }

        [Test]
        public void NoDowngradeInstallTest()
        {
            var installation = new Installation(Directory.GetCurrentDirectory());

            var package = new PackageDef();
            package.Name = "NoDowngradeTest";
            package.Version = SemanticVersion.Parse("1.0.1");
            package.OS = OperatingSystem.Current.Name;
            var newPath = DummyPackageGenerator.GeneratePackage(package);

            package.Version = SemanticVersion.Parse("1.0.0");
            var oldPath = DummyPackageGenerator.GeneratePackage(package);

            // Install new version
            var output = RunPackageCli($"install {newPath} --force", out int exitCode);
            Assert.IsTrue(exitCode == 0 && output.ToLower().Contains("installed"), "NoDowngradeTest package was not installed.");
            var installedVersion = installation.GetPackages()?.FirstOrDefault(p => p.Name == "NoDowngradeTest")?.Version;
            Assert.IsTrue(installedVersion == SemanticVersion.Parse("1.0.1"), $"NoDowngradeTest installed the wrong version: '{installedVersion}'.");
            
            // Install older version with --no-downgrade option. This should not install the old version.
            output = RunPackageCli($"install --no-downgrade {oldPath}  --force", out exitCode);
            Assert.IsTrue(exitCode == 0 && output.ToLower().Contains("no package(s) were upgraded"), "NoDowngradeTest package was not installed.");
            installedVersion = installation.GetPackages()?.FirstOrDefault(p => p.Name == "NoDowngradeTest")?.Version;
            Assert.IsTrue(installedVersion == SemanticVersion.Parse("1.0.1"), $"NoDowngradeTest failed to skip the install: '{installedVersion}'.");
            
            // Install older version without --no-downgrade option. This should install the old version.
            output = RunPackageCli($"install {oldPath} --force", out exitCode);
            Assert.IsTrue(exitCode == 0 && output.ToLower().Contains("installed"), "NoDowngradeTest package was not installed.");
            installedVersion = installation.GetPackages()?.FirstOrDefault(p => p.Name == "NoDowngradeTest")?.Version;
            Assert.IsTrue(installedVersion == SemanticVersion.Parse("1.0.0"), $"NoDowngradeTest failed to install the old version: '{installedVersion}'.");
        }

        [Test]
        public void SkipInstallExactVersionAlreadyInstalledTest()
        {
            var package = new PackageDef();
            package.Name = "ExactVersionTest";
            package.Version = SemanticVersion.Parse("1.0.1");
            package.OS = "Windows,Linux,MacOS";
            var path = DummyPackageGenerator.GeneratePackage(package);

            // Install
            var output = RunPackageCli($"install {path}", out int exitCode);
            Assert.IsTrue(exitCode == 0 && output.ToLower().Contains("installed"), "ExactVersionTest package was not installed.");
            
            // Install the exact same version. This should skip.
            output = RunPackageCli($"install {package.Name} --version 1.0.1", out exitCode);
            Assert.IsTrue(exitCode == 0 && output.ToLower().Contains("already installed"), "ExactVersionTest package install was not skipped.");
            
            // Install the exact same version with --force. This should not skip.
            output = RunPackageCli($"install {package.Name} --version 1.0.1 -f", out exitCode);
            Assert.IsTrue(exitCode == 0 && output.ToLower().Contains("already installed") == false, "ExactVersionTest package install with -f was skipped.");
            
            // Install the exact same version from file. This should not skip.
            output = RunPackageCli($"install {path}", out exitCode);
            Assert.IsTrue(exitCode == 0 && output.ToLower().Contains("already installed") == false, "ExactVersionTest package install was skipped.");
        }


        private static string RunPackageCli(string args, out int exitCode, string workingDir = null)
        {
            return RunPackageCliWrapped(args, out exitCode, workingDir);
        }

        private static string RunPackageCliWrapped(string args, out int exitCode, string workingDir, string fileName = null)
        {
            if (fileName == null) fileName = Path.GetFullPath(Path.GetFileName(Path.Combine(Path.GetDirectoryName(typeof(Package.PackageDef).Assembly.Location), "tap")));
            var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDir,
                Arguments = "package " + args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            StringBuilder output = new StringBuilder();
            var lockObj = new object();

            p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (lockObj) output.AppendLine(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (lockObj) output.AppendLine(e.Data); };

            p.Start();

            p.BeginErrorReadLine();
            p.BeginOutputReadLine();

            p.WaitForExit(125000); // TapUninstallSelfTest can hang while waiting for files to be freed up. 125 seconds ought to be enough for everyone!

            if (!p.HasExited)
            {
                p.Kill();
                exitCode = -1;
            }
            else
            {
                exitCode = p.ExitCode;
                p.WaitForExit(); // The WaitForExit(int) overload called earlier does not wait for output processing to complete, this one does.
            }
            lock (lockObj)
                return output.ToString();
        }
    }
}
