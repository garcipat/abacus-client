using Garcipat.AbacusApi.Client;
using Garcipat.AbacusApi.Client.Authentication;
using Garcipat.AbacusApi.Client.Authentication.TokenProviders.ClientCredentials;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using V2026 = Garcipat.AbacusApi.Client.V2026;

#pragma warning disable IDE0130 // Extension methods live in the DI namespace, as the Microsoft.Extensions.* ones do.

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceConfiguration
{
    /// <summary>
    /// Registers <c>IAbacusApi</c> with options from the <c>Abacus</c> section.
    /// <paramref name="configure"/> adds features, e.g. <c>api =&gt; api.UseInteractiveBrowserLogin()</c>.
    /// </summary>
    public static IServiceCollection AddAbacusApi(this IServiceCollection services, IConfiguration configuration, Action<AbacusApiBuilder>? configure = null) =>
        services.AddAbacusApi(configuration.GetSection(AbacusOptions.SectionName), configure);

    /// <summary>Registers <c>IAbacusApi</c> with options from the given section.</summary>
    public static IServiceCollection AddAbacusApi(this IServiceCollection services, IConfigurationSection section, Action<AbacusApiBuilder>? configure = null)
    {
        services.AddOptions<AbacusOptions>().Bind(section).ValidateDataAnnotations().ValidateOnStart();
        return services.AddAbacusApiCore(configure);
    }

    /// <summary>Registers <c>IAbacusApi</c> with options set in code.</summary>
    public static IServiceCollection AddAbacusApi(this IServiceCollection services, Action<AbacusOptions> configureOptions, Action<AbacusApiBuilder>? configure = null)
    {
        services.AddOptions<AbacusOptions>().Configure(configureOptions).ValidateDataAnnotations().ValidateOnStart();
        return services.AddAbacusApiCore(configure);
    }

    /// <summary>The defaults first, then the builder hooks, so a hook can replace any default.</summary>
    private static IServiceCollection AddAbacusApiCore(this IServiceCollection services, Action<AbacusApiBuilder>? configure)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAbacusTokenProvider, ClientCredentialsTokenProvider>();
        services.TryAddTransient<AbacusAuthHandler>();
        services.AddHttpClient(TokenEndpoint.HttpClientName);

        var apiClient = services.AddHttpClient<V2026.IAbacusApi, V2026.AbacusApi>((provider, http) =>
                http.BaseAddress = provider.GetRequiredService<IOptions<AbacusOptions>>().Value.GetEntityBaseAddress())
            .AddHttpMessageHandler<AbacusAuthHandler>();

        configure?.Invoke(new AbacusApiBuilder(services, apiClient));
        return services;
    }
}
