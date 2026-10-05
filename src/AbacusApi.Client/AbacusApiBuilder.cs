using Garcipat.AbacusApi.Client.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Garcipat.AbacusApi.Client;

/// <summary>Returned by <c>AddAbacusApi</c> to customise the registration.</summary>
public sealed class AbacusApiBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    /// <summary>Replaces the default <see cref="ClientCredentialsTokenProvider"/>, e.g. with an app-specific user-dependent login.</summary>
    public AbacusApiBuilder AddTokenProvider<TProvider>()
        where TProvider : class, IAbacusTokenProvider
    {
        Services.Replace(ServiceDescriptor.Singleton<IAbacusTokenProvider, TProvider>());
        return this;
    }
}
