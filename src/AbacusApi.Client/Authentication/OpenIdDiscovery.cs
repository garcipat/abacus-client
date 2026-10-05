using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Garcipat.AbacusApi.Client.Authentication;

/// <summary>
/// Reads <c>{BaseUrl}/.well-known/openid-configuration</c> once and caches it. Abacus asks clients to always take
/// the endpoints from there instead of hard-coding them.
/// </summary>
internal sealed class OpenIdDiscovery
{
    private OpenIdConfiguration? _configuration;

    /// <summary>Not thread-safe on its own; callers hold their provider's lock.</summary>
    public async Task<OpenIdConfiguration> GetAsync(HttpClient http, Uri baseUrl, CancellationToken cancellationToken)
    {
        if (_configuration is not null)
            return _configuration;

        var discoveryUrl = new Uri(baseUrl, "/.well-known/openid-configuration");
        var configuration = await http.GetFromJsonAsync<OpenIdConfiguration>(discoveryUrl, cancellationToken).ConfigureAwait(false);
        if (configuration?.TokenEndpoint is null)
            throw new InvalidOperationException($"{discoveryUrl} returned no token_endpoint.");

        return _configuration = configuration;
    }
}

internal sealed record OpenIdConfiguration(
    [property: JsonPropertyName("token_endpoint")] Uri? TokenEndpoint,
    [property: JsonPropertyName("authorization_endpoint")] Uri? AuthorizationEndpoint);

internal sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken);
