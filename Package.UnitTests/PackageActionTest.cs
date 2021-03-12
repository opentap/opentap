using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests
{
    public class PackageActionTest
    {
        [Test]
        public void PackageInstallDelegateTest()
        {
            var packageName = "MyPlugin1";
            var progress = 0;

            var act = new PackageInstallAction()
            {
                Packages = new[] {packageName},
                Repository = new[] {new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName},
                Force = true,
                OS = "Windows"
            };

            act.ProgressUpdate += (percent, message) =>
            {
                Assert.GreaterOrEqual(percent, progress);
                Assert.GreaterOrEqual(percent, 0);
                Assert.LessOrEqual(percent, 100);
                progress = percent;
            };

            try
            {
                Assert.AreEqual(0, act.Execute(CancellationToken.None));
                Assert.AreEqual(100, progress);
            }
            finally
            {
                new PackageUninstallAction() {Packages = new[] {packageName}}.Execute(CancellationToken.None);
            }
        }

        [Test]
        public void DownloadSinglePackage()
        {
            var packageName = "MyPlugin1";
            var progress = 0;

            var act = new PackageDownloadAction()
            {
                Packages = new[] {packageName},
                Repository = new[] {new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName},
                ForceInstall = true,
                OS = "Windows"
            };

            act.ProgressUpdate += (percent, message) =>
            {
                Assert.GreaterOrEqual(percent, progress);
                Assert.GreaterOrEqual(percent, 0);
                Assert.LessOrEqual(percent, 100);
                progress = percent;
            };

            Assert.AreEqual(0, act.Execute(CancellationToken.None));
            Assert.AreEqual(100, progress);

        }

        [Test]
        public void DownloadMultiplePackages()
        {
            var progress = 0;

            var act = new PackageDownloadAction()
            {
                Packages = new[] {"MyPlugin1", "MyPlugin2"},
                Repository = new[] {new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName},
                ForceInstall = true,
                OS = "Windows"
            };

            act.ProgressUpdate += (percent, message) =>
            {
                Assert.GreaterOrEqual(percent, progress);
                Assert.GreaterOrEqual(percent, 0);
                Assert.LessOrEqual(percent, 100);
                progress = percent;
            };

            Assert.AreEqual(0, act.Execute(CancellationToken.None));
            Assert.AreEqual(100, progress);
        }

        [Test]
        public void DownloadMultipleNamedPackages()
        {
            var progress = 0;
            var outFile = Path.GetTempFileName();

            var act = new PackageDownloadAction()
            {
                Packages = new[] {"MyPlugin1", "MyPlugin2"},
                Repository = new[] {new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName},
                OutputPath = outFile,
                ForceInstall = true,
                OS = "Windows"
            };
            List<(int, string)> progressUpdates = new List<(int, string)>();

            act.ProgressUpdate += (percent, message) =>
            {
                Assert.GreaterOrEqual(percent, progress);
                Assert.GreaterOrEqual(percent, 0);
                Assert.LessOrEqual(percent, 100);
                progress = percent;
                progressUpdates.Add((progress, message));
            };

            
            try
            {
                Assert.AreEqual(0, act.Execute(CancellationToken.None));
                Assert.AreEqual(100, progressUpdates.Last().Item1);
                for (int i = 1; i < progressUpdates.Count; i++)
                {
                    Assert.LessOrEqual(progressUpdates[i - 1].Item1, progressUpdates[i].Item1);
                }
            }
            finally
            {
                if (File.Exists(outFile))
                    File.Delete(outFile);
            }

        }
    }
}
