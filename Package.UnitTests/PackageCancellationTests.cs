using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenTap.Package.UnitTests
{
    [NonParallelizable]
    public class PackageCancellationTests
    {
        private sealed class CancellingRepository : IPackageRepository
        {
            private readonly CancellationTokenSource cancellation;
            private readonly string cancelOn;
            public bool WrapCancellation { get; set; }
            public string Url { get; } = "https://cancellation-test-" + Guid.NewGuid().ToString("N") + ".invalid";
            public CancellationToken ReceivedToken { get; private set; }

            public CancellingRepository(CancellationTokenSource cancellation, string cancelOn)
            {
                this.cancellation = cancellation;
                this.cancelOn = cancelOn;
            }

            private void Cancel(string operation, CancellationToken token)
            {
                if (operation != cancelOn) return;
                ReceivedToken = token;
                cancellation.Cancel();
                if (WrapCancellation && token.IsCancellationRequested)
                    throw new AggregateException(new OperationCanceledException(token));
                token.ThrowIfCancellationRequested();
            }

            public string[] GetPackageNames(CancellationToken token, params IPackageIdentifier[] compatibleWith)
            {
                Cancel("names", token);
                return new[] { "CancellationTestPackage" };
            }

            public PackageDef[] GetPackages(PackageSpecifier package, CancellationToken token, params IPackageIdentifier[] compatibleWith)
            {
                Cancel("packages", token);
                return Array.Empty<PackageDef>();
            }

            public PackageVersion[] GetPackageVersions(string name, CancellationToken token, params IPackageIdentifier[] compatibleWith)
            {
                Cancel("versions", token);
                return Array.Empty<PackageVersion>();
            }

            public void DownloadPackage(IPackageIdentifier package, string destination, CancellationToken token) => throw new NotSupportedException();
            public PackageDef[] CheckForUpdates(IPackageIdentifier[] packages, CancellationToken token) => throw new NotSupportedException();
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ListPropagatesCallerToken(bool named, bool allPlatforms)
        {
            using var session = Session.Create(SessionOptions.OverlayComponentSettings);
            PackageManagerSettings.Current.UseLocalPackageCache = false;
            using var cancellation = new CancellationTokenSource();
            var repo = new CancellingRepository(cancellation, named ? "versions" : "packages");
            PackageRepositoryHelpers.RegisterRepository(repo);
            var action = new PackageListAction
            {
                Name = named ? "CancellationTestPackage" : null,
                All = allPlatforms,
                Repository = new[] { repo.Url },
                Target = Path.GetDirectoryName(typeof(PackageCancellationTests).Assembly.Location)
            };

            Assert.Catch<OperationCanceledException>(() => action.Execute(cancellation.Token));
            Assert.AreEqual(cancellation.Token, repo.ReceivedToken);
        }

        [Test]
        public void AutoCorrectionPropagatesCallerToken()
        {
            using var cancellation = new CancellationTokenSource();
            var repo = new CancellingRepository(cancellation, "names");
            Assert.Catch<OperationCanceledException>(() => AutoCorrectPackageNames.Correct(
                new[] { "NotInstalled-" + Guid.NewGuid() }, new[] { repo }, cancellation.Token));
            Assert.AreEqual(cancellation.Token, repo.ReceivedToken);
        }

        [Test]
        public void ShowPropagatesCallerTokenToPackageLookup()
        {
            using var session = Session.Create(SessionOptions.OverlayComponentSettings);
            PackageManagerSettings.Current.UseLocalPackageCache = false;
            using var cancellation = new CancellationTokenSource();
            var repo = new CancellingRepository(cancellation, "packages");
            PackageRepositoryHelpers.RegisterRepository(repo);
            var action = new PackageShowAction
            {
                Name = "CancellationTestPackage",
                Repository = new[] { repo.Url },
                Target = Path.GetDirectoryName(typeof(PackageCancellationTests).Assembly.Location)
            };

            Assert.Catch<OperationCanceledException>(() => action.Execute(cancellation.Token));
            Assert.AreEqual(cancellation.Token, repo.ReceivedToken);
        }

        [Test]
        public void InstalledListRejectsAlreadyCancelledToken()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Catch<OperationCanceledException>(() => new PackageListAction { Installed = true }.Execute(cancellation.Token));
        }

        [Test]
        public void ParallelHelperDoesNotSwallowCancellationWrappedByRepository()
        {
            using var cancellation = new CancellationTokenSource();
            var repo = new CancellingRepository(cancellation, "packages") { WrapCancellation = true };
            Assert.Catch<OperationCanceledException>(() => PackageRepositoryHelpers.GetPackagesFromAllRepos(
                new List<IPackageRepository> { repo }, new PackageSpecifier(), cancellation.Token));
            Assert.AreEqual(cancellation.Token, repo.ReceivedToken);
        }

        // Accept a real HTTP request, but never send a response. No external repository or
        // arbitrary sleep is needed to prove cancellation interrupts an in-flight request.
        private sealed class StalledServer : IDisposable
        {
            private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource stop = new CancellationTokenSource();
            private readonly Task server;
            private bool disposed;
            public TaskCompletionSource<bool> Requested { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public string Url { get; }

            public StalledServer()
            {
                listener.Start();
                Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
                server = Task.Run(async () =>
                {
                    try
                    {
                        using var client = await listener.AcceptTcpClientAsync(stop.Token);
                        using var reader = new StreamReader(client.GetStream());
                        while (await reader.ReadLineAsync(stop.Token) is { Length: > 0 }) { }
                        Requested.TrySetResult(true);
                        await Task.Delay(Timeout.Infinite, stop.Token);
                    }
                    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                });
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                stop.Cancel();
                server.GetAwaiter().GetResult();
                listener.Stop();
                stop.Dispose();
            }
        }

        [Test]
        public async Task WaitingForAnotherVersionProbeIsCancellable()
        {
            using var server = new StalledServer();
            using var firstCancellation = new CancellationTokenSource();
            using var secondCancellation = new CancellationTokenSource();
            var repo = new HttpPackageRepository(server.Url);
            var first = Task.Run(() => repo.GetPackageVersions("OpenTAP", firstCancellation.Token));
            Task second = null;
            try
            {
                await server.Requested.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                second = Task.Run(() =>
                {
                    started.SetResult(true);
                    repo.GetPackageVersions("OpenTAP", secondCancellation.Token);
                });
                await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
                secondCancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>(async () => await second.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.IsFalse(first.IsCompleted, "Cancelling the waiter must not cancel the active probe.");
            }
            finally
            {
                firstCancellation.Cancel();
                secondCancellation.Cancel();
                server.Dispose();
                try { await first.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (OperationCanceledException) { }
                if (second != null)
                {
                    try { await second.WaitAsync(TimeSpan.FromSeconds(10)); }
                    catch (OperationCanceledException) { }
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task HttpRequestsObserveCancellation(bool versionProbe)
        {
            using var server = new StalledServer();
            using var cancellation = new CancellationTokenSource();
            var repo = new HttpPackageRepository(server.Url);
            var operation = Task.Run(() =>
            {
                if (versionProbe)
                    repo.GetPackageVersions("OpenTAP", cancellation.Token);
                else
                    PackageRepositoryHelpers.GetPackageNameAndVersionFromAllRepos(
                        new List<IPackageRepository> { repo }, new PackageSpecifier(), cancellation.Token);
            });

            try
            {
                await server.Requested.Task.WaitAsync(TimeSpan.FromSeconds(10));
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>(async () => await operation.WaitAsync(TimeSpan.FromSeconds(5)));
            }
            finally
            {
                cancellation.Cancel();
                server.Dispose();
                // Observe the worker even on failure; do not leave HTTP operations running.
                try { await operation.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (OperationCanceledException) { }
            }
        }
    }
}
