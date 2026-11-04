using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenTap.Authentication;

namespace OpenTap.Package.UnitTests;

[TestFixture]
public class HttpRepositoryTest
{
    [TestCase("host")]
    [TestCase("port")]
    [TestCase("none")]
    public async Task RepositoryAndAuthenticationClientsSelectSameToken(string tokenKind)
    {
        using var session = Session.Create(SessionOptions.OverlayComponentSettings);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var uri = new Uri($"http://127.0.0.1:{port}/");
        var settings = AuthenticationSettings.Current;
        settings.Tokens = new List<TokenInfo>();
        if (tokenKind != "none")
            settings.Tokens.Add(new("host-token", null, uri.Host));
        if (tokenKind == "port")
            settings.Tokens.Add(new("port-token", null, uri.Authority));
        // A token for an unrelated port must not be used.
        settings.Tokens.Add(new("wrong-port", null, "127.0.0.1:1"));
        var expected = tokenKind == "none" ? null : $"Bearer {tokenKind}-token";

        async Task<string> ReceiveRequest()
        {
            using var connection = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(10));
            using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, System.Text.Encoding.ASCII, false, 1024, true);
            string authorization = null;
            string line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10))))
                if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                    authorization = line.Substring("Authorization:".Length).Trim();
            var response = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 7\r\nConnection: close\r\n\r\n\"4.0.0\""u8.ToArray();
            await stream.WriteAsync(response);
            return authorization;
        }

        var repoClient = HttpPackageRepository.GetAuthenticatedClient(uri);
        using var repoHttpClient = repoClient.HttpClient;
        var received = ReceiveRequest();
        using (var client = settings.GetClient())
        using (var response = await client.GetAsync(uri).WaitAsync(TimeSpan.FromSeconds(10)))
            response.EnsureSuccessStatusCode();
        Assert.That(await received, Is.EqualTo(expected));

        received = ReceiveRequest();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Task.Run(() => repoClient.Version(cancellation.Token));
        Assert.That(await received, Is.EqualTo(expected));
    }

    private sealed class QueryHandler(Func<int, HttpStatusCode> getStatus) : HttpMessageHandler
    {
        private readonly Func<int, HttpStatusCode> getStatus = getStatus;
        public readonly List<HttpRequestMessage> Requests = [];
        public readonly List<string> Bodies = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(getStatus(Requests.Count));
        }
    }

    [TestCase(HttpStatusCode.BadGateway)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.GatewayTimeout)]
    public void DependencyQueryRetriesTransientErrors(HttpStatusCode status)
    {
        using var handler = new QueryHandler(attempt => attempt <= 2 ? status : HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        const string query = "query Query { objects { name } }";
        using var response = HttpPackageRepository.SendDependencyQuery(client,
            "https://repository.example/4.0/query", query);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(handler.Requests.Count, Is.EqualTo(3));
        Assert.That(handler.Bodies, Is.All.EqualTo(query));
        Assert.That(handler.Requests, Is.Unique, "Each retry must use a new request.");
        Assert.That(handler.Requests.TrueForAll(r => r.Method == HttpMethod.Post), Is.True);
    }

    [Test]
    public void DependencyQueryStopsAfterFiveAttempts()
    {
        using var handler = new QueryHandler(_ => HttpStatusCode.BadGateway);
        using var client = new HttpClient(handler);
        using var response = HttpPackageRepository.SendDependencyQuery(client,
            "https://repository.example/4.0/query", "query");

        Assert.That(handler.Requests.Count, Is.EqualTo(5));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
    }

    [TestCase(HttpStatusCode.OK)]
    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.NotFound)]
    public void DependencyQueryDoesNotRetryOtherResponses(HttpStatusCode status)
    {
        using var handler = new QueryHandler(_ => status);
        using var client = new HttpClient(handler);
        using var response = HttpPackageRepository.SendDependencyQuery(client,
            "https://repository.example/4.0/query", "query");

        Assert.That(handler.Requests.Count, Is.EqualTo(1));
        Assert.That(response.StatusCode, Is.EqualTo(status));
    }
}
