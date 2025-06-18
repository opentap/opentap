using System.IO;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests;

[TestFixture]
public class UninstallContextTest
{
    [Test]
    public void TestDeleteFile()
    {
        var uninstallContext = UninstallContext.Create(Installation.Current);

        // initially verify that all the files exist.
        foreach (var file in Installation.Current.GetOpenTapPackage().Files)
        {
            Assert.That(File.Exists(file.RelativeDestinationPath));
        }
            
        foreach (var file in Installation.Current.GetOpenTapPackage().Files)
        {
            uninstallContext.Delete(file);
        }

        // After deleting the files verify that all files are deleted.
        bool verifyAllFilesDeleted = true;
        foreach (var file in Installation.Current.GetOpenTapPackage().Files)
        {
            if (File.Exists(file.RelativeDestinationPath))
            {
                verifyAllFilesDeleted = false;
            }
        }
        
        uninstallContext.UndoAllDeletions();

        Assert.That(verifyAllFilesDeleted, Is.True);
        
        // finally after undo, verify that all files are back.
        foreach (var file in Installation.Current.GetOpenTapPackage().Files)
        {
            Assert.That(File.Exists(file.RelativeDestinationPath));
        }
        Assert.That(File.Exists(".uninstall/.OpenTapIgnore"), Is.True);
        
    }
}