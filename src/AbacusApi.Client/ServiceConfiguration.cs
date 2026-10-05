using Garcipat.AbacusApi.Client;
using Garcipat.AbacusApi.Client.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using V2026 = Garcipat.AbacusApi.Client.V2026;

#pragma warning disable IDE0130 // Extension methods live in the DI namespace, as the Microsoft.Extensions.* ones do.

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceConfiguration
{
    /// <summary>Registers <c>IAbacusApi</c> with options from the <c>Abacus</c> section.</summary>
    public static AbacusApiBuilder AddAbacusApi(this IServiceCollection services, IConfiguration configuration) =>
        services.AddAbacusApi(configuration.GetSection(AbacusOptions.SectionName));

    /// <summary>Registers <c>IAbacusApi</c> with options from the given section.</summary>
    public static AbacusApiBuilder AddAbacusApi(this IServiceCollection services, IConfigurationSection section)
    {
        services.AddOptions<AbacusOptions>().Bind(section).ValidateDataAnnotations().ValidateOnStart();
        return services.AddAbacusApiCore();
    }

    /// <summary>Registers <c>IAbacusApi</c> with options set in code.</summary>
    public static AbacusApiBuilder AddAbacusApi(this IServiceCollection services, Action<AbacusOptions> configure)
    {
        services.AddOptions<AbacusOptions>().Configure(configure).ValidateDataAnnotations().ValidateOnStart();
        return services.AddAbacusApiCore();
    }

    private static AbacusApiBuilder AddAbacusApiCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAbacusTokenProvider, ClientCredentialsTokenProvider>();
        services.TryAddTransient<AbacusAuthHandler>();
        services.AddHttpClient(ClientCredentialsTokenProvider.HttpClientName);

        services.AddHttpClient<V2026.IAbacusApi, V2026.AbacusApi>((provider, http) =>
                http.BaseAddress = provider.GetRequiredService<IOptions<AbacusOptions>>().Value.GetEntityBaseAddress())
            .AddHttpMessageHandler<AbacusAuthHandler>();

        return new AbacusApiBuilder(services);
    }
}
