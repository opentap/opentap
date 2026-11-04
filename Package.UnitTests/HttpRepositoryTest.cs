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
    [TestCase("host", null)]
    [TestCase("port", null)]
    [TestCase("none", null)]
    [TestCase("port", "custom")]
    [TestCase("port", "missing")]
    [TestCase("port", "CUSTOM")]
    [TestCase("host", null, "../packages")]
    [TestCase("port", null, "../packages")]
    [TestCase("none", null, "../packages")]
    [TestCase("host", null, "/api/packages")]
    [TestCase("port", null, "/api/packages")]
    [TestCase("none", null, "/api/packages")]
    public async Task RepositoryAndAuthenticationClientsSelectTokens(string tokenKind, string domain, string relativeUrl = null)
    {
        using var session = Session.Create(SessionOptions.OverlayComponentSettings);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var uri = new Uri($"http://127.0.0.1:{port}/");
        var settings = AuthenticationSettings.Current;
        settings.BaseAddress = new Uri(uri, "base/").AbsoluteUri;
        settings.Tokens = new List<TokenInfo>();
        if (tokenKind != "none")
            settings.Tokens.Add(new("host-token", null, uri.Host));
        if (tokenKind == "port")
            settings.Tokens.Add(new("port-token", null, uri.Authority));
        // A token for an unrelated port must not be used.
        settings.Tokens.Add(new("wrong-port", null, "127.0.0.1:1"));
        settings.Tokens.Add(new("explicit-token", null, "custom"));
        var expected = tokenKind == "none" ? null : $"Bearer {tokenKind}-token";
        var expectedClientToken = domain == "custom" ? "Bearer explicit-token" : expected;

        async Task<(string RequestLine, string Authorization)> ReceiveRequest()
        {
            using var connection = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(10));
            using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, System.Text.Encoding.ASCII, false, 1024, true);
            var requestLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            string authorization = null;
            string line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10))))
                if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                    authorization = line.Substring("Authorization:".Length).Trim();
            var response = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 7\r\nConnection: close\r\n\r\n\"4.0.0\""u8.ToArray();
            await stream.WriteAsync(response);
            return (requestLine, authorization);
        }

        var requestUri = uri;
        if (relativeUrl != null)
        {
            // Relative repository URLs must be resolved before selecting the destination's token.
            var repository = PackageRepositoryHelpers.DetermineRepositoryType(relativeUrl);
            var expectedPath = relativeUrl.StartsWith("/") ? relativeUrl : "/packages";
            Assert.That(repository, Is.TypeOf<HttpPackageRepository>());
            Assert.That(repository.Url, Is.EqualTo(new Uri(uri, expectedPath).AbsoluteUri));
            requestUri = new Uri(repository.Url, UriKind.Absolute);
        }
        var repoClient = HttpPackageRepository.GetAuthenticatedClient(requestUri);
        using var repoHttpClient = repoClient.HttpClient;
        var received = ReceiveRequest();
        using (var client = settings.GetClient(domain))
        using (var response = await client.GetAsync(relativeUrl ?? uri.AbsoluteUri).WaitAsync(TimeSpan.FromSeconds(10)))
            response.EnsureSuccessStatusCode();
        var clientRequest = await received;
        Assert.That(clientRequest.RequestLine, Is.EqualTo($"GET {requestUri.PathAndQuery} HTTP/1.1"));
        Assert.That(clientRequest.Authorization, Is.EqualTo(expectedClientToken));

        received = ReceiveRequest();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Task.Run(() => repoClient.Version(cancellation.Token));
        Assert.That((await received).Authorization, Is.EqualTo(expected));
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
