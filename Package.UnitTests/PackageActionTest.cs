using System;
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
        
        internal class MyTestInterface : IUserInputInterface
        {
            void IUserInputInterface.RequestUserInput(object dataObject, TimeSpan timeout, bool modal)
            {
            
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void UserInterfaceResetTest(bool interactive)
        {
            var ui = new MyTestInterface();
            var packageName = "MyPlugin1";
            
            using (OpenTap.Session.Create())
            {
                UserInput.SetInterface(ui);
                Assert.IsTrue(ReferenceEquals(ui, UserInput.GetInterface()));

                var act = new PackageInstallAction()
                {
                    Packages = new[] {packageName},
                    Repository = new[] {new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName},
                    Force = true,
                    OS = "Windows",
                    NonInteractive = interactive,
                };

                act.Execute(CancellationToken.None);
                Assert.IsTrue(ReferenceEquals(ui, UserInput.GetInterface()));

                var act2 = new PackageUninstallAction()
                {
                    Packages = new[] {packageName},
                    Force = true,
                    NonInteractive = interactive
                };

                act2.Execute(CancellationToken.None);
                
                Assert.IsTrue(ReferenceEquals(ui, UserInput.GetInterface()));
            }
        }
    }
}
