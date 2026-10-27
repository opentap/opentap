using NUnit.Framework;
using OpenTap.Cli;
using OpenTap.Package;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace OpenTap.Image.Tests
{
    /// <summary>
    /// Verifies that failures on the image deployment worker threads surface as <see cref="ExitCodeException"/>s
    /// with an exit code matching the cause of the failure.
    /// </summary>
    public class DeployErrors
    {
        class FailingRepository : IPackageRepository
        {
            public string Url { get; } = "mock://failing-download";
            public void DownloadPackage(IPackageIdentifier package, string destination, CancellationToken cancellationToken)
                => throw new HttpRequestException("Connection refused");
            public PackageDef[] GetPackages(PackageSpecifier package, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith) => [];
            public string[] GetPackageNames(CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith) => [];
            public PackageVersion[] GetPackageVersions(string packageName, CancellationToken cancellationToken, params IPackageIdentifier[] compatibleWith) => [];
            public PackageDef[] CheckForUpdates(IPackageIdentifier[] packages, CancellationToken cancellationToken) => [];
        }

        static PackageDef CreatePackage(string name, IPackageDefSource source) => new PackageDef
        {
            // A unique version ensures the package is never found in the package cache.
            Name = name,
            Version = new SemanticVersion(1, 0, 0, null, Guid.NewGuid().ToString("N")),
            Architecture = CpuArchitecture.AnyCPU,
            OS = "Windows,Linux,MacOS",
            PackageSource = source,
        };

        [Test]
        public void DownloadFailureGivesNetworkError()
        {
            var repo = new FailingRepository();
            PackageRepositoryHelpers.RegisterRepository(repo);
            using var tempInstall = new TempInstall();

            var package = CreatePackage("DownloadFailurePackage", new HttpRepositoryPackageDefSource { RepositoryUrl = repo.Url });
            var image = new ImageIdentifier([package], [repo.Url]);

            var ex = Assert.Throws<ExitCodeException>(() => image.Deploy(tempInstall.Directory, CancellationToken.None));
            Assert.AreEqual((int)ExitCodes.NetworkError, ex.ExitCode);
            StringAssert.Contains(package.Name, ex.Message);
            StringAssert.Contains("Connection refused", ex.Message);
            Assert.IsFalse(Directory.Exists(Path.Combine(tempInstall.Directory, ".install")), "Staging directory should be removed by rollback.");
        }

        [Test]
        public void CorruptPackageGivesInvalidArchiveError()
        {
            using var tempInstall = new TempInstall();
            var file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, "this is not a zip file");
                var package = CreatePackage("CorruptPackage", new FilePackageDefSource { PackageFilePath = file });
                var image = new ImageIdentifier([package], []);

                var ex = Assert.Throws<ExitCodeException>(() => image.Deploy(tempInstall.Directory, CancellationToken.None));
                Assert.AreEqual((int)PackageExitCodes.InvalidPackageArchive, ex.ExitCode);
                StringAssert.Contains(package.Name, ex.Message);
                StringAssert.Contains(file, ex.Message);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Test]
        public void MissingPackageFileGivesInstallError()
        {
            using var tempInstall = new TempInstall();
            var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".TapPackage");
            var package = CreatePackage("MissingPackage", new FilePackageDefSource { PackageFilePath = file });
            var image = new ImageIdentifier([package], []);

            var ex = Assert.Throws<ExitCodeException>(() => image.Deploy(tempInstall.Directory, CancellationToken.None));
            Assert.AreEqual((int)PackageExitCodes.PackageInstallError, ex.ExitCode);
            StringAssert.Contains(package.Name, ex.Message);
        }

        [Test]
        public void CancellationIsNotReportedAsError()
        {
            using var tempInstall = new TempInstall();
            var package = CreatePackage("CancelledPackage", new FilePackageDefSource { PackageFilePath = "unused.TapPackage" });
            var image = new ImageIdentifier([package], []);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() => image.Deploy(tempInstall.Directory, cts.Token));
        }
    }
}
