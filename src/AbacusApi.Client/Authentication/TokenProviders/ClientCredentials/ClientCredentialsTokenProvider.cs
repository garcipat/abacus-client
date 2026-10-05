using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace Garcipat.AbacusApi.Client.Authentication.TokenProviders.ClientCredentials;

/// <summary>
/// User-independent service user (OAuth client credentials). Reads the token endpoint from
/// <c>{BaseUrl}/.well-known/openid-configuration</c> once and caches the access token until shortly before it expires.
/// </summary>
public sealed class ClientCredentialsTokenProvider(IHttpClientFactory httpClientFactory, IOptions<AbacusOptions> options, TimeProvider timeProvider)
    : IAbacusTokenProvider, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly OpenIdDiscovery _discovery = new();
    private string? _accessToken;
    private DateTimeOffset _renewAt;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_accessToken is null || timeProvider.GetUtcNow() >= _renewAt)
                await RequestTokenAsync(cancellationToken).ConfigureAwait(false);

            return _accessToken!;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    private async Task RequestTokenAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrEmpty(settings.ClientSecret))
            throw new InvalidOperationException("Abacus:ClientSecret is required for the client credentials flow.");

        using var http = httpClientFactory.CreateClient(TokenEndpoint.HttpClientName);
        var configuration = await _discovery.GetAsync(http, settings.BaseUrl, cancellationToken).ConfigureAwait(false);

        var form = new List<KeyValuePair<string, string>> { new("grant_type", "client_credentials") };
        if (settings.Scopes.Count > 0)
            form.Add(new("scope", string.Join(' ', settings.Scopes)));

        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ClientId}:{settings.ClientSecret}")));

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The Abacus token endpoint returned no token.");

        _accessToken = token.AccessToken;
        _renewAt = timeProvider.GetUtcNow() + TimeSpan.FromSeconds(token.ExpiresIn) - TokenEndpoint.ExpiryMargin;
    }
}
