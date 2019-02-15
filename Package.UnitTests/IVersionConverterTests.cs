//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using NUnit.Framework;
using OpenTap.Package;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class IVersionConverterTests
    {
        [Test]
        public void MajorMinorBuildRevisionVersionTest()
        {
            var Converter = new MajorMinorBuildRevisionVersionConverter();

            var semver = Converter.Convert("1.2.3.4");
            Assert.AreEqual(1, semver.Major);
            Assert.AreEqual(2, semver.Minor);
            Assert.AreEqual(4, semver.Patch);
            Assert.AreEqual(null, semver.PreRelease);
            Assert.AreEqual("3", semver.BuildMetadata);

            Assert.Throws(typeof(ArgumentException), () => Converter.Convert("1.2.3"));
            Assert.Throws(typeof(ArgumentException), () => Converter.Convert("hej"));
            Assert.Throws(typeof(ArgumentException), () => Converter.Convert("1.2.3.4.5"));
        }

        [Test]
        public void FourValueVersionTest()
        {
            var Converter = new FourValueVersionConverter();

            var semver = Converter.Convert("1.2.3.4");
            Assert.AreEqual(1, semver.Major);
            Assert.AreEqual(2, semver.Minor);
            Assert.AreEqual(3, semver.Patch);
            Assert.AreEqual(null, semver.PreRelease);
            Assert.AreEqual("4", semver.BuildMetadata);

            Assert.Throws(typeof(ArgumentException), () => Converter.Convert("1.2.3"));
            Assert.Throws(typeof(ArgumentException), () => Converter.Convert("hej"));
            Assert.Throws(typeof(ArgumentException), () => Converter.Convert("1.2.3.4.5"));
        }

        [Test]
        public void Tap8CompatibilityVersionTest()
        {
            var Converter = new Tap8CompatibilityVersionConverter();

            var semver = Converter.Convert("1.2.3.4");
            Assert.AreEqual(1, semver.Major);
            Assert.AreEqual(2, semver.Minor);
            Assert.AreEqual(3, semver.Patch);
            Assert.AreEqual(null, semver.PreRelease);
            Assert.AreEqual("4", semver.BuildMetadata);

            semver = Converter.Convert("1.2 Development");
            Assert.AreEqual(1, semver.Major);
            Assert.AreEqual(2, semver.Minor);
            Assert.AreEqual(int.MaxValue, semver.Patch);
            Assert.AreEqual(null, semver.PreRelease);
            Assert.AreEqual(null, semver.BuildMetadata);

            semver = Converter.Convert("1");
            Assert.AreEqual(1, semver.Major);
            Assert.AreEqual(0, semver.Minor);
            Assert.AreEqual(0, semver.Patch);
            Assert.AreEqual(null, semver.PreRelease);
            Assert.AreEqual(null, semver.BuildMetadata);

            semver = Converter.Convert("1.2.3.dijf23");
            Assert.AreEqual(1, semver.Major);
            Assert.AreEqual(2, semver.Minor);
            Assert.AreEqual(3, semver.Patch);
            Assert.AreEqual(null, semver.PreRelease);
            Assert.AreEqual("dijf23", semver.BuildMetadata);
        }
    }
}
