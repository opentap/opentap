using NUnit.Framework;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpenTap.Diagnostic;

namespace OpenTap.Image.Tests
{
    internal static class MockInstallHelper
    {
        private const string repoUrl = "mock://localhost";
        public static MockRepository MockRepo()
        {
            var repo = new MockRepository(repoUrl);
            PackageRepositoryHelpers.RegisterRepository(repo);
            return repo;
        }

        public static ImageSpecifier CreateSpecifier()
        {
            return new ImageSpecifier()
            {
                Repositories = new List<string>() { repoUrl }
            };
        }

        public static TempInstall CreateInstall()
        {
            return new TempInstall(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        }
    }
    /// <summary>
    /// This is a helper for creating mock OpenTAP installations that will clean up after itself
    /// </summary>
    internal class TempInstall : IDisposable
    {
        internal TempInstall(string directoryName)
        {
            _directoryName = directoryName;
        }

        private string _directoryName;
        public Installation Installation => new Installation(_directoryName);
        public string Directory => _directoryName;

        public void Dispose()
        {
            if (System.IO.Directory.Exists(_directoryName))
                System.IO.Directory.Delete(_directoryName, true);
        }
    }

    public class Deploy
    {
        [OneTimeSetUp]
        public void SetUpMockedRepository()
        {
            MockInstallHelper.MockRepo();
        }

        [Test]
        public void DeployClean()
        {
            using var tempInstall = MockInstallHelper.CreateInstall();

            var imageSpecifier = MockInstallHelper.CreateSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
            var identifier = imageSpecifier.Resolve(CancellationToken.None);

            identifier.Deploy(tempInstall.Directory, CancellationToken.None);
            var nonSystemWidePackages = tempInstall.Installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
            Assert.AreEqual(3, nonSystemWidePackages.Count);
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing" && s.Version.ToString().StartsWith("1.0.44")));
        }

        [Test]
        public void DeployNewVersion()
        {
            var tempInstall = MockInstallHelper.CreateInstall();

            var imageSpecifier = MockInstallHelper.CreateSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
            var identifier = imageSpecifier.Resolve(CancellationToken.None);

            identifier.Deploy(tempInstall.Directory, CancellationToken.None);
            Installation installation = tempInstall.Installation;
            var nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
            Assert.AreEqual(3, nonSystemWidePackages.Count);
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing"));

            imageSpecifier.Packages.Clear();
            imageSpecifier.Packages.Add(new PackageSpecifier("Keysight Floating Licensing", new VersionSpecifier(1, 4, 1, null, null, VersionMatchBehavior.Exact)));
            imageSpecifier.MergeAndDeploy(installation, CancellationToken.None);
            installation = tempInstall.Installation;
            nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
            Assert.AreEqual(3, nonSystemWidePackages.Count);
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing" && s.Version.ToString().StartsWith("1.4.1")));
        }

        [Test]
        public void DeployNewPackage()
        {
            using var tempInstall = MockInstallHelper.CreateInstall();

            var imageSpecifier = MockInstallHelper.CreateSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
            var identifier = imageSpecifier.Resolve(CancellationToken.None);

            identifier.Deploy(tempInstall.Directory, CancellationToken.None);
            Installation installation = tempInstall.Installation;
            var nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
            Assert.AreEqual(3, nonSystemWidePackages.Count);
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing"));

            imageSpecifier.Packages.Clear();
            imageSpecifier.Packages.Add(new PackageSpecifier("Python", new VersionSpecifier(2, 3, 0, null, null, VersionMatchBehavior.Exact)));
            imageSpecifier.MergeAndDeploy(installation, CancellationToken.None);
            installation = tempInstall.Installation;
            nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
            Assert.AreEqual(4, nonSystemWidePackages.Count);
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing" && s.Version.ToString().StartsWith("1.0.44")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Python" && s.Version.ToString().StartsWith("2.3.0")));
        }

        [Test]
        public void DeployNewAndUpgrade()
        {
            using var tempInstall = MockInstallHelper.CreateInstall();

            var imageSpecifier = MockInstallHelper.CreateSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("Python", new VersionSpecifier(2, 3, 0, null, null, VersionMatchBehavior.Exact)));
            var identifier = imageSpecifier.Resolve(CancellationToken.None);

            identifier.Deploy(tempInstall.Directory, CancellationToken.None);
            Installation installation = tempInstall.Installation;
            var nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
            Assert.AreEqual(2, nonSystemWidePackages.Count);
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.15.2")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Python" && s.Version.ToString().StartsWith("2.3.0")));

            imageSpecifier.Packages.Clear();
            imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
            imageSpecifier.MergeAndDeploy(installation, CancellationToken.None);
            installation = tempInstall.Installation;
            nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
            Assert.AreEqual(4, nonSystemWidePackages.Count);
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing" && s.Version.ToString().StartsWith("1.0.44")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Python" && s.Version.ToString().StartsWith("2.3.0")));
        }

        [Test]
        public void DeployNewAndDowngrade()
        {
            using var tempInstall = MockInstallHelper.CreateInstall();

            var imageSpecifier = MockInstallHelper.CreateSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("Python", new VersionSpecifier(2, 3, 0, null, null, VersionMatchBehavior.Exact)));
            var identifier = imageSpecifier.Resolve(CancellationToken.None);

            identifier.Deploy(tempInstall.Directory, CancellationToken.None);
            Installation installation = tempInstall.Installation;
            var nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
            Assert.AreEqual(2, nonSystemWidePackages.Count);
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.15.2")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Python" && s.Version.ToString().StartsWith("2.3.0")));

            imageSpecifier.Packages.Clear();
            imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 5, 0, null, null, VersionMatchBehavior.Exact)));
            imageSpecifier.MergeAndDeploy(installation, CancellationToken.None);
            installation = tempInstall.Installation;
            nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
            Assert.AreEqual(4, nonSystemWidePackages.Count);
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.5.0")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing" && s.Version.ToString().StartsWith("1.0.44")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Python" && s.Version.ToString().StartsWith("2.3.0")));
        }

        [Test]
        public void DeployOverwrite()
        {
            using var tempInstall = MockInstallHelper.CreateInstall();

            var imageSpecifier = MockInstallHelper.CreateSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
            var identifier = imageSpecifier.Resolve(CancellationToken.None);

            identifier.Deploy(tempInstall.Directory, CancellationToken.None);
            Installation installation = tempInstall.Installation;
            var nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
            Assert.AreEqual(3, nonSystemWidePackages.Count);
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing"));

            imageSpecifier.Packages.Clear();
            imageSpecifier.Packages.Add(new PackageSpecifier("Python", new VersionSpecifier(2, 3, 0, null, null, VersionMatchBehavior.Exact)));
            identifier = imageSpecifier.Resolve(CancellationToken.None);
            identifier.Deploy(tempInstall.Directory, CancellationToken.None); // Overwrite
            installation = tempInstall.Installation;
            nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
            Assert.AreEqual(2, nonSystemWidePackages.Count);
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.15.2")));
            Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Python" && s.Version.ToString().StartsWith("2.3.0")));
        }

        [Test]
        public void DeployOverwriteTransitiveDependencies()
        {
            using var tempInstall = MockInstallHelper.CreateInstall();

            var openTapSpec = new PackageSpecifier("OpenTAP", VersionSpecifier.Parse("9.17.0-rc.4"));

            { // Deploy once
                var imageSpecifier = MockInstallHelper.CreateSpecifier();
                imageSpecifier.Packages.Add(openTapSpec);
                var res = imageSpecifier.MergeAndDeploy(tempInstall.Installation, CancellationToken.None);
                Assert.AreEqual(1, res.GetPackages().Count);
                Assert.AreEqual(res.FindPackage("OpenTAP").Version.ToString(), openTapSpec.Version.ToString());
            }
            { // Deploy twice
                var imageSpecifier = MockInstallHelper.CreateSpecifier();
                imageSpecifier.Packages.Add(openTapSpec);
                imageSpecifier.Packages.Add(new PackageSpecifier("License Injector", VersionSpecifier.Parse("9.8.0-beta.5+6fce512f")));
                var res = imageSpecifier.MergeAndDeploy(tempInstall.Installation, CancellationToken.None);
                Assert.AreEqual(3, res.GetPackages().Count);
                Assert.AreEqual(res.FindPackage("Keg").Version.ToString(), "0.1.0-beta.17+cd0310b9");
            }
        }


        [Test]
        public void DeployWithOfflineRepoNoErrors()
        {
            using var tempInstall = MockInstallHelper.CreateInstall();
            using var s = Session.Create();
            var evt = new EventTraceListener();
            var logs = new List<Event>();
            evt.MessageLogged += events =>
            {
                logs.AddRange(events.Where(e => e.EventType == (int) LogEventType.Error));
            };
            Log.AddListener(evt);

            try
            {
                var imageSpecifier = MockInstallHelper.CreateSpecifier();
                imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
                imageSpecifier.Repositories.Add("http://some-non-existing-repo.opentap.io");
                var identifier = imageSpecifier.Resolve(CancellationToken.None);

                identifier.Deploy(tempInstall.Directory, CancellationToken.None);
                Installation installation = tempInstall.Installation;
                var nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
                Assert.AreEqual(3, nonSystemWidePackages.Count);
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing" && s.Version.ToString().StartsWith("1.0.44")));
            }
            catch
            {
                Assert.Fail();
            }

            CollectionAssert.IsEmpty(logs);
        }

        [Test]
        public void Cache()
        {
            PackageCacheHelper.ClearCache();
            var imageSpecifier = MockInstallHelper.CreateSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
            var identifier = imageSpecifier.Resolve(CancellationToken.None);
            Assert.IsFalse(identifier.Cached);
            identifier.Cache();
            Assert.IsTrue(identifier.Cached);
        }
    }
}
