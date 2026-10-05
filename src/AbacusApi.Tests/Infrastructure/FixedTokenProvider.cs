using Garcipat.AbacusApi.Client.Authentication;

namespace Garcipat.AbacusApi.Tests.Infrastructure;

public sealed class FixedTokenProvider : IAbacusTokenProvider
{
    public const string Token = "test-token";

    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult(Token);
}
