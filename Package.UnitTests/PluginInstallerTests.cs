//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
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
    }
}
