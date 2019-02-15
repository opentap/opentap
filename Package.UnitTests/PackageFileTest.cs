//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System.IO;
using System.Text;
using OpenTap.Package;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class PackageFileTest
    {
        [Test]
        public void LoadPackageFile()
        {
            var packageString = Resources.GetEmbedded("ExamplePackage.xml");
            PackageDef package;
            using (var str = new MemoryStream(Encoding.UTF8.GetBytes(packageString)))
            {
                package = PackageDef.LoadFrom(str);
            }
            Assert.AreEqual(package.Dependencies.Count, 1);
            Assert.AreEqual("Tap", package.Dependencies[0].Name);
            Assert.AreEqual(package.Files.Count, 2);
            Assert.IsNotNull(package.Description);
        }
    }
}
