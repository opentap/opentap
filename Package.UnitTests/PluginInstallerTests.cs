//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using System.IO;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class PluginInstallerTests
    {
        [Test]
        public void FilesInPackageTest()
        {
            var output = PluginInstaller.FilesInPackage("TapPackages/MyPlugin1.TapPackage");
            CollectionAssert.AllItemsAreNotNull(output);
            CollectionAssert.AllItemsAreUnique(output);
            CollectionAssert.Contains(output, PackageDef.PackageDefDirectory + "/MyPlugin1/package.xml");
            CollectionAssert.Contains(output, PackageDef.PackageDefDirectory + "/MyPlugin1/Tap.Plugins.MyPlugin1.dll");
        }

        [Test]
        public void UnpackageFileTest()
        {
            var output = PluginInstaller.FilesInPackage("TapPackages/MyPlugin1.TapPackage");
            using (Stream str = new MemoryStream())
            {
                PluginInstaller.UnpackageFile("TapPackages/MyPlugin1.TapPackage", output.First(), str);
                Assert.IsTrue(str.Position > 0);
            }
        }

        [Test]
        public void InstallUninstallTest()
        {
            var pkg = PluginInstaller.InstallPluginPackage(Directory.GetCurrentDirectory(), "TapPackages/MyPlugin1.TapPackage");
            Assert.IsTrue(pkg.Files.Select(f => f.RelativeDestinationPath).All(File.Exists), "Some files did not get installed.");
            PluginInstaller.Uninstall(pkg, Directory.GetCurrentDirectory());
            Assert.IsFalse(pkg.Files.Select(f => f.RelativeDestinationPath).Any(File.Exists), "Some files did not get uninstalled.");
        }

        [Test]
        public void InstallUninstallTestSystemWide()
        {
            var pkg = PluginInstaller.InstallPluginPackage(Directory.GetCurrentDirectory(), "TapPackages/MyPlugin3.TapPackage");  // MyPlugin3.TapPackage is marked as system-wide
            Assert.IsTrue(pkg.Files.Select(f => Path.Combine(PackageDef.SystemWideInstallationDirectory, f.RelativeDestinationPath)).All(File.Exists), "Some files did not get installed.");
            PluginInstaller.Uninstall(pkg, Directory.GetCurrentDirectory());
            Assert.IsFalse(pkg.Files.Select(f => Path.Combine(PackageDef.SystemWideInstallationDirectory, f.RelativeDestinationPath)).Any(File.Exists), "Some files did not get uninstalled.");
        }

        class MockInstallation
        {
            public static List<PackageDef> GetInstallation()
            {
                var opentap = new PackageDef
                {
                    Name = "OpenTAP",
                    RawVersion = "1.0.0"
                };
                
                var A = new PackageDef()
                {
                    Name = "A",
                    RawVersion = "1.0.0"
                };
                A.Dependencies.Add(new PackageDependency("OpenTAP", VersionSpecifier.Any));
                var file = new PackageFile();
                file.FileName = "Package/A/test";
                file.CustomData.Add(new FileHashPackageAction.Hash{Value = "TEST1234"});
                A.Files.Add(file);
                
                var file2 = new PackageFile();
                file2.FileName = "Dependencies/A/test";
                file2.CustomData.Add(new FileHashPackageAction.Hash{Value = "TEST1234"});
                A.Files.Add(file2);
                
                return new List<PackageDef> {opentap, A};
            }

            public static List<PackageDef> GetConflictingFiles()
            {
                var B = new PackageDef { Name = "B", RawVersion = "2.0.0"};
                var file = new PackageFile { FileName = "Package/A/test" };
                file.CustomData.Add(new FileHashPackageAction.Hash{Value = "TEST123"});
                B.Files.Add(file);
                return new List<PackageDef>{B};
            }
            
            public static List<PackageDef> GetConflictingFilesInDependencies()
            {
                var E = new PackageDef { Name = "E", RawVersion = "2.0.0"};
                var file = new PackageFile { FileName = "Dependencies/A/test" };
                file.CustomData.Add(new FileHashPackageAction.Hash{Value = "TEST123"});
                E.Files.Add(file);
                return new List<PackageDef>{E};
            }

            
            public static List<PackageDef> GetConflictingVersion()
            {
                var D = new PackageDef { Name = "D", RawVersion = "2.0.0"};
                D.Dependencies.Add(new PackageDependency("OpenTAP", VersionSpecifier.Parse("2.0.0")));
                var file = new PackageFile { FileName = "Package/D/test" };
                file.CustomData.Add(new FileHashPackageAction.Hash{Value = "TEST123"});
                D.Files.Add(file);
                return new List<PackageDef>{D};
            }

            
            public static List<PackageDef> TestConflictingCapitalization()
            {
                var B = new PackageDef { Name = "B", RawVersion = "2.0.0" };
                var file = new PackageFile { FileName = "package/a/test" };
                file.CustomData.Add(new FileHashPackageAction.Hash{Value = "TEST123"});
                B.Files.Add(file);
                return new List<PackageDef>{B};
            }
            
            public static List<PackageDef> TestNonConflicting()
            {
                var B = new PackageDef { Name = "B", RawVersion = "2.0.0" };
                var file = new PackageFile { FileName = "Package/A/test" };
                file.CustomData.Add(new FileHashPackageAction.Hash {Value = "TEST1234"}); // same hash
                B.Files.Add(file);
                var C = new PackageDef { Name = "C", RawVersion = "2.0.0" };
                var file2 = new PackageFile { FileName = "Package/B/test" };
                file2.CustomData.Add(new FileHashPackageAction.Hash {Value = "TEST1234"});
                B.Files.Add(file2);
                return new List<PackageDef>{B, C};
            }
        }
        
        [Test]
        public void TestOverwritePackageDetection()
        {

            var success = PackageInstallAction.InstallationQuestion.Success;
            var overwrite = PackageInstallAction.InstallationQuestion.OverwriteFile;
            // Conflict
            PackageInstallAction.InstallationQuestion conflict = PackageInstallAction.CheckForOverwrittenPackages(MockInstallation.GetInstallation(), MockInstallation.GetConflictingFiles(), false);
            Assert.AreNotEqual(success, conflict);
            
            // Conflict + --force
            var conflictForced = PackageInstallAction.CheckForOverwrittenPackages(MockInstallation.GetInstallation(), MockInstallation.GetConflictingFiles(), true);
            Assert.AreEqual(overwrite, conflictForced);
            
            // No conflict
            var noConflict = PackageInstallAction.CheckForOverwrittenPackages(MockInstallation.GetInstallation(), MockInstallation.TestNonConflicting(), false);
            Assert.AreEqual(success, noConflict);
            
            // Conflicting capitalization between files "package/a/test" vs "Package/A/test". If forced this will work differently based on platform.
            var conflictCapitalization = PackageInstallAction.CheckForOverwrittenPackages(MockInstallation.GetInstallation(), MockInstallation.TestConflictingCapitalization(), false);
            Assert.AreNotEqual(success, conflictCapitalization);
            
            // Conflict but the file is inside Dependencies, so its ok.
            var dependenciesConflict = PackageInstallAction.CheckForOverwrittenPackages(MockInstallation.GetInstallation(), MockInstallation.GetConflictingFilesInDependencies(), false);
            Assert.AreEqual(success, dependenciesConflict);
        }

        [Test]
        public void TestDependencyChecker()
        {
            var installation = MockInstallation.GetInstallation();
            var noConflictingVersions = MockInstallation.GetConflictingFiles();
            
            // GetConflictingFiles has conflicting files, but no conflicting versions, so this should return None.
            var issue = DependencyChecker.CheckDependencies(installation, noConflictingVersions );
            Assert.AreEqual(DependencyChecker.Issue.None, issue);
            
            // Conflicting has no conflicting files, but conflicting versions, so this should return BrokenPackages.
            var conflictingVersion = MockInstallation.GetConflictingVersion();
            issue = DependencyChecker.CheckDependencies(installation, conflictingVersion );
            Assert.AreEqual(DependencyChecker.Issue.BrokenPackages, issue);
        }
    }
}
