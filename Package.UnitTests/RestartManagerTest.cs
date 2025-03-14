using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests;

[TestFixture]
public class TestRestartManager
{
    [Test]
    public void TestFilesInUseDetected()
    {
        // RestartManager only exists on windows
        if (OperatingSystem.Current != OperatingSystem.Windows) return;
        var file = Path.GetTempFileName();
        try
        {
            FileStream f;
            { // Nothing should be locking the file now
                var lst = RestartManager.GetProcessesUsingFiles([file]);
                Assert.That(lst.Count, Is.Zero);
            }
            { // Now the file should be locked by this test
                f = File.OpenWrite(file);
                var lst = RestartManager.GetProcessesUsingFiles([file]);
                Assert.That(lst.Count, Is.EqualTo(1));
                Assert.That(lst[0].Process.Id, Is.EqualTo(Process.GetCurrentProcess().Id));
            }
            { // Now the file should no longer be locked
                f.Close();
                var lst = RestartManager.GetProcessesUsingFiles([file]);
                Assert.That(lst.Count, Is.Zero);
            }
        }
        finally
        {
            File.Delete(file); 
        }
    }
}