using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgarcia.AbacusApi.Client.Authentication;
using Pgarcia.AbacusApi.Client.V2026;
using Pgarcia.AbacusApi.Tests.Infrastructure;
using Xunit.Abstractions;

namespace Pgarcia.AbacusApi.Tests.Integration;

/// <summary>
/// Against a real Abacus server with a user-independent service user (client credentials), e.g. the pre-configured
/// one on the Abacus test servers (scripts/Set-AbacusDemoServerEnvironment.ps1). Read-only.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ClientCredentialsLoginTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _provider;

    public ClientCredentialsLoginTests(ITestOutputHelper output)
    {
        _output = output;
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var services = new ServiceCollection();
        services.AddAbacusApi(configuration);
        _provider = services.BuildServiceProvider();
    }

    [ClientCredentialsIntegrationFact]
    public async Task GetAccessTokenAsync_ShouldReturnToken()
    {
        var token = await _provider.GetRequiredService<IAbacusTokenProvider>().GetAccessTokenAsync(CancellationToken.None);

        _output.WriteLine($"Got an access token ({token.Length} characters).");
        token.Should().NotBeNullOrEmpty();
    }

    [ClientCredentialsIntegrationFact]
    public async Task ListServiceCodesAsync_ShouldReturnServiceCodes()
    {
        var result = await _provider.GetRequiredService<IAbacusApi>().ListServiceCodesAsync(top: 5);

        foreach (var code in result.Value)
            _output.WriteLine($"Service code {code.Id}");
        result.Value.Should().NotBeNull();
    }

    [ClientCredentialsIntegrationFact]
    public async Task ListProjectsAsync_ShouldReturnProjects()
    {
        var result = await _provider.GetRequiredService<IAbacusApi>().ListProjectsAsync(top: 5, select: ["Id", "Name"]);

        foreach (var project in result.Value)
            _output.WriteLine($"Project {project.Id}: {project.Name}");
        result.Value.Should().NotBeNull();
    }

    public void Dispose() => _provider.Dispose();
}
