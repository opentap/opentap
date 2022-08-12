using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using OpenTap.Diagnostic;

namespace OpenTap.Package.UnitTests
{
    [TestFixture]
    public class PackageDefTest2
    {
        [Test]
        public void TwoPackagesWithSameName()
        {
            string inputXml = @"<?xml version='1.0' encoding='utf-8' ?>
<Package Name='Test4' xmlns ='http://opentap.io/schemas/package'>
  <Files>
    <File Path='textDocument.txt'/>
  </Files>
</Package>
";

            var dir1 = "Test4_1";
            var dir2 = "Test4_2";
            string installDir = Path.GetDirectoryName(typeof(TestPlan).Assembly.Location);
            dir1 = Path.Combine(installDir, "Packages", dir1);
            dir2 = Path.Combine(installDir, "Packages", dir2);
            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);
            var p1 = Path.Combine(dir1, "package.xml");
            var p2 = Path.Combine(dir2, "package.xml");
            File.WriteAllText(p1, inputXml);
            File.WriteAllText(p2, inputXml);
            File.WriteAllText("textDocument.txt", "");
            try
            {
                var recordLog = new RecordLogListener();
                using (Session.Create(SessionOptions.RedirectLogging))
                {
                    Log.Context.AttachListener(recordLog);
                    var i = new Installation(installDir);
                    i.GetPackages(); // this will generate a warning due to duplicate package definitions.

                    i = new Installation(installDir);
                    i.GetPackages(); // this should not generate warnings as the warning should only be emitted once. 
                }

                var warnings = recordLog.LogEvents.Where(x => x.EventType == (int)LogEventType.Warning).ToArray();
                Assert.AreEqual(1, warnings.Count(x => x.Message.Contains("Duplicate Test4 packages detected")));

                for (int i = 0; i < 2; i++)
                {
                    // uninstall twice, once for each Test4 package xml.
                    new PackageUninstallAction { Packages = new[] { "Test4" } }.Execute(CancellationToken.None);
                }
                var i2 = new Installation(installDir);
                
                Assert.IsNull(i2.FindPackage("Test4")); 
                Assert.IsFalse(File.Exists(p1));
                Assert.IsFalse(File.Exists(p2));
                Assert.IsFalse(File.Exists("textDocument.txt"));
            }
            finally
            {
                File.Delete(p1);
                File.Delete(p2);
                File.Delete("textDocument.txt");
            }
        }

        class RecordLogListener : ILogListener
        {
            public readonly List<Event> LogEvents = new List<Event>();
            public void EventsLogged(IEnumerable<Event> Events)
            {
                LogEvents.AddRange(Events);
            }

            public void Flush()
            {
                
            }
        }

    }
}