using System.Net;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Pgarcia.AbacusApi.Client.Authentication.TokenProviders.ClientCredentials;
using Pgarcia.AbacusApi.Client.Authentication;
using Pgarcia.AbacusApi.Client;
using Pgarcia.AbacusApi.Tests.Infrastructure;

namespace Pgarcia.AbacusApi.Tests.Client.Authentication.TokenProviders.ClientCredentials;

public class ClientCredentialsTokenProviderTests
{
    private const string DiscoveryUrl = "https://abacus.test/.well-known/openid-configuration";
    private const string TokenUrl = "https://abacus.test/oauth/oauth2/v1/token";

    private readonly StubHttpMessageHandler _handler;
    private readonly FakeTimeProvider _timeProvider;
    private readonly AbacusOptions _options;
    private readonly ClientCredentialsTokenProvider _uut;

    public ClientCredentialsTokenProviderTests()
    {
        _handler = new StubHttpMessageHandler()
            .Respond(DiscoveryUrl, HttpStatusCode.OK, $$"""{ "issuer": "https://abacus.test", "token_endpoint": "{{TokenUrl}}" }""");
        SetupToken("token-1");
        _timeProvider = new FakeTimeProvider();
        _options = new AbacusOptions { BaseUrl = new Uri("https://abacus.test/"), Mandant = 7777, ClientId = "client", ClientSecret = "secret" };
        _uut = new ClientCredentialsTokenProvider(GetHttpClientFactory(), Options.Create(_options), _timeProvider);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldReturnAccessToken()
    {
        var token = await _uut.GetAccessTokenAsync(CancellationToken.None);

        token.Should().Be("token-1");
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldReadTokenEndpointFromDiscovery()
    {
        await _uut.GetAccessTokenAsync(CancellationToken.None);

        _handler.Requests.Select(r => (r.Method.Method, r.Uri.AbsoluteUri)).Should().Equal(("GET", DiscoveryUrl), ("POST", TokenUrl));
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldPostClientCredentialsWithBasicAuth()
    {
        await _uut.GetAccessTokenAsync(CancellationToken.None);

        var request = _handler.Requests[1];
        request.Authorization!.Scheme.Should().Be("Basic");
        request.Authorization.Parameter.Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes("client:secret")));
        request.Body.Should().Be("grant_type=client_credentials");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithScopes_ShouldSendSpaceSeparatedScope()
    {
        _options.Scopes = ["abacus.entity.projectbooking.readwrite", "abacus.entity.project.read"];

        await _uut.GetAccessTokenAsync(CancellationToken.None);

        _handler.Requests[1].Body.Should().Be("grant_type=client_credentials&scope=abacus.entity.projectbooking.readwrite+abacus.entity.project.read");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTokenStillValid_ShouldReuseIt()
    {
        await _uut.GetAccessTokenAsync(CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromSeconds(500));

        var token = await _uut.GetAccessTokenAsync(CancellationToken.None);

        token.Should().Be("token-1");
        _handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTokenAboutToExpire_ShouldRequestNewToken()
    {
        // Arrange
        await _uut.GetAccessTokenAsync(CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromSeconds(580));
        SetupToken("token-2");

        // Act
        var token = await _uut.GetAccessTokenAsync(CancellationToken.None);

        // Assert
        token.Should().Be("token-2");
        _handler.Requests.Count(r => r.Uri.AbsoluteUri == DiscoveryUrl).Should().Be(1);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithoutClientSecret_ShouldThrow()
    {
        _options.ClientSecret = null;

        await _uut.Awaiting(x => x.GetAccessTokenAsync(CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTokenRequestFails_ShouldThrow()
    {
        _handler.Respond(TokenUrl, HttpStatusCode.Unauthorized, """{ "error": "invalid_client" }""");

        await _uut.Awaiting(x => x.GetAccessTokenAsync(CancellationToken.None))
            .Should().ThrowAsync<HttpRequestException>();
    }

    private void SetupToken(string token) =>
        _handler.Respond(TokenUrl, HttpStatusCode.OK, $$"""{ "access_token": "{{token}}", "token_type": "Bearer", "expires_in": 600 }""");

    private IHttpClientFactory GetHttpClientFactory()
    {
        var mock = new Mock<IHttpClientFactory>();
        mock.Setup(x => x.CreateClient(TokenEndpoint.HttpClientName))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
        return mock.Object;
    }
}
