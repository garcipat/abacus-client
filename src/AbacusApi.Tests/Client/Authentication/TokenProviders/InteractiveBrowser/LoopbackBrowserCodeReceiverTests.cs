using System.Net.Sockets;
using System.Net;
using AwesomeAssertions;
using Pgarcia.AbacusApi.Client.Authentication.TokenProviders.InteractiveBrowser;
using Pgarcia.AbacusApi.Client.Authentication;

namespace Pgarcia.AbacusApi.Tests.Client.Authentication.TokenProviders.InteractiveBrowser;

public class LoopbackBrowserCodeReceiverTests
{
    private static readonly Uri AuthorizationUrl = new("https://abacus.test/oauth/oauth2/v1/auth?client_id=client");

    private readonly Uri _redirectUri;
    private readonly List<Uri> _openedUrls;
    private readonly List<Task<HttpResponseMessage>> _browserRequests;
    private readonly HttpClient _browser;

    public LoopbackBrowserCodeReceiverTests()
    {
        _redirectUri = new Uri($"http://localhost:{FreePort()}/callback");
        _openedUrls = [];
        _browserRequests = [];
        _browser = new HttpClient();
    }

    [Fact]
    public async Task ReceiveAsync_ShouldOpenBrowserWithAuthorizationUrl()
    {
        var uut = CreateUut(Visit("/callback?code=abc&state=xyz"));

        await uut.ReceiveAsync(AuthorizationUrl, _redirectUri, CancellationToken.None);

        _openedUrls.Should().Equal(AuthorizationUrl);
    }

    [Fact]
    public async Task ReceiveAsync_WhenBrowserIsRedirected_ShouldReturnQueryParameters()
    {
        var uut = CreateUut(Visit("/callback?code=abc&state=x%2By"));

        var result = await uut.ReceiveAsync(AuthorizationUrl, _redirectUri, CancellationToken.None);

        result.Should().BeEquivalentTo(new Dictionary<string, string> { ["code"] = "abc", ["state"] = "x+y" });
    }

    [Fact]
    public async Task ReceiveAsync_WhenBrowserIsRedirected_ShouldShowClosePage()
    {
        var uut = CreateUut(Visit("/callback?code=abc&state=xyz"));

        await uut.ReceiveAsync(AuthorizationUrl, _redirectUri, CancellationToken.None);

        var page = await (await _browserRequests[0]).Content.ReadAsStringAsync();
        page.Should().Contain("close this window");
    }

    [Fact]
    public async Task ReceiveAsync_WithRequestToOtherPath_ShouldKeepWaiting()
    {
        var uut = CreateUut(Visit("/favicon.ico", "/callback?code=abc&state=xyz"));

        var result = await uut.ReceiveAsync(AuthorizationUrl, _redirectUri, CancellationToken.None);

        result["code"].Should().Be("abc");
        (await _browserRequests[0]).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReceiveAsync_WhenNoRedirectWithinTimeout_ShouldThrow()
    {
        var uut = new LoopbackBrowserCodeReceiver(_ => Task.CompletedTask, TimeSpan.FromMilliseconds(200));

        await uut.Awaiting(x => x.ReceiveAsync(AuthorizationUrl, _redirectUri, CancellationToken.None))
            .Should().ThrowAsync<AbacusLoginException>();
    }

    [Fact]
    public async Task ReceiveAsync_WithNonLoopbackRedirectUri_ShouldThrow()
    {
        var uut = CreateUut(Visit());

        await uut.Awaiting(x => x.ReceiveAsync(AuthorizationUrl, new Uri("https://example.com/callback"), CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
    }

    private LoopbackBrowserCodeReceiver CreateUut(Func<Uri, Task> openBrowser) => new(openBrowser, TimeSpan.FromSeconds(10));

    /// <summary>Plays the browser: records the opened URL, then requests the given paths on the loopback listener in order.</summary>
    private Func<Uri, Task> Visit(params string[] paths) => url =>
    {
        _openedUrls.Add(url);
        // Not awaited: the listener answers only after the open-browser callback has returned.
        _ = Task.Run(async () =>
        {
            foreach (var path in paths)
            {
                var request = _browser.GetAsync(new Uri(_redirectUri, path));
                _browserRequests.Add(request);
                await request;
            }
        });
        return Task.CompletedTask;
    };

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
