using System.Net.Http.Json;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Garcipat.AbacusApi.Client.Authentication.TokenProviders.InteractiveBrowser;

/// <summary>
/// User-dependent login (OAuth authorization code). The first call opens the Abacus login in the browser; afterwards
/// the access token is renewed with the refresh token, which <see cref="ITokenCache"/> keeps between runs. When the
/// refresh token is rejected (expired, or the user logged out of Abacus), the login opens again.
/// </summary>
/// <remarks>
/// Needs a user-dependent service user in Q910 (client type "Public" without secret, or "Trusted" with
/// <see cref="AbacusOptions.ClientSecret"/>) with <see cref="AbacusOptions.RedirectUri"/> registered.
/// </remarks>
public sealed class InteractiveBrowserTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<AbacusOptions> options,
    IAuthorizationCodeReceiver codeReceiver,
    ITokenCache tokenCache,
    TimeProvider timeProvider)
    : IAbacusTokenProvider, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly OpenIdDiscovery _discovery = new();
    private string? _accessToken;
    private DateTimeOffset _renewAt;
    private string? _refreshToken;
    private bool _cacheLoaded;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_accessToken is not null && timeProvider.GetUtcNow() < _renewAt)
                return _accessToken;

            var settings = options.Value;
            using var http = httpClientFactory.CreateClient(TokenEndpoint.HttpClientName);
            var configuration = await _discovery.GetAsync(http, settings.BaseUrl, cancellationToken).ConfigureAwait(false);

            if (!_cacheLoaded)
            {
                _refreshToken = await tokenCache.LoadAsync(cancellationToken).ConfigureAwait(false);
                _cacheLoaded = true;
            }

            var token = _refreshToken is null ? null : await RefreshAsync(http, configuration, settings, _refreshToken, cancellationToken).ConfigureAwait(false);
            if (token is null && _refreshToken is not null)
            {
                _refreshToken = null;
                await tokenCache.SaveAsync(null, cancellationToken).ConfigureAwait(false);
            }

            token ??= await LoginAsync(http, configuration, settings, cancellationToken).ConfigureAwait(false);
            await ApplyAsync(token, cancellationToken).ConfigureAwait(false);
            return _accessToken!;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    /// <summary>Returns <c>null</c> when Abacus rejects the refresh token, so the caller logs in again.</summary>
    private static async Task<TokenResponse?> RefreshAsync(
        HttpClient http, OpenIdConfiguration configuration, AbacusOptions settings, string refreshToken, CancellationToken cancellationToken)
    {
        var form = ClientParameters(settings);
        form.Add(new("grant_type", "refresh_token"));
        form.Add(new("refresh_token", refreshToken));

        using var response = await http.PostAsync(configuration.TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            return null;

        return await ReadTokenAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TokenResponse> LoginAsync(HttpClient http, OpenIdConfiguration configuration, AbacusOptions settings, CancellationToken cancellationToken)
    {
        var redirectUri = settings.RedirectUri
            ?? throw new InvalidOperationException("Abacus:RedirectUri is required for the interactive login.");
        var authorizationEndpoint = configuration.AuthorizationEndpoint
            ?? throw new InvalidOperationException("The Abacus OpenID configuration has no authorization_endpoint.");

        var state = RandomBase64Url();
        var verifier = settings.UsePkce ? RandomBase64Url() : null;

        var query = new List<KeyValuePair<string, string>>
        {
            new("response_type", "code"),
            new("client_id", settings.ClientId),
            new("scope", string.Join(' ', settings.Scopes)),
            new("redirect_uri", redirectUri.OriginalString),
            new("state", state),
        };
        if (verifier is not null)
        {
            query.Add(new("code_challenge", Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))));
            query.Add(new("code_challenge_method", "S256"));
        }

        var authorizationUrl = new UriBuilder(authorizationEndpoint) { Query = string.Join('&', query.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}")) }.Uri;
        var result = await codeReceiver.ReceiveAsync(authorizationUrl, redirectUri, cancellationToken).ConfigureAwait(false);

        if (result.TryGetValue("error", out var error))
        {
            result.TryGetValue("error_description", out var description);
            throw new AbacusLoginException($"The Abacus login failed: {error}: {description}");
        }

        if (!result.TryGetValue("state", out var returnedState) || returnedState != state)
            throw new AbacusLoginException("The Abacus login returned an unexpected state; the response was not accepted.");

        if (!result.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            throw new AbacusLoginException("The Abacus login returned no authorization code.");

        var form = ClientParameters(settings);
        form.Add(new("grant_type", "authorization_code"));
        form.Add(new("code", code));
        form.Add(new("redirect_uri", redirectUri.OriginalString));
        if (verifier is not null)
            form.Add(new("code_verifier", verifier));

        using var response = await http.PostAsync(configuration.TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken).ConfigureAwait(false);
        return await ReadTokenAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Abacus returns the refresh token only on the first login and keeps it on refreshes, so it is only replaced when a new one comes.</summary>
    private async Task ApplyAsync(TokenResponse token, CancellationToken cancellationToken)
    {
        _accessToken = token.AccessToken;
        _renewAt = timeProvider.GetUtcNow() + TimeSpan.FromSeconds(token.ExpiresIn) - TokenEndpoint.ExpiryMargin;

        if (token.RefreshToken is not null && token.RefreshToken != _refreshToken)
        {
            _refreshToken = token.RefreshToken;
            await tokenCache.SaveAsync(_refreshToken, cancellationToken).ConfigureAwait(false);
        }
    }

    private static List<KeyValuePair<string, string>> ClientParameters(AbacusOptions settings)
    {
        var form = new List<KeyValuePair<string, string>> { new("client_id", settings.ClientId) };
        if (!string.IsNullOrEmpty(settings.ClientSecret))
            form.Add(new("client_secret", settings.ClientSecret));
        return form;
    }

    private static async Task<TokenResponse> ReadTokenAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The Abacus token endpoint returned no token.");
    }

    private static string RandomBase64Url() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
