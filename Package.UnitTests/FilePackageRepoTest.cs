using System.IO;
using NUnit.Framework;
namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class FilePackageRepoTest
    {
        [TestCase("C:\\", "C:\\")]
        [TestCase("/", null)] // this might be C or D or ... depending on the current drive.
        [TestCase("C:", "C:\\")]
        [TestCase("D:", "D:\\")]
        [TestCase("D:\\", "D:\\")]
        [TestCase("/", null)]
        public void TestRootFileSystemPath(string okPath, string expectedPath)
        {
            
            FilePackageRepository repo = null;
            // A bug previously caused this to crash.
            Assert.DoesNotThrow(() => repo = new FilePackageRepository(okPath));
            if(expectedPath != null)
                Assert.AreEqual(repo.AbsolutePath, expectedPath);
            
        }
    }
}
