using System.Net.Http.Headers;

namespace Pgarcia.AbacusApi.Client.Authentication;

/// <summary>Adds the bearer token from <see cref="IAbacusTokenProvider"/> to every request.</summary>
public sealed class AbacusAuthHandler(IAbacusTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
