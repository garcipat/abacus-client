using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pgarcia.AbacusApi.Client.Authentication;
using Pgarcia.AbacusApi.Client.V2026;
using Pgarcia.AbacusApi.Client;
using Pgarcia.AbacusApi.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Pgarcia.AbacusApi.Tests.Integration;

/// <summary>
/// Against a real Abacus server: the first test opens the browser for the login; the refresh token is kept in the
/// DPAPI file cache, so later tests and runs log in silently. Run explicitly, e.g.
/// <c>dotnet test src/AbacusApi.slnx --filter "Category=Integration"</c>, see docs/TestingGuide.md.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InteractiveLoginTests : IDisposable
{
    /// <summary>Used when <c>Abacus__Scopes__0</c>… are not set: the user's own profile and the service codes.</summary>
    private static readonly string[] DefaultScopes = ["openid", "profile", "email", "abacus.entity.projectbase.read"];

    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _provider;

    public InteractiveLoginTests(ITestOutputHelper output)
    {
        _output = output;
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var services = new ServiceCollection();
        services.AddAbacusApi(configuration, api => api.UseInteractiveBrowserLogin());
        services.PostConfigure<AbacusOptions>(options =>
        {
            if (options.Scopes.Count == 0)
                options.Scopes = DefaultScopes;
        });
        _provider = services.BuildServiceProvider();
    }

    [InteractiveIntegrationFact]
    public async Task Login_ShouldReturnOwnUserInfo()
    {
        // Arrange
        var token = await _provider.GetRequiredService<IAbacusTokenProvider>().GetAccessTokenAsync(CancellationToken.None);
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var baseUrl = _provider.GetRequiredService<IOptions<AbacusOptions>>().Value.BaseUrl;
        var discovery = await http.GetFromJsonAsync<JsonObject>(new Uri(baseUrl, "/.well-known/openid-configuration"));

        // Act
        var userInfo = await http.GetFromJsonAsync<JsonObject>(discovery!["userinfo_endpoint"]!.GetValue<string>());

        // Assert
        _output.WriteLine(userInfo!.ToJsonString(new() { WriteIndented = true }));
        userInfo["sub"].Should().NotBeNull();
    }

    [InteractiveIntegrationFact]
    public async Task ListServiceCodesAsync_ShouldReturnServiceCodes()
    {
        var result = await _provider.GetRequiredService<IAbacusApi>().ListServiceCodesAsync(top: 5);

        foreach (var code in result.Value)
            _output.WriteLine($"{code.Id}");
        result.Value.Should().NotBeNull();
    }

    public void Dispose() => _provider.Dispose();
}
