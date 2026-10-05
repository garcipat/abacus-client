namespace Pgarcia.AbacusApi.Client.Authentication;

/// <summary>
/// Supplies the bearer token for requests to the Abacus API. The package ships
/// <see cref="TokenProviders.ClientCredentials.ClientCredentialsTokenProvider"/>; an application using the user-dependent flow registers its own
/// implementation with <c>AddAbacusApi(configuration, api =&gt; api.UseTokenProvider&lt;T&gt;())</c>.
/// </summary>
public interface IAbacusTokenProvider
{
    /// <summary>Returns a valid access token, renewing it when it has expired.</summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
