using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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

    private sealed class QueryHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpStatusCode> getStatus;
        public readonly List<HttpRequestMessage> Requests = new List<HttpRequestMessage>();
        public readonly List<string> Bodies = new List<string>();

        public QueryHandler(Func<int, HttpStatusCode> getStatus) => this.getStatus = getStatus;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(await request.Content.ReadAsStringAsync());
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
            "https://repository.example/4.0/query", query, CancellationToken.None);

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
            "https://repository.example/4.0/query", "query", CancellationToken.None);

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
            "https://repository.example/4.0/query", "query", CancellationToken.None);

        Assert.That(handler.Requests.Count, Is.EqualTo(1));
        Assert.That(response.StatusCode, Is.EqualTo(status));
    }

    [Test]
    public void DependencyQueryCancellationStopsRetryWait()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new QueryHandler(_ =>
        {
            cancellation.Cancel();
            return HttpStatusCode.BadGateway;
        });
        using var client = new HttpClient(handler);

        Assert.Catch<OperationCanceledException>(() => HttpPackageRepository.SendDependencyQuery(client,
            "https://repository.example/4.0/query", "query", cancellation.Token));
        Assert.That(handler.Requests.Count, Is.EqualTo(1));
    }

    [Test]
    public void DependencyQueryDoesNotSendWhenAlreadyCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var handler = new QueryHandler(_ => HttpStatusCode.OK);
        using var client = new HttpClient(handler);

        Assert.Catch<OperationCanceledException>(() => HttpPackageRepository.SendDependencyQuery(client,
            "https://repository.example/4.0/query", "query", cancellation.Token));
        Assert.That(handler.Requests, Is.Empty);
    }
}
