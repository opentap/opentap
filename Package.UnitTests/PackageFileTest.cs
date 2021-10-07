//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using System.IO;
using System.Text;
using OpenTap.Package;
using static OpenTap.Package.FileHashPackageAction;
using System;
using System.Security.Cryptography;

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
                package = PackageDef.FromXml(str);
            }
            Assert.AreEqual(package.Dependencies.Count, 1);
            Assert.AreEqual("Tap", package.Dependencies[0].Name);
            Assert.AreEqual(package.Files.Count, 2);
            Assert.IsNotNull(package.Description);
        }

        [Test]
        public void CompareFileHashes()
        {
            string testString = "This is a test string";
            using (MemoryStream memory = new MemoryStream(Encoding.UTF8.GetBytes(testString)))
            {
                var bytes = SHA1.Create().ComputeHash(memory);
                var oldHash = new Hash(bytes); // Old hash format - base64 encoded
                oldHash.Value = Convert.ToBase64String(bytes);
                var newHash = new Hash(bytes); // New hash format - hex encoded string
                Assert.AreEqual(oldHash, newHash);
                Assert.AreEqual(newHash, oldHash);
            }
        }
    }
}
