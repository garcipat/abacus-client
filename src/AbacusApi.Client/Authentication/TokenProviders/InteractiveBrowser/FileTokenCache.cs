using System.Security.Cryptography;
using System.Text;

namespace Garcipat.AbacusApi.Client.Authentication.TokenProviders.InteractiveBrowser;

/// <summary>
/// Stores the refresh token in a file, encrypted with DPAPI for the current Windows user, so only that user on that
/// machine can read it. Windows only; <see cref="MemoryTokenCache"/> is the default elsewhere.
/// </summary>
public sealed class FileTokenCache(string path) : ITokenCache
{
    private static readonly byte[] Entropy = "Garcipat.AbacusApi.RefreshToken"u8.ToArray();

    public string Path { get; } = path;

    /// <summary><c>%LOCALAPPDATA%\Garcipat.AbacusApi\tokens\{hash of server, Mandant and client}.bin</c>.</summary>
    public static FileTokenCache ForOptions(AbacusOptions options)
    {
        var key = $"{options.BaseUrl.AbsoluteUri}|{options.Mandant}|{options.ClientId}";
        var name = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
        var directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Garcipat.AbacusApi", "tokens");
        return new FileTokenCache(System.IO.Path.Combine(directory, $"{name}.bin"));
    }

    public async Task<string?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(Path))
            return null;

        var bytes = await File.ReadAllBytesAsync(Path, cancellationToken).ConfigureAwait(false);
        try
        {
            return Encoding.UTF8.GetString(Unprotect(bytes));
        }
        catch (CryptographicException)
        {
            // Written by another user or machine, or damaged: log in again.
            return null;
        }
    }

    public async Task SaveAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (refreshToken is null)
        {
            File.Delete(Path);
            return;
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        await File.WriteAllBytesAsync(Path, Protect(Encoding.UTF8.GetBytes(refreshToken)), cancellationToken).ConfigureAwait(false);
    }

    private static byte[] Protect(byte[] data) =>
        OperatingSystem.IsWindows()
            ? ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser)
            : throw NotSupported();

    private static byte[] Unprotect(byte[] data) =>
        OperatingSystem.IsWindows()
            ? ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser)
            : throw NotSupported();

    private static PlatformNotSupportedException NotSupported() =>
        new("FileTokenCache uses DPAPI and runs on Windows only; use MemoryTokenCache or your own ITokenCache.");
}
