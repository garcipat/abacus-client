using System.Net;
using AwesomeAssertions;
using Garcipat.AbacusApi.Client;
using Garcipat.AbacusApi.Client.Authentication;
using Garcipat.AbacusApi.Client.V2026;
using Garcipat.AbacusApi.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Garcipat.AbacusApi.Tests.Client;

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
    public void AddAbacusApi_WithoutTokenProvider_ShouldUseClientCredentials()
    {
        using var provider = BuildProvider(services => services.AddAbacusApi(Configuration()));

        provider.GetRequiredService<IAbacusTokenProvider>().Should().BeOfType<ClientCredentialsTokenProvider>();
    }

    [Fact]
    public void AddTokenProvider_ShouldReplaceDefaultProvider()
    {
        using var provider = BuildProvider(services => services.AddAbacusApi(Configuration()).AddTokenProvider<FixedTokenProvider>());

        provider.GetRequiredService<IAbacusTokenProvider>().Should().BeOfType<FixedTokenProvider>();
    }

    [Fact]
    public async Task AbacusApi_ShouldSendRequestsToMandantBaseAddress()
    {
        using var provider = BuildProvider(services => services.AddAbacusApi(Configuration()).AddTokenProvider<FixedTokenProvider>());

        await provider.GetRequiredService<IAbacusApi>().ListServiceCodesAsync();

        _handler.Requests[0].Uri.AbsoluteUri.Should().Be(ServiceCodesUrl);
    }

    [Fact]
    public async Task AbacusApi_ShouldSendBearerTokenFromProvider()
    {
        using var provider = BuildProvider(services => services.AddAbacusApi(Configuration()).AddTokenProvider<FixedTokenProvider>());

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
