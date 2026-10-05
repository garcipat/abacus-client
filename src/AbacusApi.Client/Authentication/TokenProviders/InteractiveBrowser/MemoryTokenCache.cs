namespace Garcipat.AbacusApi.Client.Authentication.TokenProviders.InteractiveBrowser;

/// <summary>Keeps the refresh token for the lifetime of the process only (the default outside Windows).</summary>
public sealed class MemoryTokenCache : ITokenCache
{
    public string? RefreshToken { get; set; }

    public Task<string?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(RefreshToken);

    public Task SaveAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        RefreshToken = refreshToken;
        return Task.CompletedTask;
    }
}
