﻿using NUnit.Framework;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpenTap.Diagnostic;
using OpenTap.Authentication;

namespace OpenTap.Image.Tests
{
    /// <summary>
    /// This is a helper for creating temprary OpenTAP installations that will clean up after itself
    /// </summary>
    internal class TempInstall : IDisposable
    {
        internal TempInstall()
        {
            _directoryName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
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
        [Test]
        public void DeleteMe()
        {
            using var tempInstall = new TempInstall();
            Directory.Delete(PackageCacheHelper.PackageCacheDirectory, true);
            Directory.CreateDirectory(PackageCacheHelper.PackageCacheDirectory);
            AuthenticationSettings.Current.BaseAddress = "http://localhost";
            var imageString = File.ReadAllText("C:/Temp/Relativeimage.json");
            var imageSpecifier = ImageSpecifier.FromString(imageString);
            var imageSpecifier2 = new ImageSpecifier();
            imageSpecifier.Repositories.AddRange(imageSpecifier2.Repositories);
            var identifier = imageSpecifier.Resolve(CancellationToken.None);
            //identifier.Deploy(tempInstall.Directory, CancellationToken.None);
        }

        [Test]
        public void DeployClean()
        {
            using var tempInstall = new TempInstall();

            var imageSpecifier = MockRepository.CreateSpecifier();
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
            var tempInstall = new TempInstall();

            var imageSpecifier = MockRepository.CreateSpecifier();
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
            using var tempInstall = new TempInstall();

            var imageSpecifier = MockRepository.CreateSpecifier();
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
            using var tempInstall = new TempInstall();

            var imageSpecifier = MockRepository.CreateSpecifier();
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
            using var tempInstall = new TempInstall();

            var imageSpecifier = MockRepository.CreateSpecifier();
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
            using var tempInstall = new TempInstall();

            var imageSpecifier = MockRepository.CreateSpecifier();
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
        [Ignore("No access to internal repository")]
        public void DeployOverwriteTransitiveDependencies()
        {
            using var tempInstall = new TempInstall();
            string problemImage = @"
{
    ""Packages"": [
        {
            ""Name"": ""License Injector"",
            ""Version"": ""9.8.0-beta.5+6fce512f""
        }],
    ""Repositories"": [
        ""https://packages.opentap.keysight.com"",
        ""https://packages.opentap.io""
    ]
}";
            var openTapSpec = new PackageSpecifier("OpenTAP", VersionSpecifier.Parse("9.16.0"));

            { // Deploy once
                var imageSpecifier = new ImageSpecifier();
                imageSpecifier.Repositories.Add("https://packages.opentap.io");
                imageSpecifier.Packages.Add(openTapSpec);
                var res = imageSpecifier.MergeAndDeploy(tempInstall.Installation, CancellationToken.None);
                Assert.AreEqual(1, res.GetPackages().Where(s => s.Class != "system-wide").Count());
                Assert.IsTrue(res.FindPackage("OpenTAP").Version.ToString().StartsWith(openTapSpec.Version.ToString()));
            }
            { // Deploy twice
                PackageCacheHelper.ClearCache();
                var imageSpecifier = ImageSpecifier.FromString(problemImage);
                var res = imageSpecifier.MergeAndDeploy(tempInstall.Installation, CancellationToken.None);
                Assert.AreEqual(5, res.GetPackages().Where(s => s.Class != "system-wide").Count());
                Assert.AreEqual(res.FindPackage("Keg").Version.ToString(), "0.1.0-beta.17+cd0310b9");
            }
        }

        [Test]
        public void DeployDependencyMissing()
        {
            using var tempInstall = new TempInstall();

            var imageSpecifier = MockRepository.CreateSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("PackageWithMissingDependency", VersionSpecifier.Any));
            try
            {
                imageSpecifier.MergeAndDeploy(tempInstall.Installation, CancellationToken.None);
                Assert.Fail("This should fail to deploy.");
            }
            catch (ImageResolveException ex)
            {
                Assert.AreEqual(1, ex.InnerExceptions.Count);
                StringAssert.Contains("Package 'MissingPackage' could not be found in any repository.", ex.InnerException.Message);
            }
        }


        [Test]
        public void DeployWithOfflineRepoNoErrors()
        {
            using var tempInstall = new TempInstall();
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
                var imageSpecifier = MockRepository.CreateSpecifier();
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
            var imageSpecifier = MockRepository.CreateSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
            var identifier = imageSpecifier.Resolve(CancellationToken.None);
            Assert.IsFalse(identifier.Cached);
            identifier.Cache();
            Assert.IsTrue(identifier.Cached);
        }
        
        public class CustomPackageActionBomb : ICustomPackageAction
        {
            public CustomPackageActionBomb()
            {
                if (BombArmed)
                    throw new Exception("Boom");
            }
            public static bool BombArmed = false;
            public int Order()
            {
                return 0;
            }

            public PackageActionStage ActionStage => PackageActionStage.Install;
            public bool Execute(PackageDef package, CustomPackageActionArgs customActionArgs)
            {
                return true;
            }
        }

        [Test]
        public void TestThrowingCustomPackageAction()
        {
            try
            {
                CustomPackageActionBomb.BombArmed = true;
                
                using var tempInstall = new TempInstall();

                var imageSpecifier = MockRepository.CreateSpecifier();
                imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
                var identifier = imageSpecifier.Resolve(CancellationToken.None);

                Assert.DoesNotThrow(() => identifier.Deploy(tempInstall.Directory, CancellationToken.None));
            }
            finally
            {
                CustomPackageActionBomb.BombArmed = false;
            }
            
        }
    }
}
