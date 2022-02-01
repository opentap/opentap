using NUnit.Framework;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class PackageDefSerializationTest
    {
        [Test]
        public void PackageFileLicenseRequiredTest()
        {
            var pf = new PackageDef {LicenseRequired = ""};
            var str = new TapSerializer().SerializeToString(pf);
            Assert.IsFalse(str.Contains("LicenseRequired=\"\""));
        }
    }
}