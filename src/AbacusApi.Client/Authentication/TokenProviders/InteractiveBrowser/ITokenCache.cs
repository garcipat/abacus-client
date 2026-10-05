namespace Pgarcia.AbacusApi.Client.Authentication.TokenProviders.InteractiveBrowser;

/// <summary>
/// Keeps the refresh token of the interactive login between application runs.
/// The default, <see cref="FileTokenCache"/>, encrypts it with DPAPI for the current Windows user.
/// </summary>
public interface ITokenCache
{
    Task<string?> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Stores the refresh token; <c>null</c> removes it.</summary>
    Task SaveAsync(string? refreshToken, CancellationToken cancellationToken);
}
