namespace Garcipat.AbacusApi.Client.Authentication.TokenProviders.InteractiveBrowser;

/// <summary>The interactive Abacus login failed: Abacus returned an error, or the response could not be trusted.</summary>
public sealed class AbacusLoginException : Exception
{
    public AbacusLoginException(string message)
        : base(message)
    {
    }

    public AbacusLoginException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
