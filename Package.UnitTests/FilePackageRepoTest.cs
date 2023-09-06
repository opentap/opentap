using NUnit.Framework;
namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class FilePackageRepoTest
    {
        [TestCase("C:\\")]
        [TestCase("C:")]
        [TestCase("D:")]
        [TestCase("D:/")]
        [TestCase("/")]
        public void TestRootFileSystemPath(string okPath)
        {
            // A bug previously caused this to crash.
            Assert.DoesNotThrow(() => new FilePackageRepository(okPath));
            
        }
    }
}
