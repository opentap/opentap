using NUnit.Framework;
using OpenTap.Package;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap.Image.Tests
{
    public class Deploy
    {
        [Test]
        public void DeployClean()
        {
            ImageSpecifier imageSpecifier = new ImageSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
            imageSpecifier.Repositories.Add("http://packages.opentap.io");
            var identifier = imageSpecifier.Resolve(CancellationToken.None);
            string temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                identifier.Deploy(temp, CancellationToken.None);
                Installation installation = new Installation(temp);
                var nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
                Assert.AreEqual(3, nonSystemWidePackages.Count);
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing" && s.Version.ToString().StartsWith("1.0.44")));
            }
            finally
            {
                Directory.Delete(temp, true);
            }
        }

        [Test]
        public void DeployNewVersion()
        {
            ImageSpecifier imageSpecifier = new ImageSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
            imageSpecifier.Repositories.Add("http://packages.opentap.io");
            var identifier = imageSpecifier.Resolve(CancellationToken.None);
            string temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                identifier.Deploy(temp, CancellationToken.None);
                Installation installation = new Installation(temp);
                var nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
                Assert.AreEqual(3, nonSystemWidePackages.Count);
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing"));

                imageSpecifier.Packages.Clear();
                imageSpecifier.Packages.Add(new PackageSpecifier("Keysight Floating Licensing", new VersionSpecifier(1, 4, 1, null, null, VersionMatchBehavior.Exact)));
                imageSpecifier.Deploy(installation, CancellationToken.None);
                installation = new Installation(temp);
                nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
                Assert.AreEqual(3, nonSystemWidePackages.Count);
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing" && s.Version.ToString().StartsWith("1.4.1")));

            }
            finally
            {
                Directory.Delete(temp, true);
            }
        }

        [Test]
        public void DeployNewPackage()
        {
            ImageSpecifier imageSpecifier = new ImageSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
            imageSpecifier.Repositories.Add("http://packages.opentap.io");
            var identifier = imageSpecifier.Resolve(CancellationToken.None);
            string temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                identifier.Deploy(temp, CancellationToken.None);
                Installation installation = new Installation(temp);
                var nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
                Assert.AreEqual(3, nonSystemWidePackages.Count);
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing"));

                imageSpecifier.Packages.Clear();
                imageSpecifier.Packages.Add(new PackageSpecifier("Python", new VersionSpecifier(2, 3, 0, null, null, VersionMatchBehavior.Exact)));
                imageSpecifier.Deploy(installation, CancellationToken.None);
                installation = new Installation(temp);
                nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
                Assert.AreEqual(4, nonSystemWidePackages.Count);
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing" && s.Version.ToString().StartsWith("1.0.44")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Python" && s.Version.ToString().StartsWith("2.3.0")));

            }
            finally
            {
                Directory.Delete(temp, true);
            }
        }

        [Test]
        public void DeployNewAndUpgrade()
        {
            ImageSpecifier imageSpecifier = new ImageSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("Python", new VersionSpecifier(2, 3, 0, null, null, VersionMatchBehavior.Exact)));
            imageSpecifier.Repositories.Add("http://packages.opentap.io");
            var identifier = imageSpecifier.Resolve(CancellationToken.None);
            string temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                identifier.Deploy(temp, CancellationToken.None);
                Installation installation = new Installation(temp);
                var nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
                Assert.AreEqual(2, nonSystemWidePackages.Count);
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.15.2")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Python" && s.Version.ToString().StartsWith("2.3.0")));

                imageSpecifier.Packages.Clear();
                imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
                imageSpecifier.Deploy(installation, CancellationToken.None);
                installation = new Installation(temp);
                nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
                Assert.AreEqual(4, nonSystemWidePackages.Count);
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing" && s.Version.ToString().StartsWith("1.0.44")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Python" && s.Version.ToString().StartsWith("2.3.0")));
            }
            finally
            {
                Directory.Delete(temp, true);
            }
        }

        [Test]
        public void DeployOverwrite()
        {
            ImageSpecifier imageSpecifier = new ImageSpecifier();
            imageSpecifier.Packages.Add(new PackageSpecifier("REST-API", new VersionSpecifier(2, 6, 3, null, null, VersionMatchBehavior.Exact)));
            imageSpecifier.Repositories.Add("http://packages.opentap.io");
            var identifier = imageSpecifier.Resolve(CancellationToken.None);
            string temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                identifier.Deploy(temp, CancellationToken.None);
                Installation installation = new Installation(temp);
                var nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
                Assert.AreEqual(3, nonSystemWidePackages.Count);
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "REST-API" && s.Version.ToString().StartsWith("2.6.3")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.16.0")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Keysight Floating Licensing"));

                imageSpecifier.Packages.Clear();
                imageSpecifier.Packages.Add(new PackageSpecifier("Python", new VersionSpecifier(2, 3, 0, null, null, VersionMatchBehavior.Exact)));
                identifier = imageSpecifier.Resolve(CancellationToken.None);
                identifier.Deploy(temp, CancellationToken.None); // Overwrite
                installation = new Installation(temp);
                nonSystemWidePackages = installation.GetPackages().Where(s => s.Class != "system-wide").ToList();
                Assert.AreEqual(2, nonSystemWidePackages.Count);
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "OpenTAP" && s.Version.ToString().StartsWith("9.15.2")));
                Assert.IsTrue(nonSystemWidePackages.Any(s => s.Name == "Python" && s.Version.ToString().StartsWith("2.3.0")));

            }
            finally
            {
                Directory.Delete(temp, true);
            }
        }
    }
}
