using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using AwesomeAssertions;
using Garcipat.AbacusApi.Client;
using Garcipat.AbacusApi.Client.Authentication;
using Garcipat.AbacusApi.Client.Authentication.TokenProviders.InteractiveBrowser;
using Garcipat.AbacusApi.Tests.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Garcipat.AbacusApi.Tests.Client.Authentication.TokenProviders.InteractiveBrowser;

public class InteractiveBrowserTokenProviderTests
{
    private const string DiscoveryUrl = "https://abacus.test/.well-known/openid-configuration";
    private const string AuthorizationUrl = "https://abacus.test/oauth/oauth2/v1/auth";
    private const string TokenUrl = "https://abacus.test/oauth/oauth2/v1/token";
    private const string RedirectUri = "http://localhost:53682/callback";

    private readonly StubHttpMessageHandler _handler;
    private readonly FakeCodeReceiver _codeReceiver;
    private readonly MemoryTokenCache _tokenCache;
    private readonly FakeTimeProvider _timeProvider;
    private readonly AbacusOptions _options;
    private readonly InteractiveBrowserTokenProvider _uut;

    public InteractiveBrowserTokenProviderTests()
    {
        _handler = new StubHttpMessageHandler().Respond(DiscoveryUrl, HttpStatusCode.OK,
            $$"""{ "authorization_endpoint": "{{AuthorizationUrl}}", "token_endpoint": "{{TokenUrl}}" }""");
        SetupTokenResponse("access-1", "refresh-1");
        _codeReceiver = new FakeCodeReceiver();
        _tokenCache = new MemoryTokenCache();
        _timeProvider = new FakeTimeProvider();
        _options = new AbacusOptions
        {
            BaseUrl = new Uri("https://abacus.test/"),
            Mandant = 7777,
            ClientId = "client",
            Scopes = ["openid", "abacus.entity.projectbooking.readwrite"],
            RedirectUri = new Uri(RedirectUri),
        };
        _uut = new InteractiveBrowserTokenProvider(GetHttpClientFactory(), Options.Create(_options), _codeReceiver, _tokenCache, _timeProvider);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithoutToken_ShouldOpenLoginWithAuthorizationRequest()
    {
        await _uut.GetAccessTokenAsync(CancellationToken.None);

        var login = _codeReceiver.AuthorizationUrls.Should().ContainSingle().Subject;
        login.GetLeftPart(UriPartial.Path).Should().Be(AuthorizationUrl);
        _codeReceiver.Parameter(0, "response_type").Should().Be("code");
        _codeReceiver.Parameter(0, "client_id").Should().Be("client");
        _codeReceiver.Parameter(0, "scope").Should().Be("openid abacus.entity.projectbooking.readwrite");
        _codeReceiver.Parameter(0, "redirect_uri").Should().Be(RedirectUri);
        _codeReceiver.Parameter(0, "state").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithPkce_ShouldSendS256Challenge()
    {
        await _uut.GetAccessTokenAsync(CancellationToken.None);

        _codeReceiver.Parameter(0, "code_challenge_method").Should().Be("S256");
        var verifier = TokenRequest(0)["code_verifier"]!;
        _codeReceiver.Parameter(0, "code_challenge").Should().Be(Challenge(verifier));
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithoutPkce_ShouldNotSendChallenge()
    {
        _options.UsePkce = false;

        await _uut.GetAccessTokenAsync(CancellationToken.None);

        HttpUtility.ParseQueryString(_codeReceiver.AuthorizationUrls[0].Query)["code_challenge"].Should().BeNull();
        TokenRequest(0)["code_verifier"].Should().BeNull();
    }

    [Fact]
    public async Task GetAccessTokenAsync_AfterLogin_ShouldExchangeCodeForToken()
    {
        var token = await _uut.GetAccessTokenAsync(CancellationToken.None);

        token.Should().Be("access-1");
        var request = TokenRequest(0);
        request["grant_type"].Should().Be("authorization_code");
        request["client_id"].Should().Be("client");
        request["code"].Should().Be("auth-code");
        request["redirect_uri"].Should().Be(RedirectUri);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithClientSecret_ShouldSendItForTrustedClient()
    {
        _options.ClientSecret = "secret";

        await _uut.GetAccessTokenAsync(CancellationToken.None);

        TokenRequest(0)["client_secret"].Should().Be("secret");
    }

    [Fact]
    public async Task GetAccessTokenAsync_AfterLogin_ShouldSaveRefreshToken()
    {
        await _uut.GetAccessTokenAsync(CancellationToken.None);

        _tokenCache.RefreshToken.Should().Be("refresh-1");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTokenStillValid_ShouldNotLoginAgain()
    {
        await _uut.GetAccessTokenAsync(CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromSeconds(500));

        var token = await _uut.GetAccessTokenAsync(CancellationToken.None);

        token.Should().Be("access-1");
        _codeReceiver.AuthorizationUrls.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTokenExpired_ShouldRefreshWithoutLogin()
    {
        // Arrange
        await _uut.GetAccessTokenAsync(CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromSeconds(580));
        SetupTokenResponse("access-2", refreshToken: null);

        // Act
        var token = await _uut.GetAccessTokenAsync(CancellationToken.None);

        // Assert
        token.Should().Be("access-2");
        _codeReceiver.AuthorizationUrls.Should().HaveCount(1);
        TokenRequest(1)["grant_type"].Should().Be("refresh_token");
        TokenRequest(1)["refresh_token"].Should().Be("refresh-1");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenRefreshReturnsNoNewRefreshToken_ShouldKeepTheOldOne()
    {
        await _uut.GetAccessTokenAsync(CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromSeconds(580));
        SetupTokenResponse("access-2", refreshToken: null);

        await _uut.GetAccessTokenAsync(CancellationToken.None);

        _tokenCache.RefreshToken.Should().Be("refresh-1");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithCachedRefreshToken_ShouldNotLogin()
    {
        _tokenCache.RefreshToken = "cached-refresh";

        var token = await _uut.GetAccessTokenAsync(CancellationToken.None);

        token.Should().Be("access-1");
        _codeReceiver.AuthorizationUrls.Should().BeEmpty();
        TokenRequest(0)["refresh_token"].Should().Be("cached-refresh");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenRefreshTokenRejected_ShouldLoginAgain()
    {
        // Arrange
        _tokenCache.RefreshToken = "expired-refresh";
        _handler.RespondOnce(TokenUrl, HttpStatusCode.BadRequest, """{ "error": "invalid_grant" }""");
        SetupTokenResponse("access-1", "refresh-new");

        // Act
        var token = await _uut.GetAccessTokenAsync(CancellationToken.None);

        // Assert
        token.Should().Be("access-1");
        _codeReceiver.AuthorizationUrls.Should().HaveCount(1);
        _tokenCache.RefreshToken.Should().Be("refresh-new");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenStateDoesNotMatch_ShouldThrow()
    {
        _codeReceiver.Response = new() { ["code"] = "auth-code", ["state"] = "forged" };

        await _uut.Awaiting(x => x.GetAccessTokenAsync(CancellationToken.None))
            .Should().ThrowAsync<AbacusLoginException>();
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenLoginReturnsError_ShouldThrowWithDescription()
    {
        _codeReceiver.Response = new() { ["error"] = "invalid_scope", ["error_description"] = "The user has no access to any scope requested." };

        await _uut.Awaiting(x => x.GetAccessTokenAsync(CancellationToken.None))
            .Should().ThrowAsync<AbacusLoginException>().WithMessage("*The user has no access to any scope requested.*");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithoutRedirectUri_ShouldThrow()
    {
        _options.RedirectUri = null;

        await _uut.Awaiting(x => x.GetAccessTokenAsync(CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    private void SetupTokenResponse(string accessToken, string? refreshToken)
    {
        var refresh = refreshToken is null ? string.Empty : $$""", "refresh_token": "{{refreshToken}}" """;
        _handler.Respond(TokenUrl, HttpStatusCode.OK, $$"""{ "access_token": "{{accessToken}}", "token_type": "Bearer", "expires_in": 600{{refresh}} }""");
    }

    private System.Collections.Specialized.NameValueCollection TokenRequest(int index) =>
        HttpUtility.ParseQueryString(_handler.Requests.Where(r => r.Uri.AbsoluteUri == TokenUrl).ElementAt(index).Body!);

    private static string Challenge(string verifier) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private IHttpClientFactory GetHttpClientFactory()
    {
        var mock = new Mock<IHttpClientFactory>();
        mock.Setup(x => x.CreateClient(TokenEndpoint.HttpClientName))
            .Returns(() => new HttpClient(_handler, disposeHandler: false));
        return mock.Object;
    }
}
