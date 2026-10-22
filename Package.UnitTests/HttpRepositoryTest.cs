using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenTap.Authentication;

namespace OpenTap.Package.UnitTests;

[TestFixture]
public class HttpRepositoryTest
{

    [Test]
    public void TestTokenWithPort()
    {
        new TokenInfo("asd", "asd", "localhost:18080");
        new TokenInfo("asd", "asd", "localhost");
        new TokenInfo("asd", "asd", "packages.opentap.io");
        new TokenInfo("asd", "asd", "packages.opentap.io:1111");
    }

    [Test]
    [Timeout(90000)]
    public async Task ExhaustedDownloadRetriesPreserveDestinationAndRemoveTemporaryFile()
    {
        // Deliberately truncate every response to exercise the real download/resume path.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        int attempts = 0;
        using var cancellation = new CancellationTokenSource();
        var server = Task.Run(async () =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                using var connection = await listener.AcceptTcpClientAsync(cancellation.Token);
                using var stream = connection.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
                Interlocked.Increment(ref attempts);
                var header = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\nContent-Length: 1048576\r\n" +
                    "Content-Disposition: attachment; filename=package.TapPackage\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header);
                await stream.WriteAsync(new byte[4096]);
            }
        });

        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "package.TapPackage");
        File.WriteAllText(destination, "existing package");
        try
        {
            var repository = new HttpPackageRepository($"http://127.0.0.1:{port}");
            var package = new PackageDef
            {
                Name = "TestPackage",
                Version = SemanticVersion.Parse("1.0.0"),
                Architecture = CpuArchitecture.AnyCPU,
                OS = "Windows,Linux,MacOS"
            };
            var failure = Assert.Catch(() => repository.DownloadPackage(package, destination, CancellationToken.None));
            Assert.That(attempts, Is.EqualTo(60), failure.ToString());
            Assert.That(File.ReadAllText(destination), Is.EqualTo("existing package"));
            Assert.That(Directory.GetFiles(directory), Is.EquivalentTo(new[] { destination }));
        }
        finally
        {
            cancellation.Cancel();
            listener.Stop();
            try { await server; }
            catch (OperationCanceledException) { }
            catch (SocketException) when (cancellation.IsCancellationRequested) { }
            Directory.Delete(directory, true);
        }
    }
}
