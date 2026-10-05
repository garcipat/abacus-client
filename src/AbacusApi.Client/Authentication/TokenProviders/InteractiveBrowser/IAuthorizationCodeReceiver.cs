namespace Garcipat.AbacusApi.Client.Authentication.TokenProviders.InteractiveBrowser;

/// <summary>
/// Shows the Abacus login for <paramref name="authorizationUrl"/> and returns the query parameters Abacus sends to
/// <paramref name="redirectUri"/> afterwards (<c>code</c>, <c>state</c>, or <c>error</c>/<c>error_description</c>).
/// The default, <see cref="LoopbackBrowserCodeReceiver"/>, opens the system browser and listens on the loopback URL.
/// </summary>
public interface IAuthorizationCodeReceiver
{
    Task<IReadOnlyDictionary<string, string>> ReceiveAsync(Uri authorizationUrl, Uri redirectUri, CancellationToken cancellationToken);
}
