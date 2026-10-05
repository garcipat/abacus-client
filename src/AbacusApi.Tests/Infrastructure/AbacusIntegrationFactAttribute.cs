namespace Garcipat.AbacusApi.Tests.Infrastructure;

/// <summary>
/// A fact against a real Abacus server, skipped unless the given environment variables are set, so it never runs on
/// CI or by accident. See docs/TestingGuide.md#integration-tests.
/// </summary>
public abstract class AbacusIntegrationFactAttribute : FactAttribute
{
    protected AbacusIntegrationFactAttribute(params string[] requiredVariables)
    {
        var missing = requiredVariables.Where(name => string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name))).ToList();
        if (missing.Count > 0)
            Skip = $"Real Abacus server not configured (missing {string.Join(", ", missing)}).";
    }
}

/// <summary>Interactive browser login (user-dependent service user).</summary>
public sealed class InteractiveIntegrationFactAttribute()
    : AbacusIntegrationFactAttribute("Abacus__BaseUrl", "Abacus__Mandant", "Abacus__ClientId", "Abacus__RedirectUri");

/// <summary>Client credentials (user-independent service user), e.g. the pre-configured one on the Abacus test servers.</summary>
public sealed class ClientCredentialsIntegrationFactAttribute()
    : AbacusIntegrationFactAttribute("Abacus__BaseUrl", "Abacus__Mandant", "Abacus__ClientId", "Abacus__ClientSecret");
