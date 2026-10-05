using System.Web;
using Pgarcia.AbacusApi.Client.Authentication.TokenProviders.InteractiveBrowser;

namespace Pgarcia.AbacusApi.Tests.Infrastructure;

/// <summary>Plays the browser: records the login URL and answers with a code and the <c>state</c> it was given.</summary>
public sealed class FakeCodeReceiver : IAuthorizationCodeReceiver
{
    private readonly List<Uri> _authorizationUrls = [];

    public IReadOnlyList<Uri> AuthorizationUrls => _authorizationUrls;

    public string Code { get; set; } = "auth-code";

    /// <summary>Replaces the whole response, e.g. with an error or a different state.</summary>
    public Dictionary<string, string>? Response { get; set; }

    public Task<IReadOnlyDictionary<string, string>> ReceiveAsync(Uri authorizationUrl, Uri redirectUri, CancellationToken cancellationToken)
    {
        _authorizationUrls.Add(authorizationUrl);
        var state = HttpUtility.ParseQueryString(authorizationUrl.Query)["state"] ?? string.Empty;
        IReadOnlyDictionary<string, string> response = Response ?? new Dictionary<string, string> { ["code"] = Code, ["state"] = state };
        return Task.FromResult(response);
    }

    public string Parameter(int login, string name) => HttpUtility.ParseQueryString(_authorizationUrls[login].Query)[name]!;
}
