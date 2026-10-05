using AwesomeAssertions;
using Pgarcia.AbacusApi.Client.Authentication;
using Pgarcia.AbacusApi.Tests.Infrastructure;

namespace Pgarcia.AbacusApi.Tests.Client.Authentication;

public class AbacusAuthHandlerTests
{
    private readonly StubHttpMessageHandler _innerHandler;
    private readonly HttpMessageInvoker _invoker;

    public AbacusAuthHandlerTests()
    {
        _innerHandler = new StubHttpMessageHandler();
        _invoker = new HttpMessageInvoker(new AbacusAuthHandler(new FixedTokenProvider()) { InnerHandler = _innerHandler });
    }

    [Fact]
    public async Task SendAsync_ShouldAddBearerTokenFromProvider()
    {
        await _invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://abacus.test/x"), CancellationToken.None);

        _innerHandler.Requests[0].Authorization!.Scheme.Should().Be("Bearer");
        _innerHandler.Requests[0].Authorization!.Parameter.Should().Be(FixedTokenProvider.Token);
    }
}
