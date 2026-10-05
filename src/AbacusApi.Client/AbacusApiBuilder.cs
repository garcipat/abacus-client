using Garcipat.AbacusApi.Client.Authentication;
using Garcipat.AbacusApi.Client.Authentication.TokenProviders.InteractiveBrowser;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Garcipat.AbacusApi.Client;

/// <summary>
/// Passed to the <c>configure</c> callback of <c>AddAbacusApi</c> to add features:
/// <code>
/// services.AddAbacusApi(configuration, api =&gt; api
///     .UseInteractiveBrowserLogin());
/// </code>
/// The defaults are registered before the callback runs, so a hook can replace them. Further features can be added as
/// extension methods on this type, using <see cref="Services"/> and <see cref="HttpClient"/>.
/// </summary>
public sealed class AbacusApiBuilder
{
    internal AbacusApiBuilder(IServiceCollection services, IHttpClientBuilder httpClient)
    {
        Services = services;
        HttpClient = httpClient;
    }

    public IServiceCollection Services { get; }

    /// <summary>The typed <c>IAbacusApi</c> client, e.g. to add handlers or resilience.</summary>
    public IHttpClientBuilder HttpClient { get; }

    /// <summary>Replaces the default <see cref="Authentication.TokenProviders.ClientCredentials.ClientCredentialsTokenProvider"/> with another <see cref="IAbacusTokenProvider"/>.</summary>
    public AbacusApiBuilder UseTokenProvider<TProvider>()
        where TProvider : class, IAbacusTokenProvider
    {
        Services.Replace(ServiceDescriptor.Singleton<IAbacusTokenProvider, TProvider>());
        return this;
    }

    /// <summary>
    /// User-dependent login: <see cref="InteractiveBrowserTokenProvider"/> opens the Abacus login in the browser on the
    /// first call and keeps the refresh token (DPAPI-encrypted file on Windows, memory elsewhere). Needs
    /// <see cref="AbacusOptions.RedirectUri"/>.
    /// </summary>
    public AbacusApiBuilder UseInteractiveBrowserLogin()
    {
        Services.TryAddSingleton<IAuthorizationCodeReceiver, LoopbackBrowserCodeReceiver>();
        Services.TryAddSingleton<ITokenCache>(provider => OperatingSystem.IsWindows()
            ? FileTokenCache.ForOptions(provider.GetRequiredService<IOptions<AbacusOptions>>().Value)
            : new MemoryTokenCache());
        return UseTokenProvider<InteractiveBrowserTokenProvider>();
    }
}
