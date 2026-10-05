using System.Net;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pgarcia.AbacusApi.Client.Authentication.TokenProviders.ClientCredentials;
using Pgarcia.AbacusApi.Client.Authentication.TokenProviders.InteractiveBrowser;
using Pgarcia.AbacusApi.Client.Authentication;
using Pgarcia.AbacusApi.Client.V2026;
using Pgarcia.AbacusApi.Client;
using Pgarcia.AbacusApi.Tests.Infrastructure;

namespace Pgarcia.AbacusApi.Tests.Client;

public class ServiceConfigurationTests
{
    private const string ServiceCodesUrl = "https://abacus.test/api/entity/v1/mandants/7777/ServiceCodes";

    private readonly StubHttpMessageHandler _handler;
    private readonly Dictionary<string, string?> _settings;

    public ServiceConfigurationTests()
    {
        _handler = new StubHttpMessageHandler().Respond(ServiceCodesUrl, HttpStatusCode.OK, """{ "value": [] }""");
        _settings = new()
        {
            ["Abacus:BaseUrl"] = "https://abacus.test",
            ["Abacus:Mandant"] = "7777",
            ["Abacus:ClientId"] = "client",
            ["Abacus:Scopes:0"] = "abacus.entity.projectbase.read",
        };
    }

    [Fact]
    public void AddAbacusApi_WithConfiguration_ShouldBindOptions()
    {
        using var provider = BuildProvider(services => services.AddAbacusApi(Configuration()));

        var options = provider.GetRequiredService<IOptions<AbacusOptions>>().Value;

        options.BaseUrl.Should().Be(new Uri("https://abacus.test"));
        options.Mandant.Should().Be(7777);
        options.ClientId.Should().Be("client");
        options.Scopes.Should().Equal("abacus.entity.projectbase.read");
    }

    [Fact]
    public void AddAbacusApi_WithoutBaseUrl_ShouldFailValidation()
    {
        _settings.Remove("Abacus:BaseUrl");
        using var provider = BuildProvider(services => services.AddAbacusApi(Configuration()));

        var act = () => provider.GetRequiredService<IOptions<AbacusOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddAbacusApi_WithAction_ShouldConfigureOptions()
    {
        using var provider = BuildProvider(services => services.AddAbacusApi(options =>
        {
            options.BaseUrl = new Uri("https://abacus.test");
            options.Mandant = 1;
            options.ClientId = "client";
        }));

        provider.GetRequiredService<IOptions<AbacusOptions>>().Value.Mandant.Should().Be(1);
    }

    [Fact]
    public void AddAbacusApi_ShouldReturnServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddAbacusApi(Configuration(), api => api.UseTokenProvider<FixedTokenProvider>());

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddAbacusApi_WithOptionsAndBuilder_ShouldApplyBoth()
    {
        using var provider = BuildProvider(services => services.AddAbacusApi(
            options =>
            {
                options.BaseUrl = new Uri("https://abacus.test");
                options.Mandant = 1;
                options.ClientId = "client";
            },
            api => api.UseTokenProvider<FixedTokenProvider>()));

        provider.GetRequiredService<IOptions<AbacusOptions>>().Value.Mandant.Should().Be(1);
        provider.GetRequiredService<IAbacusTokenProvider>().Should().BeOfType<FixedTokenProvider>();
    }

    [Fact]
    public void AddAbacusApi_WithoutTokenProvider_ShouldUseClientCredentials()
    {
        using var provider = BuildProvider(services => services.AddAbacusApi(Configuration()));

        provider.GetRequiredService<IAbacusTokenProvider>().Should().BeOfType<ClientCredentialsTokenProvider>();
    }

    [Fact]
    public void UseTokenProvider_ShouldReplaceDefaultProvider()
    {
        using var provider = BuildProvider(services => services.AddAbacusApi(Configuration(), api => api.UseTokenProvider<FixedTokenProvider>()));

        provider.GetRequiredService<IAbacusTokenProvider>().Should().BeOfType<FixedTokenProvider>();
    }

    [Fact]
    public void UseInteractiveBrowserLogin_ShouldRegisterInteractiveProvider()
    {
        using var provider = BuildProvider(services => services.AddAbacusApi(Configuration(), api => api.UseInteractiveBrowserLogin()));

        provider.GetRequiredService<IAbacusTokenProvider>().Should().BeOfType<InteractiveBrowserTokenProvider>();
        provider.GetRequiredService<IAuthorizationCodeReceiver>().Should().BeOfType<LoopbackBrowserCodeReceiver>();
    }

    [Fact]
    public void UseInteractiveBrowserLogin_ShouldUseFileCacheOnWindowsAndMemoryElsewhere()
    {
        using var provider = BuildProvider(services => services.AddAbacusApi(Configuration(), api => api.UseInteractiveBrowserLogin()));

        var cache = provider.GetRequiredService<ITokenCache>();

        if (OperatingSystem.IsWindows())
            cache.Should().BeOfType<FileTokenCache>();
        else
            cache.Should().BeOfType<MemoryTokenCache>();
    }

    [Fact]
    public async Task AbacusApi_ShouldSendRequestsToMandantBaseAddress()
    {
        using var provider = BuildProvider(services => services.AddAbacusApi(Configuration(), api => api.UseTokenProvider<FixedTokenProvider>()));

        await provider.GetRequiredService<IAbacusApi>().ListServiceCodesAsync();

        _handler.Requests[0].Uri.AbsoluteUri.Should().Be(ServiceCodesUrl);
    }

    [Fact]
    public async Task AbacusApi_ShouldSendBearerTokenFromProvider()
    {
        using var provider = BuildProvider(services => services.AddAbacusApi(Configuration(), api => api.UseTokenProvider<FixedTokenProvider>()));

        await provider.GetRequiredService<IAbacusApi>().ListServiceCodesAsync();

        _handler.Requests[0].Authorization!.Parameter.Should().Be(FixedTokenProvider.Token);
    }

    private IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(_settings).Build();

    private ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => _handler));
        return services.BuildServiceProvider();
    }
}
