using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Garcipat.AbacusApi.Tests.Infrastructure;

/// <summary>Returns configured responses per absolute URL and records the requests it received.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = [];
    private readonly List<RecordedRequest> _requests = [];

    public IReadOnlyList<RecordedRequest> Requests => _requests;

    public StubHttpMessageHandler Respond(string url, HttpStatusCode status, string json)
    {
        _responses[url] = () => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Add(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization, body));

        return _responses.TryGetValue(request.RequestUri!.AbsoluteUri, out var response)
            ? response()
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}

public sealed record RecordedRequest(HttpMethod Method, Uri Uri, AuthenticationHeaderValue? Authorization, string? Body);
