using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Pgarcia.AbacusApi.Tests.Infrastructure;

/// <summary>Returns configured responses per absolute URL and records the requests it received.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = [];
    private readonly Dictionary<string, Queue<Func<HttpResponseMessage>>> _onceResponses = [];
    private readonly List<RecordedRequest> _requests = [];

    public IReadOnlyList<RecordedRequest> Requests => _requests;

    /// <summary>The response for every request to <paramref name="url"/> (after any <see cref="RespondOnce"/> ones are used up).</summary>
    public StubHttpMessageHandler Respond(string url, HttpStatusCode status, string json)
    {
        _responses[url] = () => Response(status, json);
        return this;
    }

    /// <summary>A response for the next request to <paramref name="url"/> only; several are returned in order.</summary>
    public StubHttpMessageHandler RespondOnce(string url, HttpStatusCode status, string json)
    {
        if (!_onceResponses.TryGetValue(url, out var queue))
            _onceResponses[url] = queue = new Queue<Func<HttpResponseMessage>>();

        queue.Enqueue(() => Response(status, json));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Add(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization, body));

        var url = request.RequestUri!.AbsoluteUri;
        if (_onceResponses.TryGetValue(url, out var queue) && queue.TryDequeue(out var once))
            return once();

        return _responses.TryGetValue(url, out var response)
            ? response()
            : new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

public sealed record RecordedRequest(HttpMethod Method, Uri Uri, AuthenticationHeaderValue? Authorization, string? Body);
