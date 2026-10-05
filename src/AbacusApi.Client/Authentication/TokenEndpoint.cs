namespace Pgarcia.AbacusApi.Client.Authentication;

/// <summary>Shared by the token providers that talk to the Abacus OAuth endpoints.</summary>
public static class TokenEndpoint
{
    /// <summary>Named <see cref="HttpClient"/> for discovery and token requests (without the bearer token handler).</summary>
    public const string HttpClientName = "Pgarcia.AbacusApi.Token";

    /// <summary>Renew this long before a token expires, so a request never goes out with a token that expires in flight.</summary>
    internal static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(30);
}
