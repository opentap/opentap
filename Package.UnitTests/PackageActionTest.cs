using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using OpenTap.Diagnostic;

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

        [Test]
        public void PackageInstallOtherOsTest()
        {
            var packageName = "MyPlugin1";

            var act = new PackageInstallAction
            {
                Packages = new[] {packageName},
                Repository = new[] {Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)},
                OS = "Windows"
            };

            try
            {
                var log = new LoggingTraceListener();
                using (Session.Create(SessionOptions.RedirectLogging))
                {
                    Log.AddListener(log);
                    Assert.AreEqual(0, act.Execute(CancellationToken.None));
                }

                Event? warningLog = log.Events.FirstOrNull(x =>
                    x.EventType == (int) LogEventType.Warning &&
                    x.Message.Contains("is incompatible with the host platform"));
                if (OperatingSystem.Current != OperatingSystem.Windows)
                    Assert.IsNotNull(warningLog);
                else
                    Assert.IsNull(warningLog);
            }
            finally
            {
                new PackageUninstallAction {Packages = new[] {packageName}}.Execute(CancellationToken.None);
            }
        }

        [Test]
        public void PackageInstallOtherOsDirectTest([Values(true, false)] bool specifyPlatform)
        {
            var packageName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "TapPackages", "MyPlugin1.TapPackage");

            var act = new PackageInstallAction
            {
                Packages = new[] {packageName},
                Repository = Array.Empty<string>()
            };
            if (specifyPlatform)
                act.OS = "Windows";
            try
            {
                var log = new LoggingTraceListener();
                int exitCode;
                using (Session.Create(SessionOptions.RedirectLogging))
                {
                    Log.AddListener(log);
                    exitCode = act.Execute(CancellationToken.None);
                }

                if (OperatingSystem.Current == OperatingSystem.Windows || specifyPlatform)
                {
                    Assert.AreEqual(0, exitCode);
                    var warningLog = log.Events.FirstOrNull(x =>
                        x.EventType == (int) LogEventType.Warning &&
                        x.Message.Contains("is incompatible with the host platform"));
                    if (specifyPlatform && OperatingSystem.Current != OperatingSystem.Windows)
                        Assert.IsNotNull(warningLog);
                    else
                        Assert.IsNull(warningLog);
                }
                else
                {
                    var errorlog = log.Events.FirstOrNull(x =>
                        x.EventType == (int) LogEventType.Error &&
                        x.Message.Contains("is incompatible with the host platform"));
                    Assert.AreNotEqual(0, exitCode);
                    Assert.IsNotNull(errorlog);
                }
            }
            finally
            {
                new PackageUninstallAction {Packages = new[] {packageName}}.Execute(CancellationToken.None);
            }
        }
    }
}

class LoggingTraceListener : ILogListener
{
    public readonly List<Event> Events = new List<Event>();

    public void EventsLogged(IEnumerable<Event> events)
    {
        Events.AddRange(events);
    }

    public void Flush()
    {
    }
}